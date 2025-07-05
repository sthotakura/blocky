using System.ComponentModel.DataAnnotations;
using Blocky.Data;
using Blocky.Messages;
using Blocky.Services;
using Blocky.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JetBrains.Annotations;

namespace Blocky.ViewModels;

[UsedImplicitly]
public partial class SettingsViewModel : ObservableValidator
{
    readonly ISettingsService _settingsService;
    readonly IMessenger _messenger;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1024, 65535, ErrorMessage = "Port must be between 1024 and 65535")]
    int _proxyPort;

    // ReSharper disable once NotAccessedField.Local
    readonly Task _loadSettingsTask;

    public SettingsViewModel(ISettingsService settingsService, IMessenger messenger)
    {
        _settingsService = settingsService;
        _messenger = messenger;
        _loadSettingsTask = LoadSettings();
    }

    async Task LoadSettings()
    {
        var settings = await _settingsService.GetSettingsAsync();
        ProxyPort = settings.ProxyPort;
    }

    [RelayCommand]
    async Task Save()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        await _settingsService.UpdateSettingsAsync(new BlockySettings 
        { 
            ProxyPort = ProxyPort 
        });
        
        _messenger.Send(new CloseSettingsViewMessage());
    }
}