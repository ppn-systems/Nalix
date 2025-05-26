using Nalix.Common.Serialization;
using Nalix.Serialization;
using System;

namespace Nalix.Tests.Logging;

public class Class1
{
    public enum EnumT : byte
    {
        Default = 0,
        Value1 = 1,
        Value2 = 2,
        Value3 = 3,
    }

    // Example usage để test
    [SerializePackable(SerializeLayout.Sequential)]
    public class TestClass : IFixedSizeSerializable
    {
        public const int MaxStringLenght = 100;
        public int Id { get; set; }

        public string Name { get; set; }

        public EnumT enumT { get; set; }

        public static int Size => sizeof(int) + MaxStringLenght;

        public TestClass()
        {
        }

        public TestClass(int id, string name, EnumT enumT)
        {
            Id = id;
            Name = name;
            this.enumT = enumT;
        }
    }

    [SerializePackable(SerializeLayout.Sequential)]
    public struct TestClass2(int id, string name, double fl)
    {
        private int _id = id;

        private string _name = name;

        private double _floatValue = fl;

        public int Id
        {
            readonly get => _id;
            set => _id = value;
        }

        public string Name
        {
            readonly get => _name;
            set => _name = value;
        }

        public double FloatValue
        {
            readonly get => _floatValue;
            set => _floatValue = value;
        }
    }

    public class TestClass3
    {
        public int Id;

        public uint IDD;
    }

    public static void Main()
    {
        Test1();
        Test2();
        //Test3();
    }

    public static void Test1()
    {
        // Test serialization
        var obj = new TestClass2(123, "Hello", 3.14);
        byte[] data = Serializer.Serialize(obj);

        var objn = new TestClass2();
        Serializer.Deserialize(data, ref objn);

        Console.WriteLine($"Serialized: Id={obj.Id}, Name={obj.Name}, F={obj.FloatValue}");
        Console.WriteLine($"Deserialized: Id={objn.Id}, Name={objn.Name}, F={obj.FloatValue}");
    }

    public static void Test2()
    {
        // Test serialization
        var obj = new TestClass(123, "Hello", EnumT.Value3);
        byte[] data = Serializer.Serialize(obj);

        Console.WriteLine($"L={data.Length},D={BitConverter.ToString(data)}");

        var objn = new TestClass();
        Serializer.Deserialize(data, ref objn);

        Console.WriteLine($"Serialized: Id={obj.Id}, Name={obj.Name}, Enum={obj.enumT}");
        Console.WriteLine($"Deserialized: Id={objn.Id}, Name={objn.Name}, Enum ={objn.enumT}");
    }

    public static void Test3()
    {
        var obj = new TestClass3()
        {
            Id = 123,
            IDD = 456
        };

        byte[] data = Serializer.Serialize(obj);
        Console.WriteLine($"L={data.Length},D={BitConverter.ToString(data)}");
        var objn = new TestClass3();
        Serializer.Deserialize(data, ref objn);
        Console.WriteLine($"Serialized: Id={obj.Id}, IDD={obj.IDD}");
        Console.WriteLine($"Deserialized: Id={objn.Id}, IDD={objn.IDD}");
    }
}
