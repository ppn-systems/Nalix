namespace Nalix.Common.Package.Attributes;

/// <summary>
/// Custom attribute to indicate if a method should have packet encryption.
/// </summary>
/// <remarks>
/// This attribute can be applied to methods to specify if they should use encryption or not.
/// By default, it assumes encryption is enabled.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketEncryptionAttribute"/> class.
/// </remarks>
/// <param name="isEncrypted">Indicates if the method should be encrypted (default is true).</param>
[System.AttributeUsage(System.AttributeTargets.Method)]
public class PacketEncryptionAttribute(System.Boolean isEncrypted = true) : System.Attribute
{
    /// <summary>
    /// Gets the encryption status of the method.
    /// </summary>
    public System.Boolean IsEncrypted { get; } = isEncrypted;
}
