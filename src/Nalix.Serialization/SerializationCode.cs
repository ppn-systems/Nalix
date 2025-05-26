namespace Nalix.Serialization;

/// <summary>
/// Configuration options for serialization behavior.
/// Implements the Strategy Pattern for flexible configuration.
/// </summary>
public sealed record SerializationCode
{
    /// <summary>
    /// Whether to fail when a property cannot be processed.
    /// </summary>
    public bool FailOnPropertyErrors { get; init; } = true;

    /// <summary>
    /// Whether to validate data integrity during deserialization.
    /// </summary>
    public bool ValidateDataIntegrity { get; init; } = true;

    /// <summary>
    /// Whether to include private properties in serialization.
    /// </summary>
    public bool IncludePrivateProperties { get; init; } = false;

    /// <summary>
    /// Default serialization options.
    /// </summary>
    public static SerializationCode Default { get; } = new();
}
