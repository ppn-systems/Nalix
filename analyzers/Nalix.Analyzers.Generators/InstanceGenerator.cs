// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nalix.Analyzers.Generators.Internal;

namespace Nalix.Analyzers.Generators;

/// <summary>
/// Incremental Roslyn Source Generator for producing compile-time activation factories
/// and service mapping registrations for classes annotated with [Injectable].
/// </summary>
[Generator]
public sealed class InstanceGenerator : IIncrementalGenerator
{
    #region Diagnostics (centralized in GeneratorDiagnosticDescriptors)

    // NALIX063 — NoAccessibleConstructor
    // NALIX064 — AmbiguousConstructor
    // NALIX065 — SingletonMissingParameterlessConstructor

    #endregion Diagnostics (centralized in GeneratorDiagnosticDescriptors)

    public readonly struct RegistrationResultModel : IEquatable<RegistrationResultModel>
    {
        public string? FullyQualifiedName { get; }
        public string? SourceCodeSnippet { get; }
        public string? GeneratedNamespace { get; }
        public Diagnostic? Diagnostic { get; }

        public RegistrationResultModel(string? fullyQualifiedName, string? sourceCodeSnippet, string? generatedNamespace, Diagnostic? diagnostic)
        {
            this.FullyQualifiedName = fullyQualifiedName;
            this.SourceCodeSnippet = sourceCodeSnippet;
            this.GeneratedNamespace = generatedNamespace;
            this.Diagnostic = diagnostic;
        }

        public bool Equals(RegistrationResultModel other)
        {
            if (this.FullyQualifiedName != other.FullyQualifiedName ||
                this.SourceCodeSnippet != other.SourceCodeSnippet ||
                this.GeneratedNamespace != other.GeneratedNamespace)
            {
                return false;
            }

            if (this.Diagnostic == null && other.Diagnostic == null)
            {
                return true;
            }

            if (this.Diagnostic != null && other.Diagnostic != null)
            {
                return this.Diagnostic.Equals(other.Diagnostic);
            }

            return false;
        }

        public override bool Equals(object obj) => obj is RegistrationResultModel other && this.Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + (this.FullyQualifiedName?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.SourceCodeSnippet?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.GeneratedNamespace?.GetHashCode() ?? 0);
                hash = (hash * 23) + (this.Diagnostic?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<RegistrationResultModel> injectables = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownNames.InjectableAttributeMetadataName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => GET_INJECTABLE_MODEL(ctx))
            .Where(static m => m.FullyQualifiedName != null);

        IncrementalValuesProvider<RegistrationResultModel> singletons = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => GET_SINGLETON_MODEL(ctx))
            .Where(static m => m.FullyQualifiedName != null);

        IncrementalValueProvider<(ImmutableArray<RegistrationResultModel> Injectables, ImmutableArray<RegistrationResultModel> Singletons)> combined =
            injectables.Collect().Combine(singletons.Collect());

        context.RegisterSourceOutput(combined, static (spc, source)
            => Execute(source.Injectables, source.Singletons, spc));
    }

    private static RegistrationResultModel GET_INJECTABLE_MODEL(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return default;
        }

        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface || symbol.IsGenericType)
        {
            return default;
        }

        string classFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string generatedNamespace = SourceGenNamespaces.Get(symbol);

        List<IMethodSymbol> ctors = [.. symbol.InstanceConstructors.Where(static c => c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)];

        if (ctors.Count == 0)
        {
            return new RegistrationResultModel(
                classFullName, null, generatedNamespace,
                Diagnostic.Create(GeneratorDiagnosticDescriptors.NoAccessibleConstructor, symbol.Locations.FirstOrDefault() ?? Location.None, symbol.Name));
        }
        IEnumerable<IGrouping<int, IMethodSymbol>> ctorsByArity = ctors.GroupBy(static c => c.Parameters.Length);
        foreach (IGrouping<int, IMethodSymbol> group in ctorsByArity)
        {
            List<IMethodSymbol> groupCtors = [.. group];
            if (groupCtors.Count > 1)
            {
                for (int i = 0; i < groupCtors.Count; i++)
                {
                    for (int j = i + 1; j < groupCtors.Count; j++)
                    {
                        IMethodSymbol c1 = groupCtors[i];
                        IMethodSymbol c2 = groupCtors[j];
                        bool isDisjoint = false;
                        for (int p = 0; p < c1.Parameters.Length; p++)
                        {
                            if (ARE_TYPES_DISJOINT(c1.Parameters[p].Type, c2.Parameters[p].Type, context.SemanticModel.Compilation))
                            {
                                isDisjoint = true;
                                break;
                            }
                        }
                        if (!isDisjoint)
                        {
                            return new RegistrationResultModel(
                                classFullName, null, generatedNamespace,
                                Diagnostic.Create(GeneratorDiagnosticDescriptors.AmbiguousConstructor, symbol.Locations.FirstOrDefault() ?? Location.None, symbol.Name));
                        }
                    }
                }
            }
        }

        List<string> mappedServices = new();
        foreach (AttributeData attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == KnownNames.InjectableAttributeMetadataName)
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol serviceType)
                {
                    string serviceFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    mappedServices.Add(serviceFullName);
                }
            }
        }

        StringBuilder sb = new();
        string activatorLambda = BUILD_ACTIVATOR_LAMBDA(classFullName, ctors);
        _ = sb.AppendLine($"        global::{KnownNames.InstanceManagerMetadataName}.RegisterActivator(");
        _ = sb.AppendLine($"            typeof({classFullName}),");
        _ = sb.AppendLine($"            {activatorLambda});");
        _ = sb.AppendLine();

        foreach (string serviceName in mappedServices.Distinct())
        {
            _ = sb.AppendLine($"        global::{KnownNames.InstanceManagerMetadataName}.RegisterServiceMapping(");
            _ = sb.AppendLine($"            typeof({classFullName}),");
            _ = sb.AppendLine($"            typeof({serviceName}));");
            _ = sb.AppendLine();
        }

        // Also register a factory in SingletonActivatorCache so that
        // Singleton.Register<TInterface, TImplementation>() can resolve the implementation
        // without reflection (Activator.CreateInstance).
        //
        // Emitted when:
        //  - a public/internal parameterless constructor exists, OR
        //  - a public/internal constructor exists whose parameters are ALL derived from
        //    ConfigurationLoader (resolvable via ConfigurationManager.Instance.Get<T>()).
        IMethodSymbol? parameterlessCtor = symbol.InstanceConstructors
            .FirstOrDefault(static c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

        if (parameterlessCtor is not null)
        {
            _ = sb.AppendLine($"        global::{KnownNames.SingletonActivatorCacheMetadataName}.Register(");
            _ = sb.AppendLine($"            typeof({classFullName}),");
            _ = sb.AppendLine($"            static () => new {classFullName}());");
            _ = sb.AppendLine();
        }
        else
        {
            // No parameterless ctor — look for a ctor whose params are all ConfigurationLoader-derived.
            IMethodSymbol? allConfigCtor = symbol.InstanceConstructors
                .Where(static c => c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
                .FirstOrDefault(static c => c.Parameters.Length > 0 && c.Parameters.All(static p => !p.Type.IsValueType && INHERITS_FROM_CONFIGURATION_LOADER(p.Type)));

            if (allConfigCtor is not null)
            {
                StringBuilder ctorArgs = new();
                for (int p = 0; p < allConfigCtor.Parameters.Length; p++)
                {
                    if (p > 0)
                    {
                        _ = ctorArgs.Append(", ");
                    }

                    string paramTypeName = allConfigCtor.Parameters[p].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    _ = ctorArgs.Append($"global::{KnownNames.ConfigurationManagerMetadataName}.Instance.Get<{paramTypeName}>()");
                }

                _ = sb.AppendLine($"        global::{KnownNames.SingletonActivatorCacheMetadataName}.Register(");
                _ = sb.AppendLine($"            typeof({classFullName}),");
                _ = sb.AppendLine($"            static () => new {classFullName}({ctorArgs}));");
                _ = sb.AppendLine();
            }
        }

        return new RegistrationResultModel(classFullName, sb.ToString(), generatedNamespace, null);
    }

    private static RegistrationResultModel GET_SINGLETON_MODEL(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDecl)
        {
            return default;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
        {
            return default;
        }

        if (symbol.IsAbstract || symbol.IsGenericType || symbol.DeclaredAccessibility == Accessibility.Private)
        {
            return default;
        }

        if (!IS_SINGLETON_BASE_SUBCLASS(symbol))
        {
            return default;
        }

        string classFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string generatedNamespace = SourceGenNamespaces.Get(symbol);

        // The generated code lives in InstanceGenerated (same assembly) so it can
        // access public and internal parameterless constructors.
        IMethodSymbol? parameterlessCtor = symbol.InstanceConstructors
            .FirstOrDefault(static c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

        if (parameterlessCtor is null)
        {
            return new RegistrationResultModel(
                classFullName, null, generatedNamespace,
                Diagnostic.Create(GeneratorDiagnosticDescriptors.SingletonMissingParameterlessConstructor, symbol.Locations.FirstOrDefault() ?? Location.None, symbol.Name));
        }

        StringBuilder sb = new();
        _ = sb.AppendLine($"        // SingletonBase<T> factory: {symbol.Name}");
        _ = sb.AppendLine($"        global::{KnownNames.SingletonActivatorCacheMetadataName}.Register(");
        _ = sb.AppendLine($"            typeof({classFullName}),");
        _ = sb.AppendLine($"            static () => new {classFullName}());");
        _ = sb.AppendLine();

        return new RegistrationResultModel(classFullName, sb.ToString(), generatedNamespace, null);
    }

    private static bool IS_SINGLETON_BASE_SUBCLASS(INamedTypeSymbol symbol)
    {
        INamedTypeSymbol? current = symbol.BaseType;
        while (current is not null)
        {
            if (current.OriginalDefinition.ToDisplayString() == KnownNames.SingletonBaseMetadataName)
            {
                return true;
            }

            current = current.BaseType;
        }
        return false;
    }

    private static void Execute(
        ImmutableArray<RegistrationResultModel> injectableTargets,
        ImmutableArray<RegistrationResultModel> singletonTargets,
        SourceProductionContext context)
    {
        foreach (RegistrationResultModel m in injectableTargets)
        {
            if (m.Diagnostic != null)
            {
                context.ReportDiagnostic(m.Diagnostic);
            }
        }

        foreach (RegistrationResultModel m in singletonTargets)
        {
            if (m.Diagnostic != null)
            {
                context.ReportDiagnostic(m.Diagnostic);
            }
        }

        HashSet<string> seenInjectable = new();
        List<RegistrationResultModel> distinctInjectables = [.. injectableTargets
            .Where(m => m.SourceCodeSnippet != null && m.FullyQualifiedName != null && seenInjectable.Add(m.FullyQualifiedName))
            .OrderBy(m => m.FullyQualifiedName)];

        HashSet<string> seenSingleton = new();
        List<RegistrationResultModel> distinctSingletons = [.. singletonTargets
            .Where(m => m.SourceCodeSnippet != null && m.FullyQualifiedName != null && seenSingleton.Add(m.FullyQualifiedName) && !seenInjectable.Contains(m.FullyQualifiedName))
            .OrderBy(m => m.FullyQualifiedName)];

        if (distinctInjectables.Count == 0 && distinctSingletons.Count == 0)
        {
            return;
        }

        string generatedNamespace = distinctInjectables.Count > 0 ? distinctInjectables[0].GeneratedNamespace! : distinctSingletons[0].GeneratedNamespace!;

        StringBuilder sb = new();
        _ = sb.AppendLine("// <auto-generated/>");
        _ = sb.AppendLine("// Copyright (c) 2026 PPN Corporation. All rights reserved.");
        _ = sb.AppendLine("// Licensed under the Apache License, Version 2.0.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("// DO NOT EDIT MANUALLY.");
        _ = sb.AppendLine("// Design goals:");
        _ = sb.AppendLine("// - 100% Native AOT safety");
        _ = sb.AppendLine("// - Zero dynamic reflection or JIT-emit");
        _ = sb.AppendLine("// - Fast compile-time registered factories");
        _ = sb.AppendLine();
        _ = sb.AppendLine("#pragma warning disable CS1591");
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine("using System.Runtime.CompilerServices;");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"namespace {generatedNamespace};");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"internal static class InstanceGenerated");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine("    [ModuleInitializer]");
        _ = sb.AppendLine("    internal static void Initialize()");
        _ = sb.AppendLine("    {");

        foreach (RegistrationResultModel model in distinctInjectables)
        {
            _ = sb.Append(model.SourceCodeSnippet);
        }

        foreach (RegistrationResultModel model in distinctSingletons)
        {
            _ = sb.Append(model.SourceCodeSnippet);
        }

        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

        context.AddSource("InstanceGenerated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string BUILD_ACTIVATOR_LAMBDA(string classFullName, List<IMethodSymbol> ctors)
    {
        // Pre-compute check/cast triples for every constructor parameter so that the
        // switch-case groups by *non-config* arity (args[] count) rather than total
        // parameter count.  ConfigurationLoader-derived parameters are resolved from
        // ConfigurationManager.Instance.Get<T>() and do not consume an args[] slot.
        Dictionary<IMethodSymbol, List<(string Check, string Cast, bool IsConfig)>> ctorParamInfo = new(SymbolEqualityComparer.Default);
        foreach (IMethodSymbol ctor in ctors)
        {
            List<(string Check, string Cast, bool IsConfig)> paramInfo = new(ctor.Parameters.Length);
            for (int p = 0; p < ctor.Parameters.Length; p++)
            {
                paramInfo.Add(GET_PARAM_CHECK_AND_CAST(ctor.Parameters[p], p));
            }
            ctorParamInfo[ctor] = paramInfo;
        }

        // Separate the true parameterless constructor (0 total params) so it can be
        // emitted as the default: catch-all.  This avoids a collision with config-only
        // constructors whose non-config arity is also 0 (they would both occupy case 0:
        // and the first if(true) would make the rest unreachable).
        IMethodSymbol? parameterlessCtor = ctors.FirstOrDefault(c => c.Parameters.Length == 0);
        List<IMethodSymbol> remainingCtors = parameterlessCtor is not null
            ? [.. ctors.Where(c => !SymbolEqualityComparer.Default.Equals(c, parameterlessCtor))]
            : ctors;

        // Group the remaining constructors by the number of NON-config parameters.
        IEnumerable<IGrouping<int, IMethodSymbol>> ctorsByNonConfigArity = remainingCtors
            .GroupBy(c => ctorParamInfo[c].Count(t => !t.IsConfig))
            .OrderBy(static g => g.Key);

        StringBuilder lambda = new();
        _ = lambda.Append("static args => {\n");
        _ = lambda.Append("                switch (args.Length)\n");
        _ = lambda.Append("                {\n");

        foreach (IGrouping<int, IMethodSymbol> group in ctorsByNonConfigArity)
        {
            int nonConfigArity = group.Key;
            _ = lambda.Append($"                    case {nonConfigArity}:\n");
            List<IMethodSymbol> groupCtors = [.. group];

            if (groupCtors.Count == 1)
            {
                EMIT_SINGLE_CTOR_BODY(lambda, classFullName, groupCtors[0], ctorParamInfo[groupCtors[0]]);
            }
            else
            {
                for (int i = 0; i < groupCtors.Count; i++)
                {
                    IMethodSymbol ctor = groupCtors[i];
                    List<(string Check, string Cast, bool IsConfig)> infos = ctorParamInfo[ctor];

                    _ = lambda.Append("                        if (");
                    int argsIdx = 0;
                    bool firstCheck = true;
                    for (int p = 0; p < infos.Count; p++)
                    {
                        (string check, _, bool isConfig) = infos[p];
                        if (isConfig)
                        {
                            continue; // config params always resolve — skip in type check
                        }

                        if (!firstCheck)
                        {
                            _ = lambda.Append(" && ");
                        }
                        // Re-map args index for the check too
                        (string reindexedCheck, _, _) = GET_PARAM_CHECK_AND_CAST(ctor.Parameters[p], argsIdx);
                        _ = lambda.Append(reindexedCheck);
                        firstCheck = false;
                        argsIdx++;
                    }
                    if (firstCheck)
                    {
                        _ = lambda.Append("true"); // all config — no runtime check
                    }

                    _ = lambda.Append(")\n");
                    _ = lambda.Append("                        {\n");
                    _ = lambda.Append("                            ");
                    EMIT_CTOR_RETURN(lambda, classFullName, ctor, infos);
                    _ = lambda.Append("                        }\n");
                }
                _ = lambda.Append($"                        throw new global::System.InvalidOperationException(\"No constructor of {classFullName} with {nonConfigArity} non-config parameters matched the argument types.\");\n");
            }
        }

        // default: — if a parameterless constructor exists, use it as the catch-all;
        // otherwise throw.
        _ = lambda.Append("                    default:\n");
        if (parameterlessCtor is not null)
        {
            _ = lambda.Append("                        return new ").Append(classFullName).Append("();\n");
        }
        else
        {
            _ = lambda.Append($"                        throw new global::System.InvalidOperationException(\"No constructor of {classFullName} matches argument count \" + args.Length);\n");
        }
        _ = lambda.Append("                }\n");
        _ = lambda.Append("            }");

        return lambda.ToString();
    }

    private static void EMIT_CTOR_RETURN(StringBuilder lambda, string classFullName, IMethodSymbol ctor, List<(string Check, string Cast, bool IsConfig)> infos)
    {
        _ = lambda.Append("return new ").Append(classFullName).Append("(");
        int argsIdx = 0;
        for (int p = 0; p < infos.Count; p++)
        {
            if (p > 0)
            {
                _ = lambda.Append(", ");
            }

            (_, string cast, bool isConfig) = infos[p];
            if (isConfig)
            {
                _ = lambda.Append(cast);
            }
            else
            {
                (string _, string reindexedCast, _) = GET_PARAM_CHECK_AND_CAST(ctor.Parameters[p], argsIdx);
                _ = lambda.Append(reindexedCast);
                argsIdx++;
            }
        }
        _ = lambda.Append(");\n");
    }

    private static void EMIT_SINGLE_CTOR_BODY(StringBuilder lambda, string classFullName, IMethodSymbol ctor, List<(string Check, string Cast, bool IsConfig)> infos)
    {
        _ = lambda.Append("                        ");
        EMIT_CTOR_RETURN(lambda, classFullName, ctor, infos);
    }

    private static (string Check, string Cast, bool IsConfig) GET_PARAM_CHECK_AND_CAST(IParameterSymbol param, int idx)
    {
        ITypeSymbol type = param.Type;
        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // ConfigurationLoader-derived: resolve from ConfigurationManager
        if (!type.IsValueType && INHERITS_FROM_CONFIGURATION_LOADER(type))
        {
            string configExpr = $"global::{KnownNames.ConfigurationManagerMetadataName}.Instance.Get<{typeName}>()";
            return ("true", configExpr, true);
        }

        // Original args[index] logic
        if (type.SpecialType == SpecialType.System_Object)
        {
            return ("true", $"args[{idx}]", false);
        }

        if (type.IsValueType)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                ITypeSymbol underlying = named.TypeArguments[0];
                string underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return ($"(args[{idx}] is {underlyingName} || args[{idx}] is null)", $"({typeName})args[{idx}]", false);
            }
            else
            {
                return ($"args[{idx}] is {typeName}", $"({typeName})args[{idx}]!", false);
            }
        }
        else
        {
            return ($"(args[{idx}] is {typeName} || args[{idx}] is null)", $"({typeName})args[{idx}]!", false);
        }
    }

    private static bool ARE_TYPES_DISJOINT(ITypeSymbol t1, ITypeSymbol t2, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(t1, t2))
        {
            return false;
        }

        if (t1.SpecialType == SpecialType.System_Object || t2.SpecialType == SpecialType.System_Object)
        {
            return false;
        }

        CSharpCompilation csharpCompilation = (CSharpCompilation)compilation;
        Conversion conversion1 = csharpCompilation.ClassifyConversion(t1, t2);
        if (conversion1.Exists && (conversion1.IsImplicit || conversion1.IsIdentity))
        {
            return false;
        }

        Conversion conversion2 = csharpCompilation.ClassifyConversion(t2, t1);
        if (conversion2.Exists && (conversion2.IsImplicit || conversion2.IsIdentity))
        {
            return false;
        }

        if (t1.IsValueType && t2.IsValueType)
        {
            return true;
        }

        if (t1.IsValueType && t2.TypeKind == TypeKind.Class && t2.IsSealed)
        {
            return true;
        }

        if (t2.IsValueType && t1.TypeKind == TypeKind.Class && t1.IsSealed)
        {
            return true;
        }

        if (t1.TypeKind == TypeKind.Class && t2.TypeKind == TypeKind.Class && (t1.IsSealed || t2.IsSealed))
        {
            if (INHERITS_FROM(t1, t2) || INHERITS_FROM(t2, t1))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool INHERITS_FROM(ITypeSymbol derived, ITypeSymbol baseType)
    {
        INamedTypeSymbol? current = derived.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }
        return false;
    }

    private static bool INHERITS_FROM_CONFIGURATION_LOADER(ITypeSymbol type)
    {
        INamedTypeSymbol? current = type.BaseType;
        while (current is not null)
        {
            if (current.OriginalDefinition.ToDisplayString() == KnownNames.ConfigurationLoaderMetadataName)
            {
                return true;
            }

            current = current.BaseType;
        }
        return false;
    }
}
