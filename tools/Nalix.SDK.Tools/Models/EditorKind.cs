namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Identifies the editor UI that should be used for a reflected property.
/// </summary>
public enum EditorKind
{
    Text = 0,
    Numeric = 1,
    Enum = 2,
    Boolean = 3,
    ByteArray = 4,
    Complex = 5,
    Unsupported = 6
}
