// Copyright (c) 2025-2026 PPN Corporation.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Abstractions;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Network.Examples.Protocols;

public sealed class AutoXListener : TcpListenerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoXListener"/> class with the specified protocol handler.
    /// </summary>
    /// <param name="protocol">The protocol handler used for processing incoming connections.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public AutoXListener(IProtocol protocol) : base(protocol)
    {
    }

    // You can override methods or add additional behaviors here
    // For example, override Dispose or add custom events.
}