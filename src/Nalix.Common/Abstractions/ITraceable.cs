// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a contract for components that expose lightweight trace notifications.
/// </summary>
public interface ITraceable
{
    /// <summary>
    /// Raised when the component has a traceable lifecycle or status update.
    /// </summary>
    event Action<string>? TraceOccurred;
}
