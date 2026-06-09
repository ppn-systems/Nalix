// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nalix.Analyzers.Generators.Internal;

#pragma warning disable IDE0072 // Add missing cases

namespace Nalix.Analyzers.Generators;

/// <inheritdoc/>
[Generator]
public sealed class SerializeFormatterGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ITypeSymbol?> provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is TypeDeclarationSyntax { AttributeLists.Count: > 0 } tds &&
                    (tds is ClassDeclarationSyntax or StructDeclarationSyntax) &&
                    !tds.Modifiers.Any(static m =>
                        m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword) ||
                        m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)),
                transform: static (ctx, _) => GetSemanticTarget(ctx))
            .Where(static t => t is not null);

        context.RegisterSourceOutput(provider.Collect(), this.Execute);
    }

    // TODO: Recursive nested enum collection registration.
    //       Currently only top-level enum wrappers are auto-registered (e.g. List<MyEnum>,
    //       Dictionary<string, MyEnum>). Deeply nested shapes like
    //       List<Dictionary<string, List<MyEnum>>> are NOT covered because
    //       RegisterAllFormatters<MyEnum>() only registers single-level wrappers.
    //       The generator should either:
    //         (a) recursively register every collection-of-collection variant, or
    //         (b) emit targeted RegisterCollectionFormatters / RegisterDictionary calls
    //             for each distinct nested shape found in DTO fields.

    /// <summary>
    /// Recursively collects all enum types used within a type symbol.
    /// Handles: direct enum, Nullable&lt;Enum&gt;, arrays, List&lt;T&gt;,
    /// HashSet&lt;T&gt;, Queue&lt;T&gt;, Stack&lt;T&gt;, Dictionary&lt;K,V&gt;,
    /// ValueTuple, Memory&lt;T&gt;, ReadOnlyMemory&lt;T&gt;.
    /// </summary>
    private static void CollectEnumTypes(ITypeSymbol type, HashSet<ITypeSymbol> enums)
    {
        // Direct enum
        if (type.TypeKind == TypeKind.Enum)
        {
            enums.Add(type);
            return;
        }

        // Nullable<T>
        if (type is INamedTypeSymbol namedNullable &&
            namedNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedNullable.TypeArguments.Length == 1)
        {
            CollectEnumTypes(namedNullable.TypeArguments[0], enums);
            return;
        }

        // Arrays: T[]
        if (type is IArrayTypeSymbol arrayType)
        {
            CollectEnumTypes(arrayType.ElementType, enums);
            return;
        }

        // Named generic types: List<T>, Dictionary<K,V>, etc.
        if (type is INamedTypeSymbol named && !named.TypeArguments.IsDefaultOrEmpty)
        {
            // Use OriginalDefinition for collection detection (it keeps the generic arity marker),
            // but use named (with type args) for ValueTuple detection since OriginalDefinition
            // returns "ValueTuple<,>" without type args.
            string origDefName = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Single type argument generics
            if (origDefName == "global::System.Collections.Generic.List<T>" ||
                origDefName == "global::System.Collections.Generic.HashSet<T>" ||
                origDefName == "global::System.Collections.Generic.Queue<T>" ||
                origDefName == "global::System.Collections.Generic.Stack<T>" ||
                origDefName == "global::System.Memory<T>" ||
                origDefName == "global::System.ReadOnlyMemory<T>")
            {
                CollectEnumTypes(named.TypeArguments[0], enums);
                return;
            }

            // Dictionary<K,V>
            if (origDefName == "global::System.Collections.Generic.Dictionary<TKey, TValue>" && named.TypeArguments.Length == 2)
            {
                CollectEnumTypes(named.TypeArguments[0], enums);
                CollectEnumTypes(named.TypeArguments[1], enums);
                return;
            }

            // ValueTuple — use IsTupleType for reliable detection
            if (named.IsTupleType)
            {
                foreach (ITypeSymbol arg in named.TypeArguments)
                {
                    CollectEnumTypes(arg, enums);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Recursively collects fully-qualified type names of ValueTuple types that contain
    /// enum types and therefore need explicit ValueTupleFormatter registration.
    /// </summary>
    private static void CollectValueTupleTypes(ITypeSymbol type, HashSet<INamedTypeSymbol> tupleTypes)
    {
        // Nullable<T>
        if (type is INamedTypeSymbol namedNullable &&
            namedNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedNullable.TypeArguments.Length == 1)
        {
            CollectValueTupleTypes(namedNullable.TypeArguments[0], tupleTypes);
            return;
        }

        // Arrays: T[]
        if (type is IArrayTypeSymbol arrayType)
        {
            CollectValueTupleTypes(arrayType.ElementType, tupleTypes);
            return;
        }

        if (type is INamedTypeSymbol named && !named.TypeArguments.IsDefaultOrEmpty)
        {
            string origDefName = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Single type argument generics — recurse into element type
            if (origDefName == "global::System.Collections.Generic.List<T>" ||
                origDefName == "global::System.Collections.Generic.HashSet<T>" ||
                origDefName == "global::System.Collections.Generic.Queue<T>" ||
                origDefName == "global::System.Collections.Generic.Stack<T>" ||
                origDefName == "global::System.Memory<T>" ||
                origDefName == "global::System.ReadOnlyMemory<T>")
            {
                CollectValueTupleTypes(named.TypeArguments[0], tupleTypes);
                return;
            }

            // Dictionary<K,V>
            if (origDefName == "global::System.Collections.Generic.Dictionary<TKey, TValue>" && named.TypeArguments.Length == 2)
            {
                CollectValueTupleTypes(named.TypeArguments[0], tupleTypes);
                CollectValueTupleTypes(named.TypeArguments[1], tupleTypes);
                return;
            }

            // ValueTuple — use IsTupleType for reliable detection
            if (named.IsTupleType)
            {
                bool containsEnum = false;
                foreach (ITypeSymbol arg in named.TypeArguments)
                {
                    if (ContainsEnumType(arg))
                    {
                        containsEnum = true;
                    }
                }

                if (containsEnum)
                {
                    tupleTypes.Add(named);
                }

                // Also recurse into nested tuples
                foreach (ITypeSymbol arg in named.TypeArguments)
                {
                    CollectValueTupleTypes(arg, tupleTypes);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Returns true if the type contains an enum type at any nesting level.
    /// </summary>
    private static bool ContainsEnumType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type is INamedTypeSymbol namedNullable &&
            namedNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedNullable.TypeArguments.Length == 1)
        {
            return ContainsEnumType(namedNullable.TypeArguments[0]);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsEnumType(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol named && !named.TypeArguments.IsDefaultOrEmpty)
        {
            foreach (ITypeSymbol arg in named.TypeArguments)
            {
                if (ContainsEnumType(arg))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ITypeSymbol? GetSemanticTarget(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDef)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDef) is not ITypeSymbol symbol)
        {
            return null;
        }

        foreach (AttributeData attr in symbol.GetAttributes())
        {
            string? name = attr.AttributeClass?.Name;

            if (name == KnownNames.GenerateFormatterAttributeName)
            {
                return symbol;
            }
        }
        return null;
    }

    private static bool HasStaticCreateMethod(ITypeSymbol type)
    {
        ITypeSymbol? current = type;

        while (current is not null && current.SpecialType == SpecialType.None)
        {
            bool hasCreate = current.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m is
                {
                    Name: "Create",
                    IsStatic: true,
                    Parameters.Length: 0,
                    DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
                });

            if (hasCreate)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private void Execute(SourceProductionContext context, ImmutableArray<ITypeSymbol?> targets)
    {
        if (targets.IsDefaultOrEmpty)
        {
            return;
        }

        List<(string FullName, string FormatterName, bool IsClass)> registeredFormatters = new(targets.Length);
        HashSet<ITypeSymbol> enumTypes = new(SymbolEqualityComparer.Default);
        HashSet<INamedTypeSymbol> tupleTypes = new(SymbolEqualityComparer.Default);
        string generatedNamespace = SourceGenNamespaces.Get(targets.First(static t => t is not null)!);

        foreach (ITypeSymbol? type in targets)
        {
            if (type is null)
            {
                continue;
            }

            bool isClass = type.IsReferenceType;
            string typeName = GetTypeName(type);
            string formatterName = $"{GetFormatterName(type)}Formatter";

            if (isClass && !HasStaticCreateMethod(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "NALIX059",
                        "Missing static Create() method",
                        "Type '{0}' is marked [GenerateFormatter] but neither it nor any of its base types has a public/internal static Create() method. " +
                        "This is required for the pooling pattern used by LiteSerializer.Fill.",
                        $"{KnownNames.SerializationAbstractionsNamespace}.{KnownNames.GenerateFormatterAttributeName}",
                        DiagnosticSeverity.Error,
                        true),
                    type.Locations[0],
                    type.ToDisplayString()));

                continue;
            }

            registeredFormatters.Add((typeName, $"global::{generatedNamespace}.{KnownNames.FormatterName}.{formatterName}", isClass));

            // Collect enum types and ValueTuple wrapper types from all serializable members
            List<ISymbol> members = SerializationMember.Resolve(type);
            foreach (ISymbol member in members)
            {
                ITypeSymbol memberType = GetSymbolType(member);
                CollectEnumTypes(memberType, enumTypes);
                CollectValueTupleTypes(memberType, tupleTypes);
            }

            string interfaceName = isClass ? $"IFillableFormatter<{typeName}>" : $"IFormatter<{typeName}>";

            StringBuilder sb = new(4096);
            _ = sb.AppendLine("// <auto-generated/>");
            _ = sb.AppendLine("using System.Runtime.CompilerServices;");
            _ = sb.AppendLine($"using {KnownNames.CodecMemoryNamespace};");
            _ = sb.AppendLine($"using {KnownNames.LiteSerializerNamespace};");
            _ = sb.AppendLine($"using {KnownNames.CodecExtensionsNamespace};");
            _ = sb.AppendLine();
            _ = sb.AppendLine("#pragma warning disable CS1591");
            _ = sb.AppendLine("#pragma warning disable IDE0240");
            _ = sb.AppendLine("#nullable enable");
            _ = sb.AppendLine();
            _ = sb.AppendLine($"namespace {generatedNamespace}.{KnownNames.FormatterName};");
            _ = sb.AppendLine();
            _ = sb.AppendLine("[System.Diagnostics.StackTraceHidden]");
            _ = sb.AppendLine("[System.Diagnostics.DebuggerStepThrough]");
            _ = sb.AppendLine("[System.Runtime.CompilerServices.SkipLocalsInit]");
            _ = sb.AppendLine($"internal sealed class {formatterName} : {interfaceName}");
            _ = sb.AppendLine("{");

            // ==================== Serialize ====================
            _ = sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]");
            _ = sb.AppendLine($"    public void Serialize(ref DataWriter writer, in {typeName} value)");
            _ = sb.AppendLine("    {");
            if (isClass)
            {
                _ = sb.AppendLine("        System.ArgumentNullException.ThrowIfNull(value);");
                _ = sb.AppendLine();
            }
            if (ImplementsFixedSizeSerializable(type))
            {
                _ = sb.AppendLine($"        writer.Expand({typeName}.Size);");
            }
            _ = sb.Append(this.GenerateSerializeBody(members));
            _ = sb.AppendLine("    }");
            _ = sb.AppendLine();

            // ==================== Deserialize ====================
            _ = sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]");
            _ = sb.AppendLine($"    public {typeName} Deserialize(ref DataReader reader)");
            _ = sb.AppendLine("    {");
            if (isClass)
            {
                _ = sb.AppendLine($"        {typeName} instance = {typeName}.Create();");
                _ = sb.AppendLine("        this.Fill(ref reader, instance);");
                _ = sb.AppendLine("        return instance;");
            }
            else
            {
                _ = sb.AppendLine($"        {typeName} instance = default;");
                _ = sb.Append(this.GenerateAssignmentBody(members, "instance"));
                _ = sb.AppendLine("        return instance;");
            }
            _ = sb.AppendLine("    }");

            // ==================== Fill ====================
            if (isClass)
            {
                _ = sb.AppendLine();
                _ = sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]");
                _ = sb.AppendLine($"    public void Fill(ref DataReader reader, {typeName} value)");
                _ = sb.AppendLine("    {");
                _ = sb.AppendLine("        System.ArgumentNullException.ThrowIfNull(value);");
                _ = sb.AppendLine();
                _ = sb.Append(this.GenerateAssignmentBody(members, "value", true));
                _ = sb.AppendLine("    }");
            }

            _ = sb.AppendLine("}");
            context.AddSource($"{formatterName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        GenerateBootstrapper(context, registeredFormatters, enumTypes, tupleTypes, generatedNamespace);
    }

    private static bool ImplementsFixedSizeSerializable(ITypeSymbol type)
    {
        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (iface.Name == KnownNames.IFixedSizeSerializableName &&
                iface.ContainingNamespace?.ToDisplayString() == KnownNames.SerializationAbstractionsNamespace)
            {
                return true;
            }
        }
        return false;
    }

    private static readonly SymbolDisplayFormat s_fullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static string GetTypeName(ITypeSymbol type)
        => type.ToDisplayString(s_fullyQualifiedNullableFormat);

    private static string GetFormatterName(ITypeSymbol type)
    {
        Stack<string> names = new();
        ITypeSymbol? current = type;

        while (current is not null)
        {
            names.Push(current.MetadataName
                .Replace('`', '_')
                .Replace('.', '_')
                .Replace('+', '_'));
            current = current.ContainingType;
        }

        return string.Join("_", names);
    }

    private string GenerateSerializeBody(List<ISymbol> members)
    {
        StringBuilder code = new(members.Count * 64);

        foreach (ISymbol m in members)
        {
            ITypeSymbol type = GetSymbolType(m);

            if (type.TypeKind == TypeKind.Enum)
            {
                _ = code.AppendLine($"        writer.WriteEnum(value.{m.Name});");
            }
            else if (IsNullableValueType(type))
            {
                // Nullable<T> where T is a value type — route through FormatterProvider
                _ = code.AppendLine($"        FormatterProvider.Get<{GetTypeName(type)}>().Serialize(ref writer, value.{m.Name}!);");
            }
            else if (type.IsUnmanagedType && type.TypeKind == TypeKind.Struct)
            {
                // Custom unmanaged struct
                _ = code.AppendLine($"        writer.WriteUnmanaged(value.{m.Name});");
            }
            else if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_String)
            {
                // Primitive types (byte, int, bool, float, double, char...)
                _ = code.AppendLine($"        writer.Write(value.{m.Name});");
            }
            else
            {
                // string + complex types + reference types
                _ = code.AppendLine($"        FormatterProvider.Get<{GetTypeName(type)}>().Serialize(ref writer, value.{m.Name}!);");
            }
        }

        return code.ToString();
    }

    private string GenerateAssignmentBody(List<ISymbol> members, string targetName, bool isFillContext = false)
    {
        StringBuilder code = new(members.Count * 80);

        foreach (ISymbol m in members)
        {
            ITypeSymbol type = GetSymbolType(m);
            string memberAccess = $"{targetName}.{m.Name}";

            if (type.TypeKind == TypeKind.Enum)
            {
                _ = code.AppendLine($"        {memberAccess} = reader.{GetEnumReadMethod(type)}<{GetTypeName(type)}>();");
            }
            else if (IsNullableValueType(type))
            {
                // Nullable<T> where T is a value type — route through FormatterProvider
                _ = code.AppendLine($"        {memberAccess} = FormatterProvider.Get<{GetTypeName(type)}>().Deserialize(ref reader);");
            }
            else if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_String)
            {
                // Primitive types
                _ = code.AppendLine($"        {memberAccess} = reader.{GetPrimitiveReadMethod(type)}();");
            }
            else if (type.IsUnmanagedType && type.TypeKind == TypeKind.Struct)
            {
                // Custom unmanaged struct
                _ = code.AppendLine($"        {memberAccess} = reader.ReadUnmanaged<{GetTypeName(type)}>();");
            }
            else
            {
                // Complex types + string + array + reference types
                if (isFillContext &&
                    type.IsReferenceType &&
                    type.SpecialType != SpecialType.System_String &&
                    type.TypeKind != TypeKind.Array)
                {
                    _ = code.AppendLine($"        if ({memberAccess} is not null)");
                    _ = code.AppendLine("        {");
                    _ = code.AppendLine($"            ((IFillableFormatter<{GetTypeName(type)}>)FormatterProvider.Get<{GetTypeName(type)}>()).Fill(ref reader, {memberAccess});");
                    _ = code.AppendLine("        }");
                    _ = code.AppendLine("        else");
                    _ = code.AppendLine("        {");
                    _ = code.AppendLine($"            {memberAccess} = FormatterProvider.Get<{GetTypeName(type)}>().Deserialize(ref reader);");
                    _ = code.AppendLine("        }");
                }
                else
                {
                    _ = code.AppendLine($"        {memberAccess} = FormatterProvider.Get<{GetTypeName(type)}>().Deserialize(ref reader);");
                }
            }
        }

        return code.ToString();
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> is Nullable&lt;T&gt; where T is a value type.
    /// Nullable&lt;T&gt; is a struct and satisfies IsUnmanagedType, but WriteUnmanaged/ReadUnmanaged
    /// reject it because Nullable&lt;T&gt; doesn't satisfy the unmanaged constraint.
    /// </summary>
    private static bool IsNullableValueType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0].IsValueType;
        }
        return false;
    }

    private static string GetEnumReadMethod(ITypeSymbol type)
    {
        SpecialType? underlying = ((INamedTypeSymbol)type).EnumUnderlyingType?.SpecialType;

        return underlying switch
        {
            SpecialType.System_SByte => "ReadEnumSByte",
            SpecialType.System_Byte => "ReadEnumByte",
            SpecialType.System_Int16 => "ReadEnumInt16",
            SpecialType.System_UInt16 => "ReadEnumUInt16",
            SpecialType.System_Int32 => "ReadEnumInt32",
            SpecialType.System_UInt32 or _ => "ReadEnumUInt32"
        };
    }

    private static string GetPrimitiveReadMethod(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Char => "ReadChar",
        SpecialType.System_Byte => "ReadByte",
        SpecialType.System_SByte => "ReadSByte",
        SpecialType.System_Boolean => "ReadBoolean",
        SpecialType.System_Int16 => "ReadInt16",
        SpecialType.System_Int32 => "ReadInt32",
        SpecialType.System_Int64 => "ReadInt64",
        SpecialType.System_UInt16 => "ReadUInt16",
        SpecialType.System_UInt32 => "ReadUInt32",
        SpecialType.System_UInt64 => "ReadUInt64",
        SpecialType.System_Single => "ReadSingle",
        SpecialType.System_Double => "ReadDouble",
        _ => "ReadUnmanaged"
    };

    private static ITypeSymbol GetSymbolType(ISymbol symbol)
        => symbol is IPropertySymbol p ? p.Type : ((IFieldSymbol)symbol).Type;

    private static void GenerateBootstrapper(
        SourceProductionContext context,
        List<(string FullName, string FormatterName, bool IsClass)> registered,
        HashSet<ITypeSymbol> enumTypes,
        HashSet<INamedTypeSymbol> tupleTypes,
        string generatedNamespace)
    {
        int totalEntries = registered.Count;
        StringBuilder sb = new((totalEntries * 120) + (enumTypes.Count * 80) + (tupleTypes.Count * 80) + 512);
        _ = sb.AppendLine("using System.Runtime.CompilerServices;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("#pragma warning disable CA2255");
        _ = sb.AppendLine("#pragma warning disable IDE0240");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"namespace {generatedNamespace};");
        _ = sb.AppendLine();

        // ── SerializeFormatterGenerated bootstrapper ──
        _ = sb.AppendLine("internal static class SerializeFormatterGenerated");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    [ModuleInitializer]");
        _ = sb.AppendLine("    public static void Initialize()");
        _ = sb.AppendLine("    {");

        // ── Step 1: Register enum formatter families BEFORE DTO formatters.
        // This ensures FormatterProvider.Get<List<MyEnum>>(), Get<MyEnum[]>(),
        // Get<Dictionary<string, MyEnum>>(), etc. all resolve correctly
        // when the generated DTO formatter is constructed.
        if (enumTypes.Count > 0)
        {
            _ = sb.AppendLine("        // ── Auto-register enum formatter families used by generated DTOs ──");
            foreach (ITypeSymbol enumType in enumTypes.OrderBy(static e => e.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            {
                string enumFullName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                _ = sb.AppendLine($"        global::{KnownNames.LiteSerializerNamespace}.{KnownNames.FormatterProviderName}.RegisterAllFormatters<{enumFullName}>();");
            }
            _ = sb.AppendLine();
        }

        // ── Step 1b: Register ValueTuple formatters that contain enum types.
        // These are composite types that RegisterAllFormatters cannot handle.
        if (tupleTypes.Count > 0)
        {
            _ = sb.AppendLine("        // ── Auto-register ValueTuple formatters containing enum types ──");
            foreach (INamedTypeSymbol tupleType in tupleTypes.OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            {
                // Build type arguments for RegisterTuple<T1, T2, ...>() call
                StringBuilder args = new();
                for (int i = 0; i < tupleType.TypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        _ = args.Append(", ");
                    }
                    _ = args.Append(tupleType.TypeArguments[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
                _ = sb.AppendLine($"        global::{KnownNames.LiteSerializerNamespace}.{KnownNames.FormatterProviderName}.RegisterTuple<{args}>();");
            }
            _ = sb.AppendLine();
        }

        // ── Step 2: Register generated DTO formatters ──
        foreach ((_, string formatterName, _) in registered)
        {
            _ = sb.AppendLine($"        global::{KnownNames.LiteSerializerNamespace}.{KnownNames.FormatterProviderName}.RegisterGenerated(new {formatterName}());");
        }

        // ── Step 3: Register List<T> and T[] formatters for class types ──
        foreach ((string fullName, _, bool isClass) in registered)
        {
            if (isClass)
            {
                _ = sb.AppendLine($"        global::{KnownNames.LiteSerializerNamespace}.{KnownNames.FormatterProviderName}.RegisterCollectionFormatters<{fullName}>();");
            }
        }

        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
        _ = sb.AppendLine();

        // ── GeneratedFormatterRegistry for runtime lookup ──
        _ = sb.AppendLine("internal static partial class GeneratedFormatterRegistry");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.AppendLine($"    internal static bool TryGet<T>(out global::{KnownNames.LiteSerializerNamespace}.IFormatter<T>? formatter)");
        _ = sb.AppendLine("    {");

        foreach ((string fullName, string formatterName, _) in registered)
        {
            _ = sb.AppendLine($"        if (typeof(T) == typeof({fullName}))");
            _ = sb.AppendLine("        {");
            _ = sb.AppendLine($"            formatter = (global::{KnownNames.LiteSerializerNamespace}.IFormatter<T>)(object)new {formatterName}();");
            _ = sb.AppendLine("            return true;");
            _ = sb.AppendLine("        }");
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("        formatter = default;");
        _ = sb.AppendLine("        return false;");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

        context.AddSource("SerializeFormatterGenerated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

}
