using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Blocky.Data;
using Blocky.Messages;
using Blocky.Services;
using Blocky.Services.Contracts;
using Blocky.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JetBrains.Annotations;

namespace Blocky.ViewModels;

[UsedImplicitly]
public partial class MainWindowViewModel : ObservableObject, IRecipient<CloseSettingsViewMessage>
{
    readonly IBlockyService _blockyService;
    readonly IApplication _app;
    readonly ILogConfig _logConfig;

    readonly Task _loadTask;

    [ObservableProperty] bool _settingsViewOpen;

    [ObservableProperty] SettingsViewModel _settingsViewModel;

    public MainWindowViewModel(IBlockyService blockyService, IApplication app, SettingsViewModel settingsViewModel,
        IMessenger messenger, ILogConfig logConfig)
    {
        _blockyService = blockyService;
        _app = app;
        _settingsViewModel = settingsViewModel;
        _logConfig = logConfig;

        messenger.Register(this);
        _loadTask = LoadAsync();
    }

    // ReSharper disable once CollectionNeverQueried.Global - Bound to UI
    public ObservableCollection<BlockyRule> Rules { get; private set; } = [];

    async Task LoadAsync()
    {
        var rules = await _blockyService.GetAllRulesAsync();
        Rules = new ObservableCollection<BlockyRule>(rules);
        OnPropertyChanged(nameof(Rules));
    }

    [RelayCommand]
    void ToggleSettings() => SettingsViewOpen = !SettingsViewOpen;

    [RelayCommand]
    void Quit()
    {
        _app.Shutdown();
    }

    [RelayCommand]
    async Task AddRule()
    {
        await _loadTask;

        var vm = new RuleDialogViewModel(new BlockyRule());
        var dialog = new RuleDialog
        {
            DataContext = vm,
            Owner = _app.MainWindow!,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (dialog.ShowDialog() == true)
        {
            var rule = vm.ToBlockyRule();
            await _blockyService.AddRuleAsync(rule);
            Rules.Add(rule);
        }
    }

    [RelayCommand]
    async Task RemoveRule(object o)
    {
        if (o is BlockyRule rule)
        {
            await _blockyService.RemoveRuleAsync(rule.Id);
            Rules.Remove(rule);
        }
    }

    [RelayCommand]
    async Task EditRule(object o)
    {
        if (o is BlockyRule rule)
        {
            await _loadTask;

            var vm = new RuleDialogViewModel(rule);
            var dialog = new RuleDialog
            {
                DataContext = vm,
                Owner = _app.MainWindow!,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                var updatedRule = vm.ToBlockyRule();
                await _blockyService.UpdateRuleAsync(updatedRule);

                var index = Rules.IndexOf(rule);
                Rules[index] = updatedRule;
            }
        }
    }

    [RelayCommand]
    void OpenLog()
    {
        try
        {
            var logFilePath = _logConfig.GetCurrentLogFilePath();
            
            Process.Start(new ProcessStartInfo
            {
                FileName = logFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void IRecipient<CloseSettingsViewMessage>.Receive(CloseSettingsViewMessage message) => SettingsViewOpen = false;
}
