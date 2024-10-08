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
    public System.Int32 Port { get; init; }

    /// <summary>
    /// Gets a value indicating whether the listener is actively listening for incoming connections.
    /// </summary>
    public System.Boolean IsListening { get; init; }

    /// <summary>
    /// Gets a value indicating whether the listener has been disposed.
    /// </summary>
    public System.Boolean IsDisposed { get; init; }

    /// <summary>
    /// Gets the number of active connections currently being handled by the listener.
    /// </summary>
    public required System.String Address { get; init; }

    /// <summary>
    /// Gets a string representation of the status of the listener's socket,
    /// including socket type, protocol type, address family, and other details.
    /// </summary>
    public required System.String SocketInfo { get; init; }

    /// <summary>
    /// Retrieves detailed information about the listener socket's status, including
    /// socket type, protocol type, address family, and other relevant details.
    /// </summary>
    /// <param name="socket">The socket to retrieve status information from.</param>
    /// <returns>A string representing the status of the socket.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String GetSocketStatus(System.Net.Sockets.Socket socket)
    {
        if (socket == null)
        {
            return "_udpListener not initialized";
        }

        System.Text.StringBuilder sb = new();

        _ = sb.AppendLine($"_udpListener Type: {socket.SocketType}");
        _ = sb.AppendLine($"Protocol Type: {socket.ProtocolType}");
        _ = sb.AppendLine($"Address Family: {socket.AddressFamily}");
        _ = sb.AppendLine($"Linger State Enabled: {socket.LingerState?.Enabled}");
        _ = sb.AppendLine($"Linger State Time: {socket.LingerState?.LingerTime} seconds");

        try
        {
            _ = sb.AppendLine($"Is Listening: {socket.IsBound}");
        }
        catch
        {
            _ = sb.AppendLine("Error fetching socket listening state");
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
    public static System.String GetIpAddress(System.Net.Sockets.Socket socket)
    {
        return socket == null
            ? "_udpListener not initialized"
            : (socket.LocalEndPoint as System.Net.IPEndPoint)?
            .Address.ToString() ?? "IP not available";
    }

    /// <summary>
    /// Converts the <see cref="ListenerSnapshot"/> into a readable string representation.
    /// </summary>
    /// <returns>A string that represents the current state of the listener snapshot.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
    {
        System.Text.StringBuilder sb = new();

        _ = sb.AppendLine($"Listener Snapshot:");
        _ = sb.AppendLine($"Port: {this.Port}");
        _ = sb.AppendLine($"Is Listening: {this.IsListening}");
        _ = sb.AppendLine($"Is Disposed: {this.IsDisposed}");
        _ = sb.AppendLine($"Listener _udpListener Status: {this.SocketInfo}");
        _ = sb.AppendLine($"Address: {this.Address}");

        return sb.ToString();
    }
}