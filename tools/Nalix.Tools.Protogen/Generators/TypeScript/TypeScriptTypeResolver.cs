using System.Collections.Generic;
using Nalix.Tools.Protogen.Domain.Models;

namespace Nalix.Tools.Protogen.Generators.TypeScript;

public static class TypeScriptTypeResolver
{
    public static string GetTsType(PropertyDefinition prop)
    {
        string typeStr = prop.Kind switch
        {
            TypeKind.Primitive => GetTsTypeForPrimitive(prop.CSharpType),
            TypeKind.String => "string",
            TypeKind.Boolean => "boolean",
            TypeKind.Decimal => "number",
            TypeKind.DateTime => "bigint", // UTC ticks
            TypeKind.Guid => "string",
            TypeKind.Snowflake => "bigint",
            TypeKind.Bytes32 => "Uint8Array",
            TypeKind.Array => $"{GetElementTypeTs(prop.ElementType)}[]",
            TypeKind.List => $"{GetElementTypeTs(prop.ElementType)}[]",
            TypeKind.Stack => $"{GetElementTypeTs(prop.ElementType)}[]",
            TypeKind.Queue => $"{GetElementTypeTs(prop.ElementType)}[]",
            TypeKind.HashSet => $"Set<{GetElementTypeTs(prop.ElementType)}>",
            TypeKind.Dictionary => $"Map<{GetElementTypeTs(prop.KeyType)}, {GetElementTypeTs(prop.ValueType)}>",
            TypeKind.Memory => "Uint8Array",
            TypeKind.ValueTuple => "[" + string.Join(", ", prop.TupleElements.ConvertAll(GetElementTypeTs)) + "]",
            TypeKind.NestedPacket => prop.CSharpType,
            _ => "any"
        };

        if (prop.IsNullable && prop.Kind != TypeKind.Array && prop.Kind != TypeKind.List && prop.Kind != TypeKind.Dictionary)
        {
            typeStr += " | null";
        }

        return typeStr;
    }

    public static string GetElementTypeTs(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";
        
        string mapped = GetTsTypeForPrimitive(csharpType);
        if (mapped != "unknown") return mapped;
        
        return csharpType; // Fallback to class name
    }

    private static string GetTsTypeForPrimitive(string primitive)
    {
        return primitive switch
        {
            "Byte" or "SByte" or "Int16" or "UInt16" or "Int32" or "UInt32" or "Single" or "Double" => "number",
            "Int64" or "UInt64" => "bigint",
            "String" => "string",
            "Boolean" => "boolean",
            "Decimal" => "number",
            _ => "unknown"
        };
    }

    public static string GetWriterMethodForPrimitive(string primitive)
    {
        return primitive switch
        {
            "Byte" => "writeByte",
            "SByte" => "writeSByte",
            "Int16" => "writeInt16",
            "UInt16" => "writeUint16",
            "Int32" => "writeInt32",
            "UInt32" => "writeUint32",
            "Int64" => "writeInt64",
            "UInt64" => "writeUint64",
            "Single" => "writeSingle",
            "Double" => "writeDouble",
            "String" => "writeString",
            "Boolean" => "writeBoolean",
            "Decimal" => "writeDecimal",
            _ => "unknown"
        };
    }

    public static string GetReaderMethodForPrimitive(string primitive)
    {
        return primitive switch
        {
            "Byte" => "readByte",
            "SByte" => "readSByte",
            "Int16" => "readInt16",
            "UInt16" => "readUint16",
            "Int32" => "readInt32",
            "UInt32" => "readUint32",
            "Int64" => "readInt64",
            "UInt64" => "readUint64",
            "Single" => "readSingle",
            "Double" => "readDouble",
            "String" => "readString",
            "Boolean" => "readBoolean",
            "Decimal" => "readDecimal",
            _ => "unknown"
        };
    }
}
