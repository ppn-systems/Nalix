// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.TextFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Routing.Results.Primitives;

/// <summary>
/// Selects the smallest text-packet type that fits a UTF-8 payload and sends it.
/// Falls back to chunking when no single packet can hold the entire content.
/// </summary>
/// <typeparam name="TPacket"></typeparam>
/// <remarks>
/// - Chooses the minimal packet size to avoid memory waste.
/// - Splits on Unicode rune boundaries (no broken multi-byte characters).
/// - Works with any registered packet types (e.g., TEXT256, TEXT512, TEXT1024).
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class StringReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is string data)
        {
            int byteCount = Encoding.UTF8.GetByteCount(data);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(StringReturnHandler<>)}] " +
                                           $"handle-string bytes={byteCount} candidates={UTF8_STRING.Candidates.Length}");

            // 1) Try to fit in a single packet (choose the smallest that fits).
            foreach (Candidate c in UTF8_STRING.Candidates)
            {
                if (byteCount <= c.MaxBytes)
                {
                    object pkt = c.Rent();
                    try
                    {
                        c.Initialize(pkt, data);
                        byte[] buffer = c.Serialize(pkt);

                        if (context?.Connection?.TCP == null)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[{nameof(StringReturnHandler<>)}] send-failed transport=null");
                            return;
                        }

                        try
                        {
                            await context.Connection.TCP.SendAsync(buffer).ConfigureAwait(false);
                        }
                        catch (Exception ex)
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
            foreach (string part in UTF8_STRING.Split(data, max.MaxBytes))
            {
                object pkt = max.Rent();
                try
                {
                    max.Initialize(pkt, part);
                    byte[] buffer = max.Serialize(pkt);

                    try
                    {
                        await context.Connection.TCP.SendAsync(buffer).ConfigureAwait(false);
                    }
                    catch (Exception ex)
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
    public required string Name;
    public required int MaxBytes;
    public required Func<object> Rent;
    public required Action<object> Return;
    public required Func<object, byte[]> Serialize;
    public required Action<object, string> Initialize;
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
    internal static IEnumerable<string> Split(
        [DisallowNull] string s,
        [DisallowNull] int byteLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLimit);

        if (s.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        Encoder encoder = Encoding.UTF8.GetEncoder();
        int i = 0;
        while (i < s.Length)
        {
            int start = i;
            int bytesUsed = 0;

            // Accumulate runes until the next one would exceed the byte limit.
            while (i < s.Length)
            {
                // Get the next rune (safe on surrogate pairs).
                if (!char.IsSurrogatePair(s, i))
                {
                    // Single char rune
                    Span<char> ch = [s[i]];
                    if (!Measure(ch, ref bytesUsed, byteLimit, encoder))
                    {
                        break;
                    }

                    i++;
                }
                else
                {
                    // Surrogate pair rune
                    Span<char> ch2 = [s[i], s[i + 1]];
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Measure(
            [DisallowNull] ReadOnlySpan<char> chars,
            [DisallowNull] ref int used,
            [DisallowNull] int limit,
            [DisallowNull] Encoder enc)
        {
            enc.Convert(chars, [], flush: false,
                        out int charsUsed, out int bytesUsed, out bool completed);
            // Re-run with a real buffer of the remaining space to get accurate bytes.
            int remaining = limit - used;
            if (remaining <= 0)
            {
                return false;
            }

            Span<byte> tmp = stackalloc byte[Math.Min(remaining, 8)]; // small probe
            enc.Convert(chars, tmp, flush: false,
                        out _, out int b2, out _);

            if (used + b2 > limit)
            {
                return false;
            }

            used += b2;
            return true;
        }
    }
}
