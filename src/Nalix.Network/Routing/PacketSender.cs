// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Abstractions;

namespace Nalix.Network.Routing;

/// <summary>
/// Default implementation of <see cref="IPacketSender{TPacket}"/>.
/// Reads encryption/compression requirements from <see cref="PacketContext{TPacket}"/>.
/// </summary>
public sealed class PacketSender<TPacket> : IPacketSender<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly PacketContext<TPacket> _context;

    #endregion Fields

    internal PacketSender(PacketContext<TPacket> context, IPacketRegistry catalog)
    {
        _context = context;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask SendAsync(
        TPacket packet,
        System.Threading.CancellationToken ct = default)
    {
        System.Boolean needEncrypt = _context.Attributes.Encryption?.IsEncrypted ?? false;
        return SEND_CORE_ASYNC(packet, needEncrypt, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask SendAsync(
        TPacket packet,
        System.Boolean forceEncrypt,
        System.Threading.CancellationToken ct = default) => SEND_CORE_ASYNC(packet, forceEncrypt, ct);

    private async System.Threading.Tasks.ValueTask SEND_CORE_ASYNC(
        TPacket packet,
        System.Boolean needEncrypt,
        System.Threading.CancellationToken ct)
    {
        TPacket current = packet;

        //if (needEncrypt)
        //{
        //    if (!_catalog.TryGetTransformer(packet.GetType(), out PacketTransformer transformer) || !transformer.HasEncrypt)
        //    {
        //        await _context.Connection.SendAsync(
        //            ControlType.FAIL,
        //            ProtocolReason.CRYPTO_UNSUPPORTED,
        //            ProtocolAdvice.NONE,
        //            flags: ControlFlags.NONE,
        //            arg0: _context.Attributes.PacketOpcode.OpCode).ConfigureAwait(false);

        //        return;
        //    }

        //    current = (TPacket)transformer.Encrypt(current, _context.Connection.Secret, _context.Connection.Algorithm);
        //}

        await _context.Connection.TCP.SendAsync(current, ct).ConfigureAwait(false);
    }
}