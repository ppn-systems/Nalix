// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Hosting.Internal.Exceptions;

internal class Throw
{
    private static readonly InternalErrorException s_eventArgsMustHaveLease = new CachedInternalErrorException("Event args must have Lease.");

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EventArgsMustHaveLease() => throw s_eventArgsMustHaveLease;

    private sealed class CachedInternalErrorException(string message) : InternalErrorException(message)
    {
        public override string? StackTrace => "   at Nalix.Network.Internal.Transport (Cached Exception)";
    }
}
