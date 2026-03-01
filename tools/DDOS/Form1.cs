namespace DDOS;

public partial class Form1 : Form
{
    private TCPConnectFlooder? _tcpFlooder = null;
    private readonly TextBox txtTcpIp = new();
    private readonly TextBox txtTcpPort = new();
    private readonly TextBox txtTcpConn = new();
    private readonly Button btnTcpStart = new();
    private readonly Button btnTcpStop = new();
    private readonly Label lblStatus = new();

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

        TabControl tabControl = new()
        {
            Dock = DockStyle.Fill
        };

        tabControl.TabPages.Add(CreateTcpFloodTab());
        tabControl.TabPages.Add(CreateTcpTab());

        this.Controls.Add(tabControl);
    }

    /// <summary>
    /// Creates the TCP Flood tab with all necessary controls and events.
    /// </summary>
    /// <returns>The configured TabPage for TCP Flood.</returns>
    private TabPage CreateTcpFloodTab()
    {
        TabPage tcpTab = new("TCP - Flood")
        {
            BackColor = Color.FromArgb(200, 200, 200)
        };

        // Controls
        Label lblTcpIp = new() { Text = "IP Address:", Location = new Point(10, 10), AutoSize = true };
        txtTcpIp.Location = new Point(120, 10); txtTcpIp.Width = 120;

        Label lblTcpPort = new() { Text = "Port:", Location = new Point(10, 40), AutoSize = true };
        txtTcpPort.Location = new Point(120, 40); txtTcpPort.Width = 80;

        Label lblTcpConn = new() { Text = "Max Connections:", Location = new Point(10, 70), AutoSize = true };
        txtTcpConn.Location = new Point(120, 70); txtTcpConn.Width = 60;

        btnTcpStart.Text = "Start TCP Flood"; btnTcpStart.Location = new Point(10, 110); btnTcpStart.Width = 120;
        btnTcpStop.Text = "Stop"; btnTcpStop.Location = new Point(140, 110); btnTcpStop.Width = 80; btnTcpStop.Enabled = false;

        lblStatus.Text = "Status: Stopped"; lblStatus.Location = new Point(10, 150); lblStatus.AutoSize = true;

        // Add controls to tab
        tcpTab.Controls.AddRange(new Control[]
        {
            lblTcpIp, txtTcpIp, lblTcpPort, txtTcpPort, lblTcpConn, txtTcpConn, btnTcpStart, btnTcpStop, lblStatus
        });

        // Events
        btnTcpStart.Click += BtnTcpStart_Click;
        btnTcpStop.Click += BtnTcpStop_Click;

        return tcpTab;
    }

    /// <summary>
    /// Creates the UDP Flood (or other) tab stub for extension.
    /// </summary>
    /// <returns>The configured TabPage for UDP (or other) features.</returns>
    private static TabPage CreateTcpTab()
    {
        TabPage tcpSendTab = new("TCP - Send data")
        {
            BackColor = Color.FromArgb(200, 200, 200)
        };

        // Bạn có thể thêm controls cho chức năng khác ở đây.
        tcpSendTab.Controls.Add(new Label
        {
            Text = "Chưa có chức năng. Bạn bổ sung nếu cần.",
            Location = new Point(10, 10),
            AutoSize = true
        });
        return tcpSendTab;
    }

    /// <summary>
    /// Handles the event for starting TCP flood.
    /// </summary>
    private void BtnTcpStart_Click(Object? sender, EventArgs e)
    {
        String ip = txtTcpIp.Text.Trim();
        Int32 port = Int32.TryParse(txtTcpPort.Text, out Int32 p) ? p : 0;
        Int32 maxConn = Int32.TryParse(txtTcpConn.Text, out Int32 mc) ? mc : 0;

        if (String.IsNullOrEmpty(ip) || port == 0 || maxConn == 0)
        {
            MessageBox.Show("Vui lòng nhập đúng IP, Port và Max Connections!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnTcpStart.Enabled = false;
        btnTcpStop.Enabled = true;
        lblStatus.Text = $"Status: Running ({ip}:{port}, max {maxConn})";

        _tcpFlooder = new TCPConnectFlooder(ip, port, maxConn);
        _tcpFlooder.Start();

        // Status Update Timer
        System.Windows.Forms.Timer t = new() { Interval = 1000 };
        t.Tick += (_, _) =>
        {
            if (_tcpFlooder != null)
            {
                lblStatus.Text = $"Status: Running ({_tcpFlooder.ConnectionCount} connections)";
            }
        };
        t.Start();
        btnTcpStop.Tag = t;
    }

    /// <summary>
    /// Handles the event for stopping TCP flood.
    /// </summary>
    private void BtnTcpStop_Click(Object? sender, EventArgs e)
    {
        btnTcpStop.Enabled = false;
        btnTcpStart.Enabled = true;

        _tcpFlooder?.Stop();
        _tcpFlooder = null;
        lblStatus.Text = "Status: Stopped";

        if (btnTcpStop.Tag is System.Windows.Forms.Timer t)
        {
            t.Stop();
            t.Dispose();
            btnTcpStop.Tag = null;
        }
    }
}