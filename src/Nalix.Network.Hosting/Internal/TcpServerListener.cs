// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Network.Listeners.Tcp;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Hosting.Internal;

/// <inheritdoc />
internal sealed class TcpServerListener : TcpListenerBase
{
    /// <inheritdoc />
    public TcpServerListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <inheritdoc />
    public TcpServerListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }
}
