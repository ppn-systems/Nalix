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

        AppConfigurationService appConfigurationService = new();
        this.Resources[ToolResourceHelper.ToolTextsResourceKey] = appConfigurationService.Texts;

        AppThemeService themeService = new();
        themeService.ApplyTheme(appConfigurationService.Appearance.ThemeMode);

        IFileDialogService fileDialogService = new FileDialogService();
        IPacketCatalogService catalogService = new PacketCatalogService();
        ITcpClientService tcpClientService = new TcpClientService(catalogService, appConfigurationService);
        MainWindowViewModel viewModel = new(catalogService, tcpClientService, appConfigurationService, themeService, fileDialogService);

        MainWindow window = new()
        {
            DataContext = viewModel
        };

        window.Show();
    }
}
