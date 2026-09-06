# TradingTerminal.Infrastructure / Execution — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/ExecutionUnixSocket.cs
```cs
    9: public sealed class ExecutionUnixSocketServer : IAsyncDisposable
   20: public ExecutionUnixSocketServer(
   49: public string SocketPath => _socketPath;
   51: public async Task RunAsync(CancellationToken cancellationToken = default)
  104: public ValueTask DisposeAsync()
  167: public sealed class ExecutionUnixSocketClientEndpoint : IExecutionServiceEndpoint, IAsyncDisposable
  187: public ExecutionResource Resource { get; }
  188: public ExecutionLeaseGrant LeaseGrant { get; }
  190: public static async Task<ExecutionUnixSocketClientEndpoint> ConnectAsync(
  232: public ExecutionServiceExchange Handle(ExecutionServiceRequest request)
  287: public async ValueTask DisposeAsync()
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/MacExecutionServiceSecretStore.cs
```cs
    8: public interface IExecutionServiceSecretStore
   10:     byte[] LoadOrCreate();
   17: public sealed class MacExecutionServiceSecretStore : IExecutionServiceSecretStore
   26: public byte[] LoadOrCreate()
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/PaperExecutionServiceRuntime.cs
```cs
   10: public sealed class PaperExecutionServiceRuntime : IDisposable
   34: public SqliteOrderEventStore Ledger { get; }
   35: public DeterministicPaperVenue Venue { get; }
   36: public ReconciliationEngine Reconciliation { get; }
   37: public OrderManagementService Oms { get; }
   38: public ExecutionServiceEngine Service { get; }
   39: public ExecutionLeaseGrant LeaseGrant => _leaseGrant;
   45: public static PaperExecutionServiceRuntime Create(
  139: public ExecutionLeaseMutationResult RenewLease(TimeSpan leaseDuration)
  154: public void Dispose()
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/SqliteOrderEventStore.cs
```cs
    7: public enum SqliteExecutionLedgerFault : byte
   19: public readonly record struct SqliteExecutionLedgerIntegrity(
   25: public bool IsValid => Fault == SqliteExecutionLedgerFault.None;
   28: public sealed record DurableOrderRecoveryEntry(
   33: public enum SqliteAppendStage : byte
   42: public sealed class SqliteOrderEventStore :
   58: public SqliteOrderEventStore(
  118: public string DatabasePath { get; }
  119: public int SchemaVersion { get; }
  120: public string JournalMode { get; }
  121: public SqliteExecutionLedgerIntegrity Integrity => _integrity;
  122: public IReadOnlyList<DurableOrderRecoveryEntry> StartupRecovery => _startupRecoveryView;
  123: public bool CanAdmitNewOrders
  127: public bool CanAdmitAfterStartupReconciliation => CanAdmitNewOrders;
  129: public bool TryCompleteStartupReconciliation(ReconciliationCycleResult result)
  154: public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc)
  247: public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId)
  258: public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId)
  276: public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0)
  305: public IReadOnlyList<ReconciliationPositionSnapshot> ReadPositionProjections(ExecutionResource resource)
  332: public IReadOnlyList<ReconciliationCashSnapshot> ReadCashProjections(ExecutionResource resource)
  361: public SqliteExecutionLedgerIntegrity VerifyIntegrity()
  371: public bool TryAppend(ReconciliationCase reconciliationCase)
  422: public IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource)
  442: public IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId)
  452: public ExecutionLeaseAcquireResult Acquire(
  507: public ExecutionLeaseMutationResult Renew(
  546: public ExecutionLeaseMutationResult Release(
  580: public ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc)
  598: public void Dispose()
```
