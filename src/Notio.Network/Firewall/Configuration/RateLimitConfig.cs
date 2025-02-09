using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

public sealed class RateLimitConfig : ConfiguredBinder
{
    [Range(1, 1000)]
    public int MaxAllowedRequests { get; set; } = 100;

    [Range(1, 3600)]
    public int LockoutDurationSeconds { get; set; } = 300;

    [Range(1000, int.MaxValue)]
    public int TimeWindowInMilliseconds { get; set; } = 60000; // 1 minute
}