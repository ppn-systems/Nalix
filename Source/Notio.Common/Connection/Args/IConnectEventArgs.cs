namespace Notio.Common.Connection.Args;

/// <summary>
/// Interface representing event arguments for connection-related events.
/// </summary>
public interface IConnectEventArgs
{
    /// <summary>
    /// Gets the connection associated with the event.
    /// </summary>
    IConnection Connection { get; }
}