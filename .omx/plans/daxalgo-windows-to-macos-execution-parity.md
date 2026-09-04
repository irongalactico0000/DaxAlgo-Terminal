# DaxAlgo Windows-to-macOS execution parity plan

## Outcome

Build a native macOS/Avalonia execution path that preserves the current Windows OMS behavior without
importing WPF, DPAPI, Windows named-pipe, Windows-service, or hard-coded TWS installation assumptions.
The first shippable milestone is a complete **Paper** vertical slice. Real IB, cTrader, and Alpaca
routing remain independently disabled until their own authorization, recovery, and vendor acceptance
gates pass.

This is an implementation plan, not evidence that the missing functionality is already complete.

## Baseline and hard constraints

- Windows functional authority is the public repository execution tree at
  `../DaxAlgo-Terminal/src/windows/Execution/`.
- The Mac repository currently has no execution project reference in
  `src/linux/Shell/TradingTerminal.App.Avalonia/TradingTerminal.App.Avalonia.csproj:13`.
- The Mac app already has the portable strategy contract at
  `src/linux/Sdk/DaxAlgo.Sdk/IStrategyKernel.cs:11` and a headless runtime at
  `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxStrategyRuntime.cs:31`, but the app does
  not compose that runtime.
- A connected Mac broker is currently a market-data session. The header nevertheless renders
  `LIVE` beside `ConnectedBrokerCount` at
  `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml:310`; this must be corrected
  before an execution mode is exposed.
- The repository invariant in `AGENTS.md` currently says `introduce no live order-execution path`.
  Therefore Paper work may proceed, but any real-order composition is a hard stop until that invariant
  is deliberately changed by the repository owner.
- The current worktree contains unrelated modified and untracked work. Every implementation packet
  must preserve it and use a dedicated task record as required by `AGENTS.md` and
  `.claude/context/PROTOCOL.md:3`.
- Windows execution targets `net9.0-windows7.0` and includes platform-specific dependencies in
  `../DaxAlgo-Terminal/src/windows/Execution/TradingTerminal.Execution/TradingTerminal.Execution.csproj:4`.
  It must be decomposed, not copied as one project.

## Product acceptance milestones

| Milestone | User-visible result | Required proof | Explicitly does not unlock |
|---|---|---|---|
| M0 — Baseline locked | Existing Mac app behavior remains unchanged | Mac solution build plus existing focused tests | Any execution feature |
| M1 — Portable execution kernel | Domain, event chain, risk, OMS, leases, and reconciliation compile on plain `net9.0` | Ported Windows contract tests pass on macOS | App menu or broker routing |
| M2 — Durable Paper engine | Simulator orders survive restart and reconcile deterministically | SQLite, crash/restart, dedupe, fencing, and simulation tests pass | Real broker adapters |
| M3 — Strategy-to-Paper slice | A launched `IStrategyKernel` target becomes a risk-admitted simulated order and fill | End-to-end strategy→virtual book→replicator→OMS→ledger→projection test | Live mode |
| M4 — Avalonia execution console | User can inspect Paper orders, fills, positions, risk decisions, and faults | Headless Avalonia interaction tests and one manual visual pass | Live mode |
| M5 — Secure Mac service boundary | Execution can run behind authenticated owner-only local IPC | UDS permissions, Keychain secret, handshake, replay, frame-limit, reconnect tests | Any broker live flag |
| M6A — Alpaca paper | Full Alpaca paper account submit/cancel/replace/reconcile workflow | Vendor paper-account acceptance evidence | Alpaca live or other brokers |
| M6B — cTrader demo | Full cTrader demo account workflow within its actual capabilities | Vendor demo-account acceptance evidence | cTrader live or other brokers |
| M6C — IB paper | Full TWS/Gateway paper workflow and restart recovery | Vendor paper-account acceptance evidence on arm64/x64 packaging | IB live or other brokers |
| M7A/M7B/M7C — Live per broker | One specific broker may route real orders | Repository invariant revised; two authorization gates; all generic and broker-specific live tests pass | Other brokers automatically |

## Dependency order

```text
portable values and identities
  -> order instruction and lifecycle
  -> append-only events and projections
  -> SQLite ledger and recovery
  -> risk and admission
  -> OMS commands
  -> lease/fencing and reconciliation
  -> simulated venue/adapter
  -> service contract and Paper runtime
  -> strategy virtual-book replicator
  -> Avalonia execution console
  -> authenticated macOS IPC
  -> broker paper adapters (Alpaca, cTrader, IB independently)
  -> broker live gates (independently, only after repository policy changes)
```

## Granular implementation packets

### Phase 0 — Freeze evidence and prevent accidental regressions

- [ ] **E00. Record immutable comparison SHAs.** Put the Windows `main` SHA, Mac base SHA, dirty-file
  inventory, .NET SDK version, architecture, and macOS version in a new task record. Source authority:
  `docs/windows-macos-functional-inventory.md:5`. Acceptance: another developer can check out the two
  exact committed baselines; uncommitted Mac files are listed separately.
- [ ] **E01. Run the named Mac baseline.** Run `dotnet build TradingTerminal.Mac.slnx`, then the focused
  package, SDK drawing, sandbox portfolio, sandbox runtime, sandbox contract, Avalonia app, and UI Core
  tests. The required broad build/test commands are stated in `AGENTS.md:22`. Acceptance: record exact
  pass/fail/skip counts and preserve the known headless `/var` path issue as a separate pre-existing
  failure rather than hiding it.
- [ ] **E02. Correct execution language before adding execution.** Replace the market-data header word
  `LIVE` with `CONNECTED` or `DATA` in
  `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml:310`; retain
  `ConnectedBrokerCount` from `MainWindowViewModel.cs:262`. Acceptance: connecting a data broker never
  displays a live-trading claim; add a headless binding/text assertion.
- [ ] **E03. Add explicit capability vocabulary.** Define separate `MarketDataCapabilities` and
  `ExecutionCapabilities`; never infer execution from `IBrokerClient` connectivity. The existing Mac
  market-data-only contract is `src/linux/Core/TradingTerminal.Core/MarketData/IBrokerClient.cs:16`;
  the Windows execution capability model begins in
  `../DaxAlgo-Terminal/src/windows/Execution/TradingTerminal.Execution/Oms/BrokerExecutionAdapter.cs:275`.
  Acceptance: IB/cTrader/Alpaca can report data connected while execution unavailable, and every other
  broker reports execution unsupported.
- [ ] **E04. Decide repository policy before live work.** Keep `AGENTS.md` unchanged for M0–M6. Before
  any M7 work, create a reviewed policy change that names permitted live brokers, safety prerequisites,
  and rollback behavior. Acceptance: no `AllowLiveExecution=true` path can be composed while the
  current invariant remains.

**Phase 0 stop condition:** baseline is reproducible, data connectivity is no longer labeled live, and
execution capability cannot be inferred from broker connection state.

### Phase 1 — Create portable project boundaries

- [ ] **E10. Create `TradingTerminal.Execution.Contracts` (`net9.0`).** It owns scaled primitives,
  identifiers, order enums/records, lifecycle, public service DTOs, and adapter DTOs only. Candidate
  Windows sources are `ScaledValues.cs`, `UnitDefinition.cs`, `TradeIntent.cs`,
  `Oms/OrderIdentifiers.cs`, `Oms/OrderDomain.cs`, `Oms/OrderLifecycle.cs`, and
  `Service/ExecutionServiceContract.cs:6`. Acceptance: no project reference to Avalonia,
  Infrastructure, SQLite, broker SDKs, or operating-system APIs.
- [ ] **E11. Create `TradingTerminal.Execution` (`net9.0`).** It owns event-chain logic, in-memory store,
  risk, OMS, leases, reconciliation, coordinator, simulation, and replicator. Windows source seams:
  `Oms/OrderEventStore.cs:98`, `RiskEngine.cs:118`, `Oms/OrderManagementService.cs:111`,
  `Oms/ExecutionLease.cs:67`, `Oms/ReconciliationEngine.cs:95`, and
  `SandboxExecutionReplicator.cs:18`. Acceptance: portable project compiles on macOS without
  `System.Security.Cryptography.ProtectedData`, WPF, or Windows targeting.
- [ ] **E12. Create `TradingTerminal.Execution.Sqlite` (`net9.0`).** It owns schema, durable event store,
  projections, leases, and recovery using `Microsoft.Data.Sqlite`. Windows sources:
  `Oms/SqliteOrderLedgerSchema.cs:6` and `Oms/SqliteOrderEventStore.cs:12`. Acceptance: no UI, broker,
  or macOS Security.framework dependency.
- [ ] **E13. Create `TradingTerminal.Execution.Mac` (`net9.0`).** It owns Application Support path
  resolution, Keychain-backed confirmation/service secrets, owner-only Unix-domain sockets, and
  launch lifecycle. It replaces Windows implementations at
  `Ipc/DpapiExecutionServiceSecretStore.cs:17` and `Ipc/SecureExecutionNamedPipe.cs:11`.
  Acceptance: the portable projects have no reference back to this project.
- [ ] **E14. Create `TradingTerminal.ExecutionUi.Core` (`net9.0`).** Port UI read models, analytics,
  command state, mode projection, book store interfaces, and confirmation orchestration without WPF.
  Windows behavior sources are under `../DaxAlgo-Terminal/src/windows/Execution/TradingTerminal.ExecutionUi/`.
  Acceptance: view models build without `UseWPF` or `ScottPlot.WPF` (both are present in the Windows UI
  project at `TradingTerminal.ExecutionUi.csproj:5` and `:20`).
- [ ] **E15. Create `TradingTerminal.ExecutionUi.Avalonia` (`net9.0`).** It owns AXAML views, Avalonia
  dialogs, commands, and chart adapters only. Acceptance: Core/Contracts/Execution have no Avalonia
  reference and this project has no broker SDK reference.
- [ ] **E16. Add projects to `TradingTerminal.Mac.slnx` without composing the app.** Add matching test
  projects at the same time. Acceptance: the solution builds while the current application behavior
  remains unchanged; run `.claude/context/manage-context.ps1 deep-check` because project topology
  changed, as required by `.claude/context/PROTOCOL.md:8`.

**Phase 1 stop condition:** six correctly directed project seams compile, but no execution menu or
adapter is reachable from the Mac application.

### Phase 2 — Port exact values, identities, and order semantics

- [ ] **E20. Port scaled numeric types and checked math.** Preserve overflow, scale mismatch, rounding,
  sign, and conversion behavior from `ScaledValues.cs:57` and `UnitDefinition.cs:4`. Acceptance: table
  tests cover zero, min/max, overflow, incompatible units, positive/negative quantities, and round-trip
  serialization.
- [ ] **E21. Port every identity wrapper.** Preserve intent, bucket, leg, client-order, broker-order,
  exchange-order, correlation, causation, lease, fencing, deduplication, and reconciliation IDs from
  `Oms/OrderIdentifiers.cs`. Acceptance: empty/default values are rejected where Windows rejects them;
  equality, hash, parse, and serialization contract tests match Windows fixtures.
- [ ] **E22. Port canonical order enums.** Preserve Market, Limit, Stop, StopLimit and Day, GTC, IOC,
  FOK from `Oms/OrderDomain.cs:6` and `:22`. Acceptance: unknown numeric enum values fail closed at
  contract boundaries.
- [ ] **E23. Port `CanonicalOrderInstruction`.** Preserve required fields and type-specific price rules
  from `Oms/OrderDomain.cs:153`. Acceptance: Market rejects limit/stop prices; Limit requires limit;
  Stop requires stop; StopLimit requires both; quantity and instrument validation match Windows.
- [ ] **E24. Port all 17 lifecycle states and legal transitions.** Authority:
  `Oms/OrderLifecycle.cs:6` and `:66`. Acceptance: every legal edge is a positive test and every other
  state pair is rejected, including terminal-state mutation.
- [ ] **E25. Port public-order seam mapping.** Preserve explicit mapping faults from
  `Oms/PublicOrderSeamMapper.cs:9`. Acceptance: no SDK/public order silently defaults into a canonical
  value; unsupported types and TIFs return a typed fault.

**Phase 2 stop condition:** order intent has a deterministic, serializable meaning before any store,
UI, or broker sees it.

### Phase 3 — Port append-only events, projections, and the durable ledger

- [ ] **E30. Port event kinds, sources, drafts, and immutable events.** Authority:
  `Oms/OrderEvents.cs:8`, `:87`, `:109`, and `:130`. Acceptance: required event payloads are validated
  per kind and event timestamps/sequence IDs are explicit.
- [ ] **E31. Port event hash generation.** Preserve canonical byte ordering and previous-hash binding
  from `Oms/OrderEvents.cs:152`. Acceptance: Windows-produced golden events yield identical hashes on
  macOS; a one-bit mutation in any bound field changes the hash.
- [ ] **E32. Port hash-chain verification.** Authority: `Oms/OrderProjection.cs:64`. Acceptance: reject
  missing genesis, sequence gaps, duplicates, previous-hash mismatch, event-hash mismatch, and events
  after a terminal state.
- [ ] **E33. Port deterministic projections.** Authority: `Oms/OrderProjection.cs:226` and `:265`.
  Acceptance: replaying the same ordered event set yields identical order state; out-of-order input is
  rejected rather than sorted silently.
- [ ] **E34. Port `IOrderEventStore` and the in-memory implementation.** Authority:
  `Oms/OrderEventStore.cs:98` and `:133`. Acceptance: optimistic append, dedupe, outbox ordering,
  transition checks, and concurrent append behavior pass before SQLite begins.
- [ ] **E35. Port the SQLite schema exactly, then adapt only the path.** Preserve application ID,
  `user_version`, migrations, foreign keys, 5-second busy timeout, `synchronous=FULL`, WAL, event hashes,
  outbox, inbox dedupe, risk decisions, reconciliation cases, and lease generations from
  `Oms/SqliteOrderLedgerSchema.cs:6`. Acceptance: unrelated SQLite files are rejected; every declared
  table/index/trigger is asserted by schema tests.
- [ ] **E36. Define the Mac ledger location.** Resolve under the user’s Application Support directory,
  grouped by environment, broker, and account; never reuse the Windows `LocalApplicationData` default
  at `Oms/SqliteOrderEventStore.cs:183`. Acceptance: paths contain no Windows separator assumptions,
  directories are owner-only, and Paper and Real can never share a ledger file.
- [ ] **E37. Port durable append plus projection transactions.** Acceptance: event, projection, audit,
  and outbox effects commit atomically; injected failure at every write boundary leaves either the old
  or new complete state, never a partial state.
- [ ] **E38. Port startup integrity and recovery classification.** Authority:
  `Oms/SqliteOrderEventStore.cs:12`. Acceptance: clean, recoverable, reconciliation-required, schema-
  mismatch, hash-corrupt, and unrelated-database outcomes are distinct and tested.
- [ ] **E39. Add corruption quarantine.** On integrity/hash/schema failure, move no data automatically;
  open read-only for diagnosis, create no new live session, and surface a blocking user fault.
  Acceptance: the engine cannot arm or submit against a corrupt ledger.

**Phase 3 stop condition:** an append-only, hash-verified, transactionally projected Paper ledger
survives restart and fails closed on corruption.

### Phase 4 — Port risk, OMS, lease/fencing, and reconciliation

- [ ] **E40. Port `RiskPolicy` and its validation.** Authority: `RiskPolicy.cs:8` and `:40`.
  Acceptance: invalid, contradictory, missing, and overflow-prone limits are rejected at construction.
- [ ] **E41. Port `RiskEngine`.** Authority: `RiskEngine.cs:6`, `:17`, and `:118`. Acceptance: every
  reason code has a positive rejection test; boundary-equal values have explicit allow/reject behavior;
  decisions are recorded using scaled values, not floating point.
- [ ] **E42. Port signal-to-intent policy.** Authority: `SignalExecutionPolicy.cs:7` and `:64` plus
  `TradeIntent.cs:6`. Acceptance: flat/long/short target changes, quantity modes, reversal, no-op, and
  invalid price/quantity cases map deterministically.
- [ ] **E43. Port OMS submit.** Authority: `Oms/OrderManagementService.cs:17` and `:111`.
  Acceptance: validate → risk decision → append intent/events → adapter dispatch follows one explicit
  order; adapter dispatch never occurs if validation, risk, append, or lease validation fails.
- [ ] **E44. Port OMS cancel.** Acceptance: only cancellable states admit cancel; duplicate cancellation
  is idempotent; unknown broker identity opens a reconciliation path instead of fabricating success.
- [ ] **E45. Port OMS replace.** Acceptance: pending replace and broker-specific native/cancel-replace
  semantics are explicit; partial fill adjusts remaining quantity; stale causation IDs cannot replace a
  newer version.
- [ ] **E46. Port lease acquisition and monotonic fencing.** Authority: `Oms/ExecutionLease.cs:9`,
  `:67`, `:140`, and `:190`. Acceptance: a second owner cannot acquire a live lease; an expired/stale
  fencing token cannot submit; restart increments generation durably.
- [ ] **E47. Port reconciliation case storage.** Authority: `Oms/Reconciliation.cs:4`, `:32`, `:48`,
  `:64`, and `:120`. Acceptance: order, fill, position, and cash discrepancies retain evidence and
  resolution history.
- [ ] **E48. Port reconciliation cycles.** Authority: `Oms/ReconciliationEngine.cs:12`, `:31`, and
  `:95`. Acceptance: clean match, missing local, missing broker, quantity mismatch, cash mismatch,
  duplicate execution, unknown status, and reconnect/restart triggers are separately tested.
- [ ] **E49. Port coordinator admission ordering.** Authority: `Oms/ExecutionCoordinator.cs:4` and
  `:49`. Acceptance: mode, account, session health, capability, trading-hours, rate-limit, lease,
  authorization, and risk failures are distinguishable; first failure prevents dispatch.

**Phase 4 stop condition:** the engine can prove why an order was admitted or rejected and can prevent
two processes from controlling the same account.

### Phase 5 — Build deterministic Paper execution first

- [ ] **E50. Port the simulated venue.** Authority: `Oms/SimulatedVenue.cs:10` through `:229`.
  Acceptance: market, resting limit, stop trigger, stop-limit activation, partial fill, cancel, replace,
  reject, and expiry scenarios are deterministic under a supplied clock.
- [ ] **E51. Port the simulated adapter behind `IBrokerExecutionAdapter`.** Authority:
  `Oms/SimulatedExecutionAdapter.cs:11` and `:73`; adapter contract at
  `Oms/BrokerExecutionAdapter.cs:781`. Acceptance: the same OMS command path used by future brokers is
  used for Paper; no special UI-only fill path exists.
- [ ] **E52. Port service request/response/event contracts.** Authority:
  `Service/ExecutionServiceContract.cs:6`, `:16`, `:55`, `:79`, `:119`, and `:136`.
  Acceptance: protocol version mismatch, malformed payload, unsupported request, duplicate request,
  and cancellation are typed faults.
- [ ] **E53. Port the service engine and Paper runtime.** Authority:
  `Service/ExecutionServiceEngine.cs:9` and `Service/ExecutionServiceRuntime.cs:17`. Acceptance: a
  temporary SQLite ledger supports submit→working→fill→projection→outbox; restart restores the same
  order/position and runs reconciliation before accepting another command.
- [ ] **E54. Add crash/failure injection.** Kill or fault after intent append, dispatch receipt, broker
  ack, partial fill, cancel request, replace request, and outbox publication. Acceptance: restart
  produces no duplicate external command and no lost committed event.
- [ ] **E55. Port the Windows execution contract suite before changing semantics.** Start with the
  lifecycle, event store, SQLite, risk, lease, reconciliation, simulated venue, service integration,
  and coordinator tests under `../DaxAlgo-Terminal/tests/TradingTerminal.Execution.Tests/`.
  Acceptance: every portable upstream test is either green or listed with a precise platform/behavior
  reason; no blanket skips.

**Phase 5 stop condition:** Paper orders are durable, recoverable, and exercised through the canonical
OMS—not through backtest or market-data simulation shortcuts.

### Phase 6 — Connect the existing strategy runtime to Paper OMS

- [ ] **E60. Compose the existing runtime in the Mac app.** Add references to
  `TradingTerminal.Sandbox.Runtime` and `TradingTerminal.Sandbox.Portfolio` to the app project, which
  currently stops at package/SDK/pipeline references in
  `TradingTerminal.App.Avalonia.csproj:13`. Acceptance: selecting an authored strategy creates one
  `SandboxStrategyRuntime` using the registered `IStrategyKernel` factory.
- [ ] **E61. Add lifecycle UI state without execution.** Expose Run, Pause, Resume, Stop, runtime fault,
  and current parameters. Runtime states and transitions already exist at
  `SandboxStrategyRuntime.cs:14`, `:161`, `:194`, `:218`, and `:281`. Acceptance: invalid transitions
  are disabled and closing the strategy drains/disposes callbacks deterministically.
- [ ] **E62. Port `SandboxExecutionReplicator`.** Authority: `SandboxExecutionReplicator.cs:18`, `:38`,
  and `:55`. Acceptance: virtual target deltas produce bounded execution targets; duplicates coalesce;
  stale snapshots, wrong account/book, and tripped portfolio state do not submit.
- [ ] **E63. Enforce the only legal route.** The dependency direction must be
  `IStrategyKernel -> IVirtualBook -> SandboxExecutionReplicator -> IExecutionBookTargetIntake -> OMS`.
  The virtual-book contract is `src/linux/Sdk/DaxAlgo.Sdk/SandboxContexts.cs:113`. Acceptance: strategy
  assemblies cannot reference an adapter or execution service directly; architecture tests enforce it.
- [ ] **E64. Add a complete strategy Paper acceptance test.** Feed deterministic bars/ticks to a test
  kernel, assert target generation, risk decision, canonical order, simulated fill, ledger chain,
  position/equity update, and UI projection. Acceptance: causation/correlation IDs link every stage.
- [ ] **E65. Prove stop and fault safety.** Stop the kernel during pending order, partial fill, and
  disconnected Paper adapter. Acceptance: policy explicitly cancels or retains orders; no orphaned
  in-memory target silently disappears; faults appear in ledger/UI.

**Phase 6 stop condition:** a user-authored strategy completes an observable, audited Paper trade from
market event to portfolio projection.

### Phase 7 — Build the Avalonia Execution Console

- [ ] **E70. Port read models and analytics to UI Core.** Port orders, fills, positions, cash, risk,
  reconciliation, and summary metrics from the Windows execution UI directory. Acceptance: pure view-
  model tests do not initialize Avalonia or WPF.
- [ ] **E71. Port execution mode projection.** Preserve unmistakable Paper/Real styling and text from
  Windows `ExecutionModeStatusProjection`; tests originate in
  `../DaxAlgo-Terminal/tests/TradingTerminal.ExecutionUi.Tests/ExecutionModeStatusProjectionTests.cs:6`.
  Acceptance: Paper and Real cannot share color, label, banner, or confirmation text.
- [ ] **E72. Port execution-book persistence.** Adapt the Windows `books.json` default at
  `TradingTerminal.ExecutionUi/ExecutionBookStore.cs:73` to Application Support, using atomic replace,
  schema/version validation, and corrupt-file failure handling. Acceptance: the upstream persistence
  cases at `ExecutionBookPersistenceTests.cs:10` pass with Mac paths.
- [ ] **E73. Rebuild the console in AXAML.** Translate behavior from Windows
  `ExecutionConsoleView.xaml`; replace `ScottPlot.WPF` referenced at line 9 with an Avalonia-native
  chart or existing DaxAlgo render control. Acceptance: order ticket, open orders, fills, positions,
  risk decisions, reconciliation cases, session health, and faults are keyboard reachable and bound.
- [ ] **E74. Add the shell menu and books chip last.** Add `Execution Engine > Console`, Paper mode
  status, and books chip to `MainWindow.axaml`; wire commands in `MainWindowViewModel.cs`. Acceptance:
  navigation opens one console instance, closing/reopening preserves Paper book state, and no action
  exposes Real mode yet.
- [ ] **E75. Port UI tests and add Avalonia interaction tests.** Use Windows test intent from
  `../DaxAlgo-Terminal/tests/TradingTerminal.ExecutionUi.Tests/`; adapt WPF-only mechanics.
  Acceptance: ticket validation, mode banners, confirmation cancellation, account selection, books,
  order rows, and fault/reconciliation actions pass headlessly.
- [ ] **E76. Perform a manual visual/state audit.** Capture Paper idle, submitting, working, partial,
  filled, cancelled, rejected, disconnected, recovering, and corrupt-ledger states. Acceptance: each
  state is visually distinguishable and no market-data connection is shown as execution-ready.

**Phase 7 stop condition:** users can operate and diagnose the complete Paper path from native
Avalonia UI, while Real remains absent or hard-disabled.

### Phase 8 — Replace Windows IPC/security with macOS-native boundaries

- [ ] **E80. Keep the portable authentication protocol.** Port nonce, challenge, proof, completion,
  replay, and protocol-version behavior from `Ipc/ExecutionPipeAuthentication.cs:8` through `:103`.
  Acceptance: wrong secret, reflected proof, replayed nonce, expired handshake, downgrade, and timing-
  safe comparison cases fail.
- [ ] **E81. Keep bounded frame transport.** Port the stream-neutral interface and framing from
  `Ipc/ExecutionFrameTransport.cs:8` and `:21`. Acceptance: zero, truncated, oversized, malformed,
  cancelled, and interleaved frames fail without allocating attacker-controlled sizes.
- [ ] **E82. Implement an owner-only Unix-domain socket.** Replace
  `ExecutionNamedPipeServer`/`Client` at `Ipc/ExecutionServicePipe.cs:11` and `:171`. Bind under an
  owner-only runtime directory, reject symlinks/non-socket pre-existence, set `0600`, and verify peer
  UID where the runtime supports it. Acceptance: another local user/process without the Keychain
  secret cannot authenticate; TCP and non-loopback transports are not enabled.
- [ ] **E83. Implement Keychain-backed service secrets.** Replace DPAPI usage at
  `Ipc/DpapiExecutionServiceSecretStore.cs:66` and `:108`. Acceptance: generate 256-bit secret once,
  read only through Keychain, rotate explicitly, never log/export it, and fail closed when Keychain is
  denied or locked.
- [ ] **E84. Add service lifecycle supervision.** Start the execution host as a child or reviewed
  `launchd` agent, enforce one owner, terminate/recover predictably, and use a versioned protocol.
  Acceptance: app crash, service crash, stale socket, stale PID, upgrade mismatch, and duplicate start
  tests have deterministic recovery.
- [ ] **E85. Run IPC hostile tests.** Port the upstream handshake tests beginning at
  `../DaxAlgo-Terminal/tests/TradingTerminal.Execution.Tests/ExecutionPipeAuthenticationTests.cs:9`
  and add socket-permission, symlink, peer-identity, secret-denied, replay, oversized-frame, and
  concurrent-client cases. Acceptance: all pass on both osx-arm64 and osx-x64 CI hosts.

**Phase 8 stop condition:** privileged Paper execution works out of process and rejects local
impersonation/replay. This still does not enable Real mode.

### Phase 9 — Port brokers independently, paper/demo first

#### Alpaca

- [ ] **E90A. Port Alpaca execution options and endpoint allowlist.** Authority:
  `Alpaca/AlpacaExecutionOptions.cs:23`, `:88`, `:137`, and `:166`. Reuse the Mac Keychain credential
  seam; do not duplicate plaintext keys. Acceptance: paper endpoint is default; account-ID or endpoint
  mismatch blocks construction; live endpoint cannot be selected under the current repository policy.
- [ ] **E91A. Port HTTP order transport.** Implement submit, cancel, replace, get order, open orders,
  positions, account/cash, error mapping, bounded response bodies, timeouts, and cancellation.
  Acceptance: scripted transport tests from `AlpacaHttpExecutionTransportTests.cs:9` pass on macOS.
- [ ] **E92A. Port trade-update streaming and reconnect.** Preserve broker order IDs, fills, partials,
  cancels, rejects, replacements, commissions where provided, duplicate/out-of-order handling, and
  resubscription. Acceptance: disconnect during every lifecycle stage triggers reconciliation before
  new commands.
- [ ] **E93A. Enforce asset-specific capabilities.** Do not expose order types/TIFs only because mapper
  functions exist; use runtime capabilities from `AlpacaExecutionAdapter.cs:1253`.
  Acceptance: stock and crypto capability matrices are separate; unsupported combinations are rejected
  pre-dispatch.
- [ ] **E94A. Run Alpaca paper acceptance.** Submit/cancel/replace Market, Limit, Stop, StopLimit only
  where the account/asset reports support; verify fills, reconnect, restart, positions, cash, and ledger
  reconciliation. Acceptance evidence records account environment, app build, timestamps, and redacted
  broker IDs.

#### cTrader

- [ ] **E90C. Port cTrader options/authentication.** Preserve application/account authorization,
  environment endpoints, token refresh, expected account validation, and live gate at
  `CTrader/CTraderExecutionOptions.cs:168`. Acceptance: demo is default; wrong account/environment
  fails before command admission.
- [ ] **E91C. Port protobuf/TLS transport.** `cTrader.OpenAPI.Net` and `Google.Protobuf` are dependencies
  in the Windows execution project at `TradingTerminal.Execution.csproj:71`. Acceptance: connect,
  heartbeat, auth, symbol metadata, order events, reconnect, cancellation, and disposal are deterministic.
- [ ] **E92C. Enforce actual cTrader capabilities.** Runtime support is Market/Limit/Stop and
  GTC/IOC/FOK; StopLimit is rejected by the current Windows adapter. Acceptance: the Mac console never
  offers StopLimit or Day for cTrader, and contract tests verify pre-dispatch rejection.
- [ ] **E93C. Port submit/cancel/replace/reconciliation.** Preserve volume/price conversion from broker
  symbol metadata and broker-specific replace semantics. Acceptance: rounding cannot increase risk;
  reconnect blocks submission until orders, executions, positions, and cash reconcile.
- [ ] **E94C. Run cTrader demo acceptance.** Exercise supported type/TIF combinations, partials,
  cancel/replace, disconnect, token expiry, service restart, and broker/local mismatch cases.

#### Interactive Brokers

- [ ] **E90I. Resolve the official TWS C# API for macOS packaging.** Replace Windows probes such as
  `C:\\TWS API` in `TradingTerminal.Execution.csproj:19` with an explicit repository/external build
  input and conditional `HAS_IBAPI`. Acceptance: build output says clearly whether real transport is
  included; a missing SDK cannot produce a UI that claims IB execution is available.
- [ ] **E91I. Separate data and execution client identities.** Use an explicit IB execution client ID,
  account, host, port, paper/live environment, and session ownership. Acceptance: market-data connect
  does not authorize execution and client-ID collision is a typed, visible fault.
- [ ] **E92I. Port contract qualification and order-ID sequencing.** Resolve conId/exchange/currency/
  multiplier/minTick before admission; initialize and monotonically consume IB next-valid order IDs.
  Acceptance: reconnect never reuses an order ID and ambiguous contracts cannot submit.
- [ ] **E93I. Port all supported order/TIF mappings.** Preserve Market, Limit, Stop, StopLimit and
  Day/GTC/IOC/FOK behavior from the Windows IB adapter. Acceptance: exact outbound IB fields and inbound
  status mappings have golden tests; unsupported exchange-specific combinations reject locally.
- [ ] **E94I. Serialize callback processing.** Translate TWS callbacks for openOrder, orderStatus,
  execDetails, commissionReport, positions, account/cash, errors, connection close, and nextValidId
  through one ordered scheduler. Acceptance: concurrent callbacks cannot reorder the ledger chain.
- [ ] **E95I. Port IB cancel/replace/reconnect/reconciliation.** Acceptance: duplicate callbacks,
  permId/orderId remapping, partial fills, late commissions, TWS restart, Gateway restart, and app
  restart converge without duplicate external orders.
- [ ] **E96I. Run IB paper acceptance on packaged Mac builds.** Test TWS and Gateway where supported,
  osx-arm64 and osx-x64/Rosetta assumptions, signing, loading the C# assembly, and the full supported
  lifecycle matrix. Acceptance evidence must come from a paper account; mocks alone do not pass M6C.

**Phase 9 stop condition:** each broker earns its own paper/demo milestone. Failure of one broker does
not block Paper simulator or falsely mark another broker complete.

### Phase 10 — Add Real authorization only after policy approval

- [ ] **E100. Obtain the repository-policy change.** Revise the `AGENTS.md` live-execution prohibition
  only after M0–M6 evidence is reviewed. Acceptance: the change names exactly which broker is allowed;
  absence of approval means stop here.
- [ ] **E101. Port the session gate.** Preserve `ExecutionMode` and exact, non-persisted `LIVE`
  confirmation behavior from `Oms/LiveExecutionAuthorization.cs:9` and `:315`. Acceptance: every app
  and execution-service restart returns to Paper; whitespace/case/other text fails.
- [ ] **E102. Implement the per-account gate in Keychain.** Replace the DPAPI confirmation store at
  `Oms/LiveExecutionAuthorization.cs:123`; bind confirmation to broker ID, normalized account ID,
  environment, adapter version/capability fingerprint, and explicit user acknowledgment. Acceptance:
  changing any bound value revokes authorization.
- [ ] **E103. Require both gates at adapter construction and reconnect.** Preserve the Windows adapter
  checks at `AlpacaExecutionOptions.cs:166`, `CTraderExecutionOptions.cs:168`, and
  `InteractiveBrokersExecutionOptions.cs:226`. Acceptance: neither UI state nor a stale adapter object
  can bypass a missing gate.
- [ ] **E104. Add live blast-radius controls.** Maximum order value/quantity, daily loss, open-order
  count, position exposure, stale-price age, session hours, kill switch, and cancel-all behavior must
  be explicit and ledgered. Acceptance: each can be tripped deterministically and blocks dispatch.
- [ ] **E105. Enable one broker at a time.** Start with the broker whose M6 evidence and operational
  recovery are strongest; keep the other live feature flags absent. Acceptance: a release manifest
  names enabled live brokers and exact supported order/TIF combinations.

**Phase 10 stop condition:** only one explicitly approved, fully evidenced broker can leave Paper; all
others remain fail-closed.

### Phase 11 — CI, signing, release, and operational evidence

- [ ] **E110. Add execution projects/tests to Mac CI.** Run portable tests on every change and Mac-
  native Keychain/UDS tests on osx-arm64 and osx-x64 runners. Acceptance: missing optional IB SDK is a
  reported capability state, not a silent untested success.
- [ ] **E111. Add broker acceptance jobs as opt-in protected workflows.** Secrets come from protected
  environments; forks and normal PRs cannot access them; paper/demo only by default. Acceptance: logs
  redact keys, tokens, account IDs, socket secrets, and broker payloads that contain credentials.
- [ ] **E112. Package and sign all nested execution binaries.** Extend the existing Mac packaging
  order to the execution host and optional broker assemblies. Acceptance: `codesign --verify --deep
  --strict`, notarization, stapling, and Gatekeeper assessment succeed on the final artifact when
  credentials are configured.
- [ ] **E113. Add release capability manifest.** Publish architecture, app version, protocol version,
  schema version, SDK version, included broker transports, supported order/TIF matrices, and Paper/Real
  enablement. Acceptance: the app reads and displays the same manifest; unavailable transports are not
  shown as connectable execution routes.
- [ ] **E114. Add rollback and migration tests.** Upgrade from prior ledger/schema, reject unsupported
  downgrade, preserve audit history, and return to Paper after rollback. Acceptance: release rollback
  cannot accidentally preserve live arming or reuse an incompatible service secret/protocol.
- [ ] **E115. Produce a per-release evidence bundle.** Include test results, schema hash, golden event
  hash fixtures, package hashes, signing/notarization results, broker paper acceptance timestamps, and
  known limitations. Acceptance: no broker is called complete without its vendor evidence.

## First implementation run: exact one-by-one order

Do not begin with a broker adapter or the console. The next implementation session should stop after
the first failed gate and resume from that gate:

1. E00 — record SHAs, environment, and dirty worktree.
2. E01 — reproduce baseline build/tests.
3. E02 — fix the false `LIVE` market-data wording and test it.
4. E03 — introduce separate data/execution capability contracts and tests.
5. E10 — create the portable Contracts project.
6. E20–E25 — port values, IDs, instructions, lifecycle, and mappings with tests.
7. E11 — create the portable engine project.
8. E30–E34 — port events, chains, projections, and in-memory store.
9. E12 — create the SQLite project.
10. E35–E39 — port ledger, paths, transactions, recovery, and corruption gates.
11. E40–E49 — port risk, OMS, fencing, reconciliation, and coordinator.
12. E50–E55 — prove deterministic durable Paper execution.
13. E60–E65 — connect strategy runtime to Paper OMS.
14. E70–E76 — expose and visually verify the Avalonia Paper console.
15. E80–E85 — move Paper behind secure Mac-native IPC.
16. E90A–E94A — Alpaca paper; E90C–E94C — cTrader demo; E90I–E96I — IB paper.
17. Stop for repository-policy review at E100.
18. E101–E105 — enable live independently only for approved brokers.
19. E110–E115 — enforce CI, signing, release, migration, and evidence gates.

## Verification commands by layer

Use named targets only; never a bare `dotnet build`.

```bash
dotnet build TradingTerminal.Mac.slnx
dotnet test tests/linux/TradingTerminal.Execution.Contracts.Tests/TradingTerminal.Execution.Contracts.Tests.csproj
dotnet test tests/linux/TradingTerminal.Execution.Tests/TradingTerminal.Execution.Tests.csproj
dotnet test tests/linux/TradingTerminal.Execution.Sqlite.Tests/TradingTerminal.Execution.Sqlite.Tests.csproj
dotnet test tests/linux/TradingTerminal.Execution.Mac.Tests/TradingTerminal.Execution.Mac.Tests.csproj
dotnet test tests/linux/TradingTerminal.ExecutionUi.Core.Tests/TradingTerminal.ExecutionUi.Core.Tests.csproj
dotnet test tests/linux/TradingTerminal.ExecutionUi.Avalonia.Tests/TradingTerminal.ExecutionUi.Avalonia.Tests.csproj
dotnet test tests/linux/TradingTerminal.Sandbox.Runtime.Tests/TradingTerminal.Sandbox.Runtime.Tests.csproj
dotnet test tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj
powershell -File .claude/context/manage-context.ps1 deep-check
```

The proposed execution test projects do not exist yet; their commands become valid as E10–E16 create
them.

## Completion definition

“Mac execution parity” is not one percentage. It is complete only when:

1. The strategy route, UI route, service route, and restart/reconciliation route all use the same OMS.
2. Paper is the default after every app/service restart.
3. Every state-changing command is risk-admitted, lease-fenced, idempotent, durably evented, and
   recoverable.
4. Corrupt/unknown state blocks trading and produces a diagnostic path.
5. Market-data connection and order-execution readiness are separately represented.
6. Alpaca, cTrader, and IB each have their own truthful capability matrix and paper/demo evidence.
7. Real routing exists only for brokers explicitly approved after the repository invariant changes,
   and each requires both non-persisted session confirmation and exact per-account authorization.
8. The signed/notarized release artifact—not just a test build—passes the applicable Mac and vendor
   acceptance suite.
