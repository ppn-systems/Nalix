// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Shared.Injection;

namespace Nalix.SDK.Remote.Extensions;

/// <summary>
/// Provides extensions to enable or disable a background receive pump that continuously
/// reads packets from the network stream and enqueues them into <see cref="ReliableClient.Incoming"/>.
/// </summary>
/// <remarks>
/// The receive pump runs on a background task and is resilient to transient errors.
/// It uses a <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey, TValue}"/> to keep
/// per-client state without leaking memory once the <see cref="ReliableClient"/> is no longer referenced.
/// </remarks>
/// <seealso cref="ReliableClient"/>
/// <seealso cref="IPacket"/>
public static class ReceivePumpExtensions
{
    /// <summary>
    /// Internal per-client state for the receive pump.
    /// </summary>
    private sealed class PumpState
    {
        /// <summary>Background task that drives the receive loop.</summary>
        public System.Threading.Tasks.Task Loop { get; set; }

        /// <summary>Cancellation token source controlling the loop.</summary>
        public System.Threading.CancellationTokenSource Cancellation { get; set; }
    }

    // Per-client state storage
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReliableClient, PumpState> _states = [];

    /// <summary>
    /// Starts a background loop that receives packets via <see cref="ReliableClient.ReceiveAsync(System.Threading.CancellationToken)"/>
    /// and pushes them into <see cref="ReliableClient.Incoming"/>.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="receiveDelayMs">
    /// Optional short delay (in milliseconds) between failed iterations to avoid tight CPU loops
    /// when transient errors occur. Use <c>0</c> to disable the backoff.
    /// </param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is not connected.</exception>
    /// <remarks>
    /// Calling this method multiple times is idempotent; if the pump is already running, the call is ignored.
    /// </remarks>
    /// <example>
    /// <code>
    /// client.EnablePump(receiveDelayMs: 5);
    /// // ...later...
    /// await client.DisablePumpAsync();
    /// </code>
    /// </example>
    public static void EnablePump(this ReliableClient client, System.Int32 receiveDelayMs = 0)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            throw new System.InvalidOperationException("Client must be connected before starting the receive pump.");
        }

        var state = _states.GetOrCreateValue(client);
        if (state.Loop is { IsCompleted: false })
        {
            // Already running
            return;
        }

        // (Re)create cancellation source
        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        state.Cancellation = new System.Threading.CancellationTokenSource();
        var token = state.Cancellation.Token;

        state.Loop = System.Threading.Tasks.Task.Run(async () =>
        {
            var log = InstanceManager.Instance.GetExistingInstance<ILogger>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Receive one packet from the stream (throws on cancellation)
                    IPacket packet = await client.ReceiveAsync(token).ConfigureAwait(false);

                    // Enqueue to FIFO (caller consumes elsewhere)
                    client.Incoming.Push(packet);
                }
                catch (System.OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (System.ObjectDisposedException)
                {
                    // Stream/client disposed: exit the loop
                    log?.Debug("Receive pump stopped: underlying stream disposed.");
                    break;
                }
                catch (System.IO.EndOfStreamException)
                {
                    // Remote closed: exit the loop
                    log?.Info("Receive pump stopped: end of stream.");
                    break;
                }
                catch (System.Exception ex)
                {
                    // Transient error: log and optionally back off
                    log?.Warn("Receive pump error: {0}", ex.Message);
                    if (receiveDelayMs > 0)
                    {
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(receiveDelayMs, token).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore cancellation here
                        }
                    }
                }
            }
        }, token);
    }

    /// <summary>
    /// Stops the background receive pump and optionally awaits its completion.
    /// </summary>
    /// <param name="client">The reliable client.</param>
    /// <param name="waitForStop">
    /// If <c>true</c>, awaits the running loop to finish; otherwise only signals cancellation.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This method is safe to call multiple times. If the pump is not running, the call is a no-op.
    /// </remarks>
    /// <example>
    /// <code>
    /// await client.DisablePumpAsync(waitForStop: true);
    /// </code>
    /// </example>
    public static async System.Threading.Tasks.Task DisablePumpAsync(
        this ReliableClient client,
        System.Boolean waitForStop = true)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!_states.TryGetValue(client, out var state))
        {
            return;
        }

        try
        {
            state.Cancellation?.Cancel();

            if (waitForStop && state.Loop is not null)
            {
                try
                {
                    await state.Loop.ConfigureAwait(false);
                }
                catch
                {
                    // Swallow exceptions during shutdown to keep teardown robust
                }
            }
        }
        finally
        {
            state.Cancellation?.Dispose();
            state.Cancellation = null;
            state.Loop = null;

            // Remove per-client state so a future EnablePump() starts fresh
            _ = _states.Remove(client);
        }
    }
}
