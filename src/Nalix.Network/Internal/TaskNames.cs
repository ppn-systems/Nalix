

using Nalix.Common.Abstractions;

namespace Nalix.Network.Internal;

internal static class TaskNames
{
    internal static class Tags
    {
        public const System.String Accept = "accept";
        public const System.String Processor = "proc";
        public const System.String Limiter = "limiter";
        public const System.String Listener = "listener";
        public const System.String Dispatch = "dispatch";
        public const System.String TimeSync = "timesync";
        public const System.String TimingWheel = "timingwheel";
    }

    internal static class Groups
    {
        public const System.String Limiter = $"net.{Tags.Limiter}";
        public const System.String Dispatch = $"net.{Tags.Dispatch}";
        public const System.String TimeSync = $"sys.{Tags.TimeSync}";
        public const System.String TimingWeel = $"sys.{Tags.TimingWheel}";

        public static System.String Tcp(System.Int32 port) => $"net.tcp.{port}";
        public static System.String TcpAccept(System.Int32 port) => $"net.tcp.{port}.{Tags.Accept}";
        public static System.String TcpProcess(System.Int32 port) => $"net.tcp.{port}.{Tags.Processor}";
    }

    internal static class Workers
    {
        public static System.String PacketDispatch => $"pkt.{Tags.Dispatch}";
        public static System.String TcpAccept(System.Int32 port, System.Int32 idx) => $"tcp.{Tags.Accept}.{port}.{idx}";
        public static System.String TcpProcess(System.Int32 port, IIdentifier id) => $"tcp.{Tags.Processor}.{port}.{Safe(id.ToString(true))}";
        public static System.String TimeSync(System.TimeSpan period) => $"{Tags.TimeSync}.{period.TotalMilliseconds:0.#}ms";
        public static System.String TimingWheel(System.Int32 tick, System.Int32 size) => $"{Tags.TimingWheel}.{tick}ms.w{size}";
    }

    internal static class Recurring
    {
        public static System.String ConnLimiterCleanup(System.Int32 instanceKey) => $"connlim.cleanup.{instanceKey:X8}";

        public static System.String TokenBucketCleanup(System.Int32 instanceKey) => $"{Tags.Limiter}.cleanup.{instanceKey:X8}";
    }

    private static System.String Safe(System.String s)
    {
        if (System.String.IsNullOrEmpty(s))
        {
            return "-";
        }

        System.Span<System.Char> buf = stackalloc System.Char[s.Length];
        System.Int32 k = 0;
        foreach (System.Char c in s)
        {
            buf[k++] = System.Char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_';
        }
        return new System.String(buf[..k]);
    }
}
