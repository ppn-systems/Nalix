// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Nalix.SDK.Native.Results;

/// <summary>
/// Represents the result of a TCP session resumption operation.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
[NativeMarshalling(typeof(TcpResumeResultMarshaller))]
public struct TcpResumeResult
{
    /// <summary>
    /// Gets the reason code for the resumption status, typically mapping to a ProtocolReason.
    /// </summary>
    public ushort Reason;

    /// <summary>
    /// Gets the error code associated with the operation. A value of 0 indicates success.
    /// </summary>
    public int ErrorCode;
}

/// <summary>
/// Provides marshalling logic for the <see cref="TcpResumeResult"/> struct to support native interop.
/// </summary>
[CustomMarshaller(typeof(TcpResumeResult), MarshalMode.Default, typeof(TcpResumeResultMarshaller))]
internal static class TcpResumeResultMarshaller
{
    /// <summary>
    /// Converts an unmanaged <see cref="TcpResumeResult"/> to its managed representation.
    /// </summary>
    /// <param name="unmanaged">The unmanaged data to convert.</param>
    /// <returns>A managed <see cref="TcpResumeResult"/> structure.</returns>
    public static TcpResumeResult ConvertToManaged(in TcpResumeResult unmanaged) => unmanaged;

    /// <summary>
    /// Converts a managed <see cref="TcpResumeResult"/> to its unmanaged representation.
    /// </summary>
    /// <param name="managed">The managed data to convert.</param>
    /// <returns>The unmanaged <see cref="TcpResumeResult"/> structure.</returns>
    public static TcpResumeResult ConvertToUnmanaged(in TcpResumeResult managed) => managed;
}
