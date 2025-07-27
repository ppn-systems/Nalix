using Nalix.Common.Security.Cryptography;

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Custom attribute to indicate if a method should have packet encryption.
/// </summary>
/// <remarks>
/// This attribute can be applied to methods to specify if they should use encryption or not.
/// By default, it assumes encryption is enabled.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class PacketEncryptionAttribute : System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketEncryptionAttribute"/> class.
    /// </summary>
    /// <param name="isEncrypted">Indicates whether the method should be encrypted.</param>
    /// <param name="algorithmType">The type of symmetric algorithm used for encryption.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketEncryptionAttribute(
        System.Boolean isEncrypted = false,
        SymmetricAlgorithmType algorithmType = SymmetricAlgorithmType.Salsa20)
    {
        IsEncrypted = isEncrypted;
        AlgorithmType = algorithmType;
    }

    /// <summary>
    /// Gets the encryption status of the method.
    /// </summary>
    public System.Boolean IsEncrypted { get; }

    /// <summary>
    /// Gets the type of symmetric algorithm used for encryption.
    /// </summary>
    public SymmetricAlgorithmType AlgorithmType { get; }
}