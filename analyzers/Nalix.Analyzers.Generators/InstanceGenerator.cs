// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.



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
    #region Diagnostics

    private static readonly DiagnosticDescriptor s_noAccessibleCtorRule = new(
        id: "NALIX060",
        title: "No accessible constructor found",
        messageFormat: "Class '{0}' marked with [Injectable] must have at least one public or internal constructor",
        category: "Nalix.Framework.Injection",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_ambiguousCtorRule = new(
        id: "NALIX061",
        title: "Ambiguous constructors in injectable class",
        messageFormat: "Class '{0}' marked with [Injectable] has ambiguous constructors with the same parameter count that are not disjoint",
        category: "Nalix.Framework.Injection",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_singletonNoCtorRule = new(
        id: "NALIX062",
        title: "SingletonBase subclass missing accessible parameterless constructor",
        messageFormat: "SingletonBase subclass '{0}' must have a public or internal parameterless constructor for source-generated activation",
        category: "Nalix.Framework.Injection",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    #endregion Diagnostics

    #region IIncrementalGenerator Implementation

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ── [Injectable] types (existing) ──────────────────────────────────
        IncrementalValuesProvider<INamedTypeSymbol?> injectables = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GET_INJECTABLE_TYPE(ctx))
            .Where(static symbol => symbol is not null);

        // ── SingletonBase<T> subclasses (new) ─────────────────────────────
        IncrementalValuesProvider<INamedTypeSymbol?> singletons = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => GET_SINGLETON_TYPE(ctx))
            .Where(static symbol => symbol is not null);

        // Combine all three providers
        IncrementalValueProvider<(
            Compilation Compilation,
            ImmutableArray<INamedTypeSymbol?> Injectables,
            ImmutableArray<INamedTypeSymbol?> Singletons
        )> combined = context.CompilationProvider
            .Combine(injectables.Collect())
            .Combine(singletons.Collect())
            .Select(static (tuple, _) => (tuple.Left.Left, tuple.Left.Right, tuple.Right));

        context.RegisterSourceOutput(combined, static (spc, source)
            => Execute(source.Compilation, source.Injectables, source.Singletons, spc));
    }

    #endregion IIncrementalGenerator Implementation

    #region Private Helpers

    private static INamedTypeSymbol? GET_INJECTABLE_TYPE(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDecl)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface || symbol.IsGenericType)
        {
            return null;
        }

        bool hasInjectable = symbol.GetAttributes().Any(static attr =>
            attr.AttributeClass?.ToDisplayString() == KnownNames.InjectableAttributeMetadataName);

        return hasInjectable ? symbol : null;
    }

    /// <summary>
    /// Returns the symbol if it is a concrete (non-abstract, non-generic) class
    /// that inherits from <c>SingletonBase&lt;T&gt;</c> and is accessible from generated code
    /// (i.e. not a private nested type).
    /// </summary>
    private static INamedTypeSymbol? GET_SINGLETON_TYPE(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDecl)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (symbol.IsAbstract || symbol.IsGenericType)
        {
            return null;
        }

        // Skip private nested types — the generated code lives in a separate class
        // and cannot reference private nested types.
        if (symbol.DeclaredAccessibility == Accessibility.Private)
        {
            return null;
        }

        return IS_SINGLETON_BASE_SUBCLASS(symbol) ? symbol : null;
    }

    /// <summary>
    /// Walks the base-type chain looking for <c>SingletonBase&lt;T&gt;</c>.
    /// </summary>
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
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> injectableTargets,
        ImmutableArray<INamedTypeSymbol?> singletonTargets,
        SourceProductionContext context)
    {
        // ── Deduplicate [Injectable] targets ───────────────────────────────
        HashSet<string> seenInjectable = new();

        List<INamedTypeSymbol> distinctInjectables = [.. injectableTargets
            .Where(static p => p is not null)
            .Select(static p => p!)
            .Where(p => seenInjectable.Add(p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .OrderBy(static p => p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))];

        // ── Deduplicate SingletonBase<T> targets (exclude those already in injectable set) ──
        HashSet<string> seenSingleton = new();

        List<INamedTypeSymbol> distinctSingletons = [.. singletonTargets
            .Where(static p => p is not null)
            .Select(static p => p!)
            .Where(p => seenSingleton.Add(p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .Where(p => !seenInjectable.Contains(p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .OrderBy(static p => p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))];

        if (distinctInjectables.Count == 0 && distinctSingletons.Count == 0)
        {
            return;
        }

        // Use the first available symbol to determine the generated namespace
        INamedTypeSymbol anySymbol = distinctInjectables.Count > 0 ? distinctInjectables[0] : distinctSingletons[0];
        string generatedNamespace = SourceGenNamespaces.Get(anySymbol);

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

        // ── [Injectable] registrations (existing logic) ────────────────────
        foreach (INamedTypeSymbol symbol in distinctInjectables)
        {
            EMIT_INJECTABLE_REGISTRATION(sb, symbol, compilation, context);
        }

        // ── SingletonBase<T> factory registrations (new) ──────────────────
        foreach (INamedTypeSymbol symbol in distinctSingletons)
        {
            EMIT_SINGLETON_REGISTRATION(sb, symbol, context);
        }

        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

        context.AddSource("InstanceGenerated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Emits <c>InstanceManager.RegisterActivator(…)</c> and service-mapping lines
    /// for an <c>[Injectable]</c>-annotated type.
    /// </summary>
    private static void EMIT_INJECTABLE_REGISTRATION(
        StringBuilder sb,
        INamedTypeSymbol symbol,
        Compilation compilation,
        SourceProductionContext context)
    {
        string classFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Filter accessible constructors (public or internal)
        List<IMethodSymbol> ctors = [.. symbol.InstanceConstructors.Where(static c => c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)];

        if (ctors.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_noAccessibleCtorRule,
                symbol.Locations.FirstOrDefault() ?? Location.None,
                symbol.Name));
            return;
        }

        // Check ambiguous constructors with the same parameter count
        bool hasAmbiguity = false;
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
                            if (ARE_TYPES_DISJOINT(c1.Parameters[p].Type, c2.Parameters[p].Type, compilation))
                            {
                                isDisjoint = true;
                                break;
                            }
                        }
                        if (!isDisjoint)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                s_ambiguousCtorRule,
                                symbol.Locations.FirstOrDefault() ?? Location.None,
                                symbol.Name));
                            hasAmbiguity = true;
                            break;
                        }
                    }
                    if (hasAmbiguity)
                    {
                        break;
                    }
                }
            }
            if (hasAmbiguity)
            {
                break;
            }
        }

        if (hasAmbiguity)
        {
            return;
        }

        // Extract distinct mapped interfaces/service types from the Injectable attributes
        List<string> mappedServices = new();
        foreach (AttributeData attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == KnownNames.InjectableAttributeMetadataName)
            {
                if (attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is ITypeSymbol serviceType)
                {
                    string serviceFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    mappedServices.Add(serviceFullName);
                }
            }
        }

        // Register activator factory
        string activatorLambda = BUILD_ACTIVATOR_LAMBDA(classFullName, ctors);
        _ = sb.AppendLine($"        global::{KnownNames.InstanceManagerMetadataName}.RegisterActivator(");
        _ = sb.AppendLine($"            typeof({classFullName}),");
        _ = sb.AppendLine($"            {activatorLambda});");
        _ = sb.AppendLine();

        // Register service mappings
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
            .FirstOrDefault(static c =>
                c.Parameters.Length == 0 &&
                c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

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
                .FirstOrDefault(static c => c.Parameters.Length > 0 &&
                    c.Parameters.All(static p => !p.Type.IsValueType && INHERITS_FROM_CONFIGURATION_LOADER(p.Type)));

            if (allConfigCtor is not null)
            {
                StringBuilder ctorArgs = new();
                for (int p = 0; p < allConfigCtor.Parameters.Length; p++)
                {
                    if (p > 0)
                    {
                        _ = ctorArgs.Append(", ");
                    }

                    string paramTypeName = allConfigCtor.Parameters[p].Type
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    _ = ctorArgs.Append($"global::{KnownNames.ConfigurationManagerMetadataName}.Instance.Get<{paramTypeName}>()");
                }

                _ = sb.AppendLine($"        global::{KnownNames.SingletonActivatorCacheMetadataName}.Register(");
                _ = sb.AppendLine($"            typeof({classFullName}),");
                _ = sb.AppendLine($"            static () => new {classFullName}({ctorArgs}));");
                _ = sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Emits <c>SingletonActivatorCache.Register(…)</c> for a <c>SingletonBase&lt;T&gt;</c> subclass.
    /// Requires a parameterless constructor that is accessible from the generated code
    /// (i.e. <see langword="public"/> or <see langword="internal"/>).
    /// </summary>
    private static void EMIT_SINGLETON_REGISTRATION(
        StringBuilder sb,
        INamedTypeSymbol symbol,
        SourceProductionContext context)
    {
        string classFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // The generated code lives in InstanceGenerated (same assembly) so it can
        // access public and internal parameterless constructors.
        IMethodSymbol? parameterlessCtor = symbol.InstanceConstructors
            .FirstOrDefault(static c =>
                c.Parameters.Length == 0 &&
                c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

        if (parameterlessCtor is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_singletonNoCtorRule,
                symbol.Locations.FirstOrDefault() ?? Location.None,
                symbol.Name));
            return;
        }

        _ = sb.AppendLine($"        // SingletonBase<T> factory: {symbol.Name}");
        _ = sb.AppendLine($"        global::{KnownNames.SingletonActivatorCacheMetadataName}.Register(");
        _ = sb.AppendLine($"            typeof({classFullName}),");
        _ = sb.AppendLine($"            static () => new {classFullName}());");
        _ = sb.AppendLine();
    }

    private static string BUILD_ACTIVATOR_LAMBDA(string classFullName, List<IMethodSymbol> ctors)
    {
        // Pre-compute check/cast triples for every constructor parameter so that the
        // switch-case groups by *non-config* arity (args[] count) rather than total
        // parameter count.  ConfigurationLoader-derived parameters are resolved from
        // ConfigurationManager.Instance.Get<T>() and do not consume an args[] slot.
        Dictionary<IMethodSymbol, List<(string Check, string Cast, bool IsConfig)>> ctorParamInfo =
            new(SymbolEqualityComparer.Default);
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

    /// <summary>
    /// Emits a single <c>return new T(…);</c> line, correctly re-indexing non-config
    /// parameter slots.
    /// </summary>
    private static void EMIT_CTOR_RETURN(
        StringBuilder lambda,
        string classFullName,
        IMethodSymbol ctor,
        List<(string Check, string Cast, bool IsConfig)> infos)
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

    /// <summary>
    /// Emits a case block with a single constructor (no if-check needed).
    /// </summary>
    private static void EMIT_SINGLE_CTOR_BODY(
        StringBuilder lambda,
        string classFullName,
        IMethodSymbol ctor,
        List<(string Check, string Cast, bool IsConfig)> infos)
    {
        _ = lambda.Append("                        ");
        EMIT_CTOR_RETURN(lambda, classFullName, ctor, infos);
    }

    private static (string Check, string Cast, bool IsConfig) GET_PARAM_CHECK_AND_CAST(IParameterSymbol param, int idx)
    {
        ITypeSymbol type = param.Type;
        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // ── ConfigurationLoader-derived: resolve from ConfigurationManager ──
        if (!type.IsValueType && INHERITS_FROM_CONFIGURATION_LOADER(type))
        {
            string configExpr = $"global::{KnownNames.ConfigurationManagerMetadataName}.Instance.Get<{typeName}>()";
            return ("true", configExpr, true);
        }

        // ── Original args[index] logic ──────────────────────────────────────
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

    /// <summary>
    /// Walks the base-type chain of <paramref name="type"/> looking for
    /// <c>Nalix.Environment.Configuration.Binding.ConfigurationLoader</c>,
    /// using metadata-name comparison so it works across assemblies.
    /// </summary>
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

    #endregion Private Helpers
}
