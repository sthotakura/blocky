using System.Collections.ObjectModel;
using System.Windows;
using Blocky.Data;
using Blocky.Messages;
using Blocky.Services;
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

    readonly Task _loadTask;

    [ObservableProperty] bool _settingsViewOpen = false;
    
    [ObservableProperty] SettingsViewModel _settingsViewModel;

    public MainWindowViewModel(IBlockyService blockyService, IApplication app, SettingsViewModel settingsViewModel, IMessenger messenger)
    {
        _blockyService = blockyService;
        _app = app;
        _settingsViewModel = settingsViewModel;

        messenger.Register(this);
        _loadTask = LoadAsync();
    }
    
    public bool IsRunning => _blockyService.IsRunning;

    public ObservableCollection<BlockyRule> Rules { get; private set; } = [];
    
    Task LoadAsync()
    {
        return _blockyService
            .GetAllRulesAsync()
            .ContinueWith(task =>
            {
                Rules = new ObservableCollection<BlockyRule>(task.Result);
                OnPropertyChanged(nameof(Rules));
            });
    }

    [RelayCommand]
    void ToggleSettings()
    {
        SettingsViewOpen = !SettingsViewOpen;
    }

    [RelayCommand]
    void ToggleProxy()
    {
        if(IsRunning)
        {
            _blockyService.StopAsync();
        }
        else
        {
            _blockyService.StartAsync();
        }
        
        OnPropertyChanged(nameof(IsRunning));
    }
    
    [RelayCommand]
    async Task AddRule()
    {
        var vm = new AddRuleDialogViewModel();
        var dialog = new AddRuleDialog
        {
            DataContext = vm,
            Owner = _app.MainWindow!,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        if(dialog.ShowDialog() == true)
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

    public void Receive(CloseSettingsViewMessage message)
    {
        SettingsViewOpen = false;
    }
}