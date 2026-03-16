# Production Checklist

Use this checklist before calling your Nalix server production ready.

The goal is not perfection. The goal is to catch the failure points that show up once real traffic starts.

Treat it as a release gate, not as a general introduction to the framework.

## 1. Startup and configuration

Start by confirming that the important option types are loaded and validated during startup:

- `NetworkSocketOptions`
- `PoolingOptions`
- `DispatchOptions`
- `ConnectionLimitOptions`
- `ConnectionHubOptions`
- `TimingWheelOptions`
- `NetworkCallbackOptions`

If you skip validation, bad values usually fail later and less clearly.

## 2. Shared services

Make sure these are registered once and early:

- `ILogger`
- `IPacketRegistry`
- any app services your handlers depend on

Also make sure the server and client agree on packet catalog and registry assumptions.

## 3. Listener safety

Before launch, check:

- `TcpListenerBase` or `UdpListenerBase` starts cleanly
- `GenerateReport()` works
- `IsTimeSyncEnabled` is set intentionally
- listen port and backlog are correct
- `EnableTimeout` matches your real idle behavior expectations

## 4. Dispatch safety

Confirm the dispatcher:

- registers all required handlers
- registers middleware in the intended order
- uses explicit error handling
- can produce `GenerateReport()`

If the dispatcher is central to your app, treat missing error handling as a release blocker.

## 5. Handler design

Review handlers for:

- correct `[PacketOpcode]`
- correct packet type
- appropriate return type
- no hidden expensive blocking work
- explicit use of `PacketContext<TPacket>` only where needed

If a handler performs slow I/O or expensive computation, decide whether it needs:

- timeout metadata
- rate limiting
- concurrency limiting

## 6. Throttling and abuse controls

Before production, decide intentionally which of these you want to use:

- `ConnectionGuard`
- `TokenBucketLimiter`
- `PolicyRateLimiter`
- `ConcurrencyGate`

At minimum, most public-facing servers should have:

- connection admission control
- one packet-level rate/concurrency protection strategy

## 7. Middleware review

Confirm that middleware behavior is easy to predict:

- inbound middleware is ordered correctly
- outbound behavior is intentional
- short-circuit paths send useful control responses when appropriate
- custom metadata used by middleware is actually registered

Good production middleware should be:

- small
- deterministic
- observable in logs

## 8. Pooling and memory pressure

Check pool settings for:

- accept contexts
- socket async args
- receive contexts
- timeout tasks
- packet contexts

A useful rule of thumb:

- `Capacity` should reflect peak usage
- `Preallocate` should reflect steady-state warm usage

Too low:

- more allocations
- more GC pressure

Too high:

- unnecessary memory footprint

## 9. Callback flood protection

If you expect internet-facing traffic, review:

- `MaxPerConnectionPendingPackets`
- `MaxPendingNormalCallbacks`
- `MaxPendingPerIp`
- `CallbackWarningThreshold`

These values protect the callback path from flood behavior and are worth setting intentionally.

## 10. Observability

Before production, make sure you can obtain:

- listener reports
- protocol reports
- connection hub reports
- dispatch reports
- limiter/gate reports where relevant

If debugging requires attaching a debugger just to learn basic system state, observability is still too weak.

## 11. UDP-specific checks

If UDP is enabled, confirm:

- session identity is established before UDP traffic is trusted
- connection secret is initialized
- replay window assumptions are acceptable
- `IsAuthenticated(...)` performs the right checks
- unauthenticated and unknown-session drops are visible in diagnostics

## 12. Failure drills

Run at least these drills once:

- wrong packet opcode
- malformed packet
- idle timeout
- limiter rejection
- dispatcher with missing handler
- UDP auth failure

You do not need a perfect chaos environment. You do need proof that the failure mode is understandable.

## 13. Release checklist

Final short checklist:

- all startup options validate
- reports generate without crashing
- handlers and middleware are registered intentionally
- abuse controls are enabled
- logging is usable under failure
- timeout values are deliberate
- server boot and shutdown paths are tested

## Example validation pass

Use one short smoke test before release:

```csharp
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

await listener.Activate(ct);
Console.WriteLine(listener.GenerateReport());
Console.WriteLine(dispatch.GenerateReport());
Console.WriteLine(connectionHub.GenerateReport());

await listener.Deactivate(ct);
```

## Recommended Next Pages

- [Server Blueprint](./server-blueprint.md) — Production-oriented project structure
- [Troubleshooting](./troubleshooting.md) — Common issues and diagnostics
- [Network Options](../api/network/options/options.md) — Options class reference
