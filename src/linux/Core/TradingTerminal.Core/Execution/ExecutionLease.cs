namespace TradingTerminal.Core.Execution;

/// <summary>One independently fenced execution resource.</summary>
public readonly record struct ExecutionResource(
    VenueId VenueId,
    TradingAccountId TradingAccountId,
    ExecutionEnvironment Environment)
{
    public bool IsValid =>
        !VenueId.IsEmpty &&
        !TradingAccountId.IsEmpty &&
        Enum.IsDefined(Environment);
}

/// <summary>The lease/fence proof frozen into each dispatched order instruction.</summary>
public readonly record struct ExecutionLeaseClaim(
    ExecutionResource Resource,
    ExecutionLeaseId LeaseId,
    FencingToken FencingToken)
{
    public bool IsValid => Resource.IsValid && !LeaseId.IsEmpty && FencingToken.IsValid;
}

/// <summary>Current owner and expiry for one durable fencing generation.</summary>
public readonly record struct ExecutionLeaseGrant(
    ExecutionLeaseClaim Claim,
    RuntimeInstanceId OwnerId,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsValid =>
        Claim.IsValid &&
        !OwnerId.IsEmpty &&
        AcquiredAtUtc.Offset == TimeSpan.Zero &&
        ExpiresAtUtc.Offset == TimeSpan.Zero &&
        ExpiresAtUtc > AcquiredAtUtc;
}

public enum ExecutionLeaseFault : byte
{
    None = 0,
    InvalidInput = 1,
    HeldByAnotherOwner = 2,
    LeaseIdentityConflict = 3,
    TokenExhausted = 4,
    NotCurrent = 5,
    Expired = 6,
    Released = 7,
}

public readonly record struct ExecutionLeaseAcquireResult(
    ExecutionLeaseFault Fault,
    ExecutionLeaseGrant? Grant,
    string? Reason = null)
{
    public bool IsSuccess => Fault == ExecutionLeaseFault.None && Grant is { IsValid: true };
}

public readonly record struct ExecutionLeaseMutationResult(
    ExecutionLeaseFault Fault,
    ExecutionLeaseGrant? Grant,
    string? Reason = null)
{
    public bool IsSuccess => Fault == ExecutionLeaseFault.None;
}

public readonly record struct ExecutionLeaseValidationResult(
    ExecutionLeaseFault Fault,
    bool IsCurrent,
    string? Reason = null)
{
    public bool IsSuccess => Fault == ExecutionLeaseFault.None && IsCurrent;
}

public interface IExecutionLeaseValidator
{
    ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc);
}

public interface IExecutionLeaseStore : IExecutionLeaseValidator
{
    ExecutionLeaseAcquireResult Acquire(
        ExecutionResource resource,
        ExecutionLeaseId leaseId,
        RuntimeInstanceId ownerId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc);

    ExecutionLeaseMutationResult Renew(
        in ExecutionLeaseGrant grant,
        DateTimeOffset renewedAtUtc,
        DateTimeOffset expiresAtUtc);

    ExecutionLeaseMutationResult Release(
        in ExecutionLeaseGrant grant,
        DateTimeOffset releasedAtUtc);
}

/// <summary>Deterministic lease store used by in-process Paper tests.</summary>
public sealed class InMemoryExecutionLeaseStore : IExecutionLeaseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ExecutionResource, LeaseGeneration> _latest = [];
    private readonly HashSet<ExecutionLeaseId> _usedLeaseIds = [];

    public ExecutionLeaseAcquireResult Acquire(
        ExecutionResource resource,
        ExecutionLeaseId leaseId,
        RuntimeInstanceId ownerId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (!ValidAcquire(resource, leaseId, ownerId, acquiredAtUtc, expiresAtUtc))
            return AcquireFailed(ExecutionLeaseFault.InvalidInput, "The lease acquisition fields are invalid.");

        lock (_gate)
        {
            if (_usedLeaseIds.Contains(leaseId))
                return AcquireFailed(ExecutionLeaseFault.LeaseIdentityConflict, "The lease id was already used.");
            if (_latest.TryGetValue(resource, out var prior) &&
                prior.ReleasedAtUtc is null && prior.Grant.ExpiresAtUtc > acquiredAtUtc)
                return AcquireFailed(ExecutionLeaseFault.HeldByAnotherOwner, "An unexpired owner already holds the resource.");
            var previousToken = prior.Grant.Claim.FencingToken.Value;
            if (previousToken == long.MaxValue)
                return AcquireFailed(ExecutionLeaseFault.TokenExhausted, "The fencing-token space is exhausted.");

            var grant = new ExecutionLeaseGrant(
                new ExecutionLeaseClaim(resource, leaseId, new FencingToken(checked(previousToken + 1))),
                ownerId,
                acquiredAtUtc,
                expiresAtUtc);
            _latest[resource] = new LeaseGeneration(grant, null);
            _usedLeaseIds.Add(leaseId);
            return new ExecutionLeaseAcquireResult(ExecutionLeaseFault.None, grant);
        }
    }

    public ExecutionLeaseMutationResult Renew(
        in ExecutionLeaseGrant grant,
        DateTimeOffset renewedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (!grant.IsValid || renewedAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc.Offset != TimeSpan.Zero || expiresAtUtc <= renewedAtUtc)
            return MutationFailed(ExecutionLeaseFault.InvalidInput, "The renewal fields are invalid.");
        lock (_gate)
        {
            var current = Current(grant, renewedAtUtc);
            if (current.Fault != ExecutionLeaseFault.None)
                return MutationFailed(current.Fault, current.Reason!);
            var renewed = grant with { ExpiresAtUtc = expiresAtUtc };
            _latest[grant.Claim.Resource] = new LeaseGeneration(renewed, null);
            return new ExecutionLeaseMutationResult(ExecutionLeaseFault.None, renewed);
        }
    }

    public ExecutionLeaseMutationResult Release(
        in ExecutionLeaseGrant grant,
        DateTimeOffset releasedAtUtc)
    {
        if (!grant.IsValid || releasedAtUtc.Offset != TimeSpan.Zero)
            return MutationFailed(ExecutionLeaseFault.InvalidInput, "The release fields are invalid.");
        lock (_gate)
        {
            var current = Current(grant, releasedAtUtc, permitExpired: true);
            if (current.Fault != ExecutionLeaseFault.None)
                return MutationFailed(current.Fault, current.Reason!);
            _latest[grant.Claim.Resource] = new LeaseGeneration(current.Grant!.Value, releasedAtUtc);
            return new ExecutionLeaseMutationResult(ExecutionLeaseFault.None, current.Grant);
        }
    }

    public ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc)
    {
        if (!claim.IsValid || atUtc.Offset != TimeSpan.Zero)
            return new ExecutionLeaseValidationResult(ExecutionLeaseFault.InvalidInput, false, "The lease claim or time is invalid.");
        lock (_gate)
        {
            if (!_latest.TryGetValue(claim.Resource, out var latest) || latest.Grant.Claim != claim)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.NotCurrent, false, "The claim is not the latest generation.");
            if (latest.ReleasedAtUtc is not null)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.Released, false, "The lease was released.");
            if (latest.Grant.ExpiresAtUtc <= atUtc)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.Expired, false, "The lease expired.");
            return new ExecutionLeaseValidationResult(ExecutionLeaseFault.None, true);
        }
    }

    private ExecutionLeaseMutationResult Current(
        in ExecutionLeaseGrant grant,
        DateTimeOffset atUtc,
        bool permitExpired = false)
    {
        if (!_latest.TryGetValue(grant.Claim.Resource, out var latest) ||
            latest.Grant.Claim != grant.Claim || latest.Grant.OwnerId != grant.OwnerId)
            return MutationFailed(ExecutionLeaseFault.NotCurrent, "The lease is not the latest owner generation.");
        if (latest.ReleasedAtUtc is not null)
            return MutationFailed(ExecutionLeaseFault.Released, "The lease was released.");
        if (!permitExpired && latest.Grant.ExpiresAtUtc <= atUtc)
            return MutationFailed(ExecutionLeaseFault.Expired, "The lease expired.");
        return new ExecutionLeaseMutationResult(ExecutionLeaseFault.None, latest.Grant);
    }

    private static bool ValidAcquire(
        in ExecutionResource resource,
        in ExecutionLeaseId leaseId,
        in RuntimeInstanceId ownerId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc) =>
        resource.IsValid && !leaseId.IsEmpty && !ownerId.IsEmpty &&
        acquiredAtUtc.Offset == TimeSpan.Zero && expiresAtUtc.Offset == TimeSpan.Zero &&
        expiresAtUtc > acquiredAtUtc;

    private static ExecutionLeaseAcquireResult AcquireFailed(ExecutionLeaseFault fault, string reason) =>
        new(fault, null, reason);
    private static ExecutionLeaseMutationResult MutationFailed(ExecutionLeaseFault fault, string reason) =>
        new(fault, null, reason);
    private readonly record struct LeaseGeneration(ExecutionLeaseGrant Grant, DateTimeOffset? ReleasedAtUtc);
}
