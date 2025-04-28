using Nalix.Common.Connection;
using Nalix.Common.Package;

namespace Nalix.Network.Connection;

public sealed partial class Connection : IConnection
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(Connection));

        using var linkedCts = System.Threading.CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _ctokens.Token);

        _cstream.BeginReceive(linkedCts.Token);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Send(System.ReadOnlySpan<byte> message)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Send(IPacket packet) => Send(packet.Serialize().Span);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<bool> SendAsync(
        IPacket packet,
        System.Threading.CancellationToken cancellationToken = default)
        => await SendAsync(packet.Serialize(), cancellationToken);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<bool> SendAsync(
        System.ReadOnlyMemory<byte> message,
        System.Threading.CancellationToken cancellationToken = default)
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
