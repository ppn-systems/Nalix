using System;

namespace Notio.Serialization.Test;
public static class JsonSerializerTests
{
    public static void RunAllTests()
    {
        Test_SerializeSimpleObject();
        Test_SerializeWithJsonProperty();
        Test_SerializeWithJsonInclude();
        Test_SerializeStruct();
        Console.WriteLine("✅ All tests passed!");
    }

    private static void AssertEqual(string expected, string actual, string testName)
    {
        if (expected != actual)
        {
            Console.WriteLine($"❌ Test Failed: {testName}");
            Console.WriteLine($"Expected: {expected}");
            Console.WriteLine($"Actual:   {actual}");
            Environment.Exit(1); // Dừng chương trình nếu lỗi
        }
        else
        {
            Console.WriteLine($"✅ {testName} Passed");
        }
    }

    private static void Test_SerializeSimpleObject()
    {
        var person = new Person { Name = "Alice", Age = 25 };
        string json = Json.Serialize(person, 0);

        string expected = "{\"Name\":\"Alice\",\"Age\":25}";
        AssertEqual(expected, json, nameof(Test_SerializeSimpleObject));
    }

    private static void Test_SerializeWithJsonProperty()
    {
        var person = new Person { Name = "Bob", Age = 30 };
        string json = Json.Serialize(person, 0);

        string expected = "{\"FullName\":\"Bob\",\"Age\":30}"; // "FullName" thay vì "Name"
        AssertEqual(expected, json, nameof(Test_SerializeWithJsonProperty));
    }

    private static void Test_SerializeWithJsonInclude()
    {
        var car = new Car { Model = "Tesla", Year = 2022 };
        string json = Json.Serialize(car, 0);

        string expected = "{\"Model\":\"Tesla\"}"; // Chỉ serialize property có JsonInclude
        AssertEqual(expected, json, nameof(Test_SerializeWithJsonInclude));
    }

    private static void Test_SerializeStruct()
    {
        var point = new Point { X = 5, Y = 10 };
        string json = Json.Serialize(point, 0);

        string expected = "{\"X\":5,\"Y\":10}";
        AssertEqual(expected, json, nameof(Test_SerializeStruct));
    }
}
