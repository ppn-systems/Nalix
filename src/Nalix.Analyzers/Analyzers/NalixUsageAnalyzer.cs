// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Nalix.Analyzers.Diagnostics;

namespace Nalix.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NalixUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            DiagnosticDescriptors.DuplicateControllerOpcode,
            DiagnosticDescriptors.ControllerMethodRequiresOpcode,
            DiagnosticDescriptors.InvalidControllerHandlerSignature,
            DiagnosticDescriptors.PacketContextTypeMismatch,
            DiagnosticDescriptors.HandlerPacketTypeMismatch,
            DiagnosticDescriptors.MiddlewareTypeMismatch,
            DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute,
            DiagnosticDescriptors.ControllerMissingPacketControllerAttribute,
            DiagnosticDescriptors.PacketRegistryPacketMissingDeserializer,
            DiagnosticDescriptors.PacketBaseSelfTypeMismatch,
            DiagnosticDescriptors.PacketDeserializerSelfTypeMismatch,
            DiagnosticDescriptors.PacketBaseMissingDeserializeMethod,
            DiagnosticDescriptors.ExplicitSerializationMemberMissingOrder,
            DiagnosticDescriptors.DuplicateSerializeOrder,
            DiagnosticDescriptors.SerializeIgnoreConflictsWithOrder,
            DiagnosticDescriptors.SerializeDynamicSizeOnFixedMember,
            DiagnosticDescriptors.PacketDeserializeSignatureInvalid,
            DiagnosticDescriptors.PacketRegistryPacketMustBeConcrete,
            DiagnosticDescriptors.BufferMiddlewareRegistrationTypeMismatch,
            DiagnosticDescriptors.ResetForPoolShouldCallBase,
            DiagnosticDescriptors.NegativeSerializeOrder,
            DiagnosticDescriptors.PacketMemberOverlapsHeaderRegion,
            DiagnosticDescriptors.UnsupportedConfigurationPropertyType,
            DiagnosticDescriptors.ConfigurationPropertyNotBindable,
            DiagnosticDescriptors.MetadataProviderClearsOpcode,
            DiagnosticDescriptors.MetadataProviderOverwritesOpcodeWithoutGuard,
            DiagnosticDescriptors.RequestOptionsRetryCountNegative,
            DiagnosticDescriptors.RequestOptionsTimeoutNegative,
            DiagnosticDescriptors.RequestEncryptRequiresTcpSession,
            DiagnosticDescriptors.PacketMiddlewareMissingOrder,
            DiagnosticDescriptors.BufferMiddlewareMissingOrder,
            DiagnosticDescriptors.InboundMiddlewareAlwaysExecuteIgnored,
            DiagnosticDescriptors.MiddlewareRegistrationDuplicateOrder,
            DiagnosticDescriptors.SerializeHeaderConflictsWithOrder,
            DiagnosticDescriptors.ReservedOpCodeRange,
            DiagnosticDescriptors.GlobalDuplicateOpcode,
            DiagnosticDescriptors.AllocationInHotPath,
            DiagnosticDescriptors.OpCodeDocMismatch,
            DiagnosticDescriptors.PotentialBufferLeaseLeak
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            SymbolSet? symbols = SymbolSet.Create(startContext.Compilation);
            if (symbols is null)
            {
                return;
            }

            ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes = new();

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, symbols, globalOpcodes),
                SymbolKind.NamedType);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, symbols),
                SymbolKind.Method);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, symbols),
                OperationKind.ObjectCreation);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, symbols),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeMethodDeclaration(syntaxContext, symbols),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.MethodDeclaration);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, SymbolSet symbols, ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes)
    {
        INamedTypeSymbol typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct) || typeSymbol.IsAbstract || typeSymbol.IsImplicitlyDeclared)
        {
            return;
        }

        if (HasAttribute(typeSymbol, symbols.ControllerAttribute))
        {
            AnalyzeControllerType(context, typeSymbol, symbols, globalOpcodes);
        }

        AnalyzePacketType(context, typeSymbol, symbols);
        AnalyzeSerializationType(context, typeSymbol, symbols);
        AnalyzeConfigurationType(context, typeSymbol, symbols);
        AnalyzeMetadataProviderType(context, typeSymbol, symbols);
        AnalyzeMiddlewareType(context, typeSymbol, symbols);

        if (Implements(typeSymbol, symbols.NetworkBufferMiddlewareType) && HasAttribute(typeSymbol, symbols.MiddlewareStageAttribute))
        {
            Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
            if (location is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute,
                    location,
                    typeSymbol.Name));
            }
        }
    }

    private static void AnalyzeConfigurationType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!HasBaseType(typeSymbol, symbols.ConfigurationLoaderType))
        {
            return;
        }

        foreach (IPropertySymbol property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsImplicitlyDeclared
                || property.DeclaredAccessibility != Accessibility.Public
                || property.IsStatic
                || HasAttribute(property, symbols.ConfiguredIgnoreAttribute))
            {
                continue;
            }

            if (property.SetMethod is null)
            {
                Report(
                    context,
                    DiagnosticDescriptors.ConfigurationPropertyNotBindable,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    "does not declare a setter");
                continue;
            }

            if (property.SetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                Report(
                    context,
                    DiagnosticDescriptors.ConfigurationPropertyNotBindable,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    "does not have a public setter");
                continue;
            }

            if (!IsSupportedConfigurationType(property.Type))
            {
                Report(
                    context,
                    DiagnosticDescriptors.UnsupportedConfigurationPropertyType,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }
    }

    private static void AnalyzeMetadataProviderType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!Implements(typeSymbol, symbols.PacketMetadataProviderType))
        {
            return;
        }

        foreach (IMethodSymbol method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.Name != "Populate"
                || method.MethodKind != MethodKind.Ordinary
                || method.Parameters.Length != 2
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, symbols.MethodInfoType)
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, symbols.PacketMetadataBuilderType))
            {
                continue;
            }

            foreach (SyntaxReference syntaxReference in method.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodSyntax)
                {
                    continue;
                }

                AnalyzePopulateMethod(context, typeSymbol, method, methodSyntax, symbols);
            }
        }
    }

    private static void AnalyzeMiddlewareType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        bool isPacketMiddleware = ImplementsOpenGeneric(typeSymbol, symbols.PacketMiddlewareType);
        bool isBufferMiddleware = Implements(typeSymbol, symbols.NetworkBufferMiddlewareType);

        if (!isPacketMiddleware && !isBufferMiddleware)
        {
            return;
        }

        AttributeData? orderAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareOrderAttribute));
        AttributeData? stageAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareStageAttribute));

        if (isPacketMiddleware && orderAttribute is null)
        {
            Report(context, DiagnosticDescriptors.PacketMiddlewareMissingOrder, typeSymbol, typeSymbol.Name);
        }

        if (isBufferMiddleware)
        {
            if (orderAttribute is null)
            {
                Report(context, DiagnosticDescriptors.BufferMiddlewareMissingOrder, typeSymbol, typeSymbol.Name);
            }

            if (stageAttribute is not null)
            {
                return;
            }
        }

        if (stageAttribute is not null && IsInboundAlwaysExecute(stageAttribute, symbols.MiddlewareStageType))
        {
            Report(context, DiagnosticDescriptors.InboundMiddlewareAlwaysExecuteIgnored, typeSymbol, typeSymbol.Name);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private static void AnalyzePopulateMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IMethodSymbol methodSymbol,
        MethodDeclarationSyntax methodSyntax,
        SymbolSet symbols)
    {
        SyntaxNode? bodyRoot = (SyntaxNode?)methodSyntax.Body ?? methodSyntax.ExpressionBody?.Expression;
        if (bodyRoot is null)
        {
            return;
        }

        string builderParameterName = methodSymbol.Parameters[1].Name;
        bool hasOpcodeGuard = bodyRoot.DescendantNodesAndSelf().Any(node => IsOpcodeGuard(node, builderParameterName));
        bool reportedOverwrite = false;

        foreach (AssignmentExpressionSyntax assignment in bodyRoot.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
        {
            if (!IsBuilderOpcodeAccess(assignment.Left, builderParameterName))
            {
                continue;
            }

            if (assignment.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MetadataProviderClearsOpcode,
                    assignment.GetLocation(),
                    typeSymbol.Name));
                continue;
            }

            if (!hasOpcodeGuard && !reportedOverwrite)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MetadataProviderOverwritesOpcodeWithoutGuard,
                    assignment.GetLocation(),
                    typeSymbol.Name));
                reportedOverwrite = true;
            }
        }
    }

    private static void AnalyzeSerializationType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        AttributeData? serializePackable = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SerializePackableAttribute));

        if (serializePackable is null)
        {
            return;
        }

        bool isExplicitLayout = IsExplicitSerializeLayout(serializePackable, symbols.SerializeLayoutType);
        List<(ISymbol Member, int Order)> orderedMembers = [];

        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol)
            {
                continue;
            }

            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            bool hasIgnore = HasAttribute(member, symbols.SerializeIgnoreAttribute);
            int? headerOrder = GetSerializeOrder(member, symbols.SerializeHeaderAttribute);
            int? order = GetSerializeOrder(member, symbols.SerializeOrderAttribute);
            bool hasDynamic = HasAttribute(member, symbols.SerializeDynamicSizeAttribute);
            ITypeSymbol memberType = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;

            if (hasIgnore && order.HasValue)
            {
                Report(context, DiagnosticDescriptors.SerializeIgnoreConflictsWithOrder, member, member.Name);
            }

            if (headerOrder.HasValue && order.HasValue)
            {
                Report(
                    context,
                    DiagnosticDescriptors.SerializeHeaderConflictsWithOrder,
                    member,
                    member.Name);
            }

            int? finalOrder = headerOrder ?? order;

            if (finalOrder.HasValue)
            {
                orderedMembers.Add((member, finalOrder.Value));

                if (finalOrder.Value < 0)
                {
                    Report(context, DiagnosticDescriptors.NegativeSerializeOrder, member, member.Name, finalOrder.Value);
                }

            }
            else if (isExplicitLayout && !hasIgnore)
            {
                Report(context, DiagnosticDescriptors.ExplicitSerializationMemberMissingOrder, member, member.Name);
            }

            if (hasDynamic && IsFixedSizeSerializationType(memberType))
            {
                Report(
                    context,
                    DiagnosticDescriptors.SerializeDynamicSizeOnFixedMember,
                    member,
                    member.Name,
                    memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }

        foreach (IGrouping<int, (ISymbol Member, int Order)> duplicateGroup in orderedMembers.GroupBy(static x => x.Order).Where(static g => g.Count() > 1))
        {
            foreach ((ISymbol member, _) in duplicateGroup)
            {
                Report(
                    context,
                    DiagnosticDescriptors.DuplicateSerializeOrder,
                    member,
                    member.Name,
                    duplicateGroup.Key,
                    typeSymbol.Name);
            }
        }
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, SymbolSet symbols)
    {
        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        if (methodDeclaration.Identifier.ValueText != "ResetForPool"
            || methodDeclaration.ParameterList.Parameters.Count != 0)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!methodSymbol.IsOverride || methodSymbol.ContainingType is not INamedTypeSymbol containingType || !InheritsPacketBase(containingType, symbols))
        {
            return;
        }

        bool callsBaseReset = methodDeclaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: BaseExpressionSyntax,
                    Name.Identifier.ValueText: "ResetForPool"
                });

        if (!callsBaseReset)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ResetForPoolShouldCallBase,
                methodDeclaration.Identifier.GetLocation(),
                containingType.Name));
        }
    }

    private static void AnalyzePacketType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        INamedTypeSymbol? baseType = typeSymbol.BaseType;
        if (baseType is not null
            && baseType.IsGenericType
            && SymbolEqualityComparer.Default.Equals(baseType.ConstructedFrom, symbols.PacketBaseType))
        {
            ITypeSymbol selfTypeArgument = baseType.TypeArguments[0];
            if (!SymbolEqualityComparer.Default.Equals(selfTypeArgument, typeSymbol))
            {
                Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PacketBaseSelfTypeMismatch,
                        location,
                        typeSymbol.Name,
                        selfTypeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        foreach (INamedTypeSymbol implementedInterface in typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>())
        {
            if (!implementedInterface.IsGenericType
                || !SymbolEqualityComparer.Default.Equals(implementedInterface.ConstructedFrom, symbols.PacketDeserializerType))
            {
                continue;
            }

            ITypeSymbol selfTypeArgument = implementedInterface.TypeArguments[0];
            if (!SymbolEqualityComparer.Default.Equals(selfTypeArgument, typeSymbol))
            {
                Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PacketDeserializerSelfTypeMismatch,
                        location,
                        typeSymbol.Name,
                        selfTypeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }
    }

    private static void AnalyzeControllerType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols, ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes)
    {
        ImmutableArray<IMethodSymbol> methods = [.. typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsImplicitlyDeclared)];

        foreach (IGrouping<ushort, IMethodSymbol> duplicateGroup in methods
                     .Select(method => (Method: method, Opcode: GetOpcode(method, symbols.PacketOpcodeAttribute)))
                     .Where(entry => entry.Opcode.HasValue)
                     .GroupBy(entry => entry.Opcode!.Value, entry => entry.Method)
                     .Where(group => group.Count() > 1))
        {
            foreach (IMethodSymbol duplicateMethod in duplicateGroup)
            {
                Location? location = duplicateMethod.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is null)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateControllerOpcode,
                    location,
                    duplicateMethod.Name,
                    duplicateGroup.Key));
            }
        }

        foreach (IMethodSymbol methodSymbol in methods)
        {
            if (methodSymbol.IsOverride)
            {
                continue;
            }

            ushort? opcode = GetOpcode(methodSymbol, symbols.PacketOpcodeAttribute);
            bool isCandidate = IsNalixHandlerCandidate(methodSymbol, symbols.PacketInterface, symbols.PacketContextType);

            if (opcode.HasValue)
            {
                if (opcode.Value < 0x0100)
                {
                    Report(context, DiagnosticDescriptors.ReservedOpCodeRange, methodSymbol, methodSymbol.Name, opcode.Value);
                }

                if (globalOpcodes.TryGetValue(opcode.Value, out (IMethodSymbol Method, INamedTypeSymbol Controller) existing) && !SymbolEqualityComparer.Default.Equals(existing.Controller, typeSymbol))
                {
                    Report(context, DiagnosticDescriptors.GlobalDuplicateOpcode, methodSymbol, methodSymbol.Name, opcode.Value, existing.Method.Name, existing.Controller.Name);
                }
                else
                {
                    globalOpcodes[opcode.Value] = (methodSymbol, typeSymbol);
                }

                if (!HasSupportedSignature(methodSymbol, symbols))
                {
                    Report(context, DiagnosticDescriptors.InvalidControllerHandlerSignature, methodSymbol, methodSymbol.Name);
                }
                else
                {
                    ReportPacketContextMismatchIfAny(context, methodSymbol, symbols);
                }
            }
            else if (isCandidate)
            {
                Report(context, DiagnosticDescriptors.ControllerMethodRequiresOpcode, methodSymbol, methodSymbol.Name);
            }
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, SymbolSet symbols)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;

        AnalyzeRequestOptionsInvocation(context, invocation, symbols);

        if (SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, symbols.PacketRegistryFactoryType))
        {
            if (targetMethod.Name == "RegisterPacket")
            {
                AnalyzeRegisterPacketInvocation(context, invocation, symbols);
            }

            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType.OriginalDefinition, symbols.PacketDispatchOptionsType))
        {
            return;
        }

        string methodName = targetMethod.Name;
        if (methodName == "WithHandler")
        {
            AnalyzeWithHandlerInvocation(context, invocation, symbols);
        }
        else if (methodName == "WithMiddleware")
        {
            AnalyzeWithMiddlewareInvocation(context, invocation, symbols);
            AnalyzeMiddlewareRegistrationDuplicateOrder(context, invocation, symbols, bufferMiddleware: false);
        }
        else if (methodName == "WithBufferMiddleware")
        {
            AnalyzeWithBufferMiddlewareInvocation(context, invocation, symbols);
            AnalyzeMiddlewareRegistrationDuplicateOrder(context, invocation, symbols, bufferMiddleware: true);
        }
        else if (methodName == "RegisterPacket")
        {
            AnalyzeRegisterPacketInvocation(context, invocation, symbols);
        }
    }

    private static void AnalyzeMiddlewareRegistrationDuplicateOrder(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SymbolSet symbols,
        bool bufferMiddleware)
    {
        if (invocation.Arguments.Length != 1)
        {
            return;
        }

        IOperation? instance = invocation.Instance;
        if (instance is not IInvocationOperation previousInvocation)
        {
            return;
        }

        string expectedMethodName = bufferMiddleware ? "WithBufferMiddleware" : "WithMiddleware";
        if (previousInvocation.TargetMethod.Name != expectedMethodName
            || previousInvocation.Arguments.Length != 1)
        {
            return;
        }

        ITypeSymbol? currentType = GetUnderlyingType(invocation.Arguments[0].Value);
        ITypeSymbol? previousType = GetUnderlyingType(previousInvocation.Arguments[0].Value);
        if (currentType is null || previousType is null)
        {
            return;
        }

        int? currentOrder = GetMiddlewareOrder(currentType, symbols.MiddlewareOrderAttribute);
        int? previousOrder = GetMiddlewareOrder(previousType, symbols.MiddlewareOrderAttribute);
        if (currentOrder is null || previousOrder is null || currentOrder != previousOrder)
        {
            return;
        }

        Report(
            context,
            DiagnosticDescriptors.MiddlewareRegistrationDuplicateOrder,
            invocation.Syntax.GetLocation(),
            currentType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            currentOrder.Value,
            previousType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static void AnalyzeRequestOptionsInvocation(OperationAnalysisContext context, IInvocationOperation invocation, SymbolSet symbols)
    {
        IMethodSymbol targetMethod = invocation.TargetMethod;

        if (SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, symbols.RequestOptionsType))
        {
            if (targetMethod.Name == "WithRetry"
                && invocation.Arguments.Length == 1
                && TryGetConstantInt(invocation.Arguments[0].Value, out int retryCount)
                && retryCount < 0)
            {
                Report(context, DiagnosticDescriptors.RequestOptionsRetryCountNegative, invocation.Syntax.GetLocation(), retryCount);
            }

            if (targetMethod.Name == "WithTimeout"
                && invocation.Arguments.Length == 1
                && TryGetConstantInt(invocation.Arguments[0].Value, out int timeoutMs)
                && timeoutMs < 0)
            {
                Report(context, DiagnosticDescriptors.RequestOptionsTimeoutNegative, invocation.Syntax.GetLocation(), timeoutMs);
            }

            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, symbols.RequestExtensionsType)
            || targetMethod.Name != "RequestAsync")
        {
            return;
        }

        if (invocation.Arguments.Length < 3)
        {
            return;
        }

        IArgumentOperation optionsArgument = invocation.Arguments[2];
        if (optionsArgument.IsImplicit || IsNullLiteral(optionsArgument.Value))
        {
            return;
        }

        bool? encryptValue = TryGetEncryptValue(optionsArgument.Value, symbols);
        if (encryptValue != true)
        {
            return;
        }

        ITypeSymbol clientType = GetUnderlyingType(invocation.Arguments[0].Value) ?? targetMethod.Parameters[0].Type;
        if (!IsAssignable(clientType, symbols.TcpSessionBaseType))
        {
            Report(
                context,
                DiagnosticDescriptors.RequestEncryptRequiresTcpSession,
                invocation.Syntax.GetLocation(),
                clientType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
    }

    private static void AnalyzeWithHandlerInvocation(OperationAnalysisContext context, IInvocationOperation invocation, SymbolSet symbols)
    {
        if (invocation.Instance?.Type is not INamedTypeSymbol dispatchOptionsType || dispatchOptionsType.TypeArguments.Length != 1)
        {
            return;
        }

        ITypeSymbol dispatcherPacketType = dispatchOptionsType.TypeArguments[0];
        if (invocation.TargetMethod.TypeArguments.Length != 1)
        {
            return;
        }

        if (invocation.TargetMethod.TypeArguments[0] is not INamedTypeSymbol controllerType)
        {
            return;
        }

        if (!HasAttribute(controllerType, symbols.ControllerAttribute))
        {
            Report(
                context,
                DiagnosticDescriptors.ControllerMissingPacketControllerAttribute,
                invocation.Syntax.GetLocation(),
                controllerType.Name);
            return;
        }

        foreach (IMethodSymbol handlerMethod in controllerType.GetMembers().OfType<IMethodSymbol>())
        {
            if (handlerMethod.MethodKind != MethodKind.Ordinary
                || handlerMethod.DeclaredAccessibility != Accessibility.Public
                || !HasAttribute(handlerMethod, symbols.PacketOpcodeAttribute))
            {
                continue;
            }

            ITypeSymbol? packetType = GetLegacyPacketType(handlerMethod, symbols);
            if (packetType is null)
            {
                if (TryGetPacketContextType(handlerMethod, symbols.PacketContextType, out ITypeSymbol? contextPacketType)
                    && contextPacketType is not null
                    && !SymbolEqualityComparer.Default.Equals(contextPacketType, dispatcherPacketType))
                {
                    Report(
                        context,
                        DiagnosticDescriptors.PacketContextTypeMismatch,
                        invocation.Syntax.GetLocation(),
                        handlerMethod.Name,
                        contextPacketType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        dispatcherPacketType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                }

                continue;
            }

            if (!IsAssignable(packetType, dispatcherPacketType))
            {
                Report(
                    context,
                    DiagnosticDescriptors.HandlerPacketTypeMismatch,
                    invocation.Syntax.GetLocation(),
                    controllerType.Name,
                    handlerMethod.Name,
                    packetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    dispatcherPacketType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }
    }

    private static void AnalyzeWithBufferMiddlewareInvocation(OperationAnalysisContext context, IInvocationOperation invocation, SymbolSet symbols)
    {
        IArgumentOperation? middlewareArgument = invocation.Arguments.FirstOrDefault();
        IOperation? valueOperation = middlewareArgument?.Value;
        ITypeSymbol? middlewareType = valueOperation is IConversionOperation conversion
            ? conversion.Operand.Type
            : valueOperation?.Type;
        if (middlewareType is null)
        {
            return;
        }

        if (Implements(middlewareType, symbols.NetworkBufferMiddlewareType) && HasAttribute(middlewareType, symbols.MiddlewareStageAttribute))
        {
            Report(
                context,
                DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute,
                invocation.Syntax.GetLocation(),
                middlewareType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        else if (!Implements(middlewareType, symbols.NetworkBufferMiddlewareType))
        {
            Report(
                context,
                DiagnosticDescriptors.BufferMiddlewareRegistrationTypeMismatch,
                invocation.Syntax.GetLocation(),
                middlewareType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
    }

    private static void AnalyzeRegisterPacketInvocation(OperationAnalysisContext context, IInvocationOperation invocation, SymbolSet symbols)
    {
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, symbols.PacketRegistryFactoryType))
        {
            return;
        }

        if (invocation.TargetMethod.TypeArguments.Length != 1 || invocation.TargetMethod.TypeArguments[0] is not INamedTypeSymbol packetType)
        {
            return;
        }

        if (packetType.IsAbstract || packetType.IsGenericType || packetType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            Report(
                context,
                DiagnosticDescriptors.PacketRegistryPacketMustBeConcrete,
                invocation.Syntax.GetLocation(),
                packetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return;
        }

        INamedTypeSymbol? expectedDeserializer = symbols.PacketDeserializerType?.Construct(packetType);
        bool hasDeserializer = packetType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, expectedDeserializer));
        if (!hasDeserializer)
        {
            Report(
                context,
                DiagnosticDescriptors.PacketRegistryPacketMissingDeserializer,
                invocation.Syntax.GetLocation(),
                packetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
    }

    private static void AnalyzeWithMiddlewareInvocation(OperationAnalysisContext context, IInvocationOperation invocation, SymbolSet symbols)
    {
        if (invocation.Instance?.Type is not INamedTypeSymbol dispatchOptionsType || dispatchOptionsType.TypeArguments.Length != 1)
        {
            return;
        }

        ITypeSymbol dispatcherPacketType = dispatchOptionsType.TypeArguments[0];
        IArgumentOperation? middlewareArgument = invocation.Arguments.FirstOrDefault();
        ITypeSymbol? middlewareType = middlewareArgument is null ? null : GetUnderlyingType(middlewareArgument.Value);
        if (middlewareType is null)
        {
            return;
        }

        IEnumerable<INamedTypeSymbol> middlewareInterfaces = middlewareType.AllInterfaces
            .OfType<INamedTypeSymbol>()
            .Where(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, symbols.PacketMiddlewareType));

        bool isCompatible = middlewareInterfaces.Any(i => IsAssignable(dispatcherPacketType, i.TypeArguments[0]));
        if (!isCompatible)
        {
            Report(
                context,
                DiagnosticDescriptors.MiddlewareTypeMismatch,
                invocation.Syntax.GetLocation(),
                middlewareType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                dispatcherPacketType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
    }

    private static void ReportPacketContextMismatchIfAny(SymbolAnalysisContext context, IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (!TryGetPacketContextType(methodSymbol, symbols.PacketContextType, out ITypeSymbol? packetContextType)
            || packetContextType is null)
        {
            return;
        }

        if (methodSymbol.ContainingType is not INamedTypeSymbol controllerType)
        {
            return;
        }

        IEnumerable<ITypeSymbol> legacyPacketTypes = controllerType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => !SymbolEqualityComparer.Default.Equals(method, methodSymbol))
            .Where(method => HasAttribute(method, symbols.PacketOpcodeAttribute))
            .Select(method => GetLegacyPacketType(method, symbols))
            .Where(static type => type is not null)!;

        foreach (ITypeSymbol legacyPacketType in legacyPacketTypes)
        {
            if (!SymbolEqualityComparer.Default.Equals(legacyPacketType, packetContextType))
            {
                Report(
                    context,
                    DiagnosticDescriptors.PacketContextTypeMismatch,
                    methodSymbol,
                    methodSymbol.Name,
                    packetContextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    legacyPacketType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                break;
            }
        }
    }

    private static bool HasSupportedSignature(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (!HasSupportedReturnType(methodSymbol.ReturnType, symbols))
        {
            return false;
        }

        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;
        if (parameters.Length == 0)
        {
            return false;
        }

        ITypeSymbol firstParameterType = parameters[0].Type;
        if (IsPacketContext(firstParameterType, symbols.PacketContextType))
        {
            return parameters.Length switch
            {
                1 => true,
                2 => SymbolEqualityComparer.Default.Equals(parameters[1].Type, symbols.CancellationTokenType),
                _ => false
            };
        }

        if (Implements(firstParameterType, symbols.PacketInterface))
        {
            if (parameters.Length < 2 || !Implements(parameters[1].Type, symbols.ConnectionType))
            {
                return false;
            }

            return parameters.Length switch
            {
                2 => true,
                3 => SymbolEqualityComparer.Default.Equals(parameters[2].Type, symbols.CancellationTokenType),
                _ => false
            };
        }

        return false;
    }

    private static bool HasSupportedReturnType(ITypeSymbol returnType, SymbolSet symbols)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, symbols.TaskType)
            || SymbolEqualityComparer.Default.Equals(returnType, symbols.ValueTaskType))
        {
            return true;
        }

        if (returnType is INamedTypeSymbol namedReturnType && namedReturnType.IsGenericType)
        {
            return SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericTaskType)
                || SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericValueTaskType);
        }

        return false;
    }

    private static bool IsNalixHandlerCandidate(IMethodSymbol methodSymbol, INamedTypeSymbol? packetInterfaceSymbol, INamedTypeSymbol? packetContextSymbol)
    {
        if (methodSymbol.Parameters.Length is < 1 or > 3)
        {
            return false;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        return Implements(firstParameterType, packetInterfaceSymbol) || IsPacketContext(firstParameterType, packetContextSymbol);
    }

    private static bool TryGetPacketContextType(IMethodSymbol methodSymbol, INamedTypeSymbol? packetContextSymbol, out ITypeSymbol? packetType)
    {
        packetType = null;

        if (methodSymbol.Parameters.Length == 0)
        {
            return false;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        if (firstParameterType is not INamedTypeSymbol namedType
            || !namedType.IsGenericType
            || !SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, packetContextSymbol))
        {
            return false;
        }

        packetType = namedType.TypeArguments[0];
        return true;
    }

    private static ITypeSymbol? GetLegacyPacketType(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (methodSymbol.Parameters.Length == 0)
        {
            return null;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        if (IsPacketContext(firstParameterType, symbols.PacketContextType))
        {
            return null;
        }

        return Implements(firstParameterType, symbols.PacketInterface) ? firstParameterType : null;
    }

    private static ushort? GetOpcode(IMethodSymbol methodSymbol, INamedTypeSymbol? packetAttributeSymbol)
    {
        AttributeData? attribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, packetAttributeSymbol));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value is ushort opcode ? opcode : null;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
        => attributeSymbol is not null && symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));

    private static bool IsPacketContext(ITypeSymbol typeSymbol, INamedTypeSymbol? packetContextSymbol)
        => packetContextSymbol is not null
           && typeSymbol is INamedTypeSymbol namedType
           && namedType.IsGenericType
           && SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, packetContextSymbol);

    private static bool Implements(ITypeSymbol typeSymbol, INamedTypeSymbol? interfaceSymbol)
        => interfaceSymbol is not null && (SymbolEqualityComparer.Default.Equals(typeSymbol, interfaceSymbol)
           || typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol)));

    private static bool ImplementsOpenGeneric(ITypeSymbol typeSymbol, INamedTypeSymbol? openGenericInterfaceSymbol)
        => openGenericInterfaceSymbol is not null && typeSymbol.AllInterfaces
            .OfType<INamedTypeSymbol>()
            .Any(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, openGenericInterfaceSymbol));

    private static bool IsAssignable(ITypeSymbol from, ITypeSymbol? to)
        => to is not null && (SymbolEqualityComparer.Default.Equals(from, to)
           || from.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, to))
           || HasBaseType(from, to));

    private static bool HasBaseType(ITypeSymbol typeSymbol, ITypeSymbol? expectedBase)
    {
        if (expectedBase is null)
        {
            return false;
        }

        ITypeSymbol? current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expectedBase))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool InheritsPacketBase(INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        INamedTypeSymbol? current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && SymbolEqualityComparer.Default.Equals(current.ConstructedFrom, symbols.PacketBaseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsExplicitSerializeLayout(AttributeData serializePackable, INamedTypeSymbol? serializeLayoutType)
    {
        if (serializePackable.ConstructorArguments.Length != 1)
        {
            return false;
        }

        TypedConstant arg = serializePackable.ConstructorArguments[0];
        return arg.Type is not null
               && SymbolEqualityComparer.Default.Equals(arg.Type, serializeLayoutType)
               && arg.Value is byte rawValue
               && rawValue == 1;
    }

    private static int? GetSerializeOrder(ISymbol symbol, INamedTypeSymbol? serializeOrderAttribute)
    {
        AttributeData? attribute = symbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializeOrderAttribute));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        return value switch
        {
            int intValue => intValue,
            byte byteValue => byteValue,
            short shortValue => shortValue,
            _ => null
        };
    }

    private static int? GetMiddlewareOrder(ITypeSymbol typeSymbol, INamedTypeSymbol? middlewareOrderAttribute)
    {
        AttributeData? attribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, middlewareOrderAttribute));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        return value switch
        {
            int intValue => intValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            _ => null
        };
    }

    private static bool IsFixedSizeSerializationType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return false;
        }

        return typeSymbol.IsValueType || typeSymbol.SpecialType is not SpecialType.None;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (typeSymbol.SpecialType is SpecialType.System_Char
            or SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_DateTime)
        {
            return true;
        }

        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::System.Guid" or "global::System.TimeSpan";
    }

    private static bool TryGetConstantInt(IOperation operation, out int value)
    {
        object? constantValue = operation.ConstantValue.HasValue ? operation.ConstantValue.Value : null;
        switch (constantValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case sbyte sbyteValue:
                value = sbyteValue;
                return true;
            case byte byteValue:
                value = byteValue;
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool IsNullLiteral(IOperation operation)
        => operation.ConstantValue.HasValue && operation.ConstantValue.Value is null;

    private static ITypeSymbol? GetUnderlyingType(IOperation operation)
    {
        IOperation current = operation;
        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current.Type ?? operation.Type;
    }

    private static bool? TryGetEncryptValue(IOperation operation, SymbolSet symbols)
    {
        if (operation.Type is null || !SymbolEqualityComparer.Default.Equals(operation.Type, symbols.RequestOptionsType))
        {
            return null;
        }

        if (operation is IPropertyReferenceOperation propertyReference
            && propertyReference.Property.Name == "Default"
            && SymbolEqualityComparer.Default.Equals(propertyReference.Member.ContainingType, symbols.RequestOptionsType))
        {
            return false;
        }

        if (operation is IObjectCreationOperation creation
            && SymbolEqualityComparer.Default.Equals(creation.Type, symbols.RequestOptionsType))
        {
            foreach (IPropertyReferenceOperation initializer in creation.Initializer?.Initializers.OfType<ISimpleAssignmentOperation>()
                         .Select(static assignment => assignment.Target)
                         .OfType<IPropertyReferenceOperation>() ?? [])
            {
                if (initializer.Property.Name == "Encrypt")
                {
                    ISimpleAssignmentOperation assignment = (ISimpleAssignmentOperation)initializer.Parent!;
                    if (assignment.Value.ConstantValue is { HasValue: true, Value: bool boolValue })
                    {
                        return boolValue;
                    }
                }
            }

            return false;
        }

        if (operation is IInvocationOperation invocation
            && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, symbols.RequestOptionsType))
        {
            string methodName = invocation.TargetMethod.Name;
            if (methodName == "WithEncrypt")
            {
                if (invocation.Arguments.Length == 0)
                {
                    return true;
                }

                return invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: bool boolValue }
                    ? boolValue
                    : null;
            }

            if (methodName is "WithRetry" or "WithTimeout")
            {
                return TryGetEncryptValue(invocation.Instance!, symbols);
            }
        }

        return null;
    }

    private static bool IsBuilderOpcodeAccess(ExpressionSyntax expression, string builderParameterName)
        => expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: var identifier },
            Name.Identifier.ValueText: "Opcode"
        } && identifier == builderParameterName;

    private static bool IsInboundAlwaysExecute(AttributeData stageAttribute, INamedTypeSymbol? middlewareStageType)
    {
        if (stageAttribute.ConstructorArguments.Length != 1
            || stageAttribute.ConstructorArguments[0].Type is null
            || !SymbolEqualityComparer.Default.Equals(stageAttribute.ConstructorArguments[0].Type, middlewareStageType)
            || stageAttribute.ConstructorArguments[0].Value is not byte stageValue
            || stageValue != 0)
        {
            return false;
        }

        foreach (KeyValuePair<string, TypedConstant> namedArgument in stageAttribute.NamedArguments)
        {
            if (namedArgument.Key == "AlwaysExecute"
                && namedArgument.Value.Value is bool alwaysExecute
                && alwaysExecute)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOpcodeGuard(SyntaxNode node, string builderParameterName)
    {
        if (node is BinaryExpressionSyntax binary)
        {
            return (binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression)
                    || binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression))
                   && (IsBuilderOpcodeAccess(binary.Left, builderParameterName) || IsBuilderOpcodeAccess(binary.Right, builderParameterName));
        }

        if (node is IsPatternExpressionSyntax pattern)
        {
            return IsBuilderOpcodeAccess(pattern.Expression, builderParameterName);
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, IMethodSymbol methodSymbol, params object[] messageArgs)
    {
        Location? location = methodSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
        }
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, ISymbol symbol, params object[] messageArgs)
    {
        Location? location = symbol.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
        }
    }

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] messageArgs)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor, SyntaxNode syntax, params object[] messageArgs)
    {
        Location location = syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, SymbolSet symbols)
    {
        IMethodSymbol method = (IMethodSymbol)context.Symbol;
        AnalyzeOpCodeDocumentation(context, method, symbols);
        AnalyzeBufferLeaseLeak(context, method, symbols);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, SymbolSet symbols)
    {
        IObjectCreationOperation creation = (IObjectCreationOperation)context.Operation;
        if (creation.IsImplicit)
        {
            return;
        }

        if (context.ContainingSymbol is not IMethodSymbol method)
        {
            return;
        }

        if (IsNalixHotPath(method, symbols))
        {
            ITypeSymbol? type = creation.Type;
            if (type is null)
            {
                return;
            }

            // Warn for reference types that are not explicitly allowed in hot paths
            if (type.IsReferenceType && !IsAllowedInHotPath(type))
            {
                Report(context, DiagnosticDescriptors.AllocationInHotPath, creation.Syntax, method.Name, type.Name);
            }
        }
    }

    private static bool IsAllowedInHotPath(ITypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        string name = type.Name;
        return name.EndsWith("Exception") || name == "CancellationTokenSource" || name == "StringBuilder";
    }

    private static bool IsNalixHotPath(IMethodSymbol method, SymbolSet symbols)
    {
        if (HasAttribute(method, symbols.PacketOpcodeAttribute))
        {
            return true;
        }

        if (method.Name == "InvokeAsync" && (ImplementsOpenGeneric(method.ContainingType, symbols.PacketMiddlewareType) || Implements(method.ContainingType, symbols.NetworkBufferMiddlewareType)))
        {
            return true;
        }

        return false;
    }

    private static void AnalyzeOpCodeDocumentation(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        ushort? opcode = GetOpcode(method, symbols.PacketOpcodeAttribute);
        if (!opcode.HasValue)
        {
            return;
        }

        string? xml = method.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(xml))
        {
            return;
        }

        // Look for 0xXXXX patterns
        Match match = Regex.Match(xml, @"0x([0-9A-Fa-f]{1,4})");
        if (match.Success)
        {
            if (ushort.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out ushort docOpcode))
            {
                if (docOpcode != opcode.Value)
                {
                    Report(context, DiagnosticDescriptors.OpCodeDocMismatch, method, method.Name, docOpcode, opcode.Value);
                }
            }
        }
    }

    private static void AnalyzeBufferLeaseLeak(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        if (method.MethodKind != MethodKind.Ordinary || (method.ContainingType.TypeKind == TypeKind.Interface && method.IsAbstract))
        {
            return;
        }

        List<IParameterSymbol> leaseParams = [.. method.Parameters.Where(p => SymbolEqualityComparer.Default.Equals(p.Type, symbols.BufferLeaseType))];
        if (leaseParams.Count == 0)
        {
            return;
        }

        // Skip if the method itself is marked as [Pure]
        if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "PureAttribute"))
        {
            return;
        }

        foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = reference.GetSyntax(context.CancellationToken);
            string code = syntax.ToString();

            // Look for disposal or return via next delegate
            // This is still a heuristic but we'll make it slightly more specific
            foreach (IParameterSymbol p in leaseParams)
            {
                // Skip parameters explicitly marked as borrowed/no-dispose, or 'out' parameters 
                // where ownership is transferred to the caller.
                if (p.GetAttributes().Any(a => a.AttributeClass?.Name is "BorrowedAttribute" or "NoDisposeAttribute") || p.RefKind == RefKind.Out)
                {
                    continue;
                }

                string name = p.Name;
                // Heuristic: check if the lease is disposed, used in a using block, 
                // passed to a next delegate, or returned in any form.
                bool isDisposed = code.Contains($"{name}.Dispose()")
                                || code.Contains($"using (var {name}")
                                || code.Contains($"using var {name}")
                                || Regex.IsMatch(code, $@"\b(next|nextHandler|ExecuteAsync)\s*\([^)]*\b{name}\b")
                                || Regex.IsMatch(code, $@"return\s+[^;]*\b{name}\b")
                                || Regex.IsMatch(code, $@"=> \s*[^;]*\b{name}\b");

                if (!isDisposed)
                {
                    Report(context, DiagnosticDescriptors.PotentialBufferLeaseLeak, p, name);
                }
            }
        }
    }

    private sealed class SymbolSet
    {
        private SymbolSet(
            INamedTypeSymbol? packetOpcodeAttribute,
            INamedTypeSymbol? controllerAttribute,
            INamedTypeSymbol? packetInterface,
            INamedTypeSymbol? packetBaseType,
            INamedTypeSymbol? serializePackableAttribute,
            INamedTypeSymbol? serializeHeaderAttribute,
            INamedTypeSymbol? serializeOrderAttribute,
            INamedTypeSymbol? serializeIgnoreAttribute,
            INamedTypeSymbol? serializeDynamicSizeAttribute,
            INamedTypeSymbol? serializeLayoutType,
            INamedTypeSymbol? packetContextType,
            INamedTypeSymbol? connectionType,
            INamedTypeSymbol? packetDispatchOptionsType,
            INamedTypeSymbol? packetRegistryFactoryType,
            INamedTypeSymbol? packetDeserializerType,
            INamedTypeSymbol? packetMiddlewareType,
            INamedTypeSymbol? networkBufferMiddlewareType,
            INamedTypeSymbol? middlewareOrderAttribute,
            INamedTypeSymbol? middlewareStageAttribute,
            INamedTypeSymbol? middlewareStageType,
            INamedTypeSymbol? configurationLoaderType,
            INamedTypeSymbol? configuredIgnoreAttribute,
            INamedTypeSymbol? packetMetadataProviderType,
            INamedTypeSymbol? packetMetadataBuilderType,
            INamedTypeSymbol? methodInfoType,
            INamedTypeSymbol? requestOptionsType,
            INamedTypeSymbol? requestExtensionsType,
            INamedTypeSymbol? tcpSessionBaseType,
            INamedTypeSymbol? taskType,
            INamedTypeSymbol? genericTaskType,
            INamedTypeSymbol? valueTaskType,
            INamedTypeSymbol? genericValueTaskType,
            INamedTypeSymbol? cancellationTokenType,
            INamedTypeSymbol? bufferLeaseType,
            int packetHeaderRegionOffset)
        {
            this.PacketOpcodeAttribute = packetOpcodeAttribute;
            this.ControllerAttribute = controllerAttribute;
            this.PacketInterface = packetInterface;
            this.PacketBaseType = packetBaseType;
            this.SerializePackableAttribute = serializePackableAttribute;
            this.SerializeHeaderAttribute = serializeHeaderAttribute;
            this.SerializeOrderAttribute = serializeOrderAttribute;
            this.SerializeIgnoreAttribute = serializeIgnoreAttribute;
            this.SerializeDynamicSizeAttribute = serializeDynamicSizeAttribute;
            this.SerializeLayoutType = serializeLayoutType;
            this.PacketContextType = packetContextType;
            this.ConnectionType = connectionType;
            this.PacketDispatchOptionsType = packetDispatchOptionsType;
            this.PacketRegistryFactoryType = packetRegistryFactoryType;
            this.PacketDeserializerType = packetDeserializerType;
            this.PacketMiddlewareType = packetMiddlewareType;
            this.NetworkBufferMiddlewareType = networkBufferMiddlewareType;
            this.MiddlewareOrderAttribute = middlewareOrderAttribute;
            this.MiddlewareStageAttribute = middlewareStageAttribute;
            this.MiddlewareStageType = middlewareStageType;
            this.ConfigurationLoaderType = configurationLoaderType;
            this.ConfiguredIgnoreAttribute = configuredIgnoreAttribute;
            this.PacketMetadataProviderType = packetMetadataProviderType;
            this.PacketMetadataBuilderType = packetMetadataBuilderType;
            this.MethodInfoType = methodInfoType;
            this.RequestOptionsType = requestOptionsType;
            this.RequestExtensionsType = requestExtensionsType;
            this.TcpSessionBaseType = tcpSessionBaseType;
            this.TaskType = taskType;
            this.GenericTaskType = genericTaskType;
            this.ValueTaskType = valueTaskType;
            this.GenericValueTaskType = genericValueTaskType;
            this.CancellationTokenType = cancellationTokenType;
            this.BufferLeaseType = bufferLeaseType;
            this.PacketHeaderRegionOffset = packetHeaderRegionOffset;
        }

        public INamedTypeSymbol? PacketOpcodeAttribute { get; }
        public INamedTypeSymbol? ControllerAttribute { get; }
        public INamedTypeSymbol? PacketInterface { get; }
        public INamedTypeSymbol? PacketBaseType { get; }
        public INamedTypeSymbol? SerializePackableAttribute { get; }
        public INamedTypeSymbol? SerializeHeaderAttribute { get; }
        public INamedTypeSymbol? SerializeOrderAttribute { get; }
        public INamedTypeSymbol? SerializeIgnoreAttribute { get; }
        public INamedTypeSymbol? SerializeDynamicSizeAttribute { get; }
        public INamedTypeSymbol? SerializeLayoutType { get; }
        public INamedTypeSymbol? PacketContextType { get; }
        public INamedTypeSymbol? ConnectionType { get; }
        public INamedTypeSymbol? PacketDispatchOptionsType { get; }
        public INamedTypeSymbol? PacketRegistryFactoryType { get; }
        public INamedTypeSymbol? PacketDeserializerType { get; }
        public INamedTypeSymbol? PacketMiddlewareType { get; }
        public INamedTypeSymbol? NetworkBufferMiddlewareType { get; }
        public INamedTypeSymbol? MiddlewareOrderAttribute { get; }
        public INamedTypeSymbol? MiddlewareStageAttribute { get; }
        public INamedTypeSymbol? MiddlewareStageType { get; }
        public INamedTypeSymbol? ConfigurationLoaderType { get; }
        public INamedTypeSymbol? ConfiguredIgnoreAttribute { get; }
        public INamedTypeSymbol? PacketMetadataProviderType { get; }
        public INamedTypeSymbol? PacketMetadataBuilderType { get; }
        public INamedTypeSymbol? MethodInfoType { get; }
        public INamedTypeSymbol? RequestOptionsType { get; }
        public INamedTypeSymbol? RequestExtensionsType { get; }
        public INamedTypeSymbol? TcpSessionBaseType { get; }
        public INamedTypeSymbol? TaskType { get; }
        public INamedTypeSymbol? GenericTaskType { get; }
        public INamedTypeSymbol? ValueTaskType { get; }
        public INamedTypeSymbol? GenericValueTaskType { get; }
        public INamedTypeSymbol? CancellationTokenType { get; }
        public INamedTypeSymbol? BufferLeaseType { get; }
        public int PacketHeaderRegionOffset { get; }

        public static SymbolSet? Create(Compilation compilation)
        {
            INamedTypeSymbol? packetOpcodeAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Networking.Packets.PacketOpcodeAttribute");
            INamedTypeSymbol? controllerAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Networking.Packets.PacketControllerAttribute");
            INamedTypeSymbol? packetInterface = compilation.GetTypeByMetadataName("Nalix.Common.Networking.Packets.IPacket");
            INamedTypeSymbol? packetBaseType = compilation.GetTypeByMetadataName("Nalix.Framework.DataFrames.PacketBase`1");
            INamedTypeSymbol? serializePackableAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializePackableAttribute");
            INamedTypeSymbol? serializeHeaderAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializeHeaderAttribute");
            INamedTypeSymbol? serializeOrderAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializeOrderAttribute");
            INamedTypeSymbol? serializeIgnoreAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializeIgnoreAttribute");
            INamedTypeSymbol? serializeDynamicSizeAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializeDynamicSizeAttribute");
            INamedTypeSymbol? serializeLayoutType = compilation.GetTypeByMetadataName("Nalix.Common.Serialization.SerializeLayout");
            INamedTypeSymbol? packetHeaderOffsetType = compilation.GetTypeByMetadataName("Nalix.Common.Networking.Packets.PacketHeaderOffset");
            INamedTypeSymbol? packetContextType = compilation.GetTypeByMetadataName("Nalix.Runtime.Dispatching.PacketContext`1");
            INamedTypeSymbol? connectionType = compilation.GetTypeByMetadataName("Nalix.Common.Networking.IConnection");
            INamedTypeSymbol? packetDispatchOptionsType = compilation.GetTypeByMetadataName("Nalix.Runtime.Dispatching.PacketDispatchOptions`1");
            INamedTypeSymbol? packetRegistryFactoryType = compilation.GetTypeByMetadataName("Nalix.Framework.DataFrames.PacketRegistryFactory");
            INamedTypeSymbol? packetDeserializerType = compilation.GetTypeByMetadataName("Nalix.Common.Networking.Packets.IPacketDeserializer`1");
            INamedTypeSymbol? packetMiddlewareType = compilation.GetTypeByMetadataName("Nalix.Runtime.Middleware.IPacketMiddleware`1");
            INamedTypeSymbol? networkBufferMiddlewareType = compilation.GetTypeByMetadataName("Nalix.Runtime.Middleware.INetworkBufferMiddleware");
            INamedTypeSymbol? middlewareOrderAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Middleware.MiddlewareOrderAttribute");
            INamedTypeSymbol? middlewareStageAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Middleware.MiddlewareStageAttribute");
            INamedTypeSymbol? middlewareStageType = compilation.GetTypeByMetadataName("Nalix.Common.Middleware.MiddlewareStage");
            INamedTypeSymbol? configurationLoaderType = compilation.GetTypeByMetadataName("Nalix.Framework.Configuration.Binding.ConfigurationLoader");
            INamedTypeSymbol? configuredIgnoreAttribute = compilation.GetTypeByMetadataName("Nalix.Common.Abstractions.ConfiguredIgnoreAttribute");
            INamedTypeSymbol? packetMetadataProviderType = compilation.GetTypeByMetadataName("Nalix.Runtime.Dispatching.IPacketMetadataProvider");
            INamedTypeSymbol? packetMetadataBuilderType = compilation.GetTypeByMetadataName("Nalix.Runtime.Dispatching.PacketMetadataBuilder");
            INamedTypeSymbol? methodInfoType = compilation.GetTypeByMetadataName("System.Reflection.MethodInfo");
            INamedTypeSymbol? requestOptionsType = compilation.GetTypeByMetadataName("Nalix.SDK.Configuration.RequestOptions");
            INamedTypeSymbol? requestExtensionsType = compilation.GetTypeByMetadataName("Nalix.SDK.Transport.Extensions.RequestExtensions");
            INamedTypeSymbol? tcpSessionBaseType = compilation.GetTypeByMetadataName("Nalix.SDK.Transport.TcpSession");
            INamedTypeSymbol? taskType = compilation.GetTypeByMetadataName(typeof(Task).FullName);
            INamedTypeSymbol? genericTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            INamedTypeSymbol? valueTaskType = compilation.GetTypeByMetadataName(typeof(ValueTask).FullName);
            INamedTypeSymbol? genericValueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
            INamedTypeSymbol? cancellationTokenType = compilation.GetTypeByMetadataName(typeof(CancellationToken).FullName);
            INamedTypeSymbol? bufferLeaseType = compilation.GetTypeByMetadataName("Nalix.Common.Abstractions.IBufferLease");
            int packetHeaderRegionOffset = 12;
            if (packetHeaderOffsetType is not null)
            {
                foreach (ISymbol member in packetHeaderOffsetType.GetMembers())
                {
                    if (member is IFieldSymbol { Name: "Region", HasConstantValue: true } field)
                    {
                        packetHeaderRegionOffset = Convert.ToInt32(field.ConstantValue, CultureInfo.InvariantCulture);
                        break;
                    }
                }
            }

            return (packetOpcodeAttribute is null || controllerAttribute is null)
                ? null
                : new SymbolSet(
                    packetOpcodeAttribute,
                    controllerAttribute,
                    packetInterface,
                    packetBaseType,
                    serializePackableAttribute,
                    serializeHeaderAttribute,
                    serializeOrderAttribute,
                    serializeIgnoreAttribute,
                    serializeDynamicSizeAttribute,
                    serializeLayoutType,
                    packetContextType,
                    connectionType,
                    packetDispatchOptionsType,
                    packetRegistryFactoryType,
                    packetDeserializerType,
                    packetMiddlewareType,
                    networkBufferMiddlewareType,
                    middlewareOrderAttribute,
                    middlewareStageAttribute,
                    middlewareStageType,
                    configurationLoaderType,
                    configuredIgnoreAttribute,
                    packetMetadataProviderType,
                    packetMetadataBuilderType,
                    methodInfoType,
                    requestOptionsType,
                    requestExtensionsType,
                    tcpSessionBaseType,
                    taskType,
                    genericTaskType,
                    valueTaskType,
                    genericValueTaskType,
                    cancellationTokenType,
                    bufferLeaseType,
                    packetHeaderRegionOffset);
        }
    }
}
