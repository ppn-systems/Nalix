using Nalix.Common.Package;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Networking;

/// <summary>
/// Defines a receiver for network packets.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
public interface INetworkReceiver<TPacket> where TPacket : IPacket, IPacketDeserializer<TPacket>
{
    /// <summary>
    /// Receives a packet from the network stream.
    /// </summary>
    /// <returns>The deserialized packet.</returns>
    TPacket Receive();

    /// <summary>
    /// Asynchronously receives a packet from the network stream.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, returning the deserialized packet.</returns>
    Task<TPacket> ReceiveAsync(CancellationToken cancellationToken = default);
}
