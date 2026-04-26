namespace Nalix.Tools.Protogen.Domain.Models;

public enum TypeKind
{
    Primitive,     // byte, int, float, etc.
    String,        // string
    Boolean,       // bool
    Decimal,       // decimal
    DateTime,      // DateTime
    Guid,          // Guid
    Snowflake,     // Snowflake, ulong
    Bytes32,       // Bytes32
    Array,         // T[]
    List,          // List<T>
    Stack,         // Stack<T>
    Queue,         // Queue<T>
    HashSet,       // HashSet<T>
    Dictionary,    // Dictionary<K, V>
    Memory,        // Memory<T>, ReadOnlyMemory<T>
    ValueTuple,    // ValueTuple<T1, T2...>
    NestedPacket,  // Inherits PacketBase or has [SerializePackable]
    Unknown
}

