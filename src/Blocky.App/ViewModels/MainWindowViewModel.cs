using System.Collections.ObjectModel;
using Blocky.Core.Data;
using Blocky.Services;
using Blocky.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Blocky.ViewModels;

[UsedImplicitly]
public partial class MainWindowViewModel : ObservableObject
{
    readonly ILogger<MainWindowViewModel> _logger;
    readonly IBlockyService _blockyService;
    readonly IApplication _app;
    readonly ILogConfig _logConfig;
    readonly IDialogService _dialogService;
    readonly IShellService _shellService;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IBlockyService blockyService,
        IApplication app,
        ILogConfig logConfig,
        IDialogService dialogService,
        IShellService shellService)
    {
        _logger = logger;
        _blockyService = blockyService;
        _app = app;
        _logConfig = logConfig;
        _dialogService = dialogService;
        _shellService = shellService;

        InitializationTask = LoadAsync();
    }

    /// <summary>Completes when the initial rule load has finished (successfully or not).</summary>
    public Task InitializationTask { get; }

    public ObservableCollection<BlockyRule> Rules { get; } = [];

    [ObservableProperty]
    string? _loadError;

    async Task LoadAsync()
    {
        try
        {
            var rules = await _blockyService.GetAllRulesAsync();
            foreach (var rule in rules)
            {
                Rules.Add(rule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rules");
            LoadError = $"Failed to load rules: {ex.Message}";
        }
    }

    [RelayCommand]
    void Quit()
    {
        _app.Shutdown();
    }

    [RelayCommand]
    async Task AddRule()
    {
        await InitializationTask;

        var vm = new RuleDialogViewModel(new BlockyRule());
        if (!_dialogService.ShowRuleDialog(vm))
        {
            return;
        }

        var rule = vm.ToBlockyRule();
        try
        {
            await _blockyService.AddRuleAsync(rule);
            Rules.Add(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add rule {domain}", rule.Domain);
            _dialogService.ShowError("Add Rule", $"Failed to add rule: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task EditRule(BlockyRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        await InitializationTask;

        var vm = new RuleDialogViewModel(rule);
        if (!_dialogService.ShowRuleDialog(vm))
        {
            return;
        }

        var updatedRule = vm.ToBlockyRule();
        try
        {
            await _blockyService.UpdateRuleAsync(updatedRule);
            var index = Rules.IndexOf(rule);
            Rules[index] = updatedRule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update rule {domain}", updatedRule.Domain);
            _dialogService.ShowError("Edit Rule", $"Failed to update rule: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task RemoveRule(BlockyRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        try
        {
            await _blockyService.RemoveRuleAsync(rule.Id);
            Rules.Remove(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove rule {domain}", rule.Domain);
            _dialogService.ShowError("Remove Rule", $"Failed to remove rule: {ex.Message}");
        }
    }

    [RelayCommand]
    void OpenLog()
    {
        try
        {
            _shellService.OpenFile(_logConfig.GetCurrentLogFilePath());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log file");
            _dialogService.ShowError("Open Log", $"Failed to open log file: {ex.Message}");
        }
    }
}
