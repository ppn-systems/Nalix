// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Attributes;
using Nalix.Common.Security.Enums;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Pairs a reflected member with its sensitivity level and pre-compiled accessor delegates.
/// Storing the accessor here eliminates secondary GetProperty/GetField calls at decrypt time.
/// </summary>
internal sealed class SensitiveMemberInfo
{
    /// <summary>Gets the member name.</summary>
    public required System.String Name { get; init; }

    /// <summary>Gets the declared type of the member.</summary>
    public required System.Type MemberType { get; init; }

    /// <summary>Gets the sensitivity level declared via <see cref="SensitiveDataAttribute"/>.</summary>
    public required DataSensitivityLevel Level { get; init; }

    /// <summary>
    /// Pre-compiled getter delegate — avoids reflection on every Encrypt/Decrypt call.
    /// </summary>
    public required System.Func<System.Object, System.Object?> Getter { get; init; }

    /// <summary>
    /// Pre-compiled setter delegate — avoids reflection on every Encrypt/Decrypt call.
    /// </summary>
    public required System.Action<System.Object, System.Object?> Setter { get; init; }
}
