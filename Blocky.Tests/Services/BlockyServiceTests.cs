using Blocky.Data;
using Blocky.Services;
using Blocky.Services.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Blocky.Tests.Services;

[TestFixture]
public class BlockyServiceTests
{
    private Mock<ILogger<BlockyService>> _loggerMock = null!;
    private Mock<ICachedBlockyRuleRepo> _repoMock = null!;
    private Mock<IDateTimeService> _dateTimeServiceMock = null!;
    private BlockyService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<BlockyService>>();
        _repoMock = new Mock<ICachedBlockyRuleRepo>();
        _dateTimeServiceMock = new Mock<IDateTimeService>();
        _sut = new BlockyService(_loggerMock.Object, _repoMock.Object, _dateTimeServiceMock.Object);
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
    public void AddRuleAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.AddRuleAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
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
    public void UpdateRuleAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.UpdateRuleAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
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
    public void RemoveRuleAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.RemoveRuleAsync(Guid.Empty);
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id");
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
    public void GetRuleAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.GetRuleAsync(Guid.Empty);
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id");
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

    #region IsDomainBlockedAsync Tests

    [Test]
    public async Task IsDomainBlockedAsync_ExactDomainMatch_ReturnsTrue()
    {
        // Arrange
        var domain = "example.com";
        var rule = CreateTestRule(domain);
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync(domain);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WwwPrefixDomainMatch_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync("www.example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsDomainBlockedAsync_CaseInsensitiveMatch_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestRule("Example.COM");
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsDomainBlockedAsync_NoMatchingRule_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync("different.com");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsDomainBlockedAsync_DisabledRule_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule("example.com", isEnabled: false);
        SetupActiveRules(); // GetActiveRulesAsync should not return disabled rules
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WithTimeRestriction_WithinTimeWindow_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(10)); // 10am - within window

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WithTimeRestriction_BeforeTimeWindow_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(7)); // 7am - before window

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WithTimeRestriction_AfterTimeWindow_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(18)); // 6pm - after window

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WithTimeRestriction_AtEndTime_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(17)); // Exactly at end time (exclusive)

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsDomainBlockedAsync_WithTimeRestriction_AtStartTime_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));
        SetupActiveRules(rule);
        SetupCurrentTime(TimeSpan.FromHours(8)); // Exactly at start time (inclusive)

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsDomainBlockedAsync_WithNullDomain_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.IsDomainBlockedAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void IsDomainBlockedAsync_WithEmptyDomain_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.IsDomainBlockedAsync("");
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Domain cannot be null or empty*");
    }

    [Test]
    public void IsDomainBlockedAsync_WithWhitespaceDomain_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.IsDomainBlockedAsync("   ");
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Domain cannot be null or empty*");
    }

    [Test]
    public async Task IsDomainBlockedAsync_MultipleMatchingRules_ReturnsTrue()
    {
        // Arrange
        var rule1 = CreateTestRule("example.com");
        var rule2 = CreateTestRule("example.com");
        SetupActiveRules(rule1, rule2);
        SetupCurrentTime(TimeSpan.FromHours(10));

        // Act
        var result = await _sut.IsDomainBlockedAsync("example.com");

        // Assert
        result.Should().BeTrue();
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
        _dateTimeServiceMock.Setup(d => d.Now).Returns(new DateTime(2025, 1, 1).Add(time));
    }

    #endregion
}
