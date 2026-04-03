using System.Windows;
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

        PacketCatalogService catalogService = new();
        TcpClientService tcpClientService = new(catalogService);
        MainWindowViewModel viewModel = new(catalogService, tcpClientService);

        MainWindow window = new()
        {
            DataContext = viewModel
        };

        window.Show();
    }
}
