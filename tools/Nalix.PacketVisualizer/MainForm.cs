using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nalix.Common.Networking.Packets;

namespace Nalix.PacketVisualizer;

public partial class MainForm : Form
{
    private IPacket? _currentPacket;
    private List<Type> _packetTypes = new();

    public MainForm()
    {
        InitializeComponent();
    }

    private void btnLoadDll_Click(object sender, EventArgs e)
    {
        using OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*";
        openFileDialog.RestoreDirectory = true;

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                LoadPacketsFromDll(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading DLL: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadPacketsFromDll(string path)
    {
        var assembly = Assembly.LoadFrom(path);
        _packetTypes = assembly.GetTypes()
            .Where(t => typeof(IPacket).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        cmbPacketTypes.Items.Clear();
        foreach (var type in _packetTypes)
        {
            cmbPacketTypes.Items.Add(type.FullName ?? type.Name);
        }

        if (cmbPacketTypes.Items.Count > 0)
        {
            cmbPacketTypes.SelectedIndex = 0;
        }
        else
        {
            MessageBox.Show("No IPacket implementations found in the selected assembly.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void cmbPacketTypes_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbPacketTypes.SelectedIndex >= 0)
        {
            var type = _packetTypes[cmbPacketTypes.SelectedIndex];
            try
            {
                _currentPacket = (IPacket?)Activator.CreateInstance(type);
                propertyGrid.SelectedObject = _currentPacket;
                UpdatePacketInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating instance of {type.Name}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void btnRandomize_Click(object sender, EventArgs e)
    {
        if (_currentPacket == null) return;

        RandomizeObject(_currentPacket);
        propertyGrid.Refresh();
        UpdatePacketInfo();
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        UpdatePacketInfo();
    }

    private void UpdatePacketInfo()
    {
        if (_currentPacket == null)
        {
            lblLength.Text = "Length: 0 bytes";
            txtHexView.Clear();
            return;
        }

        try
        {
            byte[] data = _currentPacket.Serialize();
            lblLength.Text = $"Length: {data.Length} bytes (Serialized)";
            txtHexView.Text = BitConverter.ToString(data).Replace("-", " ");
        }
        catch (Exception ex)
        {
            lblLength.Text = "Error calculating length";
            txtHexView.Text = $"Serialization error: {ex.Message}";
        }
    }

    private void RandomizeObject(object obj)
    {
        var random = new Random();
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var prop in properties)
        {
            // Skip MagicNumber
            if (prop.Name == "MagicNumber")
                continue;

            // Skip properties with [SerializeIgnore]
            if (prop.GetCustomAttribute<Nalix.Common.Serialization.SerializeIgnoreAttribute>() != null)
                continue;

            try
            {
                object? value = GetRandomValue(prop, random);
                if (value != null)
                {
                    prop.SetValue(obj, value);
                }
            }
            catch
            {
                // Ignore properties that can't be set or have complex logic
            }
        }
    }

    private object? GetRandomValue(PropertyInfo prop, Random random)
    {
        Type type = prop.PropertyType;

        // Check for SerializeDynamicSize limit
        var dynamicSizeAttr = prop.GetCustomAttribute<Nalix.Common.Serialization.SerializeDynamicSizeAttribute>();
        // If attribute present, use it as EXACT length to be predictable, else default to 32 or 100
        int maxSize = dynamicSizeAttr?.Size ?? 32; 
        if (maxSize <= 0) maxSize = 32;

        if (type == typeof(int)) return random.Next();
        if (type == typeof(uint)) return (uint)random.Next();
        if (type == typeof(short)) return (short)random.Next(short.MinValue, short.MaxValue);
        if (type == typeof(ushort)) return (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
        if (type == typeof(long)) return (long)random.NextInt64();
        if (type == typeof(ulong)) return (ulong)random.NextInt64();
        if (type == typeof(byte)) return (byte)random.Next(256);
        if (type == typeof(bool)) return random.Next(2) == 0;
        if (type == typeof(float)) return (float)random.NextDouble();
        if (type == typeof(double)) return random.NextDouble();
        if (type == typeof(string))
        {
            string[] samples = { "Nalix", "Packet", "Visualizer", "PPN", "System", "Protocol", "Data" };
            string text = samples[random.Next(samples.Length)] + "_" + random.Next(1000);
            // Limit to maxSize but keep it somewhat random if we want, 
            // but the user said "ngẫu nhiên" is a problem, so let's make it fixed if attribute is there.
            if (dynamicSizeAttr != null)
            {
                if (text.Length > maxSize) return text.Substring(0, maxSize);
                return text.PadRight(maxSize, ' ');
            }
            return text;
        }
        if (type == typeof(byte[]))
        {
            // Use EXACT maxSize if attribute is present to avoid "ngẫu nhiên" length
            int len = dynamicSizeAttr != null ? maxSize : random.Next(1, maxSize + 1);
            byte[] bytes = new byte[len];
            random.NextBytes(bytes);
            return bytes;
        }
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(random.Next(values.Length));
        }

        // Support for Snowflake (custom struct)
        if (type.Name == "Snowflake")
        {
            try
            {
                // Snowflake usually has a constructor or static factory.
                // Looking at Handshake.cs, it's a struct.
                // Assuming it has a Value property or we can just randomize via reflection if it was an object.
                // For now, let's try to find a 'New' or just return default if complex.
                // Actually, let's just use the default Activator.CreateInstance which gives a zeroed struct.
                // Better: try to find a field called 'Value' or similar.
                object snowflake = Activator.CreateInstance(type)!;
                var field = type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance) 
                    ?? type.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(ulong))
                {
                    field.SetValue(snowflake, (ulong)random.NextInt64());
                }
                return snowflake;
            }
            catch { }
        }

        return null;
    }

    private void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
    {
        UpdatePacketInfo();
    }
}
