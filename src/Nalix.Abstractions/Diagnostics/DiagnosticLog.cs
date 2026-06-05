// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Diagnostics;

/// <summary>
/// Represents a unified diagnostics log payload passed to DiagnosticSource.
/// </summary>
public readonly record struct DiagnosticLog(
    string Tag,
    string Message,
    Exception? Exception = null);
