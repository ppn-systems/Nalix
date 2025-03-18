using Notio.Common.Attributes;
using System;

namespace Notio.Serialization.Test;

public static class JsonSerializerTests
{
    public static void RunAllTests()
    {
        Test_SerializeWithJsonProperty();
        Test_SerializeWithJsonInclude();
        Test_SerializeStruct();
        Test_DeserializeWithJsonProperty();
        Console.WriteLine("✅ All tests passed!");
    }

    private static void AssertEqual(string expected, string actual, string testName)
    {
        if (expected.Trim() != actual.Trim())
        {
            Console.WriteLine($"❌ Test Failed: {testName}");
            Console.WriteLine($"Expected: {expected}");
            Console.WriteLine($"Actual:   {actual}");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine($"✅ {testName} Passed");
        }
    }

    private static void Test_SerializeWithJsonProperty()
    {
        var person = new Person { Name = "Bob", Age = 30 };
        string json = Json.Serialize(person, JsonSerializerCase.None);

        string expected = "{\"FullName\": \"Bob\",\"Age\": 30}";
        AssertEqual(expected, json, nameof(Test_SerializeWithJsonProperty));
    }

    private static void Test_SerializeWithJsonInclude()
    {
        var car = new Car { Model = "Tesla", Year = 2022 };
        string json = Json.Serialize(car, JsonSerializerCase.None);

        string expected = "{\"Model\": \"Tesla\"}";
        AssertEqual(expected, json, nameof(Test_SerializeWithJsonInclude));
    }

    private static void Test_SerializeStruct()
    {
        var point = new Point { X = 5, Y = 10 };
        string json = Json.Serialize(point, JsonSerializerCase.None);

        string expected = "{\"X\": 5,\"Y\": 10}";
        AssertEqual(expected, json, nameof(Test_SerializeStruct));
    }

    private static void Test_DeserializeWithJsonProperty()
    {
        byte[] a = Json.SerializeToBytes(new Point { X = 5, Y = 10 }, Json.OptionDefault);
        string s = Json.Encoding.GetString(a);
        Console.WriteLine(s);
        Point point = Json.Deserialize<Point>(s);

        Console.WriteLine(point.X);
        Console.WriteLine(point.Y);
    }
}

public class Person
{
    [JsonInclude]
    [JsonProperty("FullName")]
    public string Name { get; set; } = string.Empty;

    [JsonInclude]
    public int Age { get; set; }
}

public class Car
{
    [JsonInclude]
    public string Model { get; set; } = string.Empty;

    public int Year { get; set; } // Không có [JsonInclude], sẽ bị bỏ qua
}

public struct Point
{
    [JsonInclude]
    public int X { get; set; }

    [JsonInclude]
    public int Y { get; set; }
}
