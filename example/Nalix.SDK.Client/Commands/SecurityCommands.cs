// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Transport.Extensions;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>Handshake, session resume, and cipher rotation commands.</summary>
internal sealed class SecurityCommands
{
    private readonly ClientSession _client;
    private readonly StatusBar _status;
    private readonly EventLog _log;

    public SecurityCommands(ClientSession client, StatusBar status, EventLog log)
    {
        _client = client;
        _status = status;
        _log = log;
    }

    public async Task HandshakeAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        string? pinnedKey = _client.Session.Options.ServerPublicKey;
        if (string.IsNullOrEmpty(pinnedKey))
        {
            _log.Warn("ServerPublicKey is not set in TransportOptions — handshake will fail for security reasons.");
            _log.Info("Tip: Set Options.ServerPublicKey to the server's X25519 public key (hex) to enable handshake.");
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .SpinnerStyle(Style.Parse("mediumpurple1"))
            .StartAsync("Performing X25519 handshake...", async _ =>
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    await _client.Session.HandshakeAsync().ConfigureAwait(false);
                    _log.Success("Handshake complete! Session is now encrypted.");
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Handshake failed: {ex.Message}");
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }).ConfigureAwait(false);
    }

    public async Task ResumeSessionAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("mediumpurple1"))
            .StartAsync("Attempting session resume...", async _ =>
            {
                try
                {
                    ProtocolReason reason = await _client.Session.ResumeSessionAsync().ConfigureAwait(false);
                    if (reason == ProtocolReason.NONE)
                    {
                        _log.Success("Session resumed successfully! Encryption active.");
                    }
                    else
                    {
                        _log.Warn($"Resume failed: {reason}. You may need to do a full handshake.");
                    }
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Resume error: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    public async Task UpdateCipherAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        CipherSuiteType chosen = AnsiConsole.Prompt(
            new SelectionPrompt<CipherSuiteType>()
                .Title("[steelblue1]Select new cipher suite:[/]")
                .AddChoices(Enum.GetValues<CipherSuiteType>()));

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .SpinnerStyle(Style.Parse("gold1"))
            .StartAsync($"Rotating cipher to {chosen}...", async _ =>
            {
                try
                {
                    await _client.Session.UpdateCipherAsync(chosen).ConfigureAwait(false);
                    _log.Success($"Cipher updated to {chosen}.");
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Cipher update failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    private bool RequireConnected()
    {
        if (_client.IsConnected)
        {
            return true;
        }

        _log.Error("Not connected — use [Connect] first.");
        return false;
    }
}
