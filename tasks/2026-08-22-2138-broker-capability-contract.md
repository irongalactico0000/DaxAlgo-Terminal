# Broker capability contract — E03

## Goal

Replace method-probing and connection-state inference with an explicit, exhaustive description of
the market-data channels supplied by every Mac broker, while separately declaring that this build
has no order-execution capability.

## Plan

1. Add immutable market-data and execution capability value types in Core.
2. Add an exhaustive `BrokerKind` capability catalog that fails on unknown enum values.
3. Expose only the market-data descriptor from `IBrokerClient`; do not add order operations.
4. Add a 13-broker table test plus completeness, fail-closed, and data/execution-separation tests.
5. Migrate ingest consumers one atomic increment at a time, beginning with live stream startup.
6. Run the focused capability/ingest suites and the complete headless test project.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Brokers/BrokerCapabilities.cs`
- `src/linux/Core/TradingTerminal.Core/MarketData/IBrokerClient.cs`
- `tests/linux/TradingTerminal.Tests.Headless/Brokers/BrokerCapabilityCatalogTests.cs`
- `src/linux/Pipeline/TradingTerminal.MarketData/MarketDataIngestService.cs`
- `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataIngest.cs`
- `tests/linux/TradingTerminal.Tests.Headless/MarketData/MarketDataIngestServiceTests.cs`
- `src/linux/Pipeline/TradingTerminal.MarketData/MarketDataRepository.cs`
- `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataRepository.cs`
- `tests/linux/TradingTerminal.Tests.Headless/MarketData/MarketDataRepositoryTests.cs`
- `src/linux/Core/TradingTerminal.Core/Strategies/StrategyBrokerCapability.cs`
- `tests/linux/TradingTerminal.Tests.Headless/Strategies/StrategyClassificationTests.cs`
- `src/linux/Tools/TradingTerminal.Recording/RecorderEntry.cs`
- `src/linux/Tools/TradingTerminal.Recording/TickRecordingService.cs`
- `src/linux/Tools/TradingTerminal.Recording/RecorderPanelView.axaml`
- `tests/linux/TradingTerminal.Tests.Headless/Recording/RecorderContractsTests.cs`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/RecorderCapabilityBindingTests.cs`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- This task record

No UI, broker transport, connection lifecycle, credential, persistence, execution, project, or
repository-remote behavior changes in these increments.

## Build filter

- `src/linux/Core/TradingTerminal.Core/TradingTerminal.Core.csproj`
- `src/linux/Pipeline/TradingTerminal.MarketData/TradingTerminal.MarketData.csproj`
- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- E03 catalog + ingest focus:
  `dotnet test tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
  `--filter 'FullyQualifiedName~MarketDataIngestServiceTests|FullyQualifiedName~BrokerCapabilityCatalogTests'`
  — 22 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.Tests.MarketData` namespace — 67 passed, 0 failed, 0 skipped.
- E03.2 `MarketDataRepositoryTests` — 8 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.Tests.MarketData` namespace after E03.2 — 71 passed, 0 failed,
  0 skipped.
- E03.3.1 `StrategyClassificationTests` — 10 passed, 0 failed, 0 skipped.
- E03.3.1 strategy classification plus capability catalog consistency — 28 passed, 0 failed,
  0 skipped.
- E03.3.2 Recorder plus capability catalog consistency — 25 passed, 0 failed, 0 skipped.
- E03.3.2 Recorder AXAML channel bindings — 4 passed, 0 failed, 0 skipped.
- Complete headless suite remains deferred until the remaining E03 consumer increments are complete.

## Findings

- `IBrokerClient` previously exposed channel methods but no support descriptor; it now exposes the
  catalog-backed `MarketDataCapabilities` property.
- Unsupported features use three different runtime signals: empty streams/lists,
  `NotSupportedException`, or unavailable registration.
- `StrategyBrokerCapability` is not authoritative: it advertises IB depth even though the client
  throws, and omits several implemented public-crypto trade tapes.
- Broker registration availability remains separate from feature capability because IB/NinjaTrader
  are optional build inputs.
- NSubstitute does not execute the default interface implementation for an unconfigured property;
  ingest test doubles must explicitly return their capability row. Real clients inherit the
  catalog-backed default.
- Historical cache lookup previously happened before capability validation, allowing data cached
  from a supported source to make an unsupported broker request appear successful.
- Unsupported live-bar repository subscriptions previously opened a hub bridge and no-op ingest
  handle, leaving consumers waiting for values that could never arrive.
- The strategy tape list contained 3 of 8 implemented live-tape brokers. The depth list had the
  correct count but incorrectly included IB and omitted Simulated.
- Combined Depth + TradeTape requirements previously returned the tape list rather than the
  intersection, allowing brokers missing one required channel to appear eligible.
- Recorder requested live bars for IronBeam, LSE, and Upstox even though ingest reduced those calls
  to no-op handles; its status and BARS chip still claimed the channel was live.
- Recorder tape/depth badges repeated the stale strategy broker sets rather than using the selected
  client's descriptor.

## Diff summary

- Added immutable source-capability and execution-capability records plus an exhaustive 13-broker
  catalog; every Mac broker remains explicitly data-only.
- Exposed the market-data descriptor from `IBrokerClient` without adding order operations.
- E03.1 makes live ingest consult the selected client's descriptor before opening L1, L2, bar, or
  trade streams.
- Unsupported channels retain a ref-counted no-op handle and never call the broker method.
- Runtime `NotSupportedException` containment remains as defense when a declared implementation
  fails or drifts from its catalog row.
- E03.2 gates aggregate instrument lookup, historical bars, live bars, L1, and L2 at the repository
  boundary before cache, hub, ingest, or broker work begins.
- Repository failures now distinguish unsupported source capability from an empty supported result
  and from a runtime vendor/transport failure.
- E03.3.1 derives tape/depth strategy eligibility from the catalog, intersects combined
  requirements, and rejects unknown requirement bits instead of treating them as broker-agnostic.
- E03.3.2 captures the selected client's descriptor once per recording entry; quote/bar/depth/tape
  ingest handles, hub counters, status text, and AXAML badges all gate on their own channel flag.
- A client declaring no live channels performs zero resolve, ingest, or hub work and is not marked
  live.

## Verification

- The table test covers every declared `BrokerKind` exactly once and rejects unknown values.
- Execution capability is unavailable for every broker independently of data connectivity.
- The E03.1 regression test proves London Strategic Edge starts L1 exactly once while making zero
  calls to its unsupported depth, live-bar, and live-trade methods.
- Existing normalization, provenance, and ref-counting tests remain green.
- E03.2 tests prove unsupported catalogs make zero discovery calls; unsupported history makes zero
  cache and REST calls; unsupported live bars make zero resolve/ingest/broker calls; and unsupported
  L2 makes zero broker subscription calls.
- E03.3.1 identity-level tests prove the complete 8-broker tape set, the 9-broker depth set without
  IB and with Simulated, the 7-broker tape/depth intersection, and fail-closed unknown flags.
- E03.3.2 tests prove LSE opens only L1, IronBeam keeps L1/L2/tape without bars, Simulated opens all
  four channels, zero-channel clients perform no downstream work, teardown clears every badge, and
  every AXAML chip binds to its corresponding capability property.
- `git diff --check` passes for the E03.1 and E03.2 files.
- `.claude/context/manage-context.ps1 check` could not run because neither `powershell` nor `pwsh`
  is installed in this environment; no project topology changed in E03.1 or E03.2.

## Risks/deferred

- E03.1–E03.2 create and enforce the source of truth in ingest and repository paths, but strategy
  classification and Recorder migrated in E03.3.1–E03.3.2; live strategy startup, quick-backtest
  history selection, broader tool UI states, and login presentation have not yet migrated to it.
- Capability means source implementation, not account entitlement or live vendor acceptance.
- Live order execution remains prohibited by `AGENTS.md`; all execution descriptors must remain
  unavailable in this build.
