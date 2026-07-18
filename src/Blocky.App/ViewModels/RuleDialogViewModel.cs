using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Blocky.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Blocky.ViewModels;

public partial class RuleDialogViewModel : ObservableValidator
{
    readonly BlockyRule _rule;

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
    [CustomValidation(typeof(RuleDialogViewModel), nameof(ValidateDomainFormat))]
    string _domain = string.Empty;

    [ObservableProperty] bool _hasTimeRestriction;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(RuleDialogViewModel), nameof(ValidateTimeRange))]
    TimeSpan? _startTime = new TimeSpan(8, 0, 0);

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

    /// <summary>
    /// Raised when the dialog should close; the argument is the dialog result.
    /// The view subscribes — the ViewModel never touches the window.
    /// </summary>
    public event Action<bool>? CloseRequested;

    [RelayCommand]
    void Save()
    {
        ClearErrors();
        ValidateAllProperties();

        if (HasErrors)
            return;

        CloseRequested?.Invoke(true);
    }

    public static ValidationResult? ValidateDomainFormat(string? domain, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return ValidationResult.Success; // Already handled by the Required attribute
        }

        var trimmedDomain = domain.Trim();

        if (trimmedDomain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedDomain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(
                "Domain should not include protocol (http:// or https://)",
                [nameof(Domain)]);
        }

        if (trimmedDomain.Contains('/') || trimmedDomain.Contains('?') || trimmedDomain.Contains('#'))
        {
            return new ValidationResult(
                "Domain should not include paths, query strings, or fragments",
                [nameof(Domain)]);
        }

        if (trimmedDomain.Contains(':'))
        {
            return new ValidationResult(
                "Domain should not include port numbers",
                [nameof(Domain)]);
        }

        if (!DomainRegex.IsMatch(trimmedDomain))
        {
            return new ValidationResult(
                "Invalid domain format. Use format like: example.com or subdomain.example.com",
                [nameof(Domain)]);
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateTimeRange(TimeSpan? startTime, ValidationContext context)
    {
        var viewModel = (RuleDialogViewModel)context.ObjectInstance;

        if (!viewModel.HasTimeRestriction)
        {
            return ValidationResult.Success;
        }

        if (viewModel.StartTime == null || viewModel.EndTime == null)
        {
            return new ValidationResult(
                "Time range must be specified when time restriction is enabled",
                [nameof(StartTime)]);
        }

        if (viewModel.StartTime == viewModel.EndTime)
        {
            return new ValidationResult(
                "Start time cannot be the same as end time",
                [nameof(StartTime)]);
        }

        return ValidationResult.Success;
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
