// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.SDK.Native;

/// <summary>
/// Defines native SDK error codes returned by interop operations.
/// </summary>
/// <remarks>
/// Negative values represent failures, while <see cref="Success"/> indicates
/// that the operation completed successfully.
/// </remarks>
[DebuggerDisplay("ErrorCode")]
public static class ErrorCode
{
    /// <summary>
    /// Indicates that the operation completed successfully.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Indicates that the provided native handle is invalid or no longer valid.
    /// </summary>
    public const int InvalidHandle = -1;

    /// <summary>
    /// Indicates that the connection attempt failed.
    /// </summary>
    public const int ConnectionFailed = -2;

    /// <summary>
    /// Indicates that sending data to the remote endpoint failed.
    /// </summary>
    public const int SendFailed = -3;

    /// <summary>
    /// Indicates that the connection handshake process failed.
    /// </summary>
    public const int HandshakeFailed = -4;

    /// <summary>
    /// Indicates that the operation exceeded the allowed timeout period.
    /// </summary>
    public const int Timeout = -5;

    /// <summary>
    /// Indicates that the requested operation failed for an unspecified reason.
    /// </summary>
    public const int OperationFailed = -6;

    /// <summary>
    /// Indicates that disconnecting from the remote endpoint failed.
    /// </summary>
    public const int DisconnectFailed = -7;

    /// <summary>
    /// Indicates that one or more provided arguments were invalid or malformed.
    /// </summary>
    public const int InvalidArgument = -8;
}
