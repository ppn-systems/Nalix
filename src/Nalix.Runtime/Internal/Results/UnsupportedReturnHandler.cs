// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.Results;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class UnsupportedReturnHandler<TPacket>(Type returnType) : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(object? result, PacketContext<TPacket> context) => throw new InvalidOperationException($"Unsupported return type: type={returnType.FullName}.");
}
