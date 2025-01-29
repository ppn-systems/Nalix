using System;

namespace Notio.Lite.Parsers;

/// <summary>
/// Models a verb option.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VerbOptionAttribute" /> class.
/// </remarks>
/// <param name="name">The name.</param>
/// <exception cref="ArgumentNullException">name.</exception>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VerbOptionAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the verb option.
    /// </summary>
    /// <value>
    /// Name.
    /// </value>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets or sets a short description of this command line verb. Usually a sentence summary.
    /// </summary>
    /// <value>
    /// The help text.
    /// </value>
    public string HelpText { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => $"  {Name}\t\t{HelpText}";
}