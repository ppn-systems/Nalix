using Nalix.Common.Serialization;
using Nalix.Serialization;
using System;

namespace Nalix.Tests.Logging;

public class Class1
{
    // Example usage để test
    [SerializePackable(SerializeLayout.Sequential)]
    public class TestClass : IFixedSizeSerializable
    {
        [SerializeOrder(1)]
        public int Id { get; set; }

        [SerializeOrder(2)]
        public string Name { get; set; }

        [SerializeIgnore]
        public static int Size => sizeof(int) + 100;

        public TestClass()
        {
        }

        public TestClass(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    [SerializePackable(SerializeLayout.Sequential)]
    public struct TestClass2(int id, string name)
    {
        private int _id = id;

        private string _name = name;

        [SerializeOrder(1)]
        public int Id
        {
            readonly get => _id;
            set => _id = value;
        }

        [SerializeOrder(2)]
        public string Name
        {
            readonly get => _name;
            set => _name = value;
        }
    }

    public static void Main()
    {
        // Test serialization
        var obj = new TestClass(123, "Hello");
        byte[] data = Serializer.Serialize(obj);

        var objn = new TestClass();
        Serializer.Deserialize(data, ref objn);

        Console.WriteLine($"Serialized: Id={obj.Id}, Name={obj.Name}");
        Console.WriteLine($"Deserialized: Id={objn.Id}, Name={objn.Name}");
    }
}
