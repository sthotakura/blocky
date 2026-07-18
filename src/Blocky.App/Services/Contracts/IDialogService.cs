using Blocky.ViewModels;

namespace Blocky.Services.Contracts;

/// <summary>
/// Keeps ViewModels free of any direct View/window dependency so they stay unit-testable.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows the rule editor modally; true when the user saved.</summary>
    bool ShowRuleDialog(RuleDialogViewModel viewModel);

    void ShowError(string title, string message);
}
