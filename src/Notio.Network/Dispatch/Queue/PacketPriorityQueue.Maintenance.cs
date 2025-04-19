using Notio.Common.Package.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

    public int Clear(PacketPriority priority)
    {
        int index = (int)priority;
        int cleared = DisposeRemainingPackets(_priorityChannels[index].Reader);

        if (cleared > 0)
        {
            Interlocked.Add(ref _totalCount, -cleared);
            Interlocked.Exchange(ref _priorityCounts[index], 0);
            if (_options.CollectStatistics)
                ResetStatistics(index);
        }

        return cleared;
    }

    public Task<int> RemoveExpiredPacketsAsync() => RemoveExpiredPacketsInternalAsync();

    public void Clear() => ClearInternal();

    public Task StartExpirationCheckerAsync(TimeSpan interval, CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                    await RemoveExpiredPacketsAsync();
                }
                catch (OperationCanceledException) { break; }
                catch { /* Optional: Logging */ }
            }
        }, cancellationToken);

    #endregion

    #region Private Methods

    private async Task<int> RemoveExpiredPacketsInternalAsync()
    {
        int totalExpired = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            var reader = _priorityChannels[i].Reader;
            var writer = _priorityChannels[i].Writer;
            var temp = new Queue<TPacket>();

            while (reader.TryRead(out var packet))
            {
                if (packet.IsExpired(_options.PacketTimeout))
                {
                    packet.Dispose();
                    totalExpired++;
                    if (_options.CollectStatistics) _expiredCounts[i]++;
                }
                else
                {
                    temp.Enqueue(packet);
                }
            }

            while (temp.TryDequeue(out var p))
                await writer.WriteAsync(p);
        }

        if (totalExpired > 0)
            Interlocked.Add(ref _totalCount, -totalExpired);

        return totalExpired;
    }

    private void ClearInternal()
    {
        int totalCleared = 0;

        for (int i = 0; i < _priorityCount; i++)
            totalCleared += DisposeRemainingPackets(_priorityChannels[i].Reader);

        if (totalCleared > 0)
            Interlocked.Exchange(ref _totalCount, 0);
    }

    private int DisposeRemainingPackets(ChannelReader<TPacket> reader)
    {
        int count = 0;
        while (reader.TryRead(out var packet))
        {
            packet.Dispose();
            count++;
        }
        return count;
    }

    private void ResetStatistics(int index)
    {
        _expiredCounts[index] = 0;
        _invalidCounts[index] = 0;
        _enqueuedCounts[index] = 0;
        _dequeuedCounts[index] = 0;
    }

    #endregion
}
