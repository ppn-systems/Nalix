// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalix.Chat.Client.Core.Networking;
using Nalix.Chat.Client.Core.State;
using Nalix.Chat.Contracts.Events;
using Nalix.Chat.Contracts.Packets;

namespace Nalix.Chat.Client.Avalonia.ViewModels;

/// <summary>
/// View model for active chat room conversation.
/// </summary>
public sealed partial class ChatRoomViewModel : ObservableObject, IAsyncDisposable
{
    private readonly INetworkClient _networkClient;
    private readonly ChatStateStore _stateStore;
    private int _disposed;

    /// <summary>
    /// Initializes a chat room view model.
    /// </summary>
    public ChatRoomViewModel(INetworkClient networkClient, ChatStateStore stateStore)
    {
        _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));

        this.Messages = [];
        this.SendMessageCommand = new AsyncRelayCommand(this.SendMessageAsync, this.CanSendMessage);

        _networkClient.ConnectionStateChanged += this.OnConnectionStateChanged;
        _networkClient.MessageReceived += this.OnMessageReceived;
    }

    /// <summary>
    /// Gets mutable message collection bound to UI.
    /// </summary>
    public ObservableCollection<ChatMessageItemViewModel> Messages { get; }

    /// <summary>
    /// Gets send-message command.
    /// </summary>
    public IAsyncRelayCommand SendMessageCommand { get; }

    [ObservableProperty]
    public partial string HeaderTitle { get; set; } = "Room: (not selected)";

    [ObservableProperty]
    public partial string ActiveRoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ParticipantId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DraftMessage { get; set; } = string.Empty;

    /// <summary>
    /// Updates active room and reloads message snapshot.
    /// </summary>
    public void SetRoom(string roomId)
    {
        this.ActiveRoomId = roomId;
        this.HeaderTitle = string.IsNullOrWhiteSpace(roomId) ? "Room: (not selected)" : $"Room: {roomId}";

        IReadOnlyList<ChatMessageBroadcast> snapshot = _stateStore.GetMessages(roomId);

        Dispatcher.UIThread.Post(() =>
        {
            this.Messages.Clear();

            foreach (ChatMessageBroadcast broadcast in snapshot)
            {
                this.Messages.Add(ChatMessageItemViewModel.FromBroadcast(broadcast));
            }
        });

        this.SendMessageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Updates participant id used by send requests.
    /// </summary>
    public void SetParticipant(string participantId)
    {
        this.ParticipantId = participantId;
        this.SendMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnDraftMessageChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    private bool CanSendMessage()
    {
        return _networkClient.ConnectionState == ConnectionState.Connected
            && !string.IsNullOrWhiteSpace(this.ActiveRoomId)
            && !string.IsNullOrWhiteSpace(this.ParticipantId)
            && !string.IsNullOrWhiteSpace(this.DraftMessage);
    }

    private async Task SendMessageAsync()
    {
        if (!this.CanSendMessage())
        {
            return;
        }

        string message = this.DraftMessage;
        await this.SetDraftMessageAsync(string.Empty).ConfigureAwait(false);

        try
        {
            ChatMessageAck ack = await _networkClient.SendMessageAsync(
                this.ActiveRoomId,
                this.ParticipantId,
                message).ConfigureAwait(false);

            if (ack.Accepted)
            {
                ChatMessageBroadcast localBroadcast = new()
                {
                    RoomId = this.ActiveRoomId,
                    ServerMessageId = ack.ServerMessageId,
                    SenderId = this.ParticipantId,
                    SenderDisplayName = this.ParticipantId,
                    Message = message,
                    ServerTimestampUnixMs = ack.ServerTimestampUnixMs
                };

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _stateStore.AppendMessage(localBroadcast);
                    this.Messages.Add(ChatMessageItemViewModel.FromBroadcast(localBroadcast));
                });
            }
            else
            {
                await this.SetDraftMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch
        {
            await this.SetDraftMessageAsync(message).ConfigureAwait(false);
            throw;
        }
    }

    private void OnMessageReceived(object? sender, ChatMessageBroadcast broadcast)
    {
        _stateStore.AppendMessage(broadcast);

        if (!string.Equals(broadcast.RoomId, this.ActiveRoomId, StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            this.Messages.Add(ChatMessageItemViewModel.FromBroadcast(broadcast));
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        Dispatcher.UIThread.Post(() => this.SendMessageCommand.NotifyCanExecuteChanged());
    }

    private Task SetDraftMessageAsync(string value)
        => Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.DraftMessage = value;
            this.SendMessageCommand.NotifyCanExecuteChanged();
        }).GetTask();

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        _networkClient.ConnectionStateChanged -= this.OnConnectionStateChanged;
        _networkClient.MessageReceived -= this.OnMessageReceived;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// UI projection of a chat message row.
/// </summary>
public sealed class ChatMessageItemViewModel
{
    /// <summary>
    /// Gets sender display name.
    /// </summary>
    public string SenderDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets message text.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets local timestamp text.
    /// </summary>
    public string TimestampText { get; init; } = string.Empty;

    /// <summary>
    /// Creates a message row from broadcast packet.
    /// </summary>
    public static ChatMessageItemViewModel FromBroadcast(ChatMessageBroadcast broadcast)
    {
        DateTimeOffset local = DateTimeOffset.FromUnixTimeMilliseconds(broadcast.ServerTimestampUnixMs).ToLocalTime();

        return new ChatMessageItemViewModel
        {
            SenderDisplayName = broadcast.SenderDisplayName,
            Message = broadcast.Message,
            TimestampText = local.ToString("HH:mm:ss")
        };
    }
}
