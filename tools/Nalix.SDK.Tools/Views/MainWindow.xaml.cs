using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nalix.SDK.Tools.ViewModels;

namespace Nalix.SDK.Tools.Views;

/// <summary>
/// Hosts the main packet testing tool shell.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => this.InitializeComponent();

    private void BuilderPacketTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || this.DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if ((comboBox.IsKeyboardFocusWithin || comboBox.IsDropDownOpen) &&
            viewModel.LoadSelectedPacketTypeCommand.CanExecute(null))
        {
            viewModel.LoadSelectedPacketTypeCommand.Execute(null);
        }
    }

    private void SentHistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel && viewModel.ReopenSelectedSentPacketCommand.CanExecute(null))
        {
            viewModel.ReopenSelectedSentPacketCommand.Execute(null);
        }
    }

    private void ReceivedHistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel && viewModel.InspectSelectedReceivedPacketCommand.CanExecute(null))
        {
            viewModel.InspectSelectedReceivedPacketCommand.Execute(null);
        }
    }

    private void CopySentHexButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.SentDetailRawHex))
        {
            Clipboard.SetText(viewModel.SentDetailRawHex);
        }
    }

    private void CopyReceivedHexButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.ReceivedDetailRawHex))
        {
            Clipboard.SetText(viewModel.ReceivedDetailRawHex);
        }
    }
}
