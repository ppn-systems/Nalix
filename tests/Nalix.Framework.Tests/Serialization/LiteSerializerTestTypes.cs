// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace Nalix.Framework.Tests.Serialization;

internal struct SmallStruct
{
    internal byte A;
}

internal struct ComplexStruct
{
    internal int I32;
    internal short I16;
    internal byte B;
}

internal sealed class NullClass
{
    internal int[] I32 = [];
    internal short? I16;
}

internal sealed class TestObject
{
    internal int Id { get; set; }
    internal string Name { get; set; } = string.Empty;
    internal TestObject? Child { get; set; }
    internal List<string> Tags { get; set; } = [];
}

internal readonly struct TestStruct(int x, float y)
{
    internal readonly int X = x;
    internal readonly float Y = y;
}
