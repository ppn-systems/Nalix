// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using Nalix.Abstractions;
using Nalix.SDK.Transport;

#pragma warning disable CA1051 // Do not declare visible instance fields

namespace Nalix.SDK.Native.Wrappers;

/// <inheritdoc/>
public sealed unsafe class NativeTcpSession : IDisposable
{
    private readonly IntPtr _handle;
    private readonly TcpSession _session;

    private int _disposed;

    // Callbacks

    /// <inheritdoc/>
    public delegate* unmanaged<IntPtr, void> OnConnectedCallback;

    /// <inheritdoc/>
    public delegate* unmanaged<IntPtr, byte*, int, void> OnMessageCallback;

    /// <inheritdoc/>
    public delegate* unmanaged<IntPtr, byte*, int, void> OnErrorCallback;

    /// <inheritdoc/>
    public delegate* unmanaged<IntPtr, void> OnDisconnectedCallback;

    /// <inheritdoc/>
    public TcpSession UnderlyingSession => _session;

    /// <inheritdoc/>
    internal NativeTcpSession(TcpSession session, IntPtr handle)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _handle = handle;

        _session.OnConnected += (_, _) => this.Call(OnConnectedCallback);
        _session.OnDisconnected += (_, _) => this.Call(OnDisconnectedCallback);
        _session.OnMessageReceived += this.HandleMessage;
        _session.OnError += this.HandleError;
    }

    private void Call(delegate* unmanaged<IntPtr, void> callback)
    {
        if (callback != null)
        {
            callback(_handle);
        }
    }

    private void HandleMessage(object? sender, IBufferLease lease)
    {
        if (OnMessageCallback == null)
        {
            lease.Dispose();
            return;
        }

        byte[] data = lease.Memory.ToArray();
        fixed (byte* p = data)
        {
            OnMessageCallback(_handle, p, data.Length);
        }
        lease.Dispose();
    }

    private void HandleError(object? sender, Exception ex)
    {
        if (OnErrorCallback == null)
        {
            return;
        }

        byte[] msg = Encoding.UTF8.GetBytes(ex.Message);
        fixed (byte* p = msg)
        {
            OnErrorCallback(_handle, p, msg.Length);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _session?.Dispose();
    }
}
