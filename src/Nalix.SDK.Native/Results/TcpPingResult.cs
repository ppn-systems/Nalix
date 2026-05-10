// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Nalix.SDK.Native.Results;

/// <summary>
/// Represents the result of a TCP ping operation performed by the native layer.
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 8)]
[NativeMarshalling(typeof(TcpPingResultMarshaller))]
public struct TcpPingResult
{
    /// <summary>
    /// Gets the round-trip time in milliseconds.
    /// </summary>
    [FieldOffset(0)]
    public double RttMs;

    /// <summary>
    /// Gets the error code associated with the ping operation. 
    /// A value of 0 typically indicates success.
    /// </summary>
    [FieldOffset(8)]
    public int ErrorCode;
}

/// <summary>
/// Provides marshalling logic for the <see cref="TcpPingResult"/> struct 
/// to facilitate efficient interop between managed and unmanaged code.
/// </summary>
[CustomMarshaller(typeof(TcpPingResult), MarshalMode.Default, typeof(TcpPingResultMarshaller))]
internal static class TcpPingResultMarshaller
{
    /// <summary>
    /// Converts an unmanaged <see cref="TcpPingResult"/> to its managed representation.
    /// </summary>
    /// <param name="unmanaged">The unmanaged data to convert.</param>
    /// <returns>A managed <see cref="TcpPingResult"/> structure.</returns>
    public static TcpPingResult ConvertToManaged(in TcpPingResult unmanaged) => unmanaged;

    /// <summary>
    /// Converts a managed <see cref="TcpPingResult"/> to its unmanaged representation.
    /// </summary>
    /// <param name="managed">The managed data to convert.</param>
    /// <returns>The unmanaged <see cref="TcpPingResult"/> structure.</returns>
    public static TcpPingResult ConvertToUnmanaged(in TcpPingResult managed) => managed;
}
