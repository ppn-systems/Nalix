// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Nalix.Analyzers.Diagnostics;

namespace Nalix.Analyzers.Analyzers;

public sealed partial class NalixUsageAnalyzer
{
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

        if (SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, symbols.NetworkApplicationBuilderType))
        {
            if (targetMethod.Name == "Build")
            {
                AnalyzeNetworkApplicationBuildInvocation(context, invocation);
            }
            else if (targetMethod.Name == "AddHandler" && targetMethod.TypeArguments.Length == 1)
            {
                AnalyzeNetworkApplicationTypeRegistration(context, invocation, targetMethod.TypeArguments[0], DiagnosticDescriptors.NetworkHostingHandlerTypeInvalid);
            }
            else if (targetMethod.Name == "AddMetadataProvider" && targetMethod.TypeArguments.Length == 1)
            {
                AnalyzeNetworkApplicationTypeRegistration(context, invocation, targetMethod.TypeArguments[0], DiagnosticDescriptors.NetworkHostingMetadataProviderTypeInvalid);
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

    private static void AnalyzeNetworkApplicationBuildInvocation(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        if (invocation.Instance is null)
        {
            return;
        }

        bool hasConnectionHub = ContainsInvocation(invocation.Instance, "ConfigureConnectionHub");
        bool hasBufferPoolManager = ContainsInvocation(invocation.Instance, "UseBufferPoolManager");
        bool hasTcpBinding = ContainsInvocation(invocation.Instance, "AddTcp");
        bool hasUdpBinding = ContainsInvocation(invocation.Instance, "AddUdp");

        if (!hasBufferPoolManager)
        {
            Report(context, DiagnosticDescriptors.NetworkHostingMissingBufferPoolManager, invocation.Syntax.GetLocation());
        }

        if (!hasConnectionHub)
        {
            Report(context, DiagnosticDescriptors.NetworkHostingMissingConnectionHub, invocation.Syntax.GetLocation());
        }

        if (!hasTcpBinding)
        {
            Report(context, DiagnosticDescriptors.NetworkHostingMissingTcpBinding, invocation.Syntax.GetLocation());
        }

        if (hasUdpBinding && !hasTcpBinding)
        {
            Report(context, DiagnosticDescriptors.NetworkHostingUdpWithoutTcpBinding, invocation.Syntax.GetLocation());
        }
    }

    private static void AnalyzeNetworkApplicationTypeRegistration(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        ITypeSymbol typeArgument,
        DiagnosticDescriptor descriptor)
    {
        if (typeArgument.TypeKind != TypeKind.Class
            || typeArgument.IsAbstract
            || typeArgument is ITypeParameterSymbol)
        {
            Report(
                context,
                descriptor,
                invocation.Syntax.GetLocation(),
                typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
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
            Report(context, DiagnosticDescriptors.ControllerMissingPacketControllerAttribute, invocation.Syntax.GetLocation(), controllerType.Name);
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
            Report(context, DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute, invocation.Syntax.GetLocation(), middlewareType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        else if (!Implements(middlewareType, symbols.NetworkBufferMiddlewareType))
        {
            Report(context, DiagnosticDescriptors.BufferMiddlewareRegistrationTypeMismatch, invocation.Syntax.GetLocation(), middlewareType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
    }
}
