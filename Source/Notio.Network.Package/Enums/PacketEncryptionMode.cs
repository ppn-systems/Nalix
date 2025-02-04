namespace Notio.Network.Package.Enums;

public enum PacketEncryptionMode : byte
{
    Xtea,
    AesGcm,
    ChaCha20Poly1305
}