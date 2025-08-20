using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Security.Enums;

namespace Nalix.Common.Packets.Models;

/// <summary>
/// Represents an immutable set of delegates used to transform packets,
/// including compression, decompression, encryption, and decryption.
/// </summary>
/// <remarks>
/// This record struct provides function delegates for applying security and 
/// compression transformations on <see cref="IPacket"/> instances. 
/// All operations return a new packet without mutating the original.
/// </remarks>
/// <param name="Compress">
/// A delegate that compresses the given <see cref="IPacket"/> and 
/// returns a new compressed instance.
/// </param>
/// <param name="Decompress">
/// A delegate that decompresses the given <see cref="IPacket"/> and 
/// returns the restored instance.
/// </param>
/// <param name="Encrypt">
/// A delegate that encrypts the given <see cref="IPacket"/> using the 
/// specified <see cref="SymmetricAlgorithmType"/> and encryption key.
/// </param>
/// <param name="Decrypt">
/// A delegate that decrypts the given <see cref="IPacket"/> using the 
/// specified <see cref="SymmetricAlgorithmType"/> and decryption key.
/// </param>
public readonly record struct PacketTransformer(
    System.Func<IPacket, IPacket> Compress,
    System.Func<IPacket, IPacket> Decompress,
    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Encrypt,
    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Decrypt);
