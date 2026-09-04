# Durable execution lease and fencing

## Goal

Prevent two Mac process generations—or a stale callback path—from dispatching Paper execution
commands for the same venue/account/environment resource.

## Functional substeps

- [x] **LEASE-01 Resource identity.** Bind venue, account, and Paper environment into one typed key.
- [x] **LEASE-02 Acquire.** Allow one unexpired owner and issue a unique lease id.
- [x] **LEASE-03 Monotonic fence.** Increment the durable per-resource generation on every new owner.
- [x] **LEASE-04 Renew.** Extend only the current unexpired owner without changing its fence.
- [x] **LEASE-05 Dispatch validation.** Re-read the durable current generation immediately before
  submit, cancel, and replace call the Paper dispatcher.
- [x] **LEASE-06 Stale rejection.** Reject an old lease id, old fence, wrong resource, wrong owner,
  expired lease, and released lease without invoking the dispatcher.
- [x] **LEASE-07 Release.** Mark the exact current owner released without deleting generation history.
- [x] **LEASE-08 Expiry recovery.** Permit a new owner only after the prior lease expires; its fence
  must be strictly greater.
- [ ] **LEASE-09 UI ownership fault.** Deferred until the Execution Console exists.

## Acceptance

- The second owner is rejected while the first lease is active.
- Renew preserves the fence and extends expiry.
- After expiry, a new owner gets fence `n + 1`; the old claim fails validation.
- Submit/cancel/replace with a stale instruction lease fails before dispatcher invocation and before
  `SendStarted`, `CancelRequested`, or `ReplaceRequested` is appended.
- Close/reopen SQLite preserves the latest generation and stale-token rejection.

## Scope

Existing Core execution contracts/OMS, existing Infrastructure SQLite store/schema, existing
headless tests. No project, UI, service, IPC, broker, or live-order path is added.

## Exact implementation result

- Core now exposes `ExecutionResource`, `ExecutionLeaseClaim`, `ExecutionLeaseGrant`, typed results,
  `IExecutionLeaseValidator`, and `IExecutionLeaseStore`, plus an in-memory deterministic store.
- The existing SQLite ledger stores every generation instead of overwriting the current owner.
- `OrderManagementService` requires a lease validator. Submit validation occurs while the order is
  still `Armed`; cancel/replace validation occurs before their request events. A failure returns
  `ExecutionLeaseRejected` and the dispatcher is not called.
- Instruction lease/fence and command venue/account/environment are combined into the exact claim
  checked at the dispatch boundary.

## Verification

- Three lease behavior tests pass.
- Durable test proves acquire → conflicting owner rejection → renew with same fence → close/reopen →
  validate → release → acquire fence `2` → old fence rejection.
- Expiry test proves takeover only at expiry and monotonically increasing fencing.
- OMS hostile test rotates the durable generation, then proves stale submit/cancel/replace make zero
  dispatcher calls and append no `SendStarted`, `CancelRequested`, or `ReplaceRequested` event.
- Combined Paper OMS + SQLite + lease slice passes **18/18** tests.

## Remaining

LEASE-09 UI ownership display remains blocked on the missing Execution Console. Cross-process file
writer exclusion exists at the SQLite ledger boundary; the later service/IPC slice must also bind
its authenticated process identity to `RuntimeInstanceId`.
