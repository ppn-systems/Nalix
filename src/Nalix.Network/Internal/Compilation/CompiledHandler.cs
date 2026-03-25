// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

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
/// <param name="MethodInfo"></param>
/// <param name="ReturnType"></param>
/// <param name="CompiledInvoker"></param>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal readonly record struct CompiledHandler<TPacket>(
    System.Reflection.MethodInfo MethodInfo, System.Type ReturnType,
    System.Func<object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<object>> CompiledInvoker) where TPacket : IPacket;
