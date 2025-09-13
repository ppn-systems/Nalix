// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Tasks.Name;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal;

internal static class NetTaskNames
{
    // Segments kept internal to avoid leaking raw literals.
    internal static class Segments
    {
        internal static readonly System.String Net = "net";
        internal static readonly System.String Pkt = "pkt";
        internal static readonly System.String Tcp = "tcp";
        internal static readonly System.String Proc = "proc";
        internal static readonly System.String Time = "time";
        internal static readonly System.String Accept = "accept";
        internal static readonly System.String Dispatch = "dispatch";
    }

    // Groups (path-style)

    public static System.String TimeSyncGroup = TaskNames.Groups.Build(Segments.Net, Segments.Time, "sync");

    public static System.String TimingWheelGroup = TaskNames.Groups.Build(Segments.Net, Segments.Time, "wheel");

    public static readonly System.String PacketDispatchGroup = TaskNames.Groups.Build(Segments.Net, Segments.Pkt, Segments.Dispatch);

    public static System.String TcpGroup(System.Int32 port) => TaskNames.Groups.Build(Segments.Net, Segments.Tcp, port.ToString());

    public static System.String TcpProcessGroup(System.Int32 port)
        => TaskNames.Groups.Build(Segments.Net, Segments.Tcp, port.ToString(), Segments.Proc);

    // Workers (dot-style)
    public static System.String TcpAcceptWorker(System.Int32 port, System.Int32 index)
        => TaskNames.Workers.Build(Segments.Tcp, Segments.Accept, port.ToString(), index.ToString());

    public static System.String TcpProcessWorker(System.Int32 port, System.String id)
        => TaskNames.Workers.Build(Segments.Tcp, Segments.Proc, port.ToString(), TaskNames.Safe(id));

    public static System.String TimeSyncWorker(System.TimeSpan period) => $"{Segments.Time}.sync.{period.TotalMilliseconds:0.#}ms";

    public static System.String TimingWheelWorker(System.Int32 tick, System.Int32 size) => $"{Segments.Time}.wheel.{tick}ms.w{size}";

    public static readonly System.String PacketDispatchWorker = TaskNames.Workers.Build(Segments.Pkt, Segments.Dispatch);
}
