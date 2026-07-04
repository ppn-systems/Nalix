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

namespace Nalix.Analyzers.Generators;

[Generator]
public sealed class RpcClientGenerator : IIncrementalGenerator
{
    public readonly struct RpcParameterModel : IEquatable<RpcParameterModel>
    {
        public string Type { get; }
        public string Name { get; }
        public string DefaultValue { get; }

        public RpcParameterModel(string type, string name, string defaultValue)
        {
            this.Type = type;
            this.Name = name;
            this.DefaultValue = defaultValue;
        }

        public bool Equals(RpcParameterModel other) => this.Type == other.Type && this.Name == other.Name && this.DefaultValue == other.DefaultValue;
        public override bool Equals(object obj) => obj is RpcParameterModel other && this.Equals(other);
        public override int GetHashCode() => (this.Type, this.Name, this.DefaultValue).GetHashCode();
    }

    public readonly struct RpcMethodModel : IEquatable<RpcMethodModel>
    {
        public string Name { get; }
        public string ReturnType { get; }
        public string GenericArgument { get; }
        public ImmutableArray<RpcParameterModel> Parameters { get; }

        public RpcMethodModel(string name, string returnType, string genericArgument, ImmutableArray<RpcParameterModel> parameters)
        {
            this.Name = name;
            this.ReturnType = returnType;
            this.GenericArgument = genericArgument;
            this.Parameters = parameters;
        }

        public bool Equals(RpcMethodModel other)
        {
            if (this.Name != other.Name || this.ReturnType != other.ReturnType || this.GenericArgument != other.GenericArgument || this.Parameters.Length != other.Parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (!this.Parameters[i].Equals(other.Parameters[i]))
                {
                    return false;
                }
            }

            return true;
        }
        public override bool Equals(object obj) => obj is RpcMethodModel other && this.Equals(other);
        public override int GetHashCode()
        {
            int hash = (this.Name, this.ReturnType, this.GenericArgument).GetHashCode();
            foreach (RpcParameterModel p in this.Parameters)
            {
                hash = (hash * 397) ^ p.GetHashCode();
            }

            return hash;
        }
    }

    public readonly struct RpcServiceModel : IEquatable<RpcServiceModel>
    {
        public string InterfaceName { get; }
        public string NamespaceName { get; }
        public ImmutableArray<RpcMethodModel> Methods { get; }

        public RpcServiceModel(string interfaceName, string namespaceName, ImmutableArray<RpcMethodModel> methods)
        {
            this.InterfaceName = interfaceName;
            this.NamespaceName = namespaceName;
            this.Methods = methods;
        }

        public bool Equals(RpcServiceModel other)
        {
            if (this.InterfaceName != other.InterfaceName || this.NamespaceName != other.NamespaceName || this.Methods.Length != other.Methods.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Methods.Length; i++)
            {
                if (!this.Methods[i].Equals(other.Methods[i]))
                {
                    return false;
                }
            }

            return true;
        }
        public override bool Equals(object obj) => obj is RpcServiceModel other && this.Equals(other);
        public override int GetHashCode()
        {
            int hash = (this.InterfaceName, this.NamespaceName).GetHashCode();
            foreach (RpcMethodModel m in this.Methods)
            {
                hash = (hash * 397) ^ m.GetHashCode();
            }

            return hash;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<RpcServiceModel> rpcServiceDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownNames.RpcServiceAttributeMetadataName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => GetRpcServiceModel(ctx))
            .Where(static m => m.InterfaceName != null); // filter invalid

        context.RegisterSourceOutput(rpcServiceDeclarations, static (spc, model) => Execute(spc, model));
    }

    private static RpcServiceModel GetRpcServiceModel(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return default;
        }

        string interfaceNamespace = symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString();
        string interfaceName = symbol.Name;

        ImmutableArray<RpcMethodModel>.Builder methods = ImmutableArray.CreateBuilder<RpcMethodModel>();

        foreach (ISymbol member in symbol.GetMembers())
        {
            if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary)
            {
                continue;
            }

            string returnType = method.ReturnType.ToDisplayString();
            string genericArgument = "";
            if (method.ReturnType is INamedTypeSymbol namedReturn && namedReturn.TypeArguments.Length > 0)
            {
                genericArgument = namedReturn.TypeArguments[0].ToDisplayString();
            }

            ImmutableArray<RpcParameterModel>.Builder parameters = ImmutableArray.CreateBuilder<RpcParameterModel>();
            foreach (IParameterSymbol param in method.Parameters)
            {
                string paramType = param.Type.ToDisplayString();
                string paramName = param.Name;
                string defaultVal = param.HasExplicitDefaultValue
                    ? (param.ExplicitDefaultValue == null ? "null" : param.ExplicitDefaultValue.ToString().ToLower())
                    : "";
                parameters.Add(new RpcParameterModel(paramType, paramName, defaultVal));
            }

            methods.Add(new RpcMethodModel(method.Name, returnType, genericArgument, parameters.ToImmutable()));
        }

        return new RpcServiceModel(interfaceName, interfaceNamespace, methods.ToImmutable());
    }

    private static void Execute(SourceProductionContext context, RpcServiceModel model)
    {
        string className = $"{model.InterfaceName}_RpcClient";
        string cleanInterfaceName = model.InterfaceName.StartsWith("I") ? model.InterfaceName.Substring(1) : model.InterfaceName;
        if (model.InterfaceName.StartsWith("I") && model.InterfaceName.Length > 1 && char.IsUpper(model.InterfaceName[1]))
        {
            className = $"{cleanInterfaceName}_RpcClient";
        }

        StringBuilder sb = new();
        _ = sb.AppendLine("// <auto-generated/>");
        _ = sb.AppendLine();
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine();
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine("using System.Threading;");
        _ = sb.AppendLine("using System.Threading.Tasks;");
        _ = sb.AppendLine("using Nalix.SDK.Transport;");
        _ = sb.AppendLine("using Nalix.SDK.Transport.Rpc;");
        _ = sb.AppendLine("using Nalix.SDK.Transport.Extensions;");
        _ = sb.AppendLine("using Nalix.SDK.Options;");
        _ = sb.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            _ = sb.AppendLine($"namespace {model.NamespaceName}");
            _ = sb.AppendLine("{");
        }

        _ = sb.AppendLine($"    /// <summary>");
        _ = sb.AppendLine($"    /// Auto-generated RPC client for <see cref=\"{model.InterfaceName}\"/>.");
        _ = sb.AppendLine($"    /// </summary>");
        _ = sb.AppendLine($"    internal sealed class {className} : {model.InterfaceName}");
        _ = sb.AppendLine($"    {{");
        _ = sb.AppendLine($"        private readonly TransportSession _session;");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"        public {className}(TransportSession session)");
        _ = sb.AppendLine($"        {{");
        _ = sb.AppendLine($"            _session = session;");
        _ = sb.AppendLine($"        }}");
        _ = sb.AppendLine();

        foreach (RpcMethodModel method in model.Methods)
        {
            GenerateMethod(sb, method);
        }

        _ = sb.AppendLine($"    }}");
        _ = sb.AppendLine();

        // Generate ModuleInitializer to register the factory
        _ = sb.AppendLine($"    internal static class {className}Initializer");
        _ = sb.AppendLine($"    {{");
        _ = sb.AppendLine($"        [System.Runtime.CompilerServices.ModuleInitializer]");
        _ = sb.AppendLine($"        public static void Initialize()");
        _ = sb.AppendLine($"        {{");
        _ = sb.AppendLine($"            Nalix.SDK.Transport.Extensions.RpcExtensions.RegisterFactory(typeof({model.InterfaceName}), session => new {className}(session));");
        _ = sb.AppendLine($"        }}");
        _ = sb.AppendLine($"    }}");

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            _ = sb.AppendLine("}");
        }

        context.AddSource($"{className}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateMethod(StringBuilder sb, RpcMethodModel method)
    {
        List<string> parameters = new();

        bool isValueTask = method.ReturnType == "System.Threading.Tasks.ValueTask" || method.ReturnType == "ValueTask";
        bool isRpcCall = method.ReturnType.StartsWith("Nalix.SDK.Transport.Rpc.RpcCall");
        bool isRpcStream = method.ReturnType.StartsWith("Nalix.SDK.Transport.Rpc.RpcStream");

        string? packetArg = null;
        string? encryptArg = null;
        string? ctArg = null;
        string? optionsArg = null;

        foreach (RpcParameterModel param in method.Parameters)
        {
            string paramDecl = $"{param.Type} {param.Name}";
            if (!string.IsNullOrEmpty(param.DefaultValue))
            {
                paramDecl += $" = {param.DefaultValue}";
            }

            parameters.Add(paramDecl);

            if (packetArg == null) // First parameter is always IPacket
            {
                packetArg = param.Name;
            }
            else if (param.Type == "bool?")
            {
                encryptArg = param.Name;
            }
            else if (param.Type == "System.Threading.CancellationToken")
            {
                ctArg = param.Name;
            }
            else if (param.Type == "Nalix.SDK.Options.RequestOptions?")
            {
                optionsArg = param.Name;
            }
        }

        string methodSignature = $"        public {method.ReturnType} {method.Name}({string.Join(", ", parameters)})";
        _ = sb.AppendLine(methodSignature);
        _ = sb.AppendLine($"        {{");

        if (isValueTask)
        {
            string encryptPass = encryptArg ?? "null";
            string ctPass = ctArg ?? "default";
            _ = sb.AppendLine($"            return new ValueTask(_session.SendAsync({packetArg}, encrypt: {encryptPass}, ct: {ctPass}));");
        }
        else if (isRpcCall || isRpcStream)
        {
            string genericArgs = string.IsNullOrEmpty(method.GenericArgument) ? "" : $"<{method.GenericArgument}>";
            string optPass = optionsArg ?? "null";
            string typeName = isRpcCall ? "RpcCall" : "RpcStream";
            _ = sb.AppendLine($"            return new {typeName}{genericArgs}(_session, {packetArg}, {optPass});");
        }

        _ = sb.AppendLine($"        }}");
        _ = sb.AppendLine();
    }
}
