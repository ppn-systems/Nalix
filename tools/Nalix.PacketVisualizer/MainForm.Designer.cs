namespace Nalix.PacketVisualizer;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.btnLoadDll = new System.Windows.Forms.Button();
        this.cmbPacketTypes = new System.Windows.Forms.ComboBox();
        this.propertyGrid = new System.Windows.Forms.PropertyGrid();
        this.lblType = new System.Windows.Forms.Label();
        this.btnRandomize = new System.Windows.Forms.Button();
        this.txtHexView = new System.Windows.Forms.RichTextBox();
        this.lblLength = new System.Windows.Forms.Label();
        this.panelBottom = new System.Windows.Forms.Panel();
        this.splitContainer1 = new System.Windows.Forms.SplitContainer();
        this.panelTop = new System.Windows.Forms.Panel();
        this.btnRefresh = new System.Windows.Forms.Button();
        
        ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
        this.splitContainer1.Panel1.SuspendLayout();
        this.splitContainer1.Panel2.SuspendLayout();
        this.splitContainer1.SuspendLayout();
        this.panelTop.SuspendLayout();
        this.panelBottom.SuspendLayout();
        this.SuspendLayout();

        // panelTop
        this.panelTop.Controls.Add(this.btnRefresh);
        this.panelTop.Controls.Add(this.btnRandomize);
        this.panelTop.Controls.Add(this.cmbPacketTypes);
        this.panelTop.Controls.Add(this.lblType);
        this.panelTop.Controls.Add(this.btnLoadDll);
        this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
        this.panelTop.Height = 80;
        this.panelTop.Padding = new System.Windows.Forms.Padding(10);

        // btnLoadDll
        this.btnLoadDll.Location = new System.Drawing.Point(10, 10);
        this.btnLoadDll.Name = "btnLoadDll";
        this.btnLoadDll.Size = new System.Drawing.Size(120, 30);
        this.btnLoadDll.Text = "Load Packet DLL";
        this.btnLoadDll.Click += new System.EventHandler(this.btnLoadDll_Click);

        // lblType
        this.lblType.Location = new System.Drawing.Point(10, 48);
        this.lblType.Size = new System.Drawing.Size(100, 23);
        this.lblType.Text = "Select Packet:";
        this.lblType.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        // cmbPacketTypes
        this.cmbPacketTypes.Location = new System.Drawing.Point(110, 48);
        this.cmbPacketTypes.Name = "cmbPacketTypes";
        this.cmbPacketTypes.Size = new System.Drawing.Size(400, 23);
        this.cmbPacketTypes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbPacketTypes.SelectedIndexChanged += new System.EventHandler(this.cmbPacketTypes_SelectedIndexChanged);

        // btnRandomize
        this.btnRandomize.Location = new System.Drawing.Point(520, 47);
        this.btnRandomize.Size = new System.Drawing.Size(100, 25);
        this.btnRandomize.Text = "Randomize";
        this.btnRandomize.Click += new System.EventHandler(this.btnRandomize_Click);

        // btnRefresh
        this.btnRefresh.Location = new System.Drawing.Point(630, 47);
        this.btnRefresh.Size = new System.Drawing.Size(100, 25);
        this.btnRefresh.Text = "Refresh";
        this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

        // splitContainer1
        this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.splitContainer1.Location = new System.Drawing.Point(0, 80);
        this.splitContainer1.Name = "splitContainer1";
        this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Vertical;

        // propertyGrid
        this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        this.propertyGrid.Name = "propertyGrid";
        this.propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid_PropertyValueChanged);

        // txtHexView
        this.txtHexView.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtHexView.Font = new System.Drawing.Font("Consolas", 10F);
        this.txtHexView.ReadOnly = true;
        this.txtHexView.BackColor = System.Drawing.Color.White;

        // panelBottom
        this.panelBottom.Controls.Add(this.lblLength);
        this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.panelBottom.Height = 40;
        this.panelBottom.Padding = new System.Windows.Forms.Padding(10);

        // lblLength
        this.lblLength.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblLength.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
        this.lblLength.ForeColor = System.Drawing.Color.DarkBlue;
        this.lblLength.Text = "Length: 0 bytes";
        this.lblLength.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        // Form logic
        this.splitContainer1.Panel1.Controls.Add(this.propertyGrid);
        this.splitContainer1.Panel2.Controls.Add(this.txtHexView);
        this.splitContainer1.SplitterDistance = 400;

        this.Controls.Add(this.splitContainer1);
        this.Controls.Add(this.panelBottom);
        this.Controls.Add(this.panelTop);

        this.ClientSize = new System.Drawing.Size(1000, 700);
        this.Text = "Nalix Packet Visualizer & Length Calculator";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

        this.panelTop.ResumeLayout(false);
        this.panelBottom.ResumeLayout(false);
        this.splitContainer1.Panel1.ResumeLayout(false);
        this.splitContainer1.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
        this.splitContainer1.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.Button btnLoadDll;
    private System.Windows.Forms.ComboBox cmbPacketTypes;
    private System.Windows.Forms.PropertyGrid propertyGrid;
    private System.Windows.Forms.Label lblType;
    private System.Windows.Forms.Button btnRandomize;
    private System.Windows.Forms.Button btnRefresh;
    private System.Windows.Forms.RichTextBox txtHexView;
    private System.Windows.Forms.Label lblLength;
    private System.Windows.Forms.Panel panelBottom;
    private System.Windows.Forms.Panel panelTop;
    private System.Windows.Forms.SplitContainer splitContainer1;
}
