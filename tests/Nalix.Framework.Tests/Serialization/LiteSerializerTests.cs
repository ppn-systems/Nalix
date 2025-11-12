// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
namespace Nalix.Framework.Tests.Serialization;

/// <summary>
/// Struct 1 byte để tránh padding khi cần kiểm tra mảng unmanaged.
/// </summary>
public struct SmallStruct
{
    public byte A;
}

/// <summary>
/// Struct unmanaged nhiều field; dùng round-trip thay vì assert kích thước cứng do padding.
/// </summary>
public struct ComplexStruct
{
    public int I32;
    public short I16;
    public byte B;
}

public class NullClass
{
    public int[] I32;

    public short? I16;
}
