using Notio.Common.Attributes;
using System.Text.Json.Serialization;

namespace Notio.Serialization.Test;

public class Person
{
    [JsonProperty("FullName")]
    public string? Name { get; set; }

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
    public int X { get; set; }
    public int Y { get; set; }
}
