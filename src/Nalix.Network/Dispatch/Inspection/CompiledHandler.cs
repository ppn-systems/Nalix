// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core.Context;

namespace Nalix.Network.Dispatch.Inspection;

/// <summary>
/// Holds metadata and a compiled delegate for a packet handler method.
/// This avoids runtime reflection by precompiling the method invoker.
/// </summary>
/// <typeparam name="TPacket">The packet type this handler processes.</typeparam>
internal readonly record struct CompiledHandler<TPacket>(
    System.Reflection.MethodInfo MethodInfo,
    System.Type ReturnType,
    System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker);
