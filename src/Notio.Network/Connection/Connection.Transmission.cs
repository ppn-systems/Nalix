using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

public sealed partial class Connection : IConnection
{
    /// <inheritdoc />
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Connection));

        using CancellationTokenSource linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _ctokens.Token);

        _cstream.BeginReceive(linkedCts.Token);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Send(IPacket packet) => Send(packet.Serialize().Span);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Send(ReadOnlySpan<byte> message)
    {
        if (_cstream.Send(message))
        {
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
            return true;
        }

        _logger?.Warn($"[{nameof(Connection)}] Failed to send message.");
        return false;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<bool> SendAsync(IPacket packet, CancellationToken cancellationToken = default)
        => await SendAsync(packet.Serialize(), cancellationToken);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<bool> SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (await _cstream.SendAsync(message, cancellationToken))
        {
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
            return true;
        }

        _logger?.Warn($"[{nameof(Connection)}] Failed to send message asynchronously.");
        return false;
    }
}
