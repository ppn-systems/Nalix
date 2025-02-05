namespace Notio.Common.Connection;

/// <summary>
/// Represents connection events and provides event data.
/// </summary>
public interface IConnectEventArgs
{
    /// <summary>
    /// The connection related to the event.
    /// </summary>
    IConnection Connection { get; }
}