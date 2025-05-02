using Nalix.Common.Connection;
using Nalix.Common.Cryptography.Asymmetric;
using Nalix.Common.Cryptography.Hashing;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Client.Internal;

/// <summary>
/// Handles the secure handshake process between a client and server using X25519 key exchange and SHA hashing.
/// </summary>
/// <typeparam name="TPacket">
/// The type of packet used in the handshake, implementing <see cref="IPacket"/>,
/// <see cref="IPacketFactory{TPacket}"/>, and <see cref="IPacketDeserializer{TPacket}"/>.
/// </typeparam>
public sealed class Handshake<TPacket>(
    NetworkReceiver<TPacket> receiver, NetworkSender<TPacket> sender,
    ConnectionContext context, IX25519 x25519, ISHA sha)
    where TPacket : IPacket, IPacketFactory<TPacket>, IPacketDeserializer<TPacket>
{
    private readonly NetworkReceiver<TPacket> _receiver = receiver;
    private readonly NetworkSender<TPacket> _sender = sender;
    private readonly ConnectionContext _context = context;
    private readonly IX25519 _x25519 = x25519;
    private readonly ISHA _sha = sha;

    /// <summary>
    /// Executes the handshake process synchronously by exchanging public keys and deriving a shared encryption key.
    /// </summary>
    /// <returns>
    /// A tuple where <c>Status</c> is <c>true</c> if the handshake was successful; otherwise, <c>false</c>.
    /// <c>Message</c> provides a descriptive result.
    /// </returns>
    /// <remarks>
    /// This method sends a handshake request with the public key, receives the server's public key,
    /// computes the shared secret, hashes it using SHA, and stores the derived key in the connection context.
    /// </remarks>
    public (bool Status, string Message) Execute()
    {
        (byte[] privateKey, byte[] publicKey) = _x25519.Generate();

        TPacket request = TPacket.Create((ushort)ConnectionCommand.StartHandshake, PacketCode.None,
            PacketType.Binary, PacketFlags.None, PacketPriority.Low, publicKey);

        _sender.Send(request);
        IPacket response = _receiver.Receive();

        if (response == null || response.Payload.Length != 32)
            return (false, $"Invalid response length: {response?.Payload.Length ?? 0}");

        byte[] secret = _x25519.Compute(privateKey, response.Payload.ToArray());
        _context.EncryptionKey = _sha.ComputeHash(secret);

        return (true, "Secure handshake completed.");
    }

    /// <summary>
    /// Executes the handshake process asynchronously by exchanging public keys and deriving a shared encryption key.
    /// </summary>
    /// <returns>
    /// A task that resolves to a tuple containing <c>Status</c> and <c>Message</c>.
    /// <c>Status</c> is <c>true</c> if the handshake was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method performs the same operation as <see cref="Execute"/> but asynchronously using <c>await</c>.
    /// </remarks>
    public async System.Threading.Tasks.Task<(bool Status, string Message)> ExecuteAsync()
    {
        (byte[] privateKey, byte[] publicKey) = _x25519.Generate();

        TPacket request = TPacket.Create((ushort)ConnectionCommand.StartHandshake, PacketCode.None,
            PacketType.Binary, PacketFlags.None, PacketPriority.Low, publicKey);

        await _sender.SendAsync(request);
        IPacket response = await _receiver.ReceiveAsync();

        if (response == null || response.Payload.Length != 32)
            return (false, $"Invalid response length: {response?.Payload.Length ?? 0}");

        byte[] secret = _x25519.Compute(privateKey, response.Payload.ToArray());
        _context.EncryptionKey = _sha.ComputeHash(secret);

        return (true, "Secure handshake completed.");
    }
}
