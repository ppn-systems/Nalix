// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Network.Protocols;

namespace Nalix.Network.Examples.Protocols;

/// <summary>
/// Minimal echo protocol example.
/// </summary>
public sealed class ExampleEchoProtocol : Protocol
{
    /// <summary>
    /// Mirrors the inbound payload back to the sender.
    /// </summary>
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Lease is not IBufferLease incoming)
        {
            args.Dispose();
            return;
        }

        using (incoming)
        {
            string message = Encoding.UTF8.GetString(incoming.Memory.Span);
            byte[] responseBytes = Encoding.UTF8.GetBytes($"ECHO: {message}");

            args.Connection.TCP.Send(responseBytes);
        }

        args.Dispose();
    }
}
