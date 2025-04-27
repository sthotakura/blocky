using System.ComponentModel.DataAnnotations;
using System.Windows;
using Blocky.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Blocky.ViewModels;

public partial class RuleDialogViewModel : ObservableValidator
{
    readonly BlockyRule _rule;
    readonly string[] _timeNames = ["Start Time", "End Time"];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Domain is required")]
    [MinLength(3, ErrorMessage = "Domain must be at least 3 characters")]
    string _domain = string.Empty;

    [ObservableProperty] bool _hasTimeRestriction;

    [ObservableProperty] TimeSpan? _startTime = new TimeSpan(8, 0, 0);

    [ObservableProperty] TimeSpan? _endTime = new TimeSpan(19, 0, 0);

    public RuleDialogViewModel(BlockyRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));

        Domain = _rule.Domain;
        HasTimeRestriction = _rule.HasTimeRestriction;
        StartTime = _rule.StartTime;
        EndTime = _rule.EndTime;
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
            StartTime = new TimeSpan(8, 0, 0);
            EndTime = new TimeSpan(19, 0, 0);
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
            var validationResult = new ValidationResult("Time range must be specified when time restriction is enabled",
                _timeNames);
            ValidateProperty(validationResult, nameof(StartTime));
            return;
        }

        if (StartTime == EndTime)
        {
            var validationResult = new ValidationResult(
                "Start time cannot be the same as end time",
                _timeNames);
            ValidateProperty(validationResult, nameof(StartTime));
        }
    }

    public BlockyRule ToBlockyRule()
    {
        return new BlockyRule
        {
            Id = _rule.Id,
            Domain = Domain,
            IsEnabled = true,
            HasTimeRestriction = HasTimeRestriction,
            StartTime = HasTimeRestriction ? StartTime : null,
            EndTime = HasTimeRestriction ? EndTime : null
        };
    }
}