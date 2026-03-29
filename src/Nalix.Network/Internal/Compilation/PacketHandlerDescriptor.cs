// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
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
[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly record struct PacketHandlerDescriptor<TPacket>(MethodInfo MethodInfo, Type ReturnType, Func<object, PacketContext<TPacket>, ValueTask<object>> CompiledInvoker)
    where TPacket : IPacket;
