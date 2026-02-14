using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Windows;
using Blocky.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Blocky.ViewModels;

public partial class RuleDialogViewModel : ObservableValidator
{
    readonly BlockyRule _rule;
    readonly string[] _timeNames = ["Start Time", "End Time"];

    // Domain validation regex: allows domain names like example.com, sub.example.com
    // Does not allow protocols (http://), paths (/path), ports (:8080), or special chars
    static readonly Regex DomainRegex = new(
        @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$",
        RegexOptions.Compiled);

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Domain is required")]
    [MinLength(3, ErrorMessage = "Domain must be at least 3 characters")]
    [MaxLength(253, ErrorMessage = "Domain must be less than 253 characters")]
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

        // Validate domain format
        ValidateDomainFormat();
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

    void ValidateDomainFormat()
    {
        if (string.IsNullOrWhiteSpace(Domain))
        {
            return; // Already handled by Required attribute
        }

        var trimmedDomain = Domain.Trim();

        // Check for invalid patterns
        if (trimmedDomain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedDomain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var validationResult = new ValidationResult(
                "Domain should not include protocol (http:// or https://)",
                [nameof(Domain)]);
            ValidateProperty(validationResult, nameof(Domain));
            return;
        }

        if (trimmedDomain.Contains('/') || trimmedDomain.Contains('?') || trimmedDomain.Contains('#'))
        {
            var validationResult = new ValidationResult(
                "Domain should not include paths, query strings, or fragments",
                [nameof(Domain)]);
            ValidateProperty(validationResult, nameof(Domain));
            return;
        }

        if (trimmedDomain.Contains(':') && !trimmedDomain.StartsWith('[')) // Allow IPv6
        {
            var validationResult = new ValidationResult(
                "Domain should not include port numbers",
                [nameof(Domain)]);
            ValidateProperty(validationResult, nameof(Domain));
            return;
        }

        // Validate against domain format regex
        if (!DomainRegex.IsMatch(trimmedDomain))
        {
            var validationResult = new ValidationResult(
                "Invalid domain format. Use format like: example.com or subdomain.example.com",
                [nameof(Domain)]);
            ValidateProperty(validationResult, nameof(Domain));
        }
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
            Domain = Domain.Trim(), // Trim whitespace from domain
            IsEnabled = true,
            HasTimeRestriction = HasTimeRestriction,
            StartTime = HasTimeRestriction ? StartTime : null,
            EndTime = HasTimeRestriction ? EndTime : null,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = _rule.CreatedAt
        };
    }
}