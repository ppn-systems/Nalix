﻿using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Analyzers;

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