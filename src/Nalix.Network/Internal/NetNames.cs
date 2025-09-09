using Nalix.Framework.Tasks.Name;

namespace Nalix.Network.Internal;

internal static class NetNames
{
    // Segments kept internal to avoid leaking raw literals.
    internal static class Seg
    {
        internal static readonly System.String Net = "net";
        internal static readonly System.String Pkt = "pkt";
        internal static readonly System.String Tcp = "tcp";
        internal static readonly System.String Dispatch = "dispatch";
        internal static readonly System.String Accept = "accept";
        internal static readonly System.String Proc = "proc";
        internal static readonly System.String Time = "time";
    }

    // Groups (path-style)

    public static System.String TimeSyncGroup = TaskNames.Groups.Build(Seg.Time, "sync");

    public static System.String TimingWheelGroup = TaskNames.Groups.Build(Seg.Time, "wheel");

    public static readonly System.String PacketDispatchGroup = TaskNames.Groups.Build(Seg.Net, Seg.Pkt, Seg.Dispatch);

    public static System.String TcpGroup(System.Int32 port) => TaskNames.Groups.Build(Seg.Net, Seg.Tcp, port.ToString());

    public static System.String TcpProcessGroup(System.Int32 port) => TaskNames.Groups.Build(Seg.Net, Seg.Tcp, port.ToString(), Seg.Proc);

    // Workers (dot-style)
    public static System.String TcpAcceptWorker(System.Int32 port, System.Int32 index)
        => TaskNames.Workers.Build(Seg.Tcp, Seg.Accept, port.ToString(), index.ToString());

    public static System.String TcpProcessWorker(System.Int32 port, System.String id)
        => TaskNames.Workers.Build(Seg.Tcp, Seg.Proc, port.ToString(), TaskNames.Safe(id));

    public static System.String TimeSyncWorker(System.TimeSpan period) => $"{Seg.Time}.sync.{period.TotalMilliseconds:0.#}ms";

    public static System.String TimingWheelWorker(System.Int32 tick, System.Int32 size) => $"{Seg.Time}.wheel.{tick}ms.w{size}";

    public static readonly System.String PacketDispatchWorker = TaskNames.Workers.Build(Seg.Pkt, Seg.Dispatch);

}
