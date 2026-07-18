using Blocky.ViewModels;

namespace Blocky.Views;

public partial class RuleDialog
{
    public RuleDialog()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.NewValue is RuleDialogViewModel viewModel)
            {
                viewModel.CloseRequested += result => DialogResult = result;
            }
        };
    }
}
