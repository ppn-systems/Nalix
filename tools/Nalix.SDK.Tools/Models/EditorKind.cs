// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Identifies the editor UI that should be used for a reflected property.
/// </summary>
public enum EditorKind
{
    Text = 0,
    Numeric = 1,
    Enum = 2,
    Boolean = 3,
    ByteArray = 4,
    Complex = 5,
    Unsupported = 6
}
