// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalix.Chat.Client.Core.Networking;
using Nalix.Chat.Client.Core.Services;
using Nalix.Chat.Contracts.Packets;

namespace Nalix.Chat.Client.Avalonia.ViewModels;

/// <summary>
/// Main shell view model for the desktop chat client.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly INetworkClient _networkClient;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly CancellationTokenSource _shutdown = new();

    private Task? _diagnosticsTask;

    /// <summary>
    /// Initializes main window view model.
    /// </summary>
    public MainWindowViewModel(
        INetworkClient networkClient,
        DiagnosticsService diagnosticsService,
        ChatRoomViewModel chatRoom,
        DiagnosticsSidebarViewModel diagnostics)
    {
        _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));
        _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
        this.ChatRoom = chatRoom ?? throw new ArgumentNullException(nameof(chatRoom));
        this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        this.Rooms = ["general", "engineering", "security", "ops"];
        this.SelectedRoom = this.Rooms[0];
        this.ParticipantId = "user-001";
        this.DisplayName = "Operator";
        this.StatusMessage = "Disconnected";

        this.ConnectCommand = new AsyncRelayCommand(this.ConnectAsync);
        this.JoinRoomCommand = new AsyncRelayCommand(this.JoinRoomAsync);

        _networkClient.ConnectionStateChanged += this.OnConnectionStateChanged;
        _networkClient.Error += this.OnNetworkError;

        this.ChatRoom.SetRoom(this.SelectedRoom);
        this.ChatRoom.SetParticipant(this.ParticipantId);
    }

    /// <summary>
    /// Gets room identifiers list.
    /// </summary>
    public ObservableCollection<string> Rooms { get; }

    /// <summary>
    /// Gets room conversation view model.
    /// </summary>
    public ChatRoomViewModel ChatRoom { get; }

    /// <summary>
    /// Gets diagnostics sidebar view model.
    /// </summary>
    public DiagnosticsSidebarViewModel Diagnostics { get; }

    /// <summary>
    /// Gets connect command.
    /// </summary>
    public IAsyncRelayCommand ConnectCommand { get; }

    /// <summary>
    /// Gets join-room command.
    /// </summary>
    public IAsyncRelayCommand JoinRoomCommand { get; }

    [ObservableProperty]
    public partial string SelectedRoom { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ParticipantId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    partial void OnSelectedRoomChanged(string value)
    {
        ChatRoom.SetRoom(value);
    }

    partial void OnParticipantIdChanged(string value)
    {
        ChatRoom.SetParticipant(value);
    }

    private async Task ConnectAsync()
    {
        try
        {
            await this.SetStatusMessageAsync("Connecting...").ConfigureAwait(false);
            await _networkClient.ConnectAsync(_shutdown.Token).ConfigureAwait(false);
            await this.SetStatusMessageAsync("Connected").ConfigureAwait(false);
            this.EnsureDiagnosticsLoop();
        }
        catch (Exception ex)
        {
            await this.SetStatusMessageAsync($"Connect failed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task JoinRoomAsync()
    {
        try
        {
            if (_networkClient.ConnectionState != ConnectionState.Connected)
            {
                await this.ConnectAsync().ConfigureAwait(false);
            }

            JoinRoomResponse response = await _networkClient.JoinRoomAsync(
                this.SelectedRoom,
                this.ParticipantId,
                this.DisplayName,
                _shutdown.Token).ConfigureAwait(false);

            if (response.Succeeded)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.ChatRoom.SetRoom(response.RoomId);
                    this.ChatRoom.SetParticipant(this.ParticipantId);
                    this.StatusMessage = $"Joined '{response.RoomId}' as {response.DisplayName}";
                });
                this.EnsureDiagnosticsLoop();
                return;
            }

            await this.SetStatusMessageAsync($"Join failed: {response.ErrorCode} ({response.Message})").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await this.SetStatusMessageAsync($"Join failed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.StatusMessage = state switch
            {
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Connected => "Connected",
                ConnectionState.Reconnecting => "Reconnecting...",
                ConnectionState.Faulted => "Connection faulted",
                ConnectionState.Disconnected => "Disconnected",
                _ => "Disconnected"
            };
        });
    }

    private void OnNetworkError(object? sender, Exception exception)
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.StatusMessage = $"Network error: {exception.Message}";
        });
    }

    private Task SetStatusMessageAsync(string message)
    {
        Dispatcher.UIThread.Post(() => this.StatusMessage = message);
        return Task.CompletedTask;
    }

    private void EnsureDiagnosticsLoop()
    {
        if (_diagnosticsTask is { IsCompleted: false })
        {
            return;
        }

        _diagnosticsTask = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(3));

            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                try
                {
                    DiagnosticsSnapshot snapshot = await _diagnosticsService.CollectAsync(_shutdown.Token).ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() =>
                    {
                        this.Diagnostics.Apply(snapshot);
                    });
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        this.StatusMessage = $"Diagnostics error: {ex.Message}";
                    });
                }
            }
        }, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _networkClient.ConnectionStateChanged -= this.OnConnectionStateChanged;
        _networkClient.Error -= this.OnNetworkError;

        if (this.ChatRoom is IAsyncDisposable chatRoomDisposable)
        {
            await chatRoomDisposable.DisposeAsync().ConfigureAwait(false);
        }

        await _networkClient.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
