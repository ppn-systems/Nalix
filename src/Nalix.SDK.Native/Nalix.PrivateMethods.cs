// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.SDK.Native.Wrappers;

namespace Nalix.SDK.Native;

public static partial class Nalix
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NativeTcpSession? GET_WRAPPER(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        GCHandle gc = GCHandle.FromIntPtr(handle);
        return gc.IsAllocated ? gc.Target as NativeTcpSession : null;
    }
}
