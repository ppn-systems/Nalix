// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Marks a class as a Nalix packet and enables automatic registration
/// when scanning assemblies.
/// </summary>
/// <remarks>
/// <para>
/// Types annotated with this attribute will be discovered and registered
/// into the packet registry during startup via reflection-based scanning.
/// </para>
/// <para>
/// This attribute is typically applied to classes that inherit from
/// <c>PacketBase&lt;T&gt;</c>.
/// </para>
/// <para>
/// Using this attribute helps avoid manual packet registration and ensures
/// explicit control over which packet types are exposed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Packet]
/// public sealed class PingRequest : PacketBase&lt;PingRequest&gt;
/// {
///     public const ushort OpCodeValue = 0x1001;
///
///     public PingRequest()
///     {
///         this.OpCode = OpCodeValue;
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class PacketAttribute : Attribute
{
}
