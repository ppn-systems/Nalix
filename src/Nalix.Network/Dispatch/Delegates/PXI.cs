// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Delegates;

/// <summary>
/// Holds metadata and a compiled delegate for a packet handler method.
/// This avoids runtime reflection by precompiling the method invoker.
/// </summary>
/// <typeparam name="TPacket">The packet type this handler processes.</typeparam>
internal readonly record struct PXI<TPacket>(
    System.Reflection.MethodInfo MethodInfo,
    System.Type ReturnType,
    System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker);
