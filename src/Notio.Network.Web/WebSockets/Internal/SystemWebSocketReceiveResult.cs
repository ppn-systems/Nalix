namespace Notio.Network.Web.WebSockets.Internal;

/// <summary>
/// Represents a wrapper around a regular WebSocketContext.
/// </summary>
/// <inheritdoc />
/// <remarks>
/// Initializes a new instance of the <see cref="SystemWebSocketReceiveResult"/> class.
/// </remarks>
/// <param name="results">The results.</param>
internal sealed class SystemWebSocketReceiveResult(System.Net.WebSockets.WebSocketReceiveResult results)
    : IWebSocketReceiveResult
{
    private readonly System.Net.WebSockets.WebSocketReceiveResult _results = results;

    /// <inheritdoc/>
    public int Count => _results.Count;

    /// <inheritdoc/>
    public bool EndOfMessage => _results.EndOfMessage;

    /// <inheritdoc/>
    public int MessageType => (int)_results.MessageType;
}