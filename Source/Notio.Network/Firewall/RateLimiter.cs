using Notio.Common.Firewall;
using System;
using System.Collections.Generic;

namespace Notio.Network.Firewall;

public class RateLimiter(int maxTokens, TimeSpan refillInterval, int tokensPerRefill) : IRateLimiter
{
    private class TokenBucket
    {
        public int Tokens { get; set; }
        public DateTime LastRefill { get; set; }
    }

    private readonly Dictionary<string, TokenBucket> _buckets = [];
    private readonly int _maxTokens = maxTokens;
    private readonly TimeSpan _refillInterval = refillInterval;
    private readonly int _tokensPerRefill = tokensPerRefill;

    public bool CheckLimit(string key)
    {
        lock (_buckets)
        {
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = new TokenBucket
                {
                    Tokens = _maxTokens,
                    LastRefill = DateTime.UtcNow
                };
                _buckets[key] = bucket;
            }

            var now = DateTime.UtcNow;
            var timeSinceLastRefill = now - bucket.LastRefill;
            var refills = (int)(timeSinceLastRefill.TotalMilliseconds / _refillInterval.TotalMilliseconds);

            if (refills > 0)
            {
                bucket.Tokens = Math.Min(_maxTokens, bucket.Tokens + refills * _tokensPerRefill);
                bucket.LastRefill = now;
            }

            if (bucket.Tokens <= 0) return false;

            bucket.Tokens--;
            return true;
        }
    }
}
