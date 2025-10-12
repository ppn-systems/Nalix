// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class NetTaskNames
{
    internal const System.String Tcp = "tcp";
    internal const System.String Udp = "udp";
    internal const System.String Net = "net";
    internal const System.String Time = "time";
    internal const System.String Sync = "sync";
    internal const System.String Wheel = "wheel";
}