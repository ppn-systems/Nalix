namespace Notio.Lite.Parsers;

/// <summary>
/// Represents a Token structure.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Token"/> struct.
/// </remarks>
/// <param name="type">The type.</param>
/// <param name="value">The value.</param>
public struct Token(TokenType type, string value)
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public TokenType Type { get; set; } = type;

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>
    /// The value.
    /// </value>
    public string Value { get; } = type == TokenType.Function || type == TokenType.Operator ? value.ToLowerInvariant() : value;
}