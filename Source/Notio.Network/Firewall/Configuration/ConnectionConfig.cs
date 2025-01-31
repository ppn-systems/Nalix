using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

public sealed class ConnectionConfig : ConfiguredBinder
{
    [Range(1, 1000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 100;
}