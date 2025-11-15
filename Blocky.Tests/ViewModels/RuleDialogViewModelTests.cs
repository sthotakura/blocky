using Blocky.Data;
using Blocky.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace Blocky.Tests.ViewModels;

[TestFixture]
public class RuleDialogViewModelTests
{
    #region Constructor Tests

    [Test]
    public void Constructor_WithValidRule_InitializesProperties()
    {
        // Arrange
        var rule = new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = "example.com",
            HasTimeRestriction = true,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        };

        // Act
        var viewModel = new RuleDialogViewModel(rule);

        // Assert
        viewModel.Domain.Should().Be("example.com");
        viewModel.HasTimeRestriction.Should().BeTrue();
        viewModel.StartTime.Should().Be(TimeSpan.FromHours(9));
        viewModel.EndTime.Should().Be(TimeSpan.FromHours(17));
    }

    [Test]
    public void Constructor_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new RuleDialogViewModel(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WithRuleWithoutTimeRestriction_InitializesCorrectly()
    {
        // Arrange
        var rule = new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = "example.com",
            HasTimeRestriction = false,
            StartTime = null,
            EndTime = null
        };

        // Act
        var viewModel = new RuleDialogViewModel(rule);

        // Assert
        viewModel.Domain.Should().Be("example.com");
        viewModel.HasTimeRestriction.Should().BeFalse();
        viewModel.StartTime.Should().BeNull();
        viewModel.EndTime.Should().BeNull();
    }

    #endregion

    #region Domain Validation Tests

    [Test]
    public void Domain_WithValidDomain_NoValidationErrors()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule);

        // Act
        viewModel.Domain = "example.com";
        viewModel.ValidateAllProperties();

        // Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    [Test]
    public void Domain_WithEmptyDomain_HasValidationError()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule);

        // Act
        viewModel.Domain = "";
        viewModel.ValidateAllProperties();

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrors(nameof(viewModel.Domain)).Cast<string>().ToList();
        errors.Should().Contain("Domain is required");
    }

    [Test]
    public void Domain_WithLessThan3Characters_HasValidationError()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule);

        // Act
        viewModel.Domain = "ab";
        viewModel.ValidateAllProperties();

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrors(nameof(viewModel.Domain)).Cast<string>().ToList();
        errors.Should().Contain("Domain must be at least 3 characters");
    }

    [Test]
    public void Domain_WithExactly3Characters_NoValidationError()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule);

        // Act
        viewModel.Domain = "abc";
        viewModel.ValidateAllProperties();

        // Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Time Restriction Tests

    [Test]
    public void HasTimeRestriction_WhenSetToFalse_ClearsStartAndEndTimes()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule)
        {
            HasTimeRestriction = true,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        };

        // Act
        viewModel.HasTimeRestriction = false;

        // Assert
        viewModel.StartTime.Should().BeNull();
        viewModel.EndTime.Should().BeNull();
    }

    [Test]
    public void HasTimeRestriction_WhenSetToTrue_WithNullTimes_SetsDefaultTimes()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule)
        {
            HasTimeRestriction = false,
            StartTime = null,
            EndTime = null
        };

        // Act
        viewModel.HasTimeRestriction = true;

        // Assert
        viewModel.StartTime.Should().Be(TimeSpan.FromHours(8));
        viewModel.EndTime.Should().Be(TimeSpan.FromHours(19));
    }

    [Test]
    public void HasTimeRestriction_WhenSetToTrue_WithExistingTimes_KeepsExistingTimes()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule)
        {
            HasTimeRestriction = false,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(16)
        };

        // Act
        viewModel.HasTimeRestriction = true;

        // Assert
        viewModel.StartTime.Should().Be(TimeSpan.FromHours(10));
        viewModel.EndTime.Should().Be(TimeSpan.FromHours(16));
    }

    #endregion

    #region Time Range Validation Tests

    [Test]
    public void ValidateTimeRange_WhenStartTimeEqualsEndTime_HasValidationError()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule)
        {
            Domain = "example.com",
            HasTimeRestriction = true,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(10)
        };

        // Act
        viewModel.ValidateAllProperties();
        // Need to manually trigger time range validation since it's called in SaveCommand
        var saveMethod = typeof(RuleDialogViewModel)
            .GetMethod("ValidateTimeRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        saveMethod?.Invoke(viewModel, null);

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrors(nameof(viewModel.StartTime)).Cast<string>().ToList();
        errors.Should().Contain("Start time cannot be the same as end time");
    }

    [Test]
    public void ValidateTimeRange_WhenTimesAreDifferent_NoValidationError()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule)
        {
            Domain = "example.com",
            HasTimeRestriction = true,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        };

        // Act
        viewModel.ValidateAllProperties();
        var saveMethod = typeof(RuleDialogViewModel)
            .GetMethod("ValidateTimeRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        saveMethod?.Invoke(viewModel, null);

        // Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region ToBlockyRule Tests

    [Test]
    public void ToBlockyRule_WithoutTimeRestriction_ReturnsRuleWithNullTimes()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(originalRule)
        {
            Domain = "example.com",
            HasTimeRestriction = false
        };

        // Act
        var result = viewModel.ToBlockyRule();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(originalRule.Id);
        result.Domain.Should().Be("example.com");
        result.IsEnabled.Should().BeTrue();
        result.HasTimeRestriction.Should().BeFalse();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
    }

    [Test]
    public void ToBlockyRule_WithTimeRestriction_ReturnsRuleWithTimes()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(originalRule)
        {
            Domain = "example.com",
            HasTimeRestriction = true,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        };

        // Act
        var result = viewModel.ToBlockyRule();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(originalRule.Id);
        result.Domain.Should().Be("example.com");
        result.IsEnabled.Should().BeTrue();
        result.HasTimeRestriction.Should().BeTrue();
        result.StartTime.Should().Be(TimeSpan.FromHours(9));
        result.EndTime.Should().Be(TimeSpan.FromHours(17));
    }

    [Test]
    public void ToBlockyRule_PreservesOriginalRuleId()
    {
        // Arrange
        var originalId = Guid.NewGuid();
        var originalRule = CreateTestRule(originalId);
        var viewModel = new RuleDialogViewModel(originalRule)
        {
            Domain = "changed.com"
        };

        // Act
        var result = viewModel.ToBlockyRule();

        // Assert
        result.Id.Should().Be(originalId);
    }

    [Test]
    public void ToBlockyRule_AlwaysSetsIsEnabledToTrue()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(originalRule)
        {
            Domain = "example.com"
        };

        // Act
        var result = viewModel.ToBlockyRule();

        // Assert
        result.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region ClearErrors Tests

    [Test]
    public void ClearErrors_RemovesAllValidationErrors()
    {
        // Arrange
        var rule = CreateTestRule();
        var viewModel = new RuleDialogViewModel(rule);
        viewModel.Domain = ""; // Create validation error
        viewModel.ValidateAllProperties();

        // Verify there are errors
        viewModel.HasErrors.Should().BeTrue();

        // Act
        viewModel.ClearErrors();

        // Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static BlockyRule CreateTestRule(Guid? id = null)
    {
        return new BlockyRule
        {
            Id = id ?? Guid.NewGuid(),
            Domain = "test.com",
            IsEnabled = true,
            HasTimeRestriction = false
        };
    }

    #endregion
}
