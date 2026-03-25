// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class NetTaskNames
{
    internal const string Tcp = "tcp";
    internal const string Udp = "udp";
    internal const string Net = "net";
    internal const string Time = "time";
    internal const string Sync = "sync";
    internal const string Wheel = "wheel";
}
