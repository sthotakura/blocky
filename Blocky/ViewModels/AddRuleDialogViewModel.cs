using System.ComponentModel.DataAnnotations;
using System.Windows;
using Blocky.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JetBrains.Annotations;

namespace Blocky.ViewModels;

[UsedImplicitly]
public partial class AddRuleDialogViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Domain is required")]
    [MinLength(3, ErrorMessage = "Domain must be at least 3 characters")]
    string _domain = string.Empty;

    [ObservableProperty] bool _hasTimeRestriction;

    [ObservableProperty] TimeSpan? _startTime = new TimeSpan(9, 0, 0); // Default 9 AM

    [ObservableProperty] TimeSpan? _endTime = new TimeSpan(17, 0, 0); // Default 5 PM

    public BlockyRule ToBlockyRule()
    {
        return new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = Domain,
            IsEnabled = true,
            HasTimeRestriction = HasTimeRestriction,
            StartTime = HasTimeRestriction ? StartTime : null,
            EndTime = HasTimeRestriction ? EndTime : null
        };
    }

    partial void OnHasTimeRestrictionChanged(bool value)
    {
        if (!value)
        {
            StartTime = null;
            EndTime = null;
        }
        else if (StartTime == null || EndTime == null)
        {
            StartTime = new TimeSpan(9, 0, 0);
            EndTime = new TimeSpan(17, 0, 0);
        }
    }

    [RelayCommand]
    void Save(Window window)
    {
        ClearErrors();
        
        ValidateAllProperties();

        if (HasErrors)
            return;

        if (HasTimeRestriction)
        {
            ValidateTimeRange();
            if (HasErrors)
                return;
        }

        window.DialogResult = true;
        window.Close();
    }

    void ValidateTimeRange()
    {
        if (StartTime == null || EndTime == null)
        {
            // Using proper validation method
            var validationResult = new ValidationResult(
                "Time range must be specified when time restriction is enabled",
                [nameof(StartTime)]);
            ValidateProperty(validationResult, nameof(StartTime));
            return;
        }

        if (StartTime == EndTime)
        {
            var validationResult = new ValidationResult(
                "Start time cannot be the same as end time",
                [nameof(StartTime)]);
            ValidateProperty(validationResult, nameof(StartTime));
        }
    }
}