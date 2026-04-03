# UDP Server Guide

This guide shows when to use `UdpListenerBase` and what to implement first.

Use it when you have already decided UDP is the right transport, but want the safest minimal server shape before layering in more game or telemetry logic.

## When UDP makes sense

Choose UDP when you care more about:

- low latency
- tolerance for packet loss
- lightweight datagrams
- telemetry, state sync, discovery, or custom real-time protocols

Choose TCP when you need ordered, reliable byte streams by default.

## Minimal shape

```csharp
public sealed class SampleUdpListener : UdpListenerBase
{
    public SampleUdpListener(IProtocol protocol) : base(protocol) { }

    protected override bool IsAuthenticated(IConnection connection, in UdpReceiveResult result)
        => true;
}
```

That is the minimum surface, but a real UDP server still needs a session story, an authentication story, and a diagnostics story.

## What you must decide

### 1. Authentication

`IsAuthenticated(...)` is the first important decision point.

Use it to:

- validate source identity
- reject spoofed or malformed datagrams
- gate traffic until a handshake token or session secret is known

### 2. Protocol behavior

Decide whether your protocol:

- handles datagrams directly
- forwards decoded messages into your own game or app logic
- shares some semantics with your TCP protocol

For most teams, the easiest model is:

- use TCP to establish session state
- use UDP to carry fast authenticated datagrams
- keep both paths tied to the same `ConnectionHub`

The current runtime expects UDP datagrams to reference an existing `ConnectionHub` connection and to carry the authenticated trailer validated by `UdpListenerBase`.

### 3. Runtime tuning

Start with:

- `NetworkSocketOptions.Port`
- `NetworkSocketOptions.BufferSize`
- `NetworkSocketOptions.MaxGroupConcurrency`

Also decide intentionally whether you want time sync and timeout behavior enabled for this deployment.

## Recommended startup order

The safest startup order is:

1. validate socket and dispatch options
2. register logger and packet registry
3. build dispatch
4. build protocol
5. build `UdpListenerBase`
6. activate dispatch, then activate the listener

That keeps transport setup and application setup aligned.

## Diagnostics you get for free

`UdpListenerBase.GenerateReport()` already tracks:

- received packets and bytes
- short packet drops
- unauthenticated drops
- unknown packet drops
- receive errors
- last time sync and drift

## Common pattern

For client-friendly docs, the easiest mental model is:

```text
receive datagram
  -> authenticate
  -> protocol handling
  -> optional diagnostics / time sync logic
```

## What teams often miss

- UDP should not invent a second identity model separate from TCP sessions
- unauthenticated drops should be visible in diagnostics
- replay and timestamp assumptions should be explicit on both client and server
- `IsAuthenticated(...)` should stay fast and deterministic
- the authenticated trailer now includes session ID, timestamp, nonce, and Poly1305 tag

## Related APIs

- [UDP Listener](../api/network/runtime/udp-listener.md)
- [UDP Session](../api/sdk/udp-session.md)
- [Protocol](../api/network/runtime/protocol.md)
- [UDP Auth Flow](./udp-auth-flow.md)
- [Security Model](../concepts/security-model.md)
