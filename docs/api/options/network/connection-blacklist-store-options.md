# Connection Blacklist Store Options

The `ConnectionBlacklistStoreOptions` class defines configuration parameters for managing and loading permanent IP blacklists.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionBlacklistStoreOptions.cs`

## Overview

Unlike dynamic/progressive bans that expire, the permanent IP blacklist allows developers and administrators to load a list of IP addresses or CIDR subnets from disk that are permanently blocked from connecting.

## Configuration Table

The options map to INI configuration sections:

| Property | Type | Default Value | Description |
|----------|------|---------------|-------------|
| `Enabled` | `bool` | `true` | If `true`, the permanent blacklist file is loaded and checked by the `ConnectionGuard`. |
| `StoreFileName` | `string` | `"blacklist.txt"` | File name under the configuration directory containing IP addresses or CIDR subnets (one per line). |
| `MaxBlacklistedIps` | `int` | `100,000` | Limits the maximum number of blacklist records loaded to prevent memory bloat. Range: 10 to 1,000,000. |

## Usage Example

### Mutating Options Programmatically

```csharp
using Nalix.Hosting;
using Nalix.Network.Options;

INetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

builder.Configure<ConnectionBlacklistStoreOptions>(options =>
{
    options.Enabled = true;
    options.StoreFileName = "firewall_blacklist.cfg";
    options.MaxBlacklistedIps = 200_000;
});
```

### INI Configuration Format

```ini
[ConnectionBlacklistStore]
; Configuration for persisting blacklisted IP addresses to disk
Enabled = true
StoreFileName = blacklist.txt
MaxBlacklistedIps = 100000
```

### Blacklist File Format (e.g. `blacklist.txt`)

The file is stored inside the application configuration folder and lists IP addresses or networks in CIDR notation:

```text
# Block a single IP
192.168.1.100

# Block a subnet
10.0.0.0/24
2001:db8::/32
```

## See Also

* [Connection Guard](../../network/connection/connection-guard.md)
* [Connection Ban Store Options](connection-ban-store-options.md)
