using System.Windows;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Services;
using Nalix.SDK.Tools.ViewModels;
using Nalix.SDK.Tools.Views;

namespace Nalix.SDK.Tools;

/// <summary>
/// Provides the application entry point for the packet testing tool.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IPacketCatalogService catalogService = new PacketCatalogService();
        ITcpClientService tcpClientService = new TcpClientService(catalogService);
        MainWindowViewModel viewModel = new(catalogService, tcpClientService);

        MainWindow window = new()
        {
            DataContext = viewModel
        };

        window.Show();
    }
}
