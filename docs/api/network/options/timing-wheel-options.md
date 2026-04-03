# TimingWheelOptions

`TimingWheelOptions` configures idle connection timeout detection in Nalix.Network.

## Source mapping

- `src/Nalix.Network/Options/TimingWheelOptions.cs`

## Properties

| Property | Meaning | Default |
|---|---|---:|
| `BucketCount` | Number of wheel buckets. | `512` |
| `TickDuration` | Tick interval in milliseconds. | `1000` |
| `IdleTimeoutMs` | Idle timeout before auto-close. | `60000` |

## How to think about it

- lower `TickDuration` gives faster timeout reaction but more overhead
- larger `BucketCount` reduces collisions
- `IdleTimeoutMs` should reflect your protocol's expected silence window

This option matters only when timeout enforcement is enabled by the listener runtime.

## Example

```csharp
var options = new TimingWheelOptions
{
    BucketCount = 512,
    TickDuration = 1000,
    IdleTimeoutMs = 60_000
};
```

## Related APIs

- [Tcp Listener](../runtime/tcp-listener.md)
- [Network Options](./options.md)
