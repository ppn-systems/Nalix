// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Configuration;

/// <summary>
/// Controls the behaviour of <see cref="RequestExtensions.RequestAsync{TResponse}(IClientConnection, IPacket, RequestOptions, System.Func{TResponse, System.Boolean}, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use the static factory <see cref="Default"/> for a ready-made instance,
/// or build one with the fluent helpers:
/// <code>
/// var opts = RequestOptions.Default
///     .WithTimeout(3_000)
///     .WithRetry(2)
///     .WithEncrypt();
/// </code>
/// </para>
/// <para>
/// <b>Retry semantics:</b> a retry is triggered only on <see cref="System.TimeoutException"/>.
/// Fatal errors (dropped connection, send failure) propagate immediately — they are never retried.
/// Each attempt gets its own <see cref="TimeoutMs"/> window; total wall-clock time is at most
/// <c>TimeoutMs × (RetryCount + 1)</c>.
/// </para>
/// </remarks>
public sealed record RequestOptions
{
    // ── Defaults ─────────────────────────────────────────────────────────────

    /// <summary>Default timeout in milliseconds (5 000 ms).</summary>
    public const System.Int32 DefaultTimeoutMs = 5_000;

    /// <summary>Ready-made instance with sensible defaults (no retry, no encrypt).</summary>
    public static RequestOptions Default { get; } = new();

    // ── Properties ───────────────────────────────────────────────────────────

    /// <summary>
    /// Milliseconds to wait for a response on each attempt.
    /// Use <c>0</c> to wait indefinitely (not recommended in production).
    /// </summary>
    public System.Int32 TimeoutMs { get; init; } = DefaultTimeoutMs;

    /// <summary>
    /// Number of additional attempts after the first one times out.
    /// <c>0</c> means try once and stop. Must be ≥ 0.
    /// </summary>
    public System.Int32 RetryCount { get; init; } = 0;

    /// <summary>
    /// When <see langword="true"/> the outbound frame is encrypted before transmission.
    /// Requires the client to be a <see cref="TcpSessionBase"/>.
    /// </summary>
    public System.Boolean Encrypt { get; init; } = false;

    // ── Validation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Throws <see cref="System.ArgumentOutOfRangeException"/> if <see cref="RetryCount"/> is negative.
    /// </summary>
    public void Validate()
    {
        if (RetryCount < 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(RetryCount), RetryCount, $"{nameof(RetryCount)} must be >= 0.");
        }
    }

    // ── Fluent builders ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="RequestOptions"/> with <see cref="TimeoutMs"/> changed.
    /// </summary>
    public RequestOptions WithTimeout(System.Int32 ms) => this with { TimeoutMs = ms };

    /// <summary>
    /// Returns a new <see cref="RequestOptions"/> with <see cref="RetryCount"/> changed.
    /// </summary>
    public RequestOptions WithRetry(System.Int32 count) => this with { RetryCount = count };

    /// <summary>
    /// Returns a new <see cref="RequestOptions"/> with <see cref="Encrypt"/> changed.
    /// </summary>
    public RequestOptions WithEncrypt(System.Boolean encrypt = true) => this with { Encrypt = encrypt };
}