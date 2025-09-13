// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Compilation;

/// <summary>
/// Holds metadata and a compiled delegate for a packet handler method.
/// This avoids runtime reflection by precompiling the method invoker.
/// </summary>
/// <typeparam name="TPacket">The packet type this handler processes.</typeparam>
internal readonly record struct HandlerInvoker<TPacket>(
    System.Reflection.MethodInfo MethodInfo, System.Type ReturnType,
    System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker);
