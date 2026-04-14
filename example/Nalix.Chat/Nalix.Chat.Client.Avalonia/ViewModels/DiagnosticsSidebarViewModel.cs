// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using CommunityToolkit.Mvvm.ComponentModel;
using Nalix.Chat.Client.Core.Services;

namespace Nalix.Chat.Client.Avalonia.ViewModels;

/// <summary>
/// View model for diagnostics sidebar.
/// </summary>
public sealed partial class DiagnosticsSidebarViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string LatencyText { get; set; } = "0.0 ms";

    [ObservableProperty]
    public partial string DriftText { get; set; } = "0.0 ms";

    [ObservableProperty]
    public partial string ActiveCipher { get; set; } = "N/A";

    [ObservableProperty]
    public partial string RotationCounterText { get; set; } = "Rotation: 0";

    [ObservableProperty]
    public partial string SessionIdentifier { get; set; } = "Not connected";

    [ObservableProperty]
    public partial string ConnectionStateText { get; set; } = "Disconnected";

    /// <summary>
    /// Applies new diagnostics snapshot.
    /// </summary>
    public void Apply(DiagnosticsSnapshot snapshot)
    {
        this.LatencyText = $"{snapshot.RttMs:F1} ms";
        this.DriftText = $"{snapshot.DriftMs:F2} ms";
        this.ActiveCipher = snapshot.ActiveCipher.ToString();
        this.RotationCounterText = $"Rotation: {snapshot.CipherRotationCounter}";
        this.SessionIdentifier = string.IsNullOrWhiteSpace(snapshot.SessionIdentifier)
            ? "Not assigned"
            : snapshot.SessionIdentifier;
        this.ConnectionStateText = snapshot.ConnectionState.ToString();
    }
}
