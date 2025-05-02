using Nalix.Common.Connection;
using Nalix.Common.Cryptography.Asymmetric;
using Nalix.Common.Cryptography.Hashing;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using System.Threading.Tasks;

namespace Nalix.Network.Client;

internal class Handshake<TPacket>(ConnectionContext context, IX25519 x25519, ISHA sha)
    where TPacket : IPacket, IPacketFactory<TPacket>
{
    private readonly ConnectionContext _context = context;
    private readonly IX25519 _x25519 = x25519;
    private readonly ISHA _sha = sha;

    public async Task<(bool Status, string Message)> PerformAsync()
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
