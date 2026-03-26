// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// This example demonstrates a minimal Protocol subclass that echoes
// received data back to the client. It uses simple mock interfaces to
// illustrate behavior in isolation.
//
// Note: Replace mocks with real Nalix types in your project.

using System.Text;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Network.Protocols;

namespace Nalix.Network.Examples.Protocols;

/// <summary>
/// Simple echo protocol: reads bytes from event args and sends the same bytes back.
/// </summary>
public class EchoProtocol : Protocol
{
    /// <summary>
    /// ProcessMessage is called when a message is available for processing.
    /// Implement domain-specific parsing and response logic here.
    /// </summary>
    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        // Basic null-check and defensive programming.
        if (args != null)
        {
            // Assume IConnectEventArgs exposes a ReadOnlyMemory<byte> Payload (this is a mock).
            using IBufferLease incoming = args.Lease;
            ReadOnlyMemory<byte> payload = incoming.Memory;

            // Convert to string for logging/debugging.
            string text = Encoding.UTF8.GetString(payload.Span);

            // Business logic: echo back upper-cased message.
            string response = $"ECHO: {text.ToUpperInvariant()}";

            // Send response via connection (assume IConnection has a Send method).
            // In a real implementation, sending is asynchronous and may use buffer pooling.
            _ = (args.Connection?.TCP.Send(Encoding.UTF8.GetBytes(response)));
        }
        else
        {
            throw new ArgumentNullException(nameof(args));
        }

        // Optionally keep connection open after post-processing.
        // KeepConnectionOpen = true; // Uncomment to keep alive
    }

    /// <summary>
    /// Optionally override OnPostProcess to add custom post-processing hooks.
    /// This runs inside Protocol.PostProcessMessage, which also handles
    /// KeepConnectionOpen/disconnect semantics and error counting.
    /// </summary>
    // Example: Inspect args and record custom metrics.

    protected override void OnPostProcess(IConnectEventArgs args) => base.OnPostProcess(args);
}
