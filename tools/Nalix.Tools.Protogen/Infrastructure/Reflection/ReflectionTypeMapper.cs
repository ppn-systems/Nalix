using System;
using Nalix.Tools.Protogen.Domain.Models;

namespace Nalix.Tools.Protogen.Infrastructure.Reflection;

public static class ReflectionTypeMapper
{
    public static void MapType(Type type, PropertyDefinition propDef)
    {
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType != type)
        {
            propDef.IsNullable = true;
            type = underlyingType;
        }

        propDef.CSharpType = type.Name;

        if (type.IsEnum)
        {
            propDef.Kind = TypeKind.Primitive;
            propDef.CSharpType = Enum.GetUnderlyingType(type).Name;
            return;
        }

        if (type.IsArray)
        {
            propDef.Kind = TypeKind.Array;
            propDef.ElementType = GetTsFriendlyName(type.GetElementType()!);
            return;
        }

        if (type.IsGenericType)
        {
            Type genericTypeDef = type.GetGenericTypeDefinition();
            string genericName = genericTypeDef.Name.Split('`')[0];

            if (genericName == "List" || genericName == "IList" || genericName == "IReadOnlyList")
            {
                propDef.Kind = TypeKind.List;
                propDef.ElementType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                return;
            }
            if (genericName == "Stack")
            {
                propDef.Kind = TypeKind.Stack;
                propDef.ElementType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                return;
            }
            if (genericName == "Queue")
            {
                propDef.Kind = TypeKind.Queue;
                propDef.ElementType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                return;
            }
            if (genericName == "HashSet" || genericName == "ISet" || genericName == "IReadOnlySet")
            {
                propDef.Kind = TypeKind.HashSet;
                propDef.ElementType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                return;
            }
            if (genericName == "Dictionary" || genericName == "IDictionary" || genericName == "IReadOnlyDictionary")
            {
                propDef.Kind = TypeKind.Dictionary;
                propDef.KeyType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                propDef.ValueType = GetTsFriendlyName(type.GetGenericArguments()[1]);
                return;
            }
            if (genericName == "Memory" || genericName == "ReadOnlyMemory")
            {
                propDef.Kind = TypeKind.Memory;
                propDef.ElementType = GetTsFriendlyName(type.GetGenericArguments()[0]);
                return;
            }
            if (genericName.StartsWith("ValueTuple"))
            {
                propDef.Kind = TypeKind.ValueTuple;
                foreach (var arg in type.GetGenericArguments())
                {
                    propDef.TupleElements.Add(GetTsFriendlyName(arg));
                }
                return;
            }
        }

        propDef.Kind = type.Name switch
        {
            "Byte" or "SByte" or "Int16" or "UInt16" or "Int32" or "UInt32" or "Int64" or "UInt64" or "Single" or "Double" => TypeKind.Primitive,
            "String" => TypeKind.String,
            "Boolean" => TypeKind.Boolean,
            "Decimal" => TypeKind.Decimal,
            "DateTime" => TypeKind.DateTime,
            "Guid" => TypeKind.Guid,
            "Snowflake" or "UInt56" => TypeKind.Snowflake,
            "Bytes32" => TypeKind.Bytes32,
            _ => TypeKind.Unknown
        };
    }

    private static string GetTsFriendlyName(Type type)
    {
        if (type.IsEnum) return Enum.GetUnderlyingType(type).Name;
        return type.Name;
    }
}
