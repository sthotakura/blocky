using System.Windows;
using Blocky.Services.Contracts;
using Blocky.ViewModels;
using Blocky.Views;

namespace Blocky.Services;

public sealed class DialogService(IApplication app) : IDialogService
{
    public bool ShowRuleDialog(RuleDialogViewModel viewModel)
    {
        var dialog = new RuleDialog
        {
            DataContext = viewModel,
            Owner = app.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        return dialog.ShowDialog() == true;
    }

    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
