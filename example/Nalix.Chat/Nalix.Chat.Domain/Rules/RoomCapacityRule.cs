// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Domain.Rules;

/// <summary>
/// Enforces maximum room capacity.
/// </summary>
public sealed class RoomCapacityRule
{
    /// <summary>
    /// Initializes a capacity rule.
    /// </summary>
    public RoomCapacityRule(int maxParticipants = 512)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParticipants);

        this.MaxParticipants = maxParticipants;
    }

    /// <summary>
    /// Gets configured room capacity.
    /// </summary>
    public int MaxParticipants { get; }

    /// <summary>
    /// Returns true when there is capacity for one more participant.
    /// </summary>
    public bool TryValidate(int participantCount, out string? reason)
    {
        if (participantCount < this.MaxParticipants)
        {
            reason = null;
            return true;
        }

        reason = $"Room capacity {this.MaxParticipants} reached.";
        return false;
    }
}
