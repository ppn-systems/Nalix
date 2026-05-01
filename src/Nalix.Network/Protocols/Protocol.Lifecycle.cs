// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private int _isDisposed;
    private int _keepConnectionOpen;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets whether the protocol keeps the connection open after a message is processed.
    /// The flag is stored atomically because it is read during hot-path post-processing.
    /// </summary>
    public virtual bool KeepConnectionOpen
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.CompareExchange(ref _keepConnectionOpen, 0, 0) == 1;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected set => Interlocked.Exchange(ref _keepConnectionOpen, value ? 1 : 0);
    }

    #endregion Properties

    #region Disposal

    /// <summary>
    /// Disposes resources used by this protocol instance.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core disposal logic. Override to release managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // The first caller flips the disposed flag from 0 to 1; later callers are ignored.
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Trace))
        {
            s_logger.LogTrace($"[NW.{nameof(Protocol)}:{nameof(Dispose)}] disposed");
        }

        // Derived protocols can release managed resources when disposing == true.
    }

    #endregion Disposal
}
