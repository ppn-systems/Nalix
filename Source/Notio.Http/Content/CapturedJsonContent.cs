namespace Notio.Http.Content;

/// <summary>
/// Provides HTTP content based on a serialized JSON object, with the JSON string captured to a property
/// so it can be read without affecting the read-once content stream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CapturedJsonContent"/> class.
/// </remarks>
/// <param name="json">The json.</param>
public class CapturedJsonContent(string json)
    : CapturedStringContent(json, "application/json; charset=UTF-8")
{
}