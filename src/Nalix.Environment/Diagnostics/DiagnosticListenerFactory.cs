// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        // DiagnosticListener cannot be constructed on Browser because the base
        // constructor is unsupported. We intentionally bypass construction and
        // override every virtual member to provide a safe no-op implementation.
        //
        // IMPORTANT:
        // Do not access any DiagnosticListener base members from this type.
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
    // Never executed when instantiated via RuntimeHelpers.GetUninitializedObject.
    public BrowserSafeDiagnosticListener()
        : base("BrowserSafe")
    {
    }

    public override bool IsEnabled(string? name) => false;

    public override bool IsEnabled(string? name, object? arg1, object? arg2 = null) => false;

    public override IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer) => NoOpDisposable.Instance;

    public override IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer, Predicate<string>? isEnabled) => NoOpDisposable.Instance;

    public override IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer, Func<string, object?, object?, bool>? isEnabled) => NoOpDisposable.Instance;

    public override IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer, Func<string, object?, object?, bool>? isEnabled, Action<Activity, object?>? onActivityImport = null, Action<Activity, object?>? onActivityExport = null) => NoOpDisposable.Instance;

    /// <inheritdoc/>
    [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "<Pending>")]
    public override void Dispose() { }

    [RequiresUnreferencedCode(
        "DiagnosticSource.Write requires unreferenced code analysis for payload property discovery.")]
    public override void Write(string name, object? value) { }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();

        private NoOpDisposable() { }

        public void Dispose() { }
    }
}
