// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.SDK.Tools.Configuration;

/// <summary>
/// Stores localizable UI text for the Nalix SDK tools application.
/// </summary>
public sealed class PacketToolTextConfig : ConfigurationLoader
{
    public string AppWindowTitle { get; set; } = "Nalix TCP Packet Testing Tool";

    public string AppHeaderTitle { get; set; } = "TCP Packet Testing Tool";

    public string AppHeaderSubtitle { get; set; } = "Reflection-driven packet builder for Nalix IPacket packets";

    public string ThemeLabel { get; set; } = "Theme";

    public string ThemeLight { get; set; } = "Light";

    public string ThemeDark { get; set; } = "Dracula";

    public string TabPacketBuilder { get; set; } = "Packet Builder";

    public string TabSendHistory { get; set; } = "Send History";

    public string TabReceiveHistory { get; set; } = "Receive History";

    public string TabPacketRegistryBrowser { get; set; } = "Packet Registry Browser";

    public string TabLog { get; set; } = "Logging";

    public string GroupConnection { get; set; } = "Connection";

    public string LabelHost { get; set; } = "Host";

    public string LabelPort { get; set; } = "Port";

    public string ButtonConnect { get; set; } = "Connect";

    public string ButtonDisconnect { get; set; } = "Disconnect";

    public string ButtonHandshake { get; set; } = "Handshake";

    public string GroupPacketSelection { get; set; } = "Packet Selection";

    public string LabelType { get; set; } = "Type";

    public string ButtonLoadPacketDll { get; set; } = "Load Packet DLL";

    public string PacketSelectionHint { get; set; } = "Packet type is selected explicitly by MagicNumber identity.";

    public string PacketAssemblyDialogTitle { get; set; } = "Select packet assembly";

    public string PacketAssemblyDialogFilter { get; set; } = "Assembly files (*.dll)|*.dll|All files (*.*)|*.*";

    public string GroupActions { get; set; } = "Actions";

    public string ButtonReset { get; set; } = "Reset";

    public string ButtonSerialize { get; set; } = "Serialize";

    public string ButtonSend { get; set; } = "Send";

    public string ButtonRepeatSend { get; set; } = "Repeat Send";

    public string LabelRepeatCount { get; set; } = "Count";

    public string LabelRepeatDelay { get; set; } = "Delay ms";

    public string StatusRepeatSendStartedFormat { get; set; } = "Repeat send started: {0} times, {1} ms delay.";

    public string StatusRepeatSendFinishedFormat { get; set; } = "Repeat send finished: {0} packet(s) sent.";

    public string StatusRepeatSendCancelled { get; set; } = "Repeat send cancelled.";

    public string GroupBuilderNotes { get; set; } = "Builder Notes";

    public string GroupRepeatSend { get; set; } = "Repeat Send";

    public string GroupSentPackets { get; set; } = "Sent Packets";

    public string ButtonReopenSelectedPacket { get; set; } = "Reopen Selected Packet";

    public string GroupReceivedPackets { get; set; } = "Received Packets";

    public string ButtonInspectSelectedPacket { get; set; } = "Inspect Selected Packet";

    public string ButtonCopyHex { get; set; } = "Copy Hex";

    public string PacketLengthLabel { get; set; } = "Length";

    public string GroupRegisteredPackets { get; set; } = "Registered Packets";

    public string ButtonFavoritePacket { get; set; } = "Favorite";

    public string ButtonFavoritesOnly { get; set; } = "Favorites Only";

    public string LabelRegistrySearch { get; set; } = "Search";

    public string RegistrySearchPlaceholder { get; set; } = "Filter by name, namespace, or magic...";

    public string GroupLogEntries { get; set; } = "Activity Log";

    public string ButtonClearLog { get; set; } = "Clear Log";

    public string PlaceholderNoLogEntries { get; set; } = "No log entries yet.";

    public string LogDetailsTitle { get; set; } = "Entry Details";

    public string LogTimestampLabel { get; set; } = "Time";

    public string LogSourceLabel { get; set; } = "Source";

    public string LogMessageLabel { get; set; } = "Message";

    public string LogSourceSystem { get; set; } = "System";

    public string LogSourceBuilder { get; set; } = "Builder";

    public string LogSourceRegistry { get; set; } = "Registry";

    public string LogSourceTcp { get; set; } = "TCP";

    public string LogSourceHistory { get; set; } = "History";

    public string LogEntrySummaryFormat { get; set; } = "{0:HH:mm:ss} | {1} | {2}";

    public string GroupPacketDiff { get; set; } = "Packet Diff";

    public string ButtonComparePrevious { get; set; } = "Compare Previous";

    public string PlaceholderNoPreviousPacketForDiff { get; set; } = "Select a packet with at least one previous entry to compare.";

    public string PacketDiffSummaryFormat { get; set; } = "{0} vs {1} - {2:N0} differing byte(s).";

    public string PacketDiffNoDifferences { get; set; } = "No byte differences found.";

    public string PacketDiffLineFormat { get; set; } = "0x{0:X4}: {1} -> {2}";

    public string PacketDiffLengthLineFormat { get; set; } = "Length: {0:N0} -> {1:N0} bytes";

    public string PacketDiffDetailTitle { get; set; } = "Diff Details";

    public string RegistryPropertyHeader { get; set; } = "Property";

    public string RegistryTypeHeader { get; set; } = "Type";

    public string HexViewerTitle { get; set; } = "Hex Viewer";

    public string HexViewerHint { get; set; } = "You can copy this hex output or close this viewer.";

    public string ButtonCopy { get; set; } = "Copy";

    public string ButtonClose { get; set; } = "Close";

    public string PlaceholderNoPacketLoaded { get; set; } = "No packet loaded";

    public string PlaceholderCurrentPacketSummary { get; set; } = "Select a packet type in Packet Builder to begin editing.";

    public string PlaceholderSentPacketTitle { get; set; } = "Select a sent packet";

    public string PlaceholderSentPacketSummary { get; set; } = "Sent packet details will appear here.";

    public string PlaceholderReceivedPacketTitle { get; set; } = "Select a received packet";

    public string PlaceholderReceivedPacketSummary { get; set; } = "Received packet details will appear here.";

    public string StatusReady { get; set; } = "Ready.";

    public string StatusPortInvalid { get; set; } = "The port must be a valid unsigned 16-bit integer.";

    public string StatusPacketEditorReset { get; set; } = "Packet editor reset to a fresh instance.";

    public string StatusSerializedFormat { get; set; } = "Serialized {0} into {1:N0} bytes.";

    public string StatusSerializationFailed { get; set; } = "Serialization failed for the current packet.";

    public string StatusLoadedPacketBuilderFormat { get; set; } = "Loaded {0} into the packet builder. Packet identity is defined by MagicNumber.";

    public string StatusPacketAssemblyLoadedFormat { get; set; } = "Loaded packet assembly {0}. Registry now contains {1:N0} packet types.";

    public string StatusPacketAssemblyNoNewTypesFormat { get; set; } = "Loaded assembly {0}, but no new packet types were discovered.";

    public string StatusPacketAssemblyReconnectRequired { get; set; } = "New packet types were loaded. Reconnect to apply the updated registry to the active TCP session.";

    public string StatusPacketAssemblyLoadFailedFormat { get; set; } = "Unable to load packet assembly: {0}";

    public string StatusPacketAssemblyLoadFailedShort { get; set; } = "Unable to load packet assembly.";

    public string StatusLoadedReceivedSnapshotFormat { get; set; } = "Loaded received packet snapshot for {0} in read-only mode.";

    public string StatusReopenedSentSnapshotFormat { get; set; } = "Reopened sent packet snapshot for {0}.";

    public string StatusPacketTypeUnavailableFormat { get; set; } = "Packet type {0} is not available in the registry browser.";

    public string StatusUnableOpenSnapshotFormat { get; set; } = "Unable to open packet snapshot: {0}";

    public string StatusSentPacketSuccessFormat { get; set; } = "{0} sent successfully.";

    public string StatusReceivedPacketSuccessFormat { get; set; } = "{0} received ({1}).";

    public string StatusConnectedFormat { get; set; } = "Connected to {0}:{1}";

    public string StatusDisconnected { get; set; } = "Disconnected";

    public string StatusHandshakeStarted { get; set; } = "Handshake started...";

    public string StatusHandshakeSuccess { get; set; } = "Handshake completed successfully. Session is now encrypted.";

    public string StatusHandshakeFailedFormat { get; set; } = "Handshake failed: {0}";

    public string StatusTcpConnectionEstablished { get; set; } = "TCP connection established.";

    public string StatusTcpDisconnectedFormat { get; set; } = "Disconnected: {0}";

    public string StatusTcpErrorFormat { get; set; } = "TCP error: {0}";

    public string StatusTcpSessionNotConnected { get; set; } = "The TCP session is not connected.";

    public string StatusPacketSentFormat { get; set; } = "Sent {0} (0x{1:X4})";

    public string UnknownPacketName { get; set; } = "Unknown Packet";

    public string HistorySummaryFormat { get; set; } = "{0:HH:mm:ss}  0x{1:X4}  {2}  ({3:N0} bytes)";

    public string DetailSummaryFormat { get; set; } = "{0} | OpCode 0x{1:X4} | Magic 0x{2:X8} | Length {3:N0} bytes | {4}";

    public string BuilderSummaryFormat { get; set; } = "{0} | Magic 0x{1:X8} | OpCode 0x{2:X4} | Length {3:N0} bytes";

    public string RegistryDetailSummaryFormat { get; set; } = "{0} | Magic 0x{1:X8}";

    public string SerializedViewerTitleFormat { get; set; } = "Serialized: {0}";

    public string ReadOnlySuffix { get; set; } = "Read-only";

    public string DynamicFormEmptyMessage { get; set; } = "No serializable properties were discovered for this packet.";

    public string PropertyValueCannotBeEmptyFormat { get; set; } = "{0} cannot be empty.";

    public string PropertyTypeNotSupportedFormat { get; set; } = "Type {0} is not supported by the editor.";

    public string ByteArrayTypeName { get; set; } = "byte[]";

    public string HexBytesFormat { get; set; } = "{0:N0} bytes";

    public string HexHint { get; set; } = "Hex input accepts spaces and line breaks.";

    public string HexUpdated { get; set; } = "Hex value updated.";

    public string HexImportButton { get; set; } = "Import .bin";

    public string HexExportButton { get; set; } = "Export .bin";

    public string HexImportDialogTitle { get; set; } = "Import binary payload";

    public string HexExportDialogTitle { get; set; } = "Export binary payload";

    public string HexDialogFilter { get; set; } = "Binary files (*.bin)|*.bin|All files (*.*)|*.*";

    public string HexExportFileName { get; set; } = "payload.bin";

    public string HexLoadedFileStatusFormat { get; set; } = "Loaded {0:N0} bytes from {1}.";

    public string HexSavedFileStatusFormat { get; set; } = "Saved {0:N0} bytes to {1}.";
}
