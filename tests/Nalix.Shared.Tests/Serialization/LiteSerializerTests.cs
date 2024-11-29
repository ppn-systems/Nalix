using System;

namespace Nalix.Shared.Tests.Serialization;

/// <summary>
/// Struct 1 byte để tránh padding khi cần kiểm tra mảng unmanaged.
/// </summary>
public struct SmallStruct
{
    public Byte A;
}

/// <summary>
/// Struct unmanaged nhiều field; dùng round-trip thay vì assert kích thước cứng do padding.
/// </summary>
public struct ComplexStruct
{
    public Int32 I32;
    public Int16 I16;
    public Byte B;
}