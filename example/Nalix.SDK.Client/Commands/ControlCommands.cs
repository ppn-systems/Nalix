// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>SendControl, AwaitControl, and RequestAsync commands.</summary>
internal sealed class ControlCommands
{
    private readonly ClientSession _client;
    private readonly StatusBar _status;
    private readonly EventLog _log;

    public ControlCommands(ClientSession client, StatusBar status, EventLog log)
    {
        _client = client;
        _status = status;
        _log = log;
    }

    public async Task SendControlFrameAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        ControlType type = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Select ControlType to send:[/]")
                .PageSize(12)
                .AddChoices(Enum.GetValues<ControlType>()));

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await _client.Session.SendControlAsync(
                opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
                type: type).ConfigureAwait(false);

            _log.Send("CONTROL", $"type={type} opcode={ProtocolOpCode.SYSTEM_CONTROL}");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"SendControl failed: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task AwaitControlFrameAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        ControlType sendType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Send ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        ControlType awaitType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Await response ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        int timeoutMs = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Timeout (ms)?[/]").DefaultValue(5000));

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await _client.Session.SendControlAsync(
                opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
                type: sendType).ConfigureAwait(false);

            _log.Send("CONTROL", $"type={sendType} — awaiting {awaitType}...");

            using Control response = await _client.Session.AwaitControlAsync(
                predicate: c => c.Type == awaitType,
                timeoutMs: timeoutMs).ConfigureAwait(false);

            _log.Recv("CONTROL", $"type={response.Type} seq={response.SequenceId} reason={response.Reason}");
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error($"No {awaitType} response within {timeoutMs}ms.");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"AwaitControl failed: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task RequestResponseAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        ControlType sendType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Request (send) ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        ControlType expectType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Expected response ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        int timeoutMs = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Timeout (ms)?[/]").DefaultValue(5000));

        int retries = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Retry count?[/]").DefaultValue(0));

        bool encrypt = AnsiConsole.Confirm("[steelblue1]Encrypt?[/]", defaultValue: false);

        using Control request = _client.Session
            .NewControl((ushort)ProtocolOpCode.SYSTEM_CONTROL, sendType)
            .Build();

        RequestOptions opts = RequestOptions.Default
            .WithTimeout(timeoutMs)
            .WithRetry(retries);

        if (encrypt)
        {
            opts = opts.WithEncrypt();
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            _log.Send("REQUEST", $"type={sendType}  expect={expectType}  timeout={timeoutMs}ms  retry={retries}  enc={encrypt}");

            using Control response = await _client.Session.RequestAsync<Control>(
                request,
                options: opts,
                predicate: c => c.Type == expectType).ConfigureAwait(false);

            _log.Recv("RESPONSE", $"type={response.Type} seq={response.SequenceId} reason={response.Reason} ts={response.Timestamp}");
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error($"RequestAsync<Control> timed out after {timeoutMs}ms × {retries + 1} attempts.");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"RequestAsync error: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
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
