// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Domain.Policies;

/// <summary>
/// Enforces message moderation invariants.
/// </summary>
public sealed class MessageModerationPolicy
{
    private readonly string[] _blockedTerms;

    /// <summary>
    /// Initializes moderation policy.
    /// </summary>
    public MessageModerationPolicy(int maxLength = 1024, string[]? blockedTerms = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        this.MaxLength = maxLength;
        _blockedTerms = blockedTerms is { Length: > 0 }
            ? blockedTerms
            : ["malware", "phishing"];
    }

    /// <summary>
    /// Gets max allowed message length.
    /// </summary>
    public int MaxLength { get; }

    /// <summary>
    /// Validates content for transport-safe chat.
    /// </summary>
    public bool TryValidate(string content, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            reason = "Message cannot be empty.";
            return false;
        }

        if (content.Length > this.MaxLength)
        {
            reason = $"Message exceeds maximum length {this.MaxLength}.";
            return false;
        }

        for (int i = 0; i < content.Length; i++)
        {
            char current = content[i];
            if (char.IsControl(current) && current is not ('\n' or '\r' or '\t'))
            {
                reason = "Message contains unsupported control characters.";
                return false;
            }
        }

        string normalized = content.ToLowerInvariant();

        for (int i = 0; i < _blockedTerms.Length; i++)
        {
            string candidate = _blockedTerms[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (normalized.Contains(candidate, StringComparison.Ordinal))
            {
                reason = "Message was blocked by moderation policy.";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
