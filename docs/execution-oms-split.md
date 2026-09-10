# Execution OMS split — seam map

Authority: Dhruv public `dhruuvsharma/DaxAlgo-Terminal`  
`src/windows/Execution` (OMS + Paper/Alpaca/IB/cTrader).

Goal product split:

1. **Research / qualification gate** — Native TradeIR smoke → Approve → Prepare (hash-bound admit).
2. **Order + position machinery** — Dhruv Execution (event-sourced OMS, risk gate, Paper book fills, adapter position qty). Live adapters: Alpaca / IB / cTrader only. **No Binance orders.**

## Where code lives today

| Tree | Order/OMS surface | Relation to Dhruv Execution |
|------|-------------------|-----------------------------|
| `DaxAlgo-Terminal` (public Windows) | `TradingTerminal.Execution` + `ExecutionUi` + `InProcessExecutionClient` | **Source of truth** |
| `DaxAlgo-Terminal-Mac-Integrated` | Ported `TradingTerminal.Execution` + Avalonia Execution Console; Paper books + Real books (`LiveBrokerBookRuntime`); `SandboxExecutionReplicator` → `IExecutionBookTargetIntake` | Forward-port of Execution Paper + live adapters |
| `DaxAlgo-Native-Paper-Integration` | Gate + thin demo session; **Admit→Prepare CreateBook** into shared `books.json` | Writes Mac-compatible Execution Paper catalog |

## Accepted constraints (product rules)

| Constraint | Product rule |
|------------|--------------|
| Strategy → OMS | Only via `SandboxExecutionReplicator` → `IExecutionBookTargetIntake` (not kernel `PlaceOrder`) |
| Manual ticket | Thin; prefer CreateBook + target/flatten or `SubmitManualOrderAsync` |
| Paper fills | Need submit/fill plan (or sandbox/flatten path); bare market ticket can sit Working@0 |
| Binance | Market data only — **no** Binance order adapter under Execution |
| Avg / realized PnL UI | Weak; adapter **qty** after fills is trading truth, not full portfolio accounting |

## Practical product split (locked)

1. **Keep** Native TradeIR smoke → Approve → Prepare as the research/qualification gate.
2. **Prepare → CreateBook:** Native `PaperDeploymentHandoff` binds an Execution Paper book into  
   `~/Library/Application Support/DaxAlgoTerminal/execution/paper/books.json` (same schema Mac `JsonPaperExecutionBookStore` reads). Operators do not manually CreateBook for the admitted account.
3. **Orders / positions:** Mac Avalonia Execution Console + Strategy Runner / Windows Execution books. **Mac live OMS full parity is Done** — Alpaca / IB / cTrader via ported `TradingTerminal.Execution` + Keychain LIVE confirmations + Brokers → Connect → Real book → Manual ticket. **Binance remains data-only.**

## Verification evidence (current)

| Requirement | Evidence | Result |
|-------------|----------|--------|
| Prepare → Execution CreateBook | `PaperDeploymentHandoffTests.Admit_and_prepare_enables_start_without_starting_trading` (asserts `ExecutionBookId` + catalog) | Passed |
| Handoff catalog → fill → position | `PaperExecutionBooksTests.Native_admit_handoff_catalog_schema_opens_selected_book_fill_and_position` | Passed |
| Paper submit → fill → position (OMS) | `PaperExecutionClientTests.Resync_rebuilds_exact_orders_fills_positions_cash_and_event_history` | Passed |
| Target replication → OMS → fill | `PaperExecutionBookTargetIntakeTests.CommittedSnapshotTraversesReplicatorAndOmsIntoPaperFill` | Passed |
| Real book Connect → Limit | `LiveExecutionConsoleFlowTests.Connect_then_real_book_then_limit_ticket_reaches_the_oms` | Passed |
| LIVE gate fail-closed | `LiveExecutionAuthorizationGateTests` + `LiveExecutionModeSwitchTests` | Passed |
| Replicator → Real book OMS | `LiveExecutionReplicatorToRealBookTests.Replicator_committed_target_reaches_real_book_oms_transport` | Passed |
| Live adapters same OMS path (no Binance) | Mac `AlpacaExecutionAdapter` / `InteractiveBrokersExecutionAdapter` / `CTraderExecutionAdapter`; `LiveExecutionAuthorizationGateTests.No_Binance_execution_adapter_type_exists` | Passed |
| Headless live OMS smoke | `--smoke-live-oms` / `--smoke-live-paper-alpaca` | Operator |
| Native gate intact | `Native_tradeir_smoke_approve_prepare_start_fills_stop_and_restore` | Passed |

## Remaining (explicit)

- UI avg/PnL polish not required for this handoff.
- Optional later: host gate + PaperExecutionBookManager in one Avalonia process so Prepare opens a live session without a second app.
- Optional later: Strategy Runner UI bind Real books to `InProcessExecutionClient` intake — **Done** (broker-Paper Real books; LIVE blocked in Runner). See Execution Console for LIVE arming.
