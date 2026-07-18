using Blocky.Core.Data;
using Blocky.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Blocky.Tests.Services;

[TestFixture]
public class BlockyServiceTests
{
    private Mock<ILogger<BlockyService>> _loggerMock = null!;
    private Mock<IBlockyRuleRepo> _repoMock = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private BlockyService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<BlockyService>>();
        _repoMock = new Mock<IBlockyRuleRepo>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        _sut = new BlockyService(_loggerMock.Object, _repoMock.Object, _timeProviderMock.Object);
    }

    #region AddRuleAsync Tests

    [Test]
    public async Task AddRuleAsync_WithValidRule_CallsRepoAddAsync()
    {
        // Arrange
        var rule = CreateTestRule("example.com");

        // Act
        await _sut.AddRuleAsync(rule);

        // Assert
        _repoMock.Verify(r => r.AddAsync(rule), Times.Once);
    }

    [Test]
    public async Task AddRuleAsync_WithValidRule_RaisesRulesChangedEvent()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        var eventFired = false;
        _sut.RulesChanged += () => eventFired = true;

        // Act
        await _sut.AddRuleAsync(rule);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Test]
    public async Task AddRuleAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.AddRuleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateRuleAsync Tests

    [Test]
    public async Task UpdateRuleAsync_WithValidRule_CallsRepoUpdateAsync()
    {
        // Arrange
        var rule = CreateTestRule("example.com");

        // Act
        await _sut.UpdateRuleAsync(rule);

        // Assert
        _repoMock.Verify(r => r.UpdateAsync(rule), Times.Once);
    }

    [Test]
    public async Task UpdateRuleAsync_WithValidRule_RaisesRulesChangedEvent()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        var eventFired = false;
        _sut.RulesChanged += () => eventFired = true;

        // Act
        await _sut.UpdateRuleAsync(rule);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Test]
    public async Task UpdateRuleAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.UpdateRuleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region RemoveRuleAsync Tests

    [Test]
    public async Task RemoveRuleAsync_WithValidId_CallsRepoDeleteAsync()
    {
        // Arrange
        var ruleId = Guid.NewGuid();

        // Act
        await _sut.RemoveRuleAsync(ruleId);

        // Assert
        _repoMock.Verify(r => r.DeleteAsync(ruleId), Times.Once);
    }

    [Test]
    public async Task RemoveRuleAsync_WithValidId_RaisesRulesChangedEvent()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var eventFired = false;
        _sut.RulesChanged += () => eventFired = true;

        // Act
        await _sut.RemoveRuleAsync(ruleId);

        // Assert
        eventFired.Should().BeTrue();
    }

    [Test]
    public async Task RemoveRuleAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.RemoveRuleAsync(Guid.Empty);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id");
    }

    #endregion

    #region GetRuleAsync Tests

    [Test]
    public async Task GetRuleAsync_WithValidId_ReturnsRuleFromRepo()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = CreateTestRule("example.com", ruleId);
        _repoMock.Setup(r => r.GetByIdAsync(ruleId)).ReturnsAsync(expectedRule);

        // Act
        var result = await _sut.GetRuleAsync(ruleId);

        // Assert
        result.Should().Be(expectedRule);
        _repoMock.Verify(r => r.GetByIdAsync(ruleId), Times.Once);
    }

    [Test]
    public async Task GetRuleAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.GetRuleAsync(Guid.Empty);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id");
    }

    #endregion

    #region GetAllRulesAsync Tests

    [Test]
    public async Task GetAllRulesAsync_ReturnsAllRulesFromRepo()
    {
        // Arrange
        var expectedRules = new List<BlockyRule>
        {
            CreateTestRule("example.com"),
            CreateTestRule("test.com")
        };
        _repoMock.Setup(r => r.GetAllRulesAsync()).ReturnsAsync(expectedRules);

        // Act
        var result = await _sut.GetAllRulesAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedRules);
        _repoMock.Verify(r => r.GetAllRulesAsync(), Times.Once);
    }

    #endregion

    #region GetBlockedDomainsAsync Tests

    [Test]
    public async Task GetBlockedDomainsAsync_WithEnabledRules_ReturnsBlockedDomains()
    {
        // Arrange
        var rule1 = CreateTestRule("example.com");
        var rule2 = CreateTestRule("test.com");
        SetupActiveRules(rule1, rule2);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.GetBlockedDomainsAsync();

        // Assert
        result.Should().Contain("example.com");
        result.Should().Contain("test.com");
        result.Length.Should().Be(2);
    }

    [Test]
    public async Task GetBlockedDomainsAsync_WithTimeRestriction_WithinWindow_ReturnsDomain()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10)); // Within window

        // Act
        var result = await _sut.GetBlockedDomainsAsync();

        // Assert
        result.Should().Contain("example.com");
        result.Length.Should().Be(1);
    }

    [Test]
    public async Task GetBlockedDomainsAsync_WithTimeRestriction_OutsideWindow_ReturnsEmpty()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(18)); // Outside window

        // Act
        var result = await _sut.GetBlockedDomainsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetBlockedDomainsAsync_WithDuplicateDomains_ReturnsDistinct()
    {
        // Arrange
        var rule1 = CreateTestRule("example.com");
        var rule2 = CreateTestRule("example.com");
        var rule3 = CreateTestRule("EXAMPLE.COM"); // Case-insensitive duplicate
        SetupActiveRules(rule1, rule2, rule3);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.GetBlockedDomainsAsync();

        // Assert
        result.Length.Should().Be(1);
        result.Should().Contain("example.com");
    }

    [Test]
    public async Task GetBlockedDomainsAsync_WithNoActiveRules_ReturnsEmpty()
    {
        // Arrange
        SetupActiveRules(); // No rules
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.GetBlockedDomainsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static BlockyRule CreateTestRule(string domain, Guid? id = null, bool isEnabled = true)
    {
        return new BlockyRule
        {
            Id = id ?? Guid.NewGuid(),
            Domain = domain,
            IsEnabled = isEnabled,
            HasTimeRestriction = false,
            StartTime = null,
            EndTime = null
        };
    }

    private static BlockyRule CreateTestRuleWithTime(string domain, TimeSpan startTime, TimeSpan endTime, bool isEnabled = true)
    {
        return new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = domain,
            IsEnabled = isEnabled,
            HasTimeRestriction = true,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    private void SetupActiveRules(params BlockyRule[] rules)
    {
        _repoMock.Setup(r => r.GetActiveRulesAsync()).ReturnsAsync(rules.ToList());
    }

    private void SetupCurrentTime(TimeSpan time)
    {
        // LocalTimeZone is UTC (see Setup), so GetLocalNow() returns this value unshifted.
        var utcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).Add(time);
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(utcNow);
    }

    #endregion
}
