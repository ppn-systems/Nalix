using Notio.Common.Attributes;
using JsonIncludeAttribute = Notio.Common.Attributes.JsonIncludeAttribute;

namespace Notio.Serialization.Test;

public class Person
{
    [JsonInclude]
    [JsonProperty("FullName")]
    public string? Name { get; set; }

    [JsonInclude]
    public int Age { get; set; }
}

public class Car
{
    [JsonInclude]
    public string? Model { get; set; }

    public int Year { get; set; } // Không có JsonInclude => sẽ bị bỏ qua
}

public struct Point
{
    [JsonInclude]
    public int X { get; set; }

    [JsonInclude]
    public int Y { get; set; }
}
