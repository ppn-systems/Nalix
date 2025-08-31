using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Security.Enums;
using System.Text.Json.Serialization;

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
[System.Diagnostics.DebuggerDisplay("PacketTransformer [C={HasCompress}, D={HasDecompress}, E={HasEncrypt}, R={HasDecrypt}]")]
public readonly record struct PacketTransformer(
    /*----------------------------------------------------------------------------------*/
    [property: JsonIgnore]
    [System.Diagnostics.CodeAnalysis.AllowNull] System.Func<IPacket, IPacket> Compress,
    [property: JsonIgnore]
    [System.Diagnostics.CodeAnalysis.AllowNull] System.Func<IPacket, IPacket> Decompress,
    /*----------------------------------------------------------------------------------*/
    [property: JsonIgnore]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Encrypt,
    /*----------------------------------------------------------------------------------*/
    [property: JsonIgnore]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Decrypt
    /*----------------------------------------------------------------------------------*/)
{
    /// <summary>
    /// True if encryption is supported.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Encrypt))]
    public System.Boolean HasEncrypt => Encrypt is not null;

    /// <summary>
    /// True if decryption is supported.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Decrypt))]
    public System.Boolean HasDecrypt => Decrypt is not null;

    /// <summary>
    /// True if compression is supported.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Compress))]
    public System.Boolean HasCompress => Compress is not null;

    /// <summary>
    /// True if decompression is supported.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Decompress))]
    public System.Boolean HasDecompress => Decompress is not null;
}
