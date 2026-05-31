// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Environment.Extensions;

namespace Nalix.Hosting.Protocols;

/// <summary>
/// Extracts operation codes from the default Nalix packet header format.
/// </summary>
public sealed class DefaultOpCodeExtractor : IOpCodeExtractor
{
    /// <inheritdoc />
    public ushort Extract(ReadOnlySpan<byte> payload) => payload.AsHeaderRef().OpCode;
}
