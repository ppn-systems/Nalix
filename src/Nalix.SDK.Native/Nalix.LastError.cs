// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

#pragma warning disable CA1720 // Identifier contains type name

namespace Nalix.SDK.Native;

/// <summary>
/// Provides access to the last native interop error message.
/// </summary>
/// <remarks>
/// This type stores the most recent exception message so unmanaged callers
/// can retrieve diagnostic information through exported native functions.
/// </remarks>
public static class LastError
{
    private static readonly Lock s_lock = new();
    private static string s_lastErrorMessage = string.Empty;

    /// <summary>
    /// Stores the specified exception as the current last error.
    /// </summary>
    /// <param name="ex">
    /// The exception to store.
    /// </param>
    /// <remarks>
    /// If <paramref name="ex"/> is <see langword="null"/>,
    /// a generic fallback error message is stored instead.
    /// </remarks>
    internal static void Set(Exception ex)
    {
        lock (s_lock)
        {
            s_lastErrorMessage = ex?.ToString() ?? "Unknown error";
        }
    }

    /// <summary>
    /// Gets the last stored error message as a UTF-8 encoded unmanaged string.
    /// </summary>
    /// <returns>
    /// A pointer to a null-terminated UTF-8 string allocated with
    /// <see cref="Marshal.AllocHGlobal(int)"/>, or <see cref="IntPtr.Zero"/>
    /// if no error message is available.
    /// </returns>
    /// <remarks>
    /// The returned pointer must be released by calling <see cref="FreeError"/>.
    /// </remarks>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.LastError.Get,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static IntPtr GetLastError()
    {
        lock (s_lock)
        {
            if (string.IsNullOrEmpty(s_lastErrorMessage))
            {
                return IntPtr.Zero;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(s_lastErrorMessage + "\0");
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }
    }

    /// <summary>
    /// Releases unmanaged memory allocated for an error message string.
    /// </summary>
    /// <param name="ptr">
    /// A pointer previously returned by <see cref="GetLastError"/>.
    /// </param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.LastError.Free,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void FreeError(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
