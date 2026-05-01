// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Client.Services;

/// <summary>
/// Manages On&lt;T&gt; / OnOnce&lt;T&gt; subscriptions.
/// Single responsibility: subscription lifecycle.
/// </summary>
internal sealed class SubscriptionManager : IDisposable
{
    private readonly ClientSession _client;
    private readonly EventLog _log;

    private IDisposable? _controlSub;
    private bool _controlSubActive;

    public bool IsControlSubActive => _controlSubActive;

    public SubscriptionManager(ClientSession client, EventLog log)
    {
        _client = client;
        _log = log;
    }

    /// <summary>Toggles the persistent On&lt;Control&gt; subscription.</summary>
    public void ToggleControlSubscription()
    {
        if (_controlSubActive)
        {
            this.StopControlSubscription();
            _log.Info("Control frame subscription stopped.");
        }
        else
        {
            _controlSub = _client.Session.On<Control>(ctrl =>
            {
                _log.Recv("CONTROL", $"type={ctrl.Type} seq={ctrl.SequenceId} reason={ctrl.Reason} ts={ctrl.Timestamp}");
            });
            _controlSubActive = true;
            _log.Success("Control frame subscription active — all incoming Control frames will be logged.");
        }
    }

    /// <summary>Registers a one-shot subscription for a specific ControlType.</summary>
    public void RegisterOneShotSubscription(ControlType target)
    {
        _ = _client.Session.OnOnce<Control>(
            predicate: c => c.Type == target,
            handler: c => _log.Recv($"ONE-SHOT [{target}]", $"seq={c.SequenceId} reason={c.Reason}"));

        _log.Info($"One-shot subscription registered — will fire once when {target} arrives.");
    }

    /// <summary>Stops the persistent subscription (idempotent).</summary>
    public void StopControlSubscription()
    {
        IDisposable? sub = Interlocked.Exchange(ref _controlSub, null);
        sub?.Dispose();
        _controlSubActive = false;
    }

    public void Dispose() => this.StopControlSubscription();
}
