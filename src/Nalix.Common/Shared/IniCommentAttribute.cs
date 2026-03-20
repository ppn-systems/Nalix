namespace Nalix.Common.Shared;

/// <summary>
/// Specifies a comment to be written above a section or key in the INI file.
/// Can be applied to both classes (section comment) and properties (key comment).
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class IniCommentAttribute : System.Attribute
{
    /// <summary>
    /// Gets the comment text. Supports multi-line via \n.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.String Comment { get; }

    /// <summary>
    /// Ini comment attribute constructor.
    /// </summary>
    public IniCommentAttribute([System.Diagnostics.CodeAnalysis.NotNull] System.String comment)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(comment, nameof(comment));
        Comment = comment;
    }
}