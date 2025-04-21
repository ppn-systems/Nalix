using Nalix.Network.Web.Enums;
using System;

namespace Nalix.Network.Web.WebSockets.Internal;

/// <summary>
/// Represents a WS Receive result.
/// </summary>
internal sealed class WebSocketReceiveResult : IWebSocketReceiveResult
{
    internal WebSocketReceiveResult(int count, Opcode code)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        Count = count;
        EndOfMessage = code == Opcode.Close;
        MessageType = code == Opcode.Text ? 0 : 1;
    }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public bool EndOfMessage { get; }

    /// <inheritdoc />
    public int MessageType { get; }
}