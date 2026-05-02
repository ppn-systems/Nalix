// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

namespace Nalix.Runtime.Internal.Compilation;

/// <summary>
/// Holds metadata and a compiled delegate for a packet handler method.
/// This avoids runtime reflection by precompiling the method invoker.
/// </summary>
/// <typeparam name="TPacket">The packet type this handler processes.</typeparam>
/// <param name="MethodInfo"></param>
/// <param name="ReturnType"></param>
/// <param name="CompiledInvoker"></param>
/// <param name="RawInvoker">
/// Compiled delegate for raw handlers that receive <see cref="BufferContext"/> instead
/// of a deserialized packet. <see langword="null"/> for standard handlers.
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly record struct PacketHandlerDescriptor<TPacket>(
    MethodInfo MethodInfo,
    Type ReturnType,
    Func<object, PacketContext<TPacket>, ValueTask<object>> CompiledInvoker,
    Func<object, BufferContext, ValueTask<object>>? RawInvoker = null)
    where TPacket : IPacket;
