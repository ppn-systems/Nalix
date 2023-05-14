using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;
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

        public string Name { get; set; } = string.Empty; // Fix: Initialize with a default value

        public EnumT EnumT { get; set; }

        public static int Size => sizeof(int) + MaxStringLenght;

        public TestClass()
        {
        }

        public TestClass(int id, string name, EnumT enumT)
        {
            Id = id;
            Name = name;
            EnumT = enumT;
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

        [SerializeDynamicSize(200)]
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

        public TestClass2() : this(0, null!, 0)
        {
        }
    }

    [SerializePackable(SerializeLayout.Sequential)]
    public class TestClass3
    {
        public int Id { get; set; }

        public int IDD { get; set; }
    }

    public static void Main()
    {
        Console.WriteLine("Tests Enter");
        Console.ReadLine();

        Test1();
        Test2();
        Test3();
        Console.WriteLine("Done");
    }

    public static void Test1()
    {
        Console.WriteLine("");
        Console.WriteLine("========================================");
        // Test serialization
        var obj = new TestClass(123, "Hello", EnumT.Value3);
        byte[] data = LiteSerializer.Serialize(obj);
        Console.WriteLine($"Serialized: Id={obj.Id}, Name={obj.Name}, Enum={obj.EnumT}");

        if (data.Length == 0)
        {
            Console.WriteLine("[ERROR] Serialized data is empty!");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Raw data: {BitConverter.ToString(data)}");
        }

        var objn = new TestClass();
        LiteSerializer.Deserialize(data, ref objn);
        Console.WriteLine($"Deserialized: Id={objn.Id}, Name={objn.Name}, Enum ={objn.EnumT}");

        Console.WriteLine("");
        Console.WriteLine("========================================");
    }

    public static void Test2()
    {
        Console.WriteLine("");
        Console.WriteLine("========================================");

        // Test serialization
        var obj = new TestClass2(123, "Hello", 3.14);
        byte[] data = LiteSerializer.Serialize(obj);
        Console.WriteLine($"Serialized: Id={obj.Id}, Name={obj.Name}, F={obj.FloatValue}");

        var objn = new TestClass2();
        LiteSerializer.Deserialize(data, ref objn);
        Console.WriteLine($"Deserialized: Id={objn.Id}, Name={objn.Name}, F={obj.FloatValue}");

        Console.WriteLine("");
        Console.WriteLine("========================================");
    }

    public static void Test3()
    {
        Console.WriteLine("");
        Console.WriteLine("========================================");

        var obj = new TestClass3()
        {
            Id = 123,
            IDD = 456
        };
        byte[] data = LiteSerializer.Serialize(obj);
        Console.WriteLine($"Serialized: Id={obj.Id}, IDD={obj.IDD}");

        var objn = new TestClass3();
        LiteSerializer.Deserialize(data, ref objn);
        Console.WriteLine($"Deserialized: Id={objn.Id}, IDD={objn.IDD}");

        Console.WriteLine("");
        Console.WriteLine("========================================");
    }
}
