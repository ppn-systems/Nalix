// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines a contract for validating packet instances and reporting validation errors.
/// </summary>
/// <typeparam name="TPacket">
/// The type of packet to validate.
/// </typeparam>
/// <remarks>
/// Implement this interface when validation logic should be decoupled from the packet itself,
/// allowing reusable or external validation strategies.
/// <para>
/// The <see cref="Validate"/> method should perform all necessary checks on the specified
/// <typeparamref name="TPacket"/> instance and return <see langword="true"/> only if it is valid.
/// </para>
/// <para>
/// Implementations should avoid throwing exceptions for validation failures and instead
/// provide descriptive error messages via the <c>error</c> parameter.
/// </para>
/// </remarks>
public interface IPacketValidatable<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Validates the specified packet instance.
    /// </summary>
    /// <param name="packet">
    /// The packet instance to validate.
    /// </param>
    /// <param name="failureReason">
    /// When this method returns, contains a description of the validation failure
    /// if validation did not succeed; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the specified packet is valid; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method should be deterministic and free of side effects.
    /// Implementations are encouraged to keep validation logic lightweight
    /// to minimize overhead in performance-critical paths such as networking pipelines.
    /// </remarks>
    bool Validate(TPacket packet, [NotNullWhen(false)] out string? failureReason);
}
