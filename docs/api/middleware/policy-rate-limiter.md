# Policy Rate Limiter

`PolicyRateLimiter` applies handler-level rate limits by mapping packet metadata onto shared token-bucket policies.

## Source mapping

- `src/Nalix.Network.Pipeline/Throttling/PolicyRateLimiter.cs`

## What it does

- reads `PacketRateLimitAttribute`-style policies
- rounds requested RPS and burst values into shared tiers
- reuses token-bucket limiters across similar policies
- caps the number of distinct policy buckets and can reuse the closest existing one
- evicts stale policies over time

## Basic usage

```csharp
[PacketRateLimit(8, burst: 2.5)]
public async Task HandleChat(PacketContext<IPacket> request) { }
```

At runtime:

```csharp
var decision = policyRateLimiter.Evaluate(opCode, packetContext);
if (!decision.Allowed)
{
    return;
}
```

## Why it exists

It avoids creating a fully separate limiter for every slightly different handler policy, which keeps memory and cleanup behavior under control.

The current implementation quantizes policies into shared tiers, keeps at most 64 active policy buckets, and periodically sweeps stale buckets after traffic checks.

## Diagnostics

`GenerateReport()` includes:

- active policy count
- policy tiers in use
- recent usage information

## Related APIs

- [Token Bucket](./token-bucket-limiter.md)
- [Packet Attributes](../routing/packet-attributes.md)
