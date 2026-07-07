// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// A zero-allocation wrapper that ensures a retrieved session entry is returned to its pool upon disposal.
/// </summary>
public readonly struct SessionScope : IDisposable
{
    private readonly SessionEntry? _entry;

    /// <summary>
    /// Initializes a new scope for the specified session entry.
    /// </summary>
    public SessionScope(SessionEntry? entry) => _entry = entry;

    /// <summary>
    /// Gets the session entry instance.
    /// </summary>
    public SessionEntry? Value => _entry;

    /// <summary>
    /// Returns true if this scope is valid (contains an entry).
    /// </summary>
    public bool IsValid => _entry != null;

    /// <summary>
    /// Returns the session resources to the object pool by disposing the underlying entry.
    /// </summary>
    public void Dispose() => _entry?.Return();

    /// <summary>
    /// Implicitly converts the scope to its underlying session entry instance.
    /// </summary>
    public static implicit operator SessionEntry?(SessionScope scope) => scope._entry;
}
