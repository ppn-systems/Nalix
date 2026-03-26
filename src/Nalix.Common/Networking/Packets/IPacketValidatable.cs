// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines a non-generic contract for validating packet instances.
/// Allows the dispatcher to perform validation without knowing the concrete packet type.
/// </summary>
public interface IPacketValidatable
{
    /// <summary>
    /// Validates the current instance.
    /// </summary>
    /// <param name="failureReason">When this method returns, contains a description of the validation failure.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    bool Validate([NotNullWhen(false)] out string? failureReason);
}
