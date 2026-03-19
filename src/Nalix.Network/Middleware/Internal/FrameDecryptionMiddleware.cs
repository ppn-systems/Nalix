using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Shared.Caching;
using Nalix.Network.Abstractions;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Network.Middleware.Internal;

[MiddlewareOrder(-50)]
internal class FrameDecryptionMiddleware : INetworkBufferMiddleware
{
    public async System.Threading.Tasks.Task<IBufferLease> InvokeAsync(
        IBufferLease lease, IConnection connection, System.Threading.CancellationToken ct,
        System.Func<IBufferLease, System.Threading.CancellationToken, System.Threading.Tasks.Task<IBufferLease>> next)
    {
        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.ENCRYPTED))
        {
            BufferLease dest = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));

            if (!FrameTransformer.TryDecrypt(lease, dest, connection.Secret))
            {
                dest.Dispose();
                return null; // fallback if failed
            }

            dest.Span.WriteFlagsLE(lease.Span
                     .ReadFlagsLE()
                     .RemoveFlag(PacketFlags.ENCRYPTED));

            return await next(dest, ct).ConfigureAwait(false);
        }
        else
        {
            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}