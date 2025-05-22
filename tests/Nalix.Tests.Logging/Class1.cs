using Nalix.Common.Serialization;
using Nalix.Serialization;
using System;

namespace Nalix.Tests.Logging;

public class Class1
{
    // Example usage để test
    public class TestClass
    {
        [FieldOrder(0)]
        private int _id;

        [FieldOrder(1)]
        private string _name;

        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public TestClass()
        { }

        public TestClass(int id, string name)
        {
            _id = id;
            _name = name;
        }
    }

    public static void Main()
    {
        // Test serialization
        var obj = new TestClass(123, "Hello");
        byte[] data = BinarySerializer<TestClass>.SerializeToArray(obj);
        var deserialized = BinarySerializer<TestClass>.DeserializeFromArray(data);

        Console.WriteLine($"Deserialized: Id={deserialized.Id}, Name={deserialized.Name}");
    }
}
