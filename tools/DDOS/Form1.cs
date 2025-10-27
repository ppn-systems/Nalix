// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using DDoS.Helpers;
using DDoS.Models;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.SDK.Transport;
using Nalix.Shared.Messaging;

namespace DDoS;

/// <summary>
/// Main form for DDOS application. This class handles only UI tasks; networking logic is managed by external services.
/// </summary>
public partial class Form1 : Form
{
    // Service fields
    private TCPConnectFlooder? _tcpFlooder;
    private ReliableClient? _reliableClient;
    private System.Windows.Forms.Timer? _statusTimer;

    // ── TCP Flood tab controls ───────────────────────────────────────────────
    private TextBox? txtTcpIp, txtTcpPort, txtTcpConn;
    private Button? btnTcpStart, btnTcpStop;
    private Label? lblFloodStatus;

    // ── Packet Sender – LEFT column ──────────────────────────────────────────
    private TextBox? txtSendIp, txtSendPort;
    private ComboBox? cmbPacketType;
    private NumericUpDown? nudOpCode;
    private ComboBox? cmbPriority;
    private ComboBox? cmbFlags;
    private ComboBox? cmbTransportProtocol;
    private TextBox? txtPacketContent;

    // ── Packet Sender – RIGHT column (Control/Directive only) ────────────────
    private Label? lblSequenceId, lblControlType, lblProtocolReason, lblProtocolAdvice;
    private Label? lblHandshakeHint;
    private NumericUpDown? nudSequenceId;
    private ComboBox? cmbControlType, cmbProtocolReason, cmbProtocolAdvice;
    private Button? btnToggleHexMode;
    private Boolean _handshakeHexMode = true;

    // ── Buttons & status ─────────────────────────────────────────────────────
    private Button? btnConnect, btnSendPacket, btnDisconnect, btnClearHistory;
    private Label? lblSendStatus, lblPacketInfo;
    private ListBox? lstPacketHistory, lstReceivedData;

    // Packet history
    private readonly List<PacketHistory> _packetHistory = [];

    /// <summary>Form constructor.</summary>
    public Form1()
    {
        InitializeComponent();
        InitializeMainUI();
    }

    /// <summary>Initializes the main UI including tab pages.</summary>
    private void InitializeMainUI()
    {
        this.BackColor = Color.FromArgb(150, 150, 150);
        this.Text = "DDoS Tool - Packet Sender";
        this.Size = new Size(900, 640);

        var tabControl = new TabControl { Dock = DockStyle.Fill };
        tabControl.TabPages.Add(CreateTcpFloodTab());
        tabControl.TabPages.Add(CreatePacketSendTab());
        this.Controls.Add(tabControl);
    }

    #region TCP Flood Tab

    /// <summary>Creates the TCP Flood tab.</summary>
    private TabPage CreateTcpFloodTab()
    {
        const Int32 CtrlWidth = 120;

        var tab = new TabPage("TCP - Flood") { BackColor = Color.FromArgb(200, 200, 200) };

        txtTcpIp = new() { Location = new Point(120, 10), Width = CtrlWidth, Text = "127.0.0.1" };
        txtTcpPort = new() { Location = new Point(120, 40), Width = CtrlWidth, Text = "57206" };
        txtTcpConn = new() { Location = new Point(120, 70), Width = CtrlWidth };
        btnTcpStart = new() { Text = "Start TCP Flood", Location = new Point(10, 110), Width = 120 };
        btnTcpStop = new() { Text = "Stop", Location = new Point(140, 110), Width = 100, Enabled = false };
        lblFloodStatus = new() { Text = "Status: Stopped", Location = new Point(10, 150), AutoSize = true };

        tab.Controls.AddRange(new Control[]
        {
            new Label { Text = "IP Address:",      Location = new Point(10, 13), AutoSize = true }, txtTcpIp,
            new Label { Text = "Port:",            Location = new Point(10, 43), AutoSize = true }, txtTcpPort,
            new Label { Text = "Max Connections:", Location = new Point(10, 73), AutoSize = true }, txtTcpConn,
            btnTcpStart, btnTcpStop, lblFloodStatus
        });

        btnTcpStart.Click += BtnTcpStart_Click;
        btnTcpStop.Click += BtnTcpStop_Click;
        return tab;
    }

    #endregion

    #region Packet Send Tab

    /// <summary>
    /// Creates the Packet Send tab with a two-column layout.
    /// Left column: common header fields. Right column: Control/Directive/Handshake extras.
    /// </summary>
    private TabPage CreatePacketSendTab()
    {
        var tab = new TabPage("Packet Sender") { BackColor = Color.FromArgb(200, 200, 200) };

        // ── Column geometry ──────────────────────────────────────────────────
        const Int32 LeftLabelX = 10;
        const Int32 LeftCtrlX = 150;
        const Int32 CtrlWidth = 140;

        const Int32 RightLabelX = 330;
        const Int32 RightCtrlX = 470;
        const Int32 RightWidth = 160;

        const Int32 RowH = 30;
        Int32 leftRow = 10;
        Int32 rightRow = 10;

        Int32 NextLeft() { Int32 y = leftRow; leftRow += RowH; return y; }
        Int32 NextRight() { Int32 y = rightRow; rightRow += RowH; return y; }

        // ════════════════════ LEFT COLUMN ════════════════════════════════════

        // IP Address
        Int32 y = NextLeft();
        txtSendIp = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, Text = "127.0.0.1" };
        tab.Controls.Add(MakeLabel("IP Address:", LeftLabelX, y)); tab.Controls.Add(txtSendIp);

        // Port
        y = NextLeft();
        txtSendPort = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, Text = "57206" };
        tab.Controls.Add(MakeLabel("Port:", LeftLabelX, y)); tab.Controls.Add(txtSendPort);

        // Packet Type
        y = NextLeft();
        cmbPacketType = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPacketType.Items.AddRange(Enum.GetNames<PacketType>());
        cmbPacketType.SelectedIndex = 0;
        tab.Controls.Add(MakeLabel("Packet Type:", LeftLabelX, y)); tab.Controls.Add(cmbPacketType);

        // OpCode
        y = NextLeft();
        nudOpCode = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, Minimum = 0, Maximum = UInt16.MaxValue, Value = 0 };
        tab.Controls.Add(MakeLabel("OpCode (0–65535):", LeftLabelX, y)); tab.Controls.Add(nudOpCode);

        // Priority
        y = NextLeft();
        cmbPriority = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPriority.Items.AddRange(Enum.GetNames<PacketPriority>());
        cmbPriority.SelectedIndex = 0;
        tab.Controls.Add(MakeLabel("Priority:", LeftLabelX, y)); tab.Controls.Add(cmbPriority);

        // Flags
        y = NextLeft();
        cmbFlags = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbFlags.Items.AddRange(Enum.GetNames<PacketFlags>());
        cmbFlags.SelectedIndex = 0;
        tab.Controls.Add(MakeLabel("Flags:", LeftLabelX, y)); tab.Controls.Add(cmbFlags);

        // Transport
        y = NextLeft();
        cmbTransportProtocol = new() { Location = new Point(LeftCtrlX, y), Width = CtrlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTransportProtocol.Items.AddRange(Enum.GetNames<ProtocolType>());
        Int32 tcpIdx = cmbTransportProtocol.Items.IndexOf(nameof(ProtocolType.TCP));
        cmbTransportProtocol.SelectedIndex = tcpIdx >= 0 ? tcpIdx : 0;
        tab.Controls.Add(MakeLabel("Transport:", LeftLabelX, y)); tab.Controls.Add(cmbTransportProtocol);

        // Content (tall – spans 3 rows visually)
        y = NextLeft();
        txtPacketContent = new()
        {
            Location = new Point(LeftCtrlX, y),
            Width = CtrlWidth + 130,   // wider content box
            Height = 80,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = "Hello from DDoS Tool!"
        };
        tab.Controls.Add(MakeLabel("Content:", LeftLabelX, y)); tab.Controls.Add(txtPacketContent);
        leftRow = y + 80 + 5; // skip past tall textbox

        // ════════════════════ RIGHT COLUMN ═══════════════════════════════════
        // These controls are shown/hidden based on PacketType selection.

        // Separator label for right column header
        var lblRightTitle = new Label
        {
            Text = "── Packet Extras ──",
            Location = new Point(RightLabelX, rightRow - 5),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Italic)
        };
        tab.Controls.Add(lblRightTitle);
        rightRow += RowH;

        // Sequence ID
        Int32 ry = NextRight();
        lblSequenceId = MakeLabel("Sequence ID:", RightLabelX, ry);
        nudSequenceId = new()
        {
            Location = new Point(RightCtrlX, ry),
            Width = 110,
            Minimum = 0,
            Maximum = UInt32.MaxValue,
            Value = 1,
            Visible = false
        };
        lblSequenceId.Visible = false;
        tab.Controls.Add(lblSequenceId); tab.Controls.Add(nudSequenceId);

        // Control Type
        ry = NextRight();
        lblControlType = MakeLabel("Control Type:", RightLabelX, ry);
        cmbControlType = new()
        {
            Location = new Point(RightCtrlX, ry),
            Width = RightWidth,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false
        };
        cmbControlType.Items.AddRange(Enum.GetNames<ControlType>());
        cmbControlType.SelectedIndex = 0;
        lblControlType.Visible = false;
        tab.Controls.Add(lblControlType); tab.Controls.Add(cmbControlType);

        // Protocol Reason
        ry = NextRight();
        lblProtocolReason = MakeLabel("Reason:", RightLabelX, ry);
        cmbProtocolReason = new()
        {
            Location = new Point(RightCtrlX, ry),
            Width = RightWidth,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false
        };
        cmbProtocolReason.Items.AddRange(Enum.GetNames<ProtocolReason>());
        cmbProtocolReason.SelectedIndex = 0;
        lblProtocolReason.Visible = false;
        tab.Controls.Add(lblProtocolReason); tab.Controls.Add(cmbProtocolReason);

        // Protocol Advice (Directive only)
        ry = NextRight();
        lblProtocolAdvice = MakeLabel("Advice:", RightLabelX, ry);
        cmbProtocolAdvice = new()
        {
            Location = new Point(RightCtrlX, ry),
            Width = RightWidth,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false
        };
        cmbProtocolAdvice.Items.AddRange(Enum.GetNames<ProtocolAdvice>());
        cmbProtocolAdvice.SelectedIndex = 0;
        lblProtocolAdvice.Visible = false;
        tab.Controls.Add(lblProtocolAdvice); tab.Controls.Add(cmbProtocolAdvice);

        // Handshake toggle button
        ry = NextRight();
        btnToggleHexMode = new()
        {
            Text = "Switch to Byte[] mode",
            Location = new Point(RightLabelX, ry),
            Width = 185,
            Height = 26,
            Visible = false
        };
        tab.Controls.Add(btnToggleHexMode);
        btnToggleHexMode.Click += BtnToggleHexMode_Click;

        // Handshake hint label
        ry = NextRight();
        lblHandshakeHint = new()
        {
            Location = new Point(RightLabelX, ry),
            Width = 300,
            AutoSize = false,
            Height = 40,
            ForeColor = Color.DarkBlue,
            Text = "Hex mode: enter bytes like\nDE AD BE EF (space-separated)",
            Visible = false
        };
        tab.Controls.Add(lblHandshakeHint);

        // ════════════════════ BOTTOM STRIP ═══════════════════════════════════
        Int32 bottomY = Math.Max(leftRow, rightRow) + 5;

        // Buttons
        btnConnect = new() { Text = "Connect", Location = new Point(10, bottomY), Width = 100 };
        btnSendPacket = new() { Text = "Send Packet", Location = new Point(115, bottomY), Width = 100, Enabled = false };
        btnDisconnect = new() { Text = "Disconnect", Location = new Point(220, bottomY), Width = 100, Enabled = false };
        btnClearHistory = new() { Text = "Clear History", Location = new Point(325, bottomY), Width = 110 };
        tab.Controls.AddRange(new Control[] { btnConnect, btnSendPacket, btnDisconnect, btnClearHistory });
        bottomY += RowH;

        // Status
        lblSendStatus = new() { Text = "Status: Disconnected", Location = new Point(10, bottomY), AutoSize = true };
        tab.Controls.Add(lblSendStatus);
        bottomY += RowH - 5;

        // Packet info
        lblPacketInfo = new() { Text = "Packet Info: Ready", Location = new Point(10, bottomY), AutoSize = true, ForeColor = Color.Blue };
        tab.Controls.Add(lblPacketInfo);
        bottomY += RowH;

        // History labels
        tab.Controls.Add(new Label { Text = "Sent History:", Location = new Point(10, bottomY), AutoSize = true });
        tab.Controls.Add(new Label { Text = "Received:", Location = new Point(450, bottomY), AutoSize = true });
        bottomY += RowH - 10;

        // History list boxes
        lstPacketHistory = new() { Location = new Point(10, bottomY), Width = 420, Height = 120, HorizontalScrollbar = true };
        lstReceivedData = new() { Location = new Point(440, bottomY), Width = 420, Height = 120, HorizontalScrollbar = true };
        tab.Controls.Add(lstPacketHistory);
        tab.Controls.Add(lstReceivedData);

        // ── Event bindings ────────────────────────────────────────────────────
        cmbPacketType.SelectedIndexChanged += CmbPacketType_SelectedIndexChanged;
        btnConnect.Click += BtnConnect_Click;
        btnSendPacket.Click += BtnSendPacket_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        btnClearHistory.Click += BtnClearHistory_Click;

        CmbPacketType_SelectedIndexChanged(null, EventArgs.Empty);
        return tab;
    }

    /// <summary>Helper: creates a right-aligned label at the given position.</summary>
    private static Label MakeLabel(String text, Int32 x, Int32 y)
        => new() { Text = text, Location = new Point(x, y + 3), AutoSize = true };

    #endregion

    #region Packet Type Selection Logic

    /// <summary>
    /// Shows/hides right-column controls based on the selected packet type.
    /// </summary>
    private void CmbPacketType_SelectedIndexChanged(Object? sender, EventArgs e)
    {
        if (cmbPacketType?.SelectedItem is not String selectedType ||
            !Enum.TryParse<PacketType>(selectedType, out PacketType packetType))
        {
            return;
        }

        Boolean isControl = packetType is PacketType.Control or PacketType.Directive;
        Boolean isDirective = packetType == PacketType.Directive;
        Boolean isHandshake = packetType == PacketType.Handshake;

        // Sequence ID row
        lblSequenceId!.Visible = isControl;
        nudSequenceId!.Visible = isControl;

        // Control Type row
        lblControlType!.Visible = isControl;
        cmbControlType!.Visible = isControl;

        // Reason row
        lblProtocolReason!.Visible = isControl;
        cmbProtocolReason!.Visible = isControl;

        // Advice row – Directive only
        lblProtocolAdvice!.Visible = isDirective;
        cmbProtocolAdvice!.Visible = isDirective;

        // Handshake extras
        btnToggleHexMode!.Visible = isHandshake;
        lblHandshakeHint!.Visible = isHandshake;

        if (isHandshake)
        {
            UpdateHandshakeHint();
            txtPacketContent?.Text = _handshakeHexMode ? "DE AD BE EF" : "222, 173, 190, 239";
        }

        UpdatePacketInfo(packetType);
    }

    /// <summary>Toggles Handshake input between hex and decimal-byte modes.</summary>
    private void BtnToggleHexMode_Click(Object? sender, EventArgs e)
    {
        _handshakeHexMode = !_handshakeHexMode;
        UpdateHandshakeHint();
        if (txtPacketContent is null)
        {
            return;
        }

        try
        {
            Byte[] bytes = _handshakeHexMode
                ? ParseByteArray(txtPacketContent.Text)
                : ParseHexString(txtPacketContent.Text);

            txtPacketContent.Text = _handshakeHexMode
                ? BitConverter.ToString(bytes).Replace("-", " ")
                : String.Join(", ", bytes);
        }
        catch { /* leave content unchanged */ }
    }

    /// <summary>Updates handshake hint text and toggle button label.</summary>
    private void UpdateHandshakeHint()
    {
        btnToggleHexMode?.Text = _handshakeHexMode ? "Switch to Byte[] mode" : "Switch to Hex mode";

        lblHandshakeHint?.Text = _handshakeHexMode
                ? "Hex mode: enter bytes like\nDE AD BE EF (space-separated)"
                : "Byte mode: decimal bytes like\n222, 173, 190, 239 (comma-sep)";
    }

    /// <summary>Updates the packet info label.</summary>
    private void UpdatePacketInfo(PacketType packetType)
    {
        if (lblPacketInfo is null)
        {
            return;
        }

        lblPacketInfo.Text = "Packet Info: " + packetType switch
        {
            PacketType.Text256 => "Text packet (max 256 bytes UTF-8)",
            PacketType.Text512 => "Text packet (max 512 bytes UTF-8)",
            PacketType.Text1024 => "Text packet (max 1024 bytes UTF-8)",
            PacketType.Control => "Control packet – fill ControlType, Reason, SeqID on the right",
            PacketType.Directive => "Directive packet – fill ControlType, Reason, Advice, SeqID on the right",
            PacketType.Handshake => "Handshake packet – enter raw bytes on the right",
            _ => "Unknown packet type"
        };
    }

    #endregion

    #region Send Logic

    /// <summary>Handles send button click.</summary>
    private async void BtnSendPacket_Click(Object? sender, EventArgs e)
    {
        if (_reliableClient?.IsConnected != true)
        {
            MessageBox.Show("Please connect before sending a packet.", "Not Connected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (cmbPacketType?.SelectedItem is not String selectedType ||
            !Enum.TryParse<PacketType>(selectedType, out PacketType packetType))
        {
            MessageBox.Show("Please select a packet type.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            IPacket? packet = CreatePacket(packetType);
            if (packet is null)
            {
                return;
            }

            ApplyCommonHeader(packet);

            Byte[] packetBytes = packet.Serialize();
            Boolean success = await _reliableClient.SendAsync(packetBytes);

            _packetHistory.Add(new PacketHistory
            {
                Timestamp = DateTime.Now,
                Type = packetType,
                Content = GetPacketContentForHistory(packet),
                Size = packetBytes.Length,
                Success = success,
                ErrorMessage = success ? null : "Send failed"
            });
            RefreshPacketHistory();

            if (lblSendStatus != null)
            {
                lblSendStatus.Text = success
                    ? $"Last Send: SUCCESS ({packetBytes.Length} bytes)"
                    : "Last Send: FAILED";
                lblSendStatus.ForeColor = success ? Color.Green : Color.Red;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sending packet: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            _packetHistory.Add(new PacketHistory
            {
                Timestamp = DateTime.Now,
                Type = packetType,
                Content = txtPacketContent?.Text ?? "",
                Size = 0,
                Success = false,
                ErrorMessage = ex.Message
            });
            RefreshPacketHistory();
        }
    }

    /// <summary>
    /// Applies OpCode, Priority, Flags, and Transport from the UI to the packet header.
    /// </summary>
    private void ApplyCommonHeader(IPacket packet)
    {
        if (packet is not FrameBase frame)
        {
            return;
        }

        if (nudOpCode != null)
        {
            frame.OpCode = (UInt16)nudOpCode.Value;
        }

        if (cmbPriority?.SelectedItem is String priStr &&
            Enum.TryParse<PacketPriority>(priStr, out var priority))
        {
            frame.Priority = priority;
        }

        if (cmbFlags?.SelectedItem is String flagStr &&
            Enum.TryParse<PacketFlags>(flagStr, out var flags))
        {
            frame.Flags = flags;
        }

        if (cmbTransportProtocol?.SelectedItem is String protoStr &&
            Enum.TryParse<ProtocolType>(protoStr, out var proto))
        {
            frame.Protocol = proto;
        }
    }

    /// <summary>Creates a typed packet from current UI state.</summary>
    private IPacket? CreatePacket(PacketType packetType)
    {
        String content = txtPacketContent?.Text ?? "";
        try
        {
            return packetType switch
            {
                PacketType.Text256 => PacketBuilder.CreateText256(content),
                PacketType.Text512 => PacketBuilder.CreateText512(content),
                PacketType.Text1024 => PacketBuilder.CreateText1024(content),
                PacketType.Control => CreateControlPacket(),
                PacketType.Directive => CreateDirectivePacket(),
                PacketType.Handshake => CreateHandshakePacket(),
                _ => throw new ArgumentException($"Unsupported packet type: {packetType}")
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            MessageBox.Show($"Content too large for {packetType}: {ex.Message}",
                "Size Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating packet: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }

    /// <summary>Creates a Control packet.</summary>
    private Nalix.Shared.Messaging.Controls.Control CreateControlPacket()
    {
        var type = Enum.Parse<ControlType>(cmbControlType?.SelectedItem?.ToString() ?? "NONE");
        var reason = Enum.Parse<ProtocolReason>(cmbProtocolReason?.SelectedItem?.ToString() ?? "NONE");
        var seqId = (UInt32)(nudSequenceId?.Value ?? 0);
        return PacketBuilder.CreateControl(type, seqId, reason);
    }

    /// <summary>Creates a Directive packet.</summary>
    private Nalix.Shared.Messaging.Controls.Directive CreateDirectivePacket()
    {
        var type = Enum.Parse<ControlType>(cmbControlType?.SelectedItem?.ToString() ?? "NONE");
        var reason = Enum.Parse<ProtocolReason>(cmbProtocolReason?.SelectedItem?.ToString() ?? "NONE");
        var advice = Enum.Parse<ProtocolAdvice>(cmbProtocolAdvice?.SelectedItem?.ToString() ?? "NONE");
        var seqId = (UInt32)(nudSequenceId?.Value ?? 0);
        return PacketBuilder.CreateDirective(type, reason, advice, seqId);
    }

    /// <summary>Creates a Handshake packet from hex or decimal-byte input.</summary>
    private Nalix.Shared.Messaging.Controls.Handshake CreateHandshakePacket()
    {
        String raw = txtPacketContent?.Text.Trim() ?? "";
        Byte[] data = _handshakeHexMode ? ParseHexString(raw) : ParseByteArray(raw);
        return PacketBuilder.CreateHandshake(data);
    }

    /// <summary>Parses a space/dash-separated hex string into bytes.</summary>
    private static Byte[] ParseHexString(String hex)
    {
        String clean = hex.Replace(" ", "").Replace("-", "");
        if (clean.Length % 2 != 0)
        {
            throw new FormatException("Hex string must have an even number of hex characters.");
        }

        Byte[] result = new Byte[clean.Length / 2];
        for (Int32 i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        }

        return result;
    }

    /// <summary>Parses a comma-separated decimal byte list into bytes.</summary>
    private static Byte[] ParseByteArray(String byteList)
        => byteList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Byte.Parse)
            .ToArray();

    #endregion

    #region History

    /// <summary>Returns a summary string for the packet history list.</summary>
    private static String GetPacketContentForHistory(IPacket packet)
        => packet switch
        {
            Nalix.Shared.Messaging.Text.Text256 t => $"[TEXT256]   Op={t.OpCode} \"{t.Content}\"",
            Nalix.Shared.Messaging.Text.Text512 t => $"[TEXT512]   Op={t.OpCode} \"{t.Content}\"",
            Nalix.Shared.Messaging.Text.Text1024 t => $"[TEXT1024]  Op={t.OpCode} \"{t.Content}\"",
            Nalix.Shared.Messaging.Controls.Control c => $"[CONTROL]   Op={c.OpCode} Type={c.Type} Seq={c.SequenceId} Reason={c.Reason} Pri={c.Priority}",
            Nalix.Shared.Messaging.Controls.Directive d => $"[DIRECTIVE] Op={d.OpCode} Type={d.Type} Seq={d.SequenceId} Reason={d.Reason} Action={d.Action}",
            Nalix.Shared.Messaging.Controls.Handshake h =>
                $"[HANDSHAKE] Op={h.OpCode} {h.Data?.Length ?? 0}B [{BitConverter.ToString(h.Data ?? [])}]",
            _ => packet.GetType().Name
        };

    /// <summary>Refreshes the sent-history list box (last 50 entries).</summary>
    private void RefreshPacketHistory()
    {
        if (lstPacketHistory is null)
        {
            return;
        }

        lstPacketHistory.Items.Clear();
        foreach (PacketHistory h in _packetHistory.TakeLast(50))
        {
            lstPacketHistory.Items.Add(h);
        }

        if (lstPacketHistory.Items.Count > 0)
        {
            lstPacketHistory.TopIndex = lstPacketHistory.Items.Count - 1;
        }
    }

    /// <summary>Clears the packet send history.</summary>
    private void BtnClearHistory_Click(Object? sender, EventArgs e)
    {
        _packetHistory.Clear();
        RefreshPacketHistory();
        if (lblSendStatus != null)
        {
            lblSendStatus.Text = "Status: History cleared";
            lblSendStatus.ForeColor = Color.Black;
        }
    }

    #endregion

    #region TCP Flood Events

    private void BtnTcpStart_Click(Object? sender, EventArgs e)
    {
        String ip = txtTcpIp?.Text.Trim() ?? "";
        Boolean validPort = Int32.TryParse(txtTcpPort?.Text, out Int32 port);
        Boolean validConn = Int32.TryParse(txtTcpConn?.Text, out Int32 maxConn);

        if (String.IsNullOrEmpty(ip) || !validPort || port == 0 || !validConn || maxConn == 0)
        {
            MessageBox.Show("Please fill in valid IP, Port, and Max Connections.", "Missing Info",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnTcpStart!.Enabled = false;
        btnTcpStop!.Enabled = true;
        lblFloodStatus!.Text = $"Status: Running ({ip}:{port}, max {maxConn})";

        _tcpFlooder = new TCPConnectFlooder(ip, port, maxConn);
        _tcpFlooder.Start();

        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += (_, _) =>
        {
            if (_tcpFlooder != null)
            {
                lblFloodStatus!.Text = $"Status: Running ({_tcpFlooder.ConnectionCount} connections)";
            }
        };
        _statusTimer.Start();
    }

    private void BtnTcpStop_Click(Object? sender, EventArgs e)
    {
        btnTcpStop!.Enabled = false;
        btnTcpStart!.Enabled = true;
        _tcpFlooder?.Stop();
        _tcpFlooder = null;
        lblFloodStatus!.Text = "Status: Stopped";
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _statusTimer = null;
    }

    #endregion

    #region Connection Events

    private async void BtnConnect_Click(Object? sender, EventArgs e)
    {
        String ip = txtSendIp?.Text.Trim() ?? "";
        Boolean validPort = Int32.TryParse(txtSendPort?.Text, out Int32 port);

        if (String.IsNullOrEmpty(ip) || !validPort || port == 0)
        {
            MessageBox.Show("Please fill in a valid IP and Port.", "Missing Info",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _reliableClient?.Dispose();
        _reliableClient = new ReliableClient();

        btnConnect!.Enabled = false;
        lblSendStatus!.Text = "Status: Connecting...";

        _reliableClient.OnConnected += (_, _) =>
        {
            btnSendPacket!.Invoke(() => btnSendPacket.Enabled = true);
            btnDisconnect!.Invoke(() => btnDisconnect.Enabled = true);
            lblSendStatus!.Invoke(() =>
            {
                lblSendStatus.Text = "Status: Connected – Ready to send packets";
                lblSendStatus.ForeColor = Color.Green;
            });
        };

        _reliableClient.OnDisconnected += (_, _) =>
        {
            btnSendPacket!.Invoke(() => btnSendPacket.Enabled = false);
            btnDisconnect!.Invoke(() => btnDisconnect.Enabled = false);
            btnConnect!.Invoke(() => btnConnect.Enabled = true);
            lblSendStatus!.Invoke(() =>
            {
                lblSendStatus.Text = "Status: Disconnected";
                lblSendStatus.ForeColor = Color.Red;
            });
        };

        _reliableClient.OnError += (_, ex) =>
        {
            lblSendStatus!.Invoke(() =>
            {
                lblSendStatus.Text = $"Error: {ex.Message}";
                lblSendStatus.ForeColor = Color.Red;
            });
        };

        _reliableClient.OnMessageReceived += (_, lease) =>
        {
            String text = System.Text.Encoding.UTF8.GetString(lease.Span);
            lstReceivedData?.Invoke(() =>
            {
                lstReceivedData.Items.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
                if (lstReceivedData.Items.Count > 0)
                {
                    lstReceivedData.TopIndex = lstReceivedData.Items.Count - 1;
                }
            });
        };

        try
        {
            await _reliableClient.ConnectAsync(ip, (UInt16)port);
        }
        catch (Exception ex)
        {
            lblSendStatus!.Text = $"Connect Failed: {ex.Message}";
            lblSendStatus.ForeColor = Color.Red;
            btnConnect!.Enabled = true;
            btnSendPacket!.Enabled = false;
            btnDisconnect!.Enabled = false;
        }
    }

    private async void BtnDisconnect_Click(Object? sender, EventArgs e)
    {
        if (_reliableClient != null)
        {
            await _reliableClient.DisconnectAsync();
            _reliableClient.Dispose();
            _reliableClient = null;
        }
    }

    #endregion
}