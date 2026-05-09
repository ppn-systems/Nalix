// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Nalix.SDK.Native.Results;

/// <summary>
/// Represents the result of a TCP time synchronization operation.
/// </summary>
/// <remarks>
/// This structure is designed for native interop scenarios and uses sequential layout
/// to ensure binary compatibility between managed and unmanaged code.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
[NativeMarshalling(typeof(TcpTimeSyncResultMarshaller))]
public struct TcpTimeSyncResult
{
    /// <summary>
    /// Gets or sets the measured round-trip time in milliseconds.
    /// </summary>
    public double RttMs;

    /// <summary>
    /// Gets or sets the adjusted time offset in milliseconds.
    /// </summary>
    public double AdjustedMs;

    /// <summary>
    /// Gets or sets the operation error code.
    /// </summary>
    /// <remarks>
    /// A value of <c>0</c> indicates success.
    /// Non-zero values indicate a transport or synchronization failure.
    /// </remarks>
    public int ErrorCode;
}

/// <summary>
/// Provides custom marshalling support for <see cref="TcpTimeSyncResult"/>.
/// </summary>
/// <remarks>
/// This marshaller performs blittable passthrough conversion between managed
/// and unmanaged representations.
/// </remarks>
[CustomMarshaller(typeof(TcpTimeSyncResult), MarshalMode.Default, typeof(TcpTimeSyncResultMarshaller))]
internal static class TcpTimeSyncResultMarshaller
{
    /// <summary>
    /// Converts an unmanaged <see cref="TcpTimeSyncResult"/> instance
    /// to its managed representation.
    /// </summary>
    /// <param name="unmanaged">
    /// The unmanaged value to convert.
    /// </param>
    /// <returns>
    /// The managed <see cref="TcpTimeSyncResult"/> instance.
    /// </returns>
    public static TcpTimeSyncResult ConvertToManaged(in TcpTimeSyncResult unmanaged) => unmanaged;

    /// <summary>
    /// Converts a managed <see cref="TcpTimeSyncResult"/> instance
    /// to its unmanaged representation.
    /// </summary>
    /// <param name="managed">
    /// The managed value to convert.
    /// </param>
    /// <returns>
    /// The unmanaged <see cref="TcpTimeSyncResult"/> instance.
    /// </returns>
    public static TcpTimeSyncResult ConvertToUnmanaged(in TcpTimeSyncResult managed) => managed;
}
