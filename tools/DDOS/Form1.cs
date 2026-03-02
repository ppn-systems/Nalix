using DDOS.Helpers;
using DDOS.Models;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.SDK.Transport;
using Nalix.Shared.Messaging.Controls;

namespace DDOS;

/// <summary>
/// Main form for DDOS application. This class handles only UI tasks; networking logic is managed by external services.
/// </summary>
public partial class Form1 : Form
{
    // Service fields
    private TCPConnectFlooder? _tcpFlooder;
    private ReliableClient? _reliableClient;
    private System.Windows.Forms.Timer? _statusTimer;

    // UI controls for TCP Flood tab
    private TextBox? txtTcpIp, txtTcpPort, txtTcpConn;
    private Button? btnTcpStart, btnTcpStop;
    private Label? lblFloodStatus;

    // UI controls for TCP Send tab - UPDATED
    private TextBox? txtSendIp, txtSendPort;
    private ComboBox? cmbPacketType;
    private TextBox? txtPacketContent;
    private NumericUpDown? nudSequenceId;
    private ComboBox? cmbControlType, cmbProtocolReason, cmbProtocolAdvice;
    private Button? btnConnect, btnSendPacket, btnDisconnect, btnClearHistory;
    private Label? lblSendStatus, lblPacketInfo;
    private ListBox? lstPacketHistory, lstReceivedData;

    // Packet history storage
    private readonly List<PacketHistory> _packetHistory = [];

    /// <summary>
    /// Form constructor
    /// </summary>
    public Form1()
    {
        InitializeComponent();
        InitializeMainUI();
    }

    /// <summary>
    /// Initializes the main user interface, including tabs and controls.
    /// </summary>
    private void InitializeMainUI()
    {
        this.BackColor = Color.FromArgb(150, 150, 150);
        this.Text = "DDOS Tool - Packet Sender";
        this.Size = new Size(800, 600);

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        tabControl.TabPages.Add(CreateTcpFloodTab());
        tabControl.TabPages.Add(CreatePacketSendTab()); // Renamed and updated

        this.Controls.Add(tabControl);
    }

    /// <summary>
    /// Creates the TCP Flood tab with controls and event bindings.
    /// </summary>
    private TabPage CreateTcpFloodTab()
    {
        var tcpTab = new TabPage("TCP - Flood")
        {
            BackColor = Color.FromArgb(200, 200, 200)
        };

        // Giữ nguyên code TCP Flood như cũ
        txtTcpIp = new() { Location = new Point(120, 10), Width = 120 };
        txtTcpPort = new() { Location = new Point(120, 40), Width = 80 };
        txtTcpConn = new() { Location = new Point(120, 70), Width = 60 };

        btnTcpStart = new() { Text = "Start TCP Flood", Location = new Point(10, 110), Width = 120 };
        btnTcpStop = new() { Text = "Stop", Location = new Point(140, 110), Width = 80, Enabled = false };

        lblFloodStatus = new() { Text = "Status: Stopped", Location = new Point(10, 150), AutoSize = true };

        var lblTcpIp = new Label { Text = "IP Address:", Location = new Point(10, 10), AutoSize = true };
        var lblTcpPort = new Label { Text = "Port:", Location = new Point(10, 40), AutoSize = true };
        var lblTcpConn = new Label { Text = "Max Connections:", Location = new Point(10, 70), AutoSize = true };

        tcpTab.Controls.AddRange(new System.Windows.Forms.Control[] {
            lblTcpIp, txtTcpIp, lblTcpPort, txtTcpPort, lblTcpConn, txtTcpConn,
            btnTcpStart, btnTcpStop, lblFloodStatus
        });

        btnTcpStart.Click += BtnTcpStart_Click;
        btnTcpStop.Click += BtnTcpStop_Click;

        return tcpTab;
    }

    /// <summary>
    /// Creates the Packet Send tab with controls and event bindings.
    /// </summary>
    private TabPage CreatePacketSendTab()
    {
        var packetTab = new TabPage("Packet Sender")
        {
            BackColor = Color.FromArgb(200, 200, 200)
        };

        // Connection controls
        txtSendIp = new() { Location = new Point(120, 10), Width = 120, Text = "127.0.0.1" };
        txtSendPort = new() { Location = new Point(120, 40), Width = 80, Text = "57206" };

        // Packet type selection
        cmbPacketType = new() { Location = new Point(120, 70), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPacketType.Items.AddRange(Enum.GetNames<PacketType>());
        cmbPacketType.SelectedIndex = 0; // Default to Text256

        // Packet content
        txtPacketContent = new()
        {
            Location = new Point(120, 100),
            Width = 300,
            Height = 60,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = "Hello from DDOS Tool!"
        };

        // Sequence ID for Control/Directive packets
        nudSequenceId = new()
        {
            Location = new Point(120, 170),
            Width = 80,
            Maximum = UInt32.MaxValue,
            Value = 1
        };

        // Control Type dropdown (for Control/Directive packets)
        cmbControlType = new()
        {
            Location = new Point(120, 200),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        cmbControlType.Items.AddRange(Enum.GetNames<ControlType>());
        cmbControlType.SelectedIndex = 0;

        // Protocol Reason dropdown (for Control/Directive packets)
        cmbProtocolReason = new()
        {
            Location = new Point(230, 200),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        cmbProtocolReason.Items.AddRange(Enum.GetNames<ProtocolReason>());
        cmbProtocolReason.SelectedIndex = 0;

        // Protocol Advice dropdown (for Directive packets)
        cmbProtocolAdvice = new()
        {
            Location = new Point(340, 200),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        cmbProtocolAdvice.Items.AddRange(Enum.GetNames<ProtocolAdvice>());
        cmbProtocolAdvice.SelectedIndex = 0;

        // Buttons
        btnConnect = new() { Text = "Connect", Location = new Point(10, 240), Width = 100 };
        btnSendPacket = new() { Text = "Send Packet", Location = new Point(120, 240), Width = 100, Enabled = false };
        btnDisconnect = new() { Text = "Disconnect", Location = new Point(230, 240), Width = 100, Enabled = false };
        btnClearHistory = new() { Text = "Clear History", Location = new Point(340, 240), Width = 100 };

        // Status and info labels
        lblSendStatus = new() { Text = "Status: Disconnected", Location = new Point(10, 270), AutoSize = true };
        lblPacketInfo = new() { Text = "Packet Info: Ready", Location = new Point(10, 290), AutoSize = true, ForeColor = Color.Blue };

        // History ListBox
        lstPacketHistory = new()
        {
            Location = new Point(10, 320),
            Width = 350,
            Height = 120,
            HorizontalScrollbar = true
        };

        // Received data ListBox
        lstReceivedData = new()
        {
            Location = new Point(370, 320),
            Width = 350,
            Height = 120,
            HorizontalScrollbar = true
        };

        // Labels
        var labels = new[] {
            new Label { Text = "IP Address:", Location = new Point(10, 10), AutoSize = true },
            new Label { Text = "Port:", Location = new Point(10, 40), AutoSize = true },
            new Label { Text = "Packet Type:", Location = new Point(10, 70), AutoSize = true },
            new Label { Text = "Content:", Location = new Point(10, 100), AutoSize = true },
            new Label { Text = "Sequence ID:", Location = new Point(10, 170), AutoSize = true },
            new Label { Text = "Control Type:", Location = new Point(10, 200), AutoSize = true },
            new Label { Text = "Sent History:", Location = new Point(10, 305), AutoSize = true },
            new Label { Text = "Received:", Location = new Point(370, 305), AutoSize = true }
        };

        // Add all controls to tab
        packetTab.Controls.AddRange([
            txtSendIp, txtSendPort, cmbPacketType, txtPacketContent, nudSequenceId,
            cmbControlType, cmbProtocolReason, cmbProtocolAdvice,
            btnConnect, btnSendPacket, btnDisconnect, btnClearHistory,
            lblSendStatus, lblPacketInfo, lstPacketHistory, lstReceivedData
        ]);
        packetTab.Controls.AddRange(labels);

        // Event bindings
        cmbPacketType.SelectedIndexChanged += CmbPacketType_SelectedIndexChanged;
        btnConnect.Click += BtnConnect_Click;
        btnSendPacket.Click += BtnSendPacket_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        btnClearHistory.Click += BtnClearHistory_Click;

        // Initialize packet type selection
        CmbPacketType_SelectedIndexChanged(null, EventArgs.Empty);

        return packetTab;
    }

    /// <summary>
    /// Xử lý khi thay đổi packet type
    /// </summary>
    private void CmbPacketType_SelectedIndexChanged(Object? sender, EventArgs e)
    {
        if (cmbPacketType?.SelectedItem is String selectedType &&
            Enum.TryParse<PacketType>(selectedType, out PacketType packetType))
        {
            // Enable/disable controls based on packet type
            Boolean isControlPacket = packetType is PacketType.Control or PacketType.Directive;
            Boolean isDirectivePacket = packetType == PacketType.Directive;

            cmbControlType!.Enabled = isControlPacket;
            cmbProtocolReason!.Enabled = isControlPacket;
            cmbProtocolAdvice!.Enabled = isDirectivePacket;
            nudSequenceId!.Enabled = isControlPacket;

            // Update packet info
            UpdatePacketInfo();
        }
    }

    /// <summary>
    /// Cập nhật thông tin packet hiện tại
    /// </summary>
    private void UpdatePacketInfo()
    {
        if (cmbPacketType?.SelectedItem is String selectedType &&
            Enum.TryParse<PacketType>(selectedType, out PacketType packetType))
        {
            String info = packetType switch
            {
                PacketType.Text256 => "Text packet (max 256 bytes)",
                PacketType.Text512 => "Text packet (max 512 bytes)",
                PacketType.Text1024 => "Text packet (max 1024 bytes)",
                PacketType.Control => "Control packet (system commands)",
                PacketType.Directive => "Directive packet (instructions)",
                PacketType.Handshake => "Handshake packet (connection setup)",
                _ => "Unknown packet type"
            };

            lblPacketInfo?.Text = $"Packet Info: {info}";
        }
    }

    /// <summary>
    /// Xử lý sự kiện gửi packet
    /// </summary>
    private async void BtnSendPacket_Click(Object? sender, EventArgs e)
    {
        if (_reliableClient?.IsConnected != true)
        {
            MessageBox.Show("Vui lòng kết nối trước khi gửi packet!", "Chưa kết nối",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (cmbPacketType?.SelectedItem is not String selectedType ||
            !Enum.TryParse<PacketType>(selectedType, out PacketType packetType))
        {
            MessageBox.Show("Vui lòng chọn loại packet!", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            IPacket? packet = CreatePacket(packetType);
            if (packet == null)
            {
                return; // Error already shown in CreatePacket
            }

            // Serialize packet to bytes
            Byte[] packetBytes = packet.Serialize();

            // Send packet
            Boolean success = await _reliableClient.SendAsync(packetBytes);

            // Add to history
            var history = new PacketHistory
            {
                Timestamp = DateTime.Now,
                Type = packetType,
                Content = GetPacketContentForHistory(packet),
                Size = packetBytes.Length,
                Success = success,
                ErrorMessage = success ? null : "Send failed"
            };

            _packetHistory.Add(history);
            RefreshPacketHistory();

            // Update status
            if (lblSendStatus != null)
            {
                lblSendStatus.Text = success ? $"Last Send: SUCCESS ({packetBytes.Length} bytes)" : "Last Send: FAILED";
                lblSendStatus.ForeColor = success ? Color.Green : Color.Red;
            }

            //// Return packet to pool
            //PacketBuilder.ReturnToPool(packet);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi gửi packet: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Add error to history
            var errorHistory = new PacketHistory
            {
                Timestamp = DateTime.Now,
                Type = packetType,
                Content = txtPacketContent?.Text ?? "",
                Size = 0,
                Success = false,
                ErrorMessage = ex.Message
            };

            _packetHistory.Add(errorHistory);
            RefreshPacketHistory();
        }
    }

    /// <summary>
    /// Tạo packet dựa trên loại đã chọn
    /// </summary>
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

                PacketType.Handshake => PacketBuilder.CreateHandshake(
                    System.Text.Encoding.UTF8.GetBytes(content)),

                _ => throw new ArgumentException($"Unsupported packet type: {packetType}")
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            MessageBox.Show($"Nội dung quá dài cho loại packet {packetType}: {ex.Message}",
                "Lỗi kích thước", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tạo packet: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }

    /// <summary>
    /// Tạo Control packet
    /// </summary>
    private Nalix.Shared.Messaging.Controls.Control CreateControlPacket()
    {
        var controlType = Enum.Parse<ControlType>(cmbControlType?.SelectedItem?.ToString() ?? "PING");
        var reason = Enum.Parse<ProtocolReason>(cmbProtocolReason?.SelectedItem?.ToString() ?? "NONE");
        var sequenceId = (UInt32)(nudSequenceId?.Value ?? 0);

        return PacketBuilder.CreateControl(controlType, sequenceId, reason);
    }

    /// <summary>
    /// Tạo Directive packet
    /// </summary>
    private Directive CreateDirectivePacket()
    {
        var controlType = Enum.Parse<ControlType>(cmbControlType?.SelectedItem?.ToString() ?? "PING");
        var reason = Enum.Parse<ProtocolReason>(cmbProtocolReason?.SelectedItem?.ToString() ?? "NONE");
        var advice = Enum.Parse<ProtocolAdvice>(cmbProtocolAdvice?.SelectedItem?.ToString() ?? "NONE");
        var sequenceId = (UInt32)(nudSequenceId?.Value ?? 0);

        return PacketBuilder.CreateDirective(controlType, reason, advice, sequenceId);
    }

    /// <summary>
    /// Lấy nội dung packet để hiển thị trong history
    /// </summary>
    private String GetPacketContentForHistory(IPacket packet)
    {
        return packet switch
        {
            Nalix.Shared.Messaging.Text.Text256 text => $"Text: \"{text.Content}\"",
            Nalix.Shared.Messaging.Text.Text512 text => $"Text: \"{text.Content}\"",
            Nalix.Shared.Messaging.Text.Text1024 text => $"Text: \"{text.Content}\"",

            Nalix.Shared.Messaging.Controls.Control ctrl =>
                $"Control: {ctrl.Type} (Seq: {ctrl.SequenceId}, Reason: {ctrl.Reason})",

            Nalix.Shared.Messaging.Controls.Directive dir =>
                $"Directive: {dir.Type} (Seq: {dir.SequenceId}, Reason: {dir.Reason}, Action: {dir.Action})",

            Nalix.Shared.Messaging.Controls.Handshake hs =>
                $"Handshake: {hs.Data?.Length ?? 0} bytes",

            _ => packet.GetType().Name
        };
    }

    /// <summary>
    /// Refresh packet history display
    /// </summary>
    private void RefreshPacketHistory()
    {
        if (lstPacketHistory == null)
        {
            return;
        }

        lstPacketHistory.Items.Clear();

        // Hiển thị 50 packet gần nhất
        var recentHistory = _packetHistory.TakeLast(50).ToList();

        foreach (var history in recentHistory)
        {
            lstPacketHistory.Items.Add(history);
        }

        // Auto scroll to bottom
        if (lstPacketHistory.Items.Count > 0)
        {
            lstPacketHistory.TopIndex = lstPacketHistory.Items.Count - 1;
        }
    }

    /// <summary>
    /// Clear packet history
    /// </summary>
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

    // Giữ nguyên các method cũ cho TCP Flood
    private void BtnTcpStart_Click(Object? sender, EventArgs e)
    {
        String ip = txtTcpIp?.Text.Trim() ?? "";
        Boolean validPort = Int32.TryParse(txtTcpPort?.Text, out Int32 port);
        Boolean validMaxConn = Int32.TryParse(txtTcpConn?.Text, out Int32 maxConn);

        if (String.IsNullOrEmpty(ip) || !validPort || port == 0 || !validMaxConn || maxConn == 0)
        {
            MessageBox.Show("Vui lòng nhập đúng IP, Port và Max Connections!", "Thiếu thông tin",
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

    // Updated connection methods for packet sending
    private async void BtnConnect_Click(Object? sender, EventArgs e)
    {
        String ip = txtSendIp?.Text.Trim() ?? "";
        Boolean validPort = Int32.TryParse(txtSendPort?.Text, out Int32 port);

        if (String.IsNullOrEmpty(ip) || !validPort || port == 0)
        {
            MessageBox.Show("Vui lòng nhập đúng IP và Port!", "Thiếu thông tin",
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
                lblSendStatus.Text = "Status: Connected - Ready to send packets";
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
            String receivedText = System.Text.Encoding.UTF8.GetString(lease.Span);
            lstReceivedData?.Invoke(() =>
            {
                lstReceivedData.Items.Add($"[{DateTime.Now:HH:mm:ss}] {receivedText}");
                // Auto scroll to bottom
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
}