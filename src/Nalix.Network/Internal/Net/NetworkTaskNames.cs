﻿// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Tasks.Name;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Net;

/// <summary>
/// Provides standardized naming conventions for network-related task groups and workers.
/// </summary>
/// <remarks>
/// This class builds hierarchical identifiers for background or async tasks within the
/// networking subsystem, using consistent <c>TaskNames.Groups</c> and <c>TaskNames.Workers</c>
/// patterns.
/// </remarks>
internal static class NetworkTaskNames
{
    #region Segments

    /// <summary>
    /// Internal constant segments used to build task identifiers.
    /// </summary>
    internal static class Segments
    {
        internal const System.String Net = "net";
        internal const System.String Packet = "pkt";
        internal const System.String Tcp = "tcp";
        internal const System.String Udp = "udp";
        internal const System.String Process = "proc";
        internal const System.String Receive = "recv";
        internal const System.String Time = "time";
        internal const System.String Accept = "accept";
        internal const System.String Dispatch = "dispatch";
        internal const System.String Sync = "sync";
        internal const System.String Wheel = "wheel";
    }

    #endregion

    #region Groups (Path-style: net/time/sync)

    /// <summary>
    /// Group for network time synchronization tasks.
    /// </summary>
    public static readonly System.String TimeSyncGroup =
        TaskNames.Groups.Build(Segments.Net, Segments.Time, Segments.Sync);

    /// <summary>
    /// Group for timing wheel scheduler tasks.
    /// </summary>
    public static readonly System.String TimingWheelGroup =
        TaskNames.Groups.Build(Segments.Net, Segments.Time, Segments.Wheel);

    /// <summary>
    /// Group for packet dispatching operations.
    /// </summary>
    public static readonly System.String PacketDispatchGroup =
        TaskNames.Groups.Build(Segments.Net, Segments.Packet, Segments.Dispatch);

    /// <summary>
    /// Group for TCP-level tasks associated with a specific port.
    /// </summary>
    public static System.String TcpGroup(System.Int32 port) =>
        TaskNames.Groups.Build(Segments.Net, Segments.Tcp, port.ToString());

    /// <summary>
    /// Group for TCP processing workers at a specific port.
    /// </summary>
    public static System.String TcpProcessGroup(System.Int32 port) =>
        TaskNames.Groups.Build(Segments.Net, Segments.Tcp, port.ToString(), Segments.Process);

    /// <summary>
    /// Group for UDP-related tasks at a specific port (e.g., <c>net/udp/7777</c>).
    /// </summary>
    public static System.String UdpGroup(System.Int32 port) =>
        TaskNames.Groups.Build(Segments.Net, Segments.Udp, port.ToString());

    /// <summary>
    /// Group for UDP processing workers at a specific port
    /// (e.g., <c>net/udp/7777/proc</c>).
    /// </summary>
    public static System.String UdpProcessGroup(System.Int32 port) =>
        TaskNames.Groups.Build(Segments.Net, Segments.Udp, port.ToString(), Segments.Process);

    #endregion

    #region Workers (Dot-style: tcp.accept.8080.0)

    /// <summary>
    /// Worker name for TCP acceptor loop (per port and worker index).
    /// </summary>
    public static System.String TcpAcceptWorker(System.Int32 port, System.Int32 index) =>
        TaskNames.Workers.Build(Segments.Tcp, Segments.Accept, port.ToString(), index.ToString());

    /// <summary>
    /// Worker name for TCP packet processing.
    /// </summary>
    public static System.String TcpProcessWorker(System.Int32 port, System.String id) =>
        TaskNames.Workers.Build(Segments.Tcp, Segments.Process, port.ToString(), TaskNames.Safe(id));

    /// <summary>
    /// Worker name for UDP processing loop (no extra id),
    /// e.g., <c>udp.proc.7777</c>.
    /// </summary>
    public static System.String UdpProcessWorker(System.Int32 port) =>
        TaskNames.Workers.Build(Segments.Udp, Segments.Process, port.ToString());

    /// <summary>
    /// Worker name for UDP processing loop with a custom id
    /// (safe-encoded), e.g., <c>udp.proc.7777.shardA</c>.
    /// </summary>
    public static System.String UdpProcessWorker(System.Int32 port, System.Int32 id) =>
        TaskNames.Workers.Build(Segments.Udp, Segments.Process, port.ToString(), id.ToString());

    /// <summary>
    /// Worker name for UDP receive worker by index,
    /// e.g., <c>udp.recv.7777.0</c>.
    /// </summary>
    public static System.String UdpReceiveWorker(System.Int32 port, System.Int32 index) =>
        TaskNames.Workers.Build(Segments.Udp, Segments.Receive, port.ToString(), index.ToString());

    /// <summary>
    /// Worker name for periodic time synchronization task.
    /// </summary>
    public static System.String TimeSyncWorker(System.TimeSpan period) =>
        $"{Segments.Time}.{Segments.Sync}.{period.TotalMilliseconds:0.#}ms";

    /// <summary>
    /// Worker name for timing wheel tick handler.
    /// </summary>
    public static System.String TimingWheelWorker(System.Int32 tickIntervalMs, System.Int32 wheelSize) =>
        $"{Segments.Time}.{Segments.Wheel}.{tickIntervalMs}ms.w{wheelSize}";

    /// <summary>
    /// Worker name for packet dispatching loop.
    /// </summary>
    public static readonly System.String PacketDispatchWorker =
        TaskNames.Workers.Build(Segments.Packet, Segments.Dispatch);

    #endregion
}
