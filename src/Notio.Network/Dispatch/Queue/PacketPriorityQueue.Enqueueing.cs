using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    /// <summary>
    /// Add a packet to the queue
    /// </summary>
    /// <param name="packet">Packet to add</param>
    /// <returns>True if added successfully, False if the queue is full</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Enqueue(TPacket packet)
    {
        if (packet == null)
            return false;

        int priorityIndex = (int)packet.Priority;

        if (_options.MaxCapacity > 0 && _totalCount >= _options.MaxCapacity)
            return false;

        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _priorityCounts[priorityIndex]);

            if (_options.EnableMetrics)
                Interlocked.Increment(ref _enqueuedCounts[priorityIndex]);

            return true;
        }

        return false;
    }
}
