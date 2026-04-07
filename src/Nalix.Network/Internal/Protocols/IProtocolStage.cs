using Nalix.Common.Networking;

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// A single-step (stage) interface in a pipeline protocol. Returns true if the chain continues, returns false if it stops here.
/// </summary>
internal interface IProtocolStage
{
    void ProcessMessage(object? sender, IConnectEventArgs args);
}
