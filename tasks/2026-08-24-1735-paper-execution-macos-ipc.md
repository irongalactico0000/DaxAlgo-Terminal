# Paper execution macOS IPC

## Goal

Move the already-functional local Paper console across an authenticated macOS-local service boundary using the existing Core, Infrastructure, UI Core, and Avalonia app projects. The composed console must continue to exercise the same OMS/SQLite/Paper venue while no live broker route is introduced.

## Windows behavior being ported

- Length-prefixed, maximum-size-bounded typed JSON frames.
- Mutual nonce/HMAC-SHA256 authentication with direction-separated transcripts.
- Protocol-version negotiation that fails closed.
- A per-user service secret protected by the operating-system credential store.
- A local-only execution endpoint that cannot be reached over TCP.
- Request/response transport that preserves duplicate-request and fencing behavior.

## Mac destination and substitutions

- Portable frame/authentication contracts remain below the app in the existing execution source set.
- Windows named pipes become owner-only Unix-domain sockets.
- Windows DPAPI becomes the macOS login Keychain.
- `PaperExecutionDesktopSession` owns server/client lifetime; the Avalonia console continues to receive `IPaperExecutionClient`, not the OMS or ledger.

## Functional completion sequence

1. Port bounded frames and mutual authentication with hostile tests.
2. Add Keychain secret retrieval/generation with an injectable test seam.
3. Add socket-path validation, pre-existing-node rejection, mode `0600`, accept/connect/dispose behavior, and peer-local enforcement.
4. Transport versioned `ExecutionServiceRequest`/`ExecutionServiceExchange` over the authenticated socket.
5. Compose the desktop Paper client through that endpoint and prove submit/cancel/replace/reconcile/kill/restart still pass.
6. Surface service/auth/socket faults as read-only/unavailable UI state.

## Hard safety boundaries

- Unix-domain socket only; no TCP listener.
- Secret bytes never enter logs, config, ledger, or IPC frames.
- Symlinks and non-socket pre-existing paths fail closed.
- Frame sizes are bounded before allocation.
- Authentication completes before any execution request is accepted.
- Environment remains `SimulatedPaper`; no broker adapter or live authorization is added.

## Blast radius

- Existing execution folders in Core, Infrastructure, UI Core, and App Avalonia.
- Existing execution and App test projects.
- Generated context and this task record.

## Verification

## Result

The existing Mac Paper Console now crosses a real authenticated Unix-domain socket for every
status, resync, submit, cancel, replace, reconcile, and kill/flatten service exchange. The server
still runs inside the Avalonia process in this milestone; the boundary is real, but Windows-style
independent process supervision is not yet claimed.

## Component-level completion ledger

| Component | What Windows does | Mac implementation now | Proven substeps | Remaining difference |
|---|---|---|---|---|
| Frame envelope | Prefixes every JSON frame with a fixed-width length and rejects attacker-controlled sizes before allocation | `StreamExecutionFrameTransport` uses a four-byte big-endian length and a 1 MiB default ceiling | Round-trip; zero length; oversize output; oversize input; truncated input | No known behavioral gap in this layer |
| JSON boundary | Deserializes one declared frame type and rejects malformed input | Uses strict camel-case JSON and rejects unmapped members and JSON null | Real status, submit, lifecycle event, projection, and response DTOs cross the socket | The JSON byte representation is Mac protocol v1; cross-running a Windows binary against it is not claimed |
| Mutual authentication | Client nonce, service nonce, service proof, client proof, completion | `ExecutionIpcAuthenticator` implements the same five-stage exchange with 32-byte nonces and HMAC-SHA256 | Success; wrong secret; reflected service proof; version mismatch; absent peer termination | No persistent replay cache is needed for fresh random nonce pairs; entropy-source failure still relies on the OS RNG exception |
| Proof separation | Server proof cannot be reused as a client proof | Distinct `server-proof` and `client-proof` transcript domains bind client version, server version, acceptance bit, and both nonces | A deliberately reflected server proof is rejected by both peers | No known behavioral gap in this layer |
| Protocol negotiation | Exact version agreement; downgrade/mismatch fails before requests | Protocol v1 is bound into the authenticated transcript and completion | v2 client against v1 service fails as `ProtocolVersionMismatch` | No multi-version compatibility policy yet; v1 only is intentional |
| Shared secret | Windows encrypts one 256-bit secret with DPAPI | `MacExecutionServiceSecretStore` stores one 256-bit generic-password item in the current user's login Keychain | Exact length validation; stable injectable seam; secret clones are zeroed after use | Real Keychain allow/deny/locked acceptance is not automated because it can prompt the signed app user |
| Local transport | Windows uses a current-user-only named pipe and rejects remote-machine access | Mac uses `AddressFamily.Unix` only; there is no TCP listener | Real connect/accept/request/response over a Unix socket | No separate launch agent/service process yet |
| Filesystem ownership | Named-pipe DACL permits the current user | Socket parent is forced to `0700`; socket node is forced to `0600` | Exact modes asserted on macOS | Parent ancestry above the immediate private directory is supplied by `/private/tmp`; the unique child is owner-only |
| Peer identity | Named-pipe endpoint is bound to the current user | Both accepted and connecting sockets call macOS `getpeereid` and compare with `geteuid` | Same-user real connection succeeds | A different-UID acceptance test requires a second local OS account and is not part of unit CI |
| Unsafe path handling | Service will not attach to an attacker-owned endpoint | Pre-existing file/directory/symlink socket targets and a symlink parent are rejected; nothing is overwritten | Existing file remains byte-for-byte; symbolic parent is rejected | No stale-socket takeover policy; stale state intentionally requires operator/app cleanup |
| Authentication deadline | Silent/malicious peers cannot occupy a handshake indefinitely | Every connect/handshake has a five-second linked deadline | Wrong-secret peer is rejected and the listener accepts a later authorized peer | Timeout duration is currently a constant, not configuration |
| Request correlation | Client verifies response belongs to its request | Endpoint checks request ID, protocol, event count, event ordering, and final cursor | Real status response correlation plus full desktop submit/resync lifecycle | No multiplexing; one authenticated client connection is serialized intentionally |
| Lease/fence transport | Mutations present the exact writer generation | Status/resync and mutations now carry the current lease/fence on the wire; service still independently validates mutations | Restart receives a newer durable fence; stale/invalid mutations remain covered by execution tests | Client receives expected grant during same-process composition; a future external host needs an authenticated grant-discovery message |
| Server connection lifetime | Execution host owns accepted connections and drains them on shutdown | Accepted tasks are tracked; cancellation waits for all active handlers before `RunAsync` completes | Desktop session can dispose client, cancel server, await drain, delete socket, then close ledger | Separate-process crash/restart supervision remains missing |
| Desktop composition | Windows UI talks to the execution service, not directly to the OMS | `PaperExecutionDesktopSession` starts the server, authenticates the endpoint, and injects only `IExecutionServiceEndpoint` into `PaperExecutionClient` | Refresh → submit → fill → restart → resync → working order → cancel → kill → flatten all pass through IPC | Server and UI share a process in this milestone |
| User-visible failure | Windows execution service faults block the control plane | Keychain/socket/auth/ledger startup exceptions open `PaperExecutionUnavailableWindow`; it says no order was sent and remains fail-closed | Structural copy test and real headless render with exact reason | No one-click diagnostic export yet |

## Exact desktop call chain now

1. The user opens **Execution Engine → Paper Execution Console**.
2. DI lazily constructs one `PaperExecutionDesktopSession`; merely starting the app does not open the ledger or socket.
3. The session opens the account-isolated SQLite Paper ledger and acquires a durable lease/fencing generation.
4. It creates a unique short socket directory under `/private/tmp`, changes it to `0700`, binds `paper.sock`, and changes the node to `0600`.
5. The client connects locally; both ends validate the macOS peer UID.
6. Client and server exchange fresh nonces and direction-separated HMAC proofs using the Keychain secret.
7. `PaperExecutionClient.RefreshAsync` sends `Status`, then one or more cursor-based `Resync` requests.
8. The service verifies protocol/resource/payload shape and returns a correlated response followed by exactly `EventCount` durable outbox events.
9. The client rejects request-ID mismatch, protocol mismatch, invalid event count, non-monotonic outbox order, or final-cursor mismatch.
10. Submit builds the existing canonical instruction and risk evidence, then sends it across the socket with the current lease/fence.
11. The service performs OMS validation, risk, append, dispatch to the deterministic Paper venue, callback ingestion, SQLite projection, and outbox creation.
12. The client resyncs the immutable event chain and rebuilds the console orders/fills/positions/cash rows.
13. On app/session disposal, the client closes first, the server cancellation drains all handlers, the socket node and unique directory are removed, the lease is released, and the ledger closes.

## Defects found and fixed during the slice

1. **macOS socket-path ceiling:** the initial Application Support-derived socket path exceeded the
   approximately 104-byte Darwin Unix-socket limit. The socket now uses a unique short owner-only
   directory; the durable ledger remains in Application Support.
2. **Read-request wire identity:** Status/Resync originally used default identity structs. Their
   strict JSON converters correctly refused to serialize an empty `ExecutionLeaseId`. Read requests
   now carry the current grant even though only mutations require fencing admission.
3. **Accepted-connection ownership:** initial server code fire-and-forgot connection tasks. The
   server now tracks and drains them before shutdown completes.
4. **Socket cleanup:** cleanup no longer depends on `File.Exists` classifying a Unix socket as a
   regular file; `File.Delete` is idempotently attempted on the exact owned path.
5. **Invisible startup fault:** a Keychain/socket/ledger failure previously appeared only in the
   activity log. The shell now opens a dedicated fail-closed unavailable window with the exact
   reason and an explicit statement that no order was sent.

## Verification evidence

- Hostile frame/auth/socket suite: **7/7 passed**.
- Complete execution namespace: **88/88 passed**.
- Portable Execution UI Core: **19/19 passed**.
- Full Avalonia app tests: **120/120 passed**.
- Focused actual console + failure surfaces: **6/6 passed**.
- Named `TradingTerminal.App.Avalonia.csproj` build: **0 warnings, 0 errors**.
- Context regeneration completed after the public execution endpoint/frame/authentication API
  additions.

## Exact status after this milestone

- **Complete for the current Mac Paper product:** bounded framing, mutual authentication, Keychain
  secret implementation, owner-only Unix socket, same-user verification, request transport,
  shutdown cleanup, desktop composition, and visible fail-closed startup errors.
- **Not complete versus the Windows deployment model:** the execution host is not yet a separately
  supervised child/launchd process; no signed-app Keychain acceptance run exists; no crash/reconnect
  loop exists across two independent processes.
- **Still intentionally absent:** every IB/cTrader/Alpaca order adapter and every real-money/live
  authorization route.
