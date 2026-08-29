// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a packet type as safe to retry: sending it more than once for the same logical
/// operation produces no duplicate side effects on the server (e.g. a pure read, or a
/// write keyed by a caller-supplied idempotency token).
/// </summary>
/// <remarks>
/// This is documentation-only metadata. Nalix does not currently enforce it at runtime —
/// SDK retry helpers (e.g. <c>RequestOptions.RetryCount</c>) apply uniformly regardless of
/// this attribute. Presence of the attribute is the signal; it has no properties.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketIdempotentAttribute : Attribute;
