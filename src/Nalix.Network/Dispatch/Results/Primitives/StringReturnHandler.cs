// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;

namespace Nalix.Network.Dispatch.Results.Primitives;

/// <summary>
/// Selects the smallest text-packet type that fits a UTF-8 payload and sends it.
/// Falls back to chunking when no single packet can hold the entire content.
/// </summary>
/// <remarks>
/// - Chooses the minimal packet size to avoid memory waste.
/// - Splits on Unicode rune boundaries (no broken multi-byte characters).
/// - Works with any registered packet types (e.g., TEXT256, TEXT512, TEXT1024).
/// </remarks>
internal sealed class StringReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Internal registry describing how to rent/return/operate on a concrete text packet type.
    /// </summary>
    internal sealed class Candidate
    {
        public required System.String Name;
        public required System.Int32 MaxBytes;
        public required System.Func<System.Object> Rent;
        public required System.Action<System.Object> Return;
        public required System.Func<System.Object, System.Byte[]> Serialize;
        public required System.Action<System.Object, System.String> Initialize;
    }

    // TODO: Add or remove candidates here to match what you ship in Shared.
    // Order matters: smallest first.
    internal static readonly Candidate[] Candidates =
    [
        new Candidate
        {
            Name = nameof(Text256),
            MaxBytes = Text256.DynamicSize,
            Rent = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Get<Text256>,
            Return = o => InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return((Text256)o),
            Initialize = (o, s) => ((Text256)o).Initialize(s),
            Serialize = o => ((Text256)o).Serialize(),
        },
        new Candidate
        {
            Name = nameof(Text512),
            MaxBytes = Text512.DynamicSize,
            Rent = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Get<Text512>,
            Return = o => InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return((Text512)o),
            Initialize = (o, s) => ((Text512)o).Initialize(s),
            Serialize = o => ((Text512)o).Serialize(),
        },
        new Candidate
        {
            Name = nameof(Text1024),
            MaxBytes = Text1024.DynamicSize,
            Rent = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Get<Text1024>,
            Return = o => InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return((Text1024)o),
            Initialize = (o, s) => ((Text1024)o).Initialize(s),
            Serialize = o => ((Text1024)o).Serialize(),
        },
    ];

    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.String data)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(data);

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?.Trace(
                $"[{nameof(StringReturnHandler<TPacket>)}] " +
                $"Handling string return with {byteCount} bytes. " +
                $"Using {Candidates.Length} candidates for serialization.");
#endif

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    var pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, data);
                        System.Byte[] buffer = c.Serialize(pkt);
                        _ = await context.Connection.Tcp.SendAsync(buffer)
                                                        .ConfigureAwait(false);
                        return;
                    }
                    finally
                    {
                        c.Return(pkt);
                    }
                }
            }

            // 2) Fallback: chunk by UTF-8 byte limit using the largest candidate.
            Candidate max = Candidates[^1];
            foreach (System.String part in SplitUtf8ByBytes(data, max.MaxBytes))
            {
                var pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);
                    _ = await context.Connection.Tcp.SendAsync(buffer)
                                                    .ConfigureAwait(false);
                }
                finally
                {
                    max.Return(pkt);
                }
            }
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(StringReturnHandler<TPacket>)}] " +
                                       $"Received unsupported result type '{result?.GetType().Name ?? "null"}'. " +
                                       $"Result will not be processed, but stored in context properties.");
    }

    /// <summary>
    /// Splits a string into segments that do not exceed a given UTF-8 byte limit,
    /// preserving Unicode rune boundaries.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <param name="byteLimit">Maximum bytes per segment (UTF-8).</param>
    /// <returns>An enumerable of segments.</returns>
    internal static System.Collections.Generic.IEnumerable<System.String> SplitUtf8ByBytes(
        System.String s, System.Int32 byteLimit)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLimit);

        if (s.Length == 0)
        {
            yield return System.String.Empty;
            yield break;
        }

        var encoder = System.Text.Encoding.UTF8.GetEncoder();
        var byteBuffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(byteLimit);
        try
        {
            System.Int32 i = 0;
            while (i < s.Length)
            {
                System.Int32 start = i;
                System.Int32 bytesUsed = 0;

                // Accumulate runes until the next one would exceed the byte limit.
                while (i < s.Length)
                {
                    // Get the next rune (safe on surrogate pairs).
                    if (!System.Char.IsSurrogatePair(s, i))
                    {
                        // Single char rune
                        System.Span<System.Char> ch = [s[i]];
                        if (!TryMeasure(ch, ref bytesUsed, byteLimit, encoder))
                        {
                            break;
                        }

                        i += 1;
                    }
                    else
                    {
                        // Surrogate pair rune
                        System.Span<System.Char> ch2 = [s[i], s[i + 1]];
                        if (!TryMeasure(ch2, ref bytesUsed, byteLimit, encoder))
                        {
                            break;
                        }

                        i += 2;
                    }
                }

                yield return s[start..i];
                encoder.Reset();
            }
        }
        finally
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(byteBuffer, clearArray: true);
        }

        // Local: simulate encoding to check size incrementally without allocating strings.
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static System.Boolean TryMeasure(
            System.ReadOnlySpan<System.Char> chars,
            ref System.Int32 used, System.Int32 limit, System.Text.Encoder enc)
        {
            enc.Convert(chars, [], flush: false,
                        out System.Int32 charsUsed, out System.Int32 bytesUsed, out System.Boolean completed);
            // Re-run with a real buffer of the remaining space to get accurate bytes.
            System.Int32 remaining = limit - used;
            if (remaining <= 0)
            {
                return false;
            }

            System.Span<System.Byte> tmp = stackalloc System.Byte[System.Math.Min(remaining, 8)]; // small probe
            enc.Convert(chars, tmp, flush: false,
                        out _, out System.Int32 b2, out _);

            if (used + b2 > limit)
            {
                return false;
            }

            used += b2;
            return true;
        }
    }
}