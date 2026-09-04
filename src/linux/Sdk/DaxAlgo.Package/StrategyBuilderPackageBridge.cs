using System.Text;
using System.Text.Json;

namespace DaxAlgo.Package;

/// <summary>
/// StrategyBuilder → open-package publication seam.
///
/// <para>StrategyBuilder emits a hash-bound <c>ConfirmedStrategyRun</c>. That run is not an installable
/// Terminal package. This bridge attaches the confirmed-input digest as a provenance payload so
/// Extensions verification and Marketplace handoff can prove which reviewed run produced the
/// <c>.daxalgostrategy</c> submission.</para>
/// </summary>
public static class StrategyBuilderPackageBridge
{
    public const string ProvenanceLogicalPath = "provenance/strategybuilder-confirmed-run.v1.json";
    public const string ProvenanceSchema = "daxalgo.strategybuilder-package-provenance/1";

    /// <summary>
    /// Builds a package request that carries StrategyBuilder confirmation as an immutable provenance
    /// payload. Callers still supply strategy source/assembly payloads.
    /// </summary>
    public static DaxPackageRequest CreateStrategySubmission(
        string packageId,
        string version,
        string displayName,
        string entryTypeName,
        string confirmedRunId,
        string confirmedInputSha256,
        string reviewSha256,
        string confirmedBy,
        string confirmedAt,
        IReadOnlyList<DaxPayloadSource> strategyPayloads,
        string? description = null,
        string? publisher = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedRunId);
        RequireSha256(confirmedInputSha256, nameof(confirmedInputSha256));
        RequireSha256(reviewSha256, nameof(reviewSha256));
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedAt);
        ArgumentNullException.ThrowIfNull(strategyPayloads);

        var provenance = DaxPayloadSource.FromBytes(
            ProvenanceLogicalPath,
            DaxPayloadRole.Provenance,
            EncodeProvenance(
                confirmedRunId,
                confirmedInputSha256,
                reviewSha256,
                confirmedBy,
                confirmedAt));

        var payloads = new List<DaxPayloadSource>(strategyPayloads.Count + 1);
        payloads.AddRange(strategyPayloads);
        payloads.Add(provenance);

        return new DaxPackageRequest
        {
            Kind = DaxPackageKind.Strategy,
            Id = packageId,
            Version = version,
            DisplayName = displayName,
            EntryTypeName = entryTypeName,
            Description = description,
            Publisher = publisher,
            Payloads = payloads,
        };
    }

    /// <summary>Reads the confirmed-input digest from a provenance payload, or null if absent/malformed.</summary>
    public static string? TryReadConfirmedInputSha256(ReadOnlySpan<byte> provenanceBytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(provenanceBytes.ToArray());
            var root = doc.RootElement;
            if (!root.TryGetProperty("schema", out var schema) ||
                schema.GetString() != ProvenanceSchema)
                return null;
            if (!root.TryGetProperty("confirmedInputSha256", out var hash))
                return null;
            var value = hash.GetString();
            if (value is null || value.Length != 64) return null;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!ok) return null;
            }
            return value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] EncodeProvenance(
        string confirmedRunId,
        string confirmedInputSha256,
        string reviewSha256,
        string confirmedBy,
        string confirmedAt)
    {
        // Stable property order — this is digested as a package payload.
        var json =
            "{"
            + $"\"schema\":{JsonSerializer.Serialize(ProvenanceSchema)},"
            + $"\"runId\":{JsonSerializer.Serialize(confirmedRunId)},"
            + $"\"confirmedInputSha256\":{JsonSerializer.Serialize(confirmedInputSha256)},"
            + $"\"reviewSha256\":{JsonSerializer.Serialize(reviewSha256)},"
            + $"\"confirmedBy\":{JsonSerializer.Serialize(confirmedBy)},"
            + $"\"confirmedAt\":{JsonSerializer.Serialize(confirmedAt)}"
            + "}";
        return Encoding.UTF8.GetBytes(json);
    }

    private static void RequireSha256(string value, string name)
    {
        if (value.Length != 64)
            throw new ArgumentException("SHA-256 digests must be 64 lowercase hex characters.", name);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!ok)
                throw new ArgumentException("SHA-256 digests must be 64 lowercase hex characters.", name);
        }
    }
}
