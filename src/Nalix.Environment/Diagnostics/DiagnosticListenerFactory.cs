// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nalix.Environment.Diagnostics;

/// <summary>
/// Provides a platform-safe factory for instantiating <see cref="DiagnosticListener"/> objects,
/// returning a safe No-Op browser adapter when running in a WebAssembly sandbox.
/// </summary>
public static class DiagnosticListenerFactory
{
    /// <summary>
    /// Creates a platform-safe <see cref="DiagnosticListener"/> with the specified name.
    /// </summary>
    /// <param name="name">The name of the diagnostic listener.</param>
    /// <returns>A safe <see cref="DiagnosticListener"/> instance.</returns>
    public static DiagnosticListener Create(string name)
    {
        if (OperatingSystem.IsBrowser())
        {
            return (DiagnosticListener)RuntimeHelpers.GetUninitializedObject(typeof(BrowserSafeDiagnosticListener));
        }

        return new DiagnosticListener(name);
    }
}

/// <summary>
/// A subclass of <see cref="DiagnosticListener"/> that acts as a safe No-Op browser adapter,
/// overriding all virtual methods to avoid touching uninitialized base fields.
/// </summary>
internal sealed class BrowserSafeDiagnosticListener : DiagnosticListener
{
    // Required to satisfy the compiler, but will be bypassed during instantiation via GetUninitializedObject.
    public BrowserSafeDiagnosticListener() : base("Safe")
    {
    }

    public override bool IsEnabled(string? name) => false;

    public override bool IsEnabled(string? name, object? arg1, object? arg2 = null) => false;

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("DiagnosticSource.Write requires unreferenced code analysis for payload property discovery.")]
    public override void Write(string name, object? value)
    {
    }
}
