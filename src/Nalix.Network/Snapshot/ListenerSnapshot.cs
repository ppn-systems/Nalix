namespace Nalix.Network.Snapshot;

/// <summary>
/// This snapshot captures information such as the port, listening status,
/// disposed status, socket status, and active connections of the listener.
/// </summary>
public record ListenerSnapshot
{
    /// <summary>
    /// Gets the port on which the listener is accepting connections.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets a value indicating whether the listener is actively listening for incoming connections.
    /// </summary>
    public bool IsListening { get; init; }

    /// <summary>
    /// Gets a value indicating whether the listener has been disposed.
    /// </summary>
    public bool IsDisposed { get; init; }

    /// <summary>
    /// Gets the number of active connections currently being handled by the listener.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets a string representation of the status of the listener's socket,
    /// including socket type, protocol type, address family, and other details.
    /// </summary>
    public required string ListenerSocketStatus { get; init; }

    /// <summary>
    /// Retrieves detailed information about the listener socket's status, including
    /// socket type, protocol type, address family, and other relevant details.
    /// </summary>
    /// <param name="socket">The socket to retrieve status information from.</param>
    /// <returns>A string representing the status of the socket.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string GetSocketStatus(System.Net.Sockets.Socket socket)
    {
        if (socket == null)
            return "_udpSocket not initialized";

        System.Text.StringBuilder sb = new();

        sb.AppendLine($"_udpSocket Type: {socket.SocketType}");
        sb.AppendLine($"Protocol Type: {socket.ProtocolType}");
        sb.AppendLine($"Address Family: {socket.AddressFamily}");
        sb.AppendLine($"Linger State Enabled: {socket.LingerState?.Enabled}");
        sb.AppendLine($"Linger State Time: {socket.LingerState?.LingerTime} seconds");

        try
        {
            sb.AppendLine($"Is Listening: {socket.IsBound}");
        }
        catch
        {
            sb.AppendLine("Error fetching socket listening state");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Retrieves the IP address of the local endpoint associated with the specified socket.
    /// </summary>
    /// <param name="socket">The socket to retrieve status information from.</param>
    /// <returns>A string representing the status of the socket.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string GetIpAddress(System.Net.Sockets.Socket socket)
    {
        if (socket == null)
            return "_udpSocket not initialized";

        return (socket.LocalEndPoint as System.Net.IPEndPoint)?
            .Address.ToString() ?? "IP not available";
    }

    /// <summary>
    /// Converts the <see cref="ListenerSnapshot"/> into a readable string representation.
    /// </summary>
    /// <returns>A string that represents the current state of the listener snapshot.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        System.Text.StringBuilder sb = new();

        sb.AppendLine($"Listener Snapshot:");
        sb.AppendLine($"Port: {Port}");
        sb.AppendLine($"Is Listening: {IsListening}");
        sb.AppendLine($"Is Disposed: {IsDisposed}");
        sb.AppendLine($"Listener _udpSocket Status: {ListenerSocketStatus}");
        sb.AppendLine($"Address: {Address}");

        return sb.ToString();
    }
}
