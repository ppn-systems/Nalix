// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nalix.Analyzers.Diagnostics;

namespace Nalix.Analyzers.Analyzers;

public sealed partial class NalixUsageAnalyzer
{
    private static void AnalyzeRpcServiceType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (typeSymbol.TypeKind != TypeKind.Interface)
        {
            return;
        }

        bool hasRpcAttribute = HasAttribute(typeSymbol, symbols.RpcServiceAttribute);
        if (!hasRpcAttribute)
        {
            return;
        }

        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member.Kind != SymbolKind.Method)
            {
                Report(context, DiagnosticDescriptors.RpcServiceContainsInvalidMembers, member, typeSymbol.Name, member.Name);
                continue;
            }

            IMethodSymbol method = (IMethodSymbol)member;
            if (method.MethodKind != MethodKind.Ordinary)
            {
                continue;
            }

            AnalyzeRpcMethod(context, method, symbols);
        }
    }

    private static void AnalyzeRpcMethod(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        // 1. Verify return type
        ITypeSymbol returnType = method.ReturnType;
        bool isSupportedReturnType = false;

        if (returnType is INamedTypeSymbol namedReturn)
        {
            if (IsSymbol(namedReturn.OriginalDefinition, symbols.RpcCallType) ||
                IsSymbol(namedReturn.OriginalDefinition, symbols.RpcStreamType) ||
                IsSymbol(namedReturn.OriginalDefinition, symbols.ValueTaskType))
            {
                isSupportedReturnType = true;
            }
        }

        if (!isSupportedReturnType)
        {
            Report(context, DiagnosticDescriptors.RpcServiceInvalidReturnType, method, method.Name);
        }

        // 2. Verify parameters
        if (method.Parameters.Length == 0)
        {
            Report(context, DiagnosticDescriptors.RpcServiceInvalidParameters, method, method.Name);
            return;
        }

        ITypeSymbol firstParameterType = method.Parameters[0].Type;
        if (!Implements(firstParameterType, symbols.PacketInterface))
        {
            Report(context, DiagnosticDescriptors.RpcServiceInvalidParameters, method, method.Name);
            return;
        }

        for (int i = 1; i < method.Parameters.Length; i++)
        {
            ITypeSymbol paramType = method.Parameters[i].Type;
            bool isSupportedParam = false;

            if (IsSymbol(paramType, symbols.RequestOptionsType) ||
                IsSymbol(paramType, symbols.CancellationTokenType))
            {
                isSupportedParam = true;
            }
            else if (paramType is INamedTypeSymbol namedParam)
            {
                if (namedParam.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                    namedParam.TypeArguments[0].SpecialType == SpecialType.System_Boolean)
                {
                    isSupportedParam = true; // bool? encrypt
                }
                else if (namedParam.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                         namedParam.TypeArguments[0] is INamedTypeSymbol funcParam &&
                         funcParam.OriginalDefinition.ToDisplayString() == "System.Func<T, TResult>" &&
                         funcParam.TypeArguments[1].SpecialType == SpecialType.System_Boolean)
                {
                    isSupportedParam = true; // Func<TResponse, bool>?
                }
            }

            if (!isSupportedParam)
            {
                Report(context, DiagnosticDescriptors.RpcServiceInvalidParameters, method, method.Name);
                break;
            }
        }
    }
}
