// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;
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
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is System.String data)
        {
            System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(data);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(StringReturnHandler<>)}] " +
                                           $"handle-string bytes={byteCount} candidates={UTF8_STRING.Candidates.Length}");

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    var pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, data);
                        System.Byte[] buffer = c.Serialize(pkt);

                        if (context?.Connection?.TCP == null)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[{nameof(StringReturnHandler<>)}] send-failed transport=null");
                            return;
                        }

                        try
                        {
                            System.Boolean sent = await context.Connection.TCP.SendAsync(buffer).ConfigureAwait(false);
                            if (!sent)
                            {
                                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                        .Warn($"[{nameof(StringReturnHandler<>)}] send-failed");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"[{nameof(StringReturnHandler<>)}] error-serializing", ex);
                        }

                        return;
                    }
                    finally
                    {
                        c.Return(pkt);
                    }
                }
            }

            // 2) Fallback: chunk by UTF-8 byte limit using the largest candidate.
            if (context?.Connection?.TCP == null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(StringReturnHandler<>)}] send-failed transport=null");
                return;
            }

            Candidate max = UTF8_STRING.Candidates[^1];
            foreach (System.String part in UTF8_STRING.Split(data, max.MaxBytes))
            {
                var pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    System.Byte[] buffer = max.Serialize(pkt);

                    try
                    {
                        System.Boolean sent = await context.Connection.TCP.SendAsync(buffer).ConfigureAwait(false);
                        if (!sent)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[{nameof(StringReturnHandler<>)}] send-failed msg=chunk");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[{nameof(StringReturnHandler<>)}] error-serializing msg=chunk", ex);
                    }
                }
                finally
                {
                    max.Return(pkt);
                }
            }
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(StringReturnHandler<>)}] " +
                                       $"unsupported-result type={result?.GetType().Name ?? "null"}");
    }
}

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

internal static class UTF8_STRING
{
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

    /// <summary>
    /// Splits a string into segments that do not exceed a given UTF-8 byte limit,
    /// preserving Unicode rune boundaries.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <param name="byteLimit">Maximum bytes per segment (UTF-8).</param>
    /// <returns>An enumerable of segments.</returns>
    internal static System.Collections.Generic.IEnumerable<System.String> Split(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String s,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Int32 byteLimit)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLimit);

        if (s.Length == 0)
        {
            yield return System.String.Empty;
            yield break;
        }

        System.Text.Encoder encoder = System.Text.Encoding.UTF8.GetEncoder();
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
                    if (!Measure(ch, ref bytesUsed, byteLimit, encoder))
                    {
                        break;
                    }

                    i++;
                }
                else
                {
                    // Surrogate pair rune
                    System.Span<System.Char> ch2 = [s[i], s[i + 1]];
                    if (!Measure(ch2, ref bytesUsed, byteLimit, encoder))
                    {
                        break;
                    }

                    i += 2;
                }
            }

            yield return s[start..i];
            encoder.Reset();
        }


        // Local: simulate encoding to check size incrementally without allocating strings.
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static System.Boolean Measure(
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.ReadOnlySpan<System.Char> chars,
            [System.Diagnostics.CodeAnalysis.DisallowNull] ref System.Int32 used,
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.Int32 limit,
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.Text.Encoder enc)
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