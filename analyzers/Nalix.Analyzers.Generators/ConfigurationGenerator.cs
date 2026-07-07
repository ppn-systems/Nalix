// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
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

/// <summary>
/// Generates AOT-safe configuration binding helpers for types derived from
/// <c>ConfigurationLoader</c>.
/// </summary>
[Generator]
public class ConfigurationGenerator : IIncrementalGenerator
{
    public readonly struct ConfigPropertyModel : IEquatable<ConfigPropertyModel>
    {
        public string Name { get; }
        public string IniMethod { get; }
        public string? Comment { get; }
        public bool IsString { get; }
        public bool IsEnum { get; }
        public ImmutableArray<string> ValidationSnippets { get; }

        public ConfigPropertyModel(string name, string iniMethod, string? comment, bool isString, bool isEnum, ImmutableArray<string> validationSnippets)
        {
            this.Name = name;
            this.IniMethod = iniMethod;
            this.Comment = comment;
            this.IsString = isString;
            this.IsEnum = isEnum;
            this.ValidationSnippets = validationSnippets;
        }

        public bool Equals(ConfigPropertyModel other)
        {
            if (this.Name != other.Name || this.IniMethod != other.IniMethod || this.Comment != other.Comment || this.IsString != other.IsString || this.IsEnum != other.IsEnum || this.ValidationSnippets.Length != other.ValidationSnippets.Length)
            {
                return false;
            }

            for (int i = 0; i < this.ValidationSnippets.Length; i++)
            {
                if (this.ValidationSnippets[i] != other.ValidationSnippets[i])
                {
                    return false;
                }
            }

            return true;
        }
        public override bool Equals(object obj) => obj is ConfigPropertyModel other && this.Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + (this.Name?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.IniMethod?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.Comment?.GetHashCode() ?? 0);
                hash = (hash * 23) + this.IsString.GetHashCode();
                hash = (hash * 23) + this.IsEnum.GetHashCode();
                foreach (string s in this.ValidationSnippets)
                {
                    hash = (hash * 23) + (s?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }
    }

    public readonly struct ConfigClassModel : IEquatable<ConfigClassModel>
    {
        public string HintName { get; }
        public string Namespace { get; }
        public string Name { get; }
        public string? SectionComment { get; }
        public ImmutableArray<string> NestingOpeners { get; }
        public ImmutableArray<ConfigPropertyModel> Properties { get; }

        public ConfigClassModel(string hintName, string @namespace, string name, string? sectionComment, ImmutableArray<string> nestingOpeners, ImmutableArray<ConfigPropertyModel> properties)
        {
            this.HintName = hintName;
            this.Namespace = @namespace;
            this.Name = name;
            this.SectionComment = sectionComment;
            this.NestingOpeners = nestingOpeners;
            this.Properties = properties;
        }

        public bool Equals(ConfigClassModel other)
        {
            if (this.HintName != other.HintName || this.Namespace != other.Namespace || this.Name != other.Name || this.SectionComment != other.SectionComment || this.NestingOpeners.Length != other.NestingOpeners.Length || this.Properties.Length != other.Properties.Length)
            {
                return false;
            }

            for (int i = 0; i < this.NestingOpeners.Length; i++)
            {
                if (this.NestingOpeners[i] != other.NestingOpeners[i])
                {
                    return false;
                }
            }

            for (int i = 0; i < this.Properties.Length; i++)
            {
                if (!this.Properties[i].Equals(other.Properties[i]))
                {
                    return false;
                }
            }

            return true;
        }
        public override bool Equals(object obj) => obj is ConfigClassModel other && this.Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + (this.HintName?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.Namespace?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.Name?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.SectionComment?.GetHashCode() ?? 0);
                foreach (string s in this.NestingOpeners)
                {
                    hash = (hash * 23) + (s?.GetHashCode() ?? 0);
                }

                foreach (ConfigPropertyModel p in this.Properties)
                {
                    hash = (hash * 23) + p.GetHashCode();
                }

                return hash;
            }
        }
    }

    /// <summary>
    /// Registers the incremental source-generation pipeline.
    /// </summary>
    /// <param name="context">The Roslyn incremental generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ConfigClassModel> configClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.BaseList != null,
                transform: static (ctx, _) => GetTargetModel(ctx))
            .Where(static m => m.Name != null);

        context.RegisterSourceOutput(configClasses, static (spc, source) => Execute(source, spc));
    }

    private static ConfigClassModel GetTargetModel(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
        {
            return default;
        }

        bool isConfigLoader = false;
        INamedTypeSymbol? baseType = symbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == KnownNames.ConfigurationLoader &&
                baseType.ContainingNamespace.ToString() == KnownNames.ConfigurationBindingNamespace)
            {
                isConfigLoader = true;
                break;
            }
            baseType = baseType.BaseType;
        }

        if (!isConfigLoader)
        {
            return default;
        }

        string ns = symbol.ContainingNamespace.ToString();
        string name = symbol.Name;
        string hintName = symbol.ToDisplayString().Replace(".", "_").Replace("<", "_").Replace(">", "_");

        List<IPropertySymbol> props = [.. symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(IsBindableProperty)];

        // Section comment from [IniComment] on the class
        AttributeData? sectionCommentAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is KnownNames.IniCommentAttribute or KnownNames.IniCommentShort);
        string? sectionComment = sectionCommentAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();

        // Build nesting chain (outermost first)
        List<INamedTypeSymbol> nestingChain = new();
        INamedTypeSymbol current = symbol.ContainingType;
        while (current != null)
        {
            nestingChain.Insert(0, current);
            current = current.ContainingType;
        }

        ImmutableArray<string>.Builder nestingOpeners = ImmutableArray.CreateBuilder<string>();
        foreach (INamedTypeSymbol container in nestingChain)
        {
            string keyword = container.IsRecord ? "record" : "class";
            nestingOpeners.Add($"partial {keyword} {container.Name}");
        }

        ImmutableArray<ConfigPropertyModel>.Builder propModels = ImmutableArray.CreateBuilder<ConfigPropertyModel>();
        foreach (IPropertySymbol p in props)
        {
            string propName = p.Name;
            string method = GetIniMethod(p.Type);
            bool isString = p.Type.SpecialType == SpecialType.System_String;
            bool isEnum = p.Type.TypeKind == TypeKind.Enum;

            // Property comment
            AttributeData? commentAttr = p.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name is KnownNames.IniCommentAttribute or KnownNames.IniCommentShort);
            string? comment = commentAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();

            ImmutableArray<string>.Builder validationSnippets = ImmutableArray.CreateBuilder<string>();
            foreach (AttributeData attr in p.GetAttributes())
            {
                string attrName = attr.AttributeClass?.Name ?? "";
                string attrNs = attr.AttributeClass?.ContainingNamespace.ToString() ?? "";

                if (attrName == KnownNames.ValueRangeAttributeName && attrNs == KnownNames.ValidationNamespace)
                {
                    // [ValueRange(min, max)] — double or long constructor args
                    bool useInt64 = false;
                    foreach (KeyValuePair<string, TypedConstant> na in attr.NamedArguments)
                    {
                        if (na.Key == "UseInt64" && na.Value.Value is true) { useInt64 = true; break; }
                    }

                    string minVal, maxVal, minStr, maxStr;
                    if (useInt64)
                    {
                        long lo = 0, hi = 0;
                        foreach (KeyValuePair<string, TypedConstant> na in attr.NamedArguments)
                        {
                            if (na.Key == "MinimumInt64" && na.Value.Value is long lv)
                            {
                                lo = lv;
                            }
                            else if (na.Key == "MaximumInt64" && na.Value.Value is long hv)
                            {
                                hi = hv;
                            }
                        }
                        foreach (TypedConstant ca in attr.ConstructorArguments)
                        {
                            if (ca.Value is long lv)
                            {
                                if (lo == 0 && lv != 0)
                                {
                                    lo = lv;
                                }
                                else
                                {
                                    hi = lv;
                                }
                            }
                        }
                        minStr = lo.ToString();
                        maxStr = hi.ToString();
                        string loLit = lo switch { int.MaxValue => "global::System.Int32.MaxValue", long.MaxValue => "global::System.Int64.MaxValue", _ => lo.ToString() };
                        string hiLit = hi switch { int.MaxValue => "global::System.Int32.MaxValue", long.MaxValue => "global::System.Int64.MaxValue", _ => hi.ToString() };
                        minVal = loLit;
                        maxVal = hiLit;
                    }
                    else
                    {
                        minStr = attr.ConstructorArguments[0].Value?.ToString() ?? "0";
                        maxStr = attr.ConstructorArguments[1].Value?.ToString() ?? "0";
                        minVal = minStr;
                        maxVal = maxStr;
                        if (attr.ConstructorArguments[0].Value is double) { minVal += "d"; maxVal += "d"; }
                    }

                    validationSnippets.Add($"if (this.{p.Name} < {minVal} || this.{p.Name} > {maxVal}) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} must be between {minStr} and {maxStr}.\");");
                }
                else if (attrName == KnownNames.DurationRangeAttributeName && attrNs == KnownNames.ValidationNamespace)
                {
                    // [DurationRange("00:00:01", "1.00:00:00")] — string TimeSpan args
                    string minStr = attr.ConstructorArguments[0].Value?.ToString() ?? "00:00:00";
                    string maxStr = attr.ConstructorArguments[1].Value?.ToString() ?? "00:00:00";
                    validationSnippets.Add($"if (this.{p.Name} < global::System.TimeSpan.Parse(\"{minStr}\") || this.{p.Name} > global::System.TimeSpan.Parse(\"{maxStr}\")) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} must be between {minStr} and {maxStr}.\");");
                }
                else if (attrName == KnownNames.LengthAttributeName && attrNs == KnownNames.ValidationNamespace)
                {
                    // [Length(n)] — minimum length for string / collection
                    string minLen = attr.ConstructorArguments[0].Value?.ToString() ?? "0";
                    if (p.Type.SpecialType == SpecialType.System_String || p.Type is IArrayTypeSymbol)
                    {
                        validationSnippets.Add($"if (this.{p.Name} is not null && this.{p.Name}.Length < {minLen}) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} length must be at least {minLen}.\");");
                    }
                    else
                    {
                        // ICollection<T> / IReadOnlyCollection<T> — try .Count
                        validationSnippets.Add($"if (this.{p.Name} is not null && this.{p.Name}.Count < {minLen}) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} count must be at least {minLen}.\");");
                    }
                }
                else if (attrName == "RangeAttribute" && attrNs is "System.ComponentModel.DataAnnotations")
                {
                    // Legacy DataAnnotations RangeAttribute — kept for backward compatibility
                    string minVal = "", maxVal = "", minStr = "", maxStr = "";
                    if (attr.ConstructorArguments.Length >= 3 || (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type))
                    {
                        minStr = attr.ConstructorArguments[1].Value?.ToString() ?? "0";
                        maxStr = attr.ConstructorArguments[2].Value?.ToString() ?? "0";
                        minVal = minStr;
                        maxVal = maxStr;

                        if (attr.ConstructorArguments[0].Value is ITypeSymbol ts)
                        {
                            if (ts.SpecialType == SpecialType.System_Double) { minVal += "d"; maxVal += "d"; }
                            else if (ts.SpecialType == SpecialType.System_Single) { minVal += "f"; maxVal += "f"; }
                            else if (ts.SpecialType == SpecialType.System_Decimal) { minVal += "m"; maxVal += "m"; }
                            else if (ts.Name == "TimeSpan")
                            {
                                minVal = $"global::System.TimeSpan.Parse(\"{minStr}\")";
                                maxVal = $"global::System.TimeSpan.Parse(\"{maxStr}\")";
                            }
                        }
                    }
                    else if (attr.ConstructorArguments.Length >= 2)
                    {
                        minStr = attr.ConstructorArguments[0].Value?.ToString() ?? "0";
                        maxStr = attr.ConstructorArguments[1].Value?.ToString() ?? "0";
                        minVal = minStr;
                        maxVal = maxStr;

                        if (attr.ConstructorArguments[0].Value is double) { minVal += "d"; maxVal += "d"; }
                        else if (attr.ConstructorArguments[0].Value is float) { minVal += "f"; maxVal += "f"; }
                    }

                    validationSnippets.Add($"if (this.{p.Name} < {minVal} || this.{p.Name} > {maxVal}) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} must be between {minStr} and {maxStr}.\");");
                }
                else if ((attrName == "RequiredAttribute" && attrNs is "System.ComponentModel.DataAnnotations") || (attrName == KnownNames.RequiredAttributeName && attrNs == KnownNames.ValidationNamespace))
                {
                    // Legacy DataAnnotations RequiredAttribute — kept for backward compatibility
                    if (!p.Type.IsValueType || p.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                    {
                        validationSnippets.Add($"if (this.{p.Name} == null) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} is required.\");");
                    }
                }
                else if (attrName == KnownNames.AllowedEnumAttributeName && attrNs == KnownNames.ValidationNamespace)
                {
                    // [Required] — Nalix AOT-safe required check
                    if (p.Type.TypeKind == TypeKind.Enum)
                    {
                        string enumType = p.Type.ToDisplayString();
                        validationSnippets.Add($"if (!global::System.Enum.IsDefined(typeof({enumType}), this.{p.Name})) throw new global::Nalix.Abstractions.Exceptions.ValidationException(\"{p.Name} is not a valid {enumType} value.\");");
                    }
                }
            }

            propModels.Add(new ConfigPropertyModel(propName, method, comment, isString, isEnum, validationSnippets.ToImmutable()));
        }

        return new ConfigClassModel(hintName, ns, name, sectionComment, nestingOpeners.ToImmutable(), propModels.ToImmutable());
    }

    private static bool IsBindableProperty(IPropertySymbol p)
    {
        if (p.GetMethod == null || p.SetMethod == null)
        {
            return false;
        }

        if (p.GetAttributes().Any(a => a.AttributeClass?.Name == KnownNames.ConfiguredIgnoreAttribute))
        {
            return false;
        }

        return true;
    }

    private static void Execute(ConfigClassModel model, SourceProductionContext context)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("// <auto-generated/>");
        _ = sb.AppendLine("#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member");
        _ = sb.AppendLine("#pragma warning disable CS8604 // Possible null reference argument.");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine($"using {KnownNames.ConfigurationBindingNamespace};");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"namespace {model.Namespace};");
        _ = sb.AppendLine();

        string indent = "";
        foreach (string opener in model.NestingOpeners)
        {
            _ = sb.AppendLine($"{indent}{opener}");
            _ = sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        _ = sb.AppendLine($"{indent}partial class {model.Name}");
        _ = sb.AppendLine($"{indent}{{");
        string i = indent + "    ";

        // ── CopyPropertiesTo ──
        _ = sb.AppendLine($"{i}protected override void CopyPropertiesTo(global::{KnownNames.ConfigurationBindingNamespace}.{KnownNames.ConfigurationLoader} other)");
        _ = sb.AppendLine($"{i}{{");
        _ = sb.AppendLine($"{i}    if (other is {model.Name} t)");
        _ = sb.AppendLine($"{i}    {{");
        foreach (ConfigPropertyModel p in model.Properties)
        {
            _ = sb.AppendLine($"{i}        t.{p.Name} = this.{p.Name};");
        }
        _ = sb.AppendLine($"{i}    }}");
        _ = sb.AppendLine($"{i}}}");
        _ = sb.AppendLine();

        // ── BindProperties ──
        _ = sb.AppendLine($"{i}protected override void BindProperties(global::{KnownNames.ConfigurationBindingNamespace}.IniConfig configFile, string section)");
        _ = sb.AppendLine($"{i}{{");

        if (!string.IsNullOrEmpty(model.SectionComment))
        {
            _ = sb.AppendLine($"{i}    configFile.WriteComment(section, null, \"{Escape(model.SectionComment)}\");");
        }

        foreach (ConfigPropertyModel p in model.Properties)
        {
            string propName = p.Name;
            string varName = $"_v_{propName}";
            string method = p.IniMethod;

            _ = sb.AppendLine($"{i}    try");
            _ = sb.AppendLine($"{i}    {{");

            if (method == "GetString")
            {
                _ = sb.AppendLine($"{i}        var {varName} = configFile.{method}(section, \"{propName}\");");
                _ = sb.AppendLine($"{i}        if (!string.IsNullOrEmpty({varName}))");
                _ = sb.AppendLine($"{i}            this.{propName} = {varName};");
                _ = sb.AppendLine($"{i}        else");
                _ = sb.AppendLine($"{i}        {{");
                EmitWriteDefault(sb, p, i + "            ");
                _ = sb.AppendLine($"{i}        }}");
            }
            else if (method.StartsWith("GetEnum"))
            {
                _ = sb.AppendLine($"{i}        var {varName} = configFile.{method}(section, \"{propName}\");");
                _ = sb.AppendLine($"{i}        if ({varName} != null)");
                _ = sb.AppendLine($"{i}            this.{propName} = {varName}.Value;");
                _ = sb.AppendLine($"{i}        else");
                _ = sb.AppendLine($"{i}        {{");
                EmitWriteDefault(sb, p, i + "            ");
                _ = sb.AppendLine($"{i}        }}");
            }
            else
            {
                _ = sb.AppendLine($"{i}        var {varName} = configFile.{method}(section, \"{propName}\");");
                _ = sb.AppendLine($"{i}        if ({varName}.HasValue)");
                _ = sb.AppendLine($"{i}            this.{propName} = {varName}.Value;");
                _ = sb.AppendLine($"{i}        else");
                _ = sb.AppendLine($"{i}        {{");
                EmitWriteDefault(sb, p, i + "            ");
                _ = sb.AppendLine($"{i}        }}");
            }

            _ = sb.AppendLine($"{i}    }}");
            _ = sb.AppendLine($"{i}    catch (Exception ex) when (global::{KnownNames.ExceptionsAbstractionsNamespace}.ExceptionClassifier.IsNonFatal(ex))");
            _ = sb.AppendLine($"{i}    {{");
            _ = sb.AppendLine($"{i}        System.Diagnostics.Trace.TraceError($\"Configuration binding failed for section={{section}} key={propName}: {{ex.Message}}\");");
            _ = sb.AppendLine($"{i}    }}");
        }

        _ = sb.AppendLine($"{i}}}");
        _ = sb.AppendLine();

        // ── ValidateDataAnnotations ──
        _ = sb.AppendLine($"{i}/// <summary>");
        _ = sb.AppendLine($"{i}/// Auto-generated method that validates all properties marked with validation attributes.");
        _ = sb.AppendLine($"{i}/// </summary>");
        _ = sb.AppendLine($"{i}protected override void ValidateDataAnnotations()");
        _ = sb.AppendLine($"{i}{{");
        foreach (ConfigPropertyModel p in model.Properties)
        {
            foreach (string snippet in p.ValidationSnippets)
            {
                _ = sb.AppendLine($"{i}    {snippet}");
            }
        }
        _ = sb.AppendLine($"{i}}}");

        _ = sb.AppendLine($"{indent}}}");

        for (int n = model.NestingOpeners.Length - 1; n >= 0; n--)
        {
            string closeIndent = new(' ', n * 4);
            _ = sb.AppendLine($"{closeIndent}}}");
        }

        context.AddSource($"{model.HintName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitWriteDefault(StringBuilder sb, ConfigPropertyModel p, string ind)
    {
        string propName = p.Name;
        if (p.Comment != null)
        {
            _ = sb.AppendLine($"{ind}configFile.WriteComment(section, \"{propName}\", \"{Escape(p.Comment)}\");");
        }

        string valueExpr = p.IsString ? $"this.{propName} ?? string.Empty" : $"this.{propName}.ToString()";
        _ = sb.AppendLine($"{ind}configFile.WriteValue(section, \"{propName}\", {valueExpr});");
    }

    private static string GetIniMethod(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return $"GetEnum<{type.ToDisplayString()}>";
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable &&
            nullable.TypeArguments[0].TypeKind == TypeKind.Enum)
        {
            return $"GetEnum<{nullable.TypeArguments[0].ToDisplayString()}>";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "GetBool",
            SpecialType.System_Char => "GetChar",
            SpecialType.System_SByte => "GetSByte",
            SpecialType.System_Byte => "GetByte",
            SpecialType.System_Int16 => "GetInt16",
            SpecialType.System_UInt16 => "GetUInt16",
            SpecialType.System_Int32 => "GetInt32",
            SpecialType.System_UInt32 => "GetUInt32",
            SpecialType.System_Int64 => "GetInt64",
            SpecialType.System_UInt64 => "GetUInt64",
            SpecialType.System_Single => "GetSingle",
            SpecialType.System_Double => "GetDouble",
            SpecialType.System_Decimal => "GetDecimal",
            SpecialType.System_DateTime => "GetDateTime",
            SpecialType.System_String => "GetString",
            SpecialType.None or _ => type.Name == "TimeSpan" ? "GetTimeSpan" : type.Name == "Guid" ? "GetGuid" : "GetString"
        };
    }

    private static string Escape(string? s) =>
        s?.Replace("\\", "\\\\")
          .Replace("\"", "\\\"")
          .Replace("\n", "\\n")
          .Replace("\r", "\\r") ?? string.Empty;
}
