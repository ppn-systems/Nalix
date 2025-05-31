//using Nalix.Common.Connection.Protocols;
//using Nalix.Common.Cryptography.Asymmetric;
//using Nalix.Common.Cryptography.Hashing;
//using Nalix.Common.Package;
//using Nalix.Common.Package.Enums;
//using Nalix.Shared.Net.Transport;

//namespace Nalix.Shared.Net.Clients;

///// <summary>
///// Handles the secure handshake process between a client and server using X25519 key exchange and SHA hashing.
///// </summary>
///// <typeparam name="TPacket">
///// The type of packet used in the handshake, implementing <see cref="IPacket"/>,
///// <see cref="IPacketFactory{TPacket}"/>, and <see cref="IPacketDeserializer{TPacket}"/>.
///// </typeparam>
//internal sealed class Handshake<TPacket>(
//    NetReader<TPacket> receiver,
//    NetSender<TPacket> sender,
//    NetContext context,
//    IX25519 x25519,
//    ISHA sha)
//    where TPacket : IPacket, IPacketFactory<TPacket>, IPacketDeserializer<TPacket>
//{
//    private readonly NetReader<TPacket> _receiver = receiver;
//    private readonly NetSender<TPacket> _sender = sender;
//    private readonly NetContext _context = context;
//    private readonly IX25519 _x25519 = x25519;
//    private readonly ISHA _sha = sha;

//    /// <summary>
//    /// Executes the handshake process synchronously by exchanging public keys and deriving a shared encryption key.
//    /// </summary>
//    /// <returns>
//    /// A tuple where <c>Status</c> is <c>true</c> if the handshake was successful; otherwise, <c>false</c>.
//    /// <c>Message</c> provides a descriptive result.
//    /// </returns>
//    /// <remarks>
//    /// This method sends a handshake request with the public key, receives the server's public key,
//    /// computes the shared secret, hashes it using SHA, and stores the derived key in the connection context.
//    /// </remarks>
//    public (bool Status, string Message) Execute()
//    {
//        (byte[] privateKey, byte[] publicKey) = _x25519.Generate();

//        TPacket request = TPacket.Create(
//            (ushort)ProtocolCommand.StartHandshake,
//            PacketType.Binary, PacketFlags.None, PacketPriority.Low, publicKey);

//        _sender.Send(request);
//        IPacket response = _receiver.Receive();

//        if (response == null || response.Payload.Length != 32)
//            return (false, $"Handshake failed: invalid response length ({response?.Payload.Length ?? 0}). " +
//                           $"Please ensure that the server is reachable and supports the protocol.");

//        byte[] secret = _x25519.Compute(privateKey, response.Payload.ToArray());
//        _context.EncryptionKey = _sha.ComputeHash(secret);

//        return (true, "Handshake successful: secure connection established.");
//    }

//    /// <summary>
//    /// Executes the handshake process asynchronously by exchanging public keys and deriving a shared encryption key.
//    /// </summary>
//    /// <returns>
//    /// A task that resolves to a tuple containing <c>Status</c> and <c>Message</c>.
//    /// <c>Status</c> is <c>true</c> if the handshake was successful; otherwise, <c>false</c>.
//    /// </returns>
//    /// <remarks>
//    /// This method performs the same operation as <see cref="Execute"/> but asynchronously using <c>await</c>.
//    /// </remarks>
//    public async System.Threading.Tasks.Task<(bool Status, string Message)> ExecuteAsync()
//    {
//        (byte[] privateKey, byte[] publicKey) = _x25519.Generate();

//        TPacket request = TPacket.Create(
//            (ushort)ProtocolCommand.StartHandshake,
//            PacketType.Binary, PacketFlags.None, PacketPriority.Low, publicKey);

//        await _sender.SendAsync(request);
//        IPacket response = await _receiver.ReceiveAsync();

//        if (response == null || response.Payload.Length != 32)
//            return (false, $"Handshake failed: invalid response length ({response?.Payload.Length ?? 0}). " +
//                           $"Please check server connectivity and protocol compatibility.");

//        byte[] secret = _x25519.Compute(privateKey, response.Payload.ToArray());
//        _context.EncryptionKey = _sha.ComputeHash(secret);

//        return (true, "Handshake successful: secure connection established.");
//    }
//}
