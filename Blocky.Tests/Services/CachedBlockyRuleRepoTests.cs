using Blocky.Data;
using Blocky.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Blocky.Tests.Services;

[TestFixture]
public class CachedBlockyRuleRepoTests
{
    private Mock<ILogger<CachedBlockyRuleRepo>> _loggerMock = null!;
    private Mock<IBlockyRuleRepo> _repoMock = null!;
    private CachedBlockyRuleRepo _sut = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<CachedBlockyRuleRepo>>();
        _repoMock = new Mock<IBlockyRuleRepo>();
        _sut = new CachedBlockyRuleRepo(_loggerMock.Object, _repoMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _sut?.Dispose();
    }

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_FirstCall_FetchesFromRepo()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = CreateTestRule("example.com", ruleId);
        _repoMock.Setup(r => r.GetByIdAsync(ruleId)).ReturnsAsync(expectedRule);

        // Act
        var result = await _sut.GetByIdAsync(ruleId);

        // Assert
        result.Should().Be(expectedRule);
        _repoMock.Verify(r => r.GetByIdAsync(ruleId), Times.Once);
    }

    [Test]
    public async Task GetByIdAsync_SecondCall_ReturnsCachedValue()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = CreateTestRule("example.com", ruleId);
        _repoMock.Setup(r => r.GetByIdAsync(ruleId)).ReturnsAsync(expectedRule);

        // Act
        var result1 = await _sut.GetByIdAsync(ruleId);
        var result2 = await _sut.GetByIdAsync(ruleId);

        // Assert
        result1.Should().Be(expectedRule);
        result2.Should().Be(expectedRule);
        _repoMock.Verify(r => r.GetByIdAsync(ruleId), Times.Once); // Called only once
    }

    [Test]
    public void GetByIdAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.GetByIdAsync(Guid.Empty);
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id*");
    }

    [Test]
    public async Task GetByIdAsync_ConcurrentCalls_FetchesFromRepoOnlyOnce()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = CreateTestRule("example.com", ruleId);
        _repoMock.Setup(r => r.GetByIdAsync(ruleId))
            .ReturnsAsync(expectedRule)
            .Verifiable();

        // Act - Make 10 concurrent calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetByIdAsync(ruleId).AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBe(expectedRule);
        _repoMock.Verify(r => r.GetByIdAsync(ruleId), Times.Once); // Should be called only once due to locking
    }

    #endregion

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_ValidRule_CallsRepoAndCachesRule()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        _repoMock.Setup(r => r.AddAsync(rule)).Returns(Task.CompletedTask);

        // Act
        await _sut.AddAsync(rule);

        // Assert
        _repoMock.Verify(r => r.AddAsync(rule), Times.Once);

        // Verify it's cached by calling GetByIdAsync
        _repoMock.Setup(r => r.GetByIdAsync(rule.Id)).ReturnsAsync(rule);
        var cachedRule = await _sut.GetByIdAsync(rule.Id);
        cachedRule.Should().Be(rule);
        _repoMock.Verify(r => r.GetByIdAsync(rule.Id), Times.Never); // Should not call repo, should use cache
    }

    [Test]
    public void AddAsync_NullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.AddAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ValidRule_CallsRepoAndUpdatesCache()
    {
        // Arrange
        var originalRule = CreateTestRule("example.com");
        var updatedRule = new BlockyRule
        {
            Id = originalRule.Id,
            Domain = "updated.com",
            IsEnabled = true
        };

        _repoMock.Setup(r => r.GetByIdAsync(originalRule.Id)).ReturnsAsync(originalRule);
        _repoMock.Setup(r => r.UpdateAsync(updatedRule)).Returns(Task.CompletedTask);

        // Add to cache first
        await _sut.GetByIdAsync(originalRule.Id);

        // Act
        await _sut.UpdateAsync(updatedRule);

        // Assert
        _repoMock.Verify(r => r.UpdateAsync(updatedRule), Times.Once);

        // Verify cache is updated
        _repoMock.Setup(r => r.GetByIdAsync(updatedRule.Id)).ReturnsAsync(updatedRule);
        var cachedRule = await _sut.GetByIdAsync(updatedRule.Id);
        cachedRule.Should().Be(updatedRule);
        cachedRule!.Domain.Should().Be("updated.com");
    }

    [Test]
    public void UpdateAsync_NullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.UpdateAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ValidId_CallsRepoAndRemovesFromCache()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var rule = CreateTestRule("example.com", ruleId);
        _repoMock.Setup(r => r.GetByIdAsync(ruleId)).ReturnsAsync(rule);
        _repoMock.Setup(r => r.DeleteAsync(ruleId)).Returns(Task.CompletedTask);

        // Add to cache first
        await _sut.GetByIdAsync(ruleId);

        // Act
        await _sut.DeleteAsync(ruleId);

        // Assert
        _repoMock.Verify(r => r.DeleteAsync(ruleId), Times.Once);

        // Verify it's removed from cache
        _repoMock.Setup(r => r.GetByIdAsync(ruleId)).ReturnsAsync((BlockyRule?)null);
        await _sut.GetByIdAsync(ruleId);
        _repoMock.Verify(r => r.GetByIdAsync(ruleId), Times.Once); // Should call repo again since cache was cleared
    }

    [Test]
    public void DeleteAsync_WithEmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.DeleteAsync(Guid.Empty);
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id*");
    }

    #endregion

    #region GetAllRulesAsync Tests

    [Test]
    public async Task GetAllRulesAsync_FirstCall_FetchesFromRepo()
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

    [Test]
    public async Task GetAllRulesAsync_SecondCall_ReturnsCachedValue()
    {
        // Arrange
        var expectedRules = new List<BlockyRule>
        {
            CreateTestRule("example.com"),
            CreateTestRule("test.com")
        };
        _repoMock.Setup(r => r.GetAllRulesAsync()).ReturnsAsync(expectedRules);

        // Act
        var result1 = await _sut.GetAllRulesAsync();
        var result2 = await _sut.GetAllRulesAsync();

        // Assert
        result1.Should().BeEquivalentTo(expectedRules);
        result2.Should().BeEquivalentTo(expectedRules);
        _repoMock.Verify(r => r.GetAllRulesAsync(), Times.Once); // Called only once
    }

    [Test]
    public async Task GetAllRulesAsync_ConcurrentCalls_FetchesFromRepoOnlyOnce()
    {
        // Arrange
        var expectedRules = new List<BlockyRule>
        {
            CreateTestRule("example.com"),
            CreateTestRule("test.com")
        };
        _repoMock.Setup(r => r.GetAllRulesAsync())
            .ReturnsAsync(expectedRules)
            .Verifiable();

        // Act - Make 10 concurrent calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetAllRulesAsync())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeEquivalentTo(expectedRules));
        _repoMock.Verify(r => r.GetAllRulesAsync(), Times.Once); // Should be called only once due to locking
    }

    #endregion

    #region GetActiveRulesAsync Tests

    [Test]
    public async Task GetActiveRulesAsync_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var allRules = new List<BlockyRule>
        {
            CreateTestRule("example.com", isEnabled: true),
            CreateTestRule("disabled.com", isEnabled: false),
            CreateTestRule("test.com", isEnabled: true)
        };
        _repoMock.Setup(r => r.GetAllRulesAsync()).ReturnsAsync(allRules);

        // Act
        var result = await _sut.GetActiveRulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.IsEnabled);
        result.Select(r => r.Domain).Should().BeEquivalentTo(new[] { "example.com", "test.com" });
    }

    [Test]
    public async Task GetActiveRulesAsync_WithNoEnabledRules_ReturnsEmpty()
    {
        // Arrange
        var allRules = new List<BlockyRule>
        {
            CreateTestRule("disabled1.com", isEnabled: false),
            CreateTestRule("disabled2.com", isEnabled: false)
        };
        _repoMock.Setup(r => r.GetAllRulesAsync()).ReturnsAsync(allRules);

        // Act
        var result = await _sut.GetActiveRulesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetActiveRulesAsync_SecondCall_UsesCachedAllRules()
    {
        // Arrange
        var allRules = new List<BlockyRule>
        {
            CreateTestRule("example.com", isEnabled: true),
            CreateTestRule("disabled.com", isEnabled: false)
        };
        _repoMock.Setup(r => r.GetAllRulesAsync()).ReturnsAsync(allRules);

        // Act
        var result1 = await _sut.GetActiveRulesAsync();
        var result2 = await _sut.GetActiveRulesAsync();

        // Assert
        result1.Should().HaveCount(1);
        result2.Should().HaveCount(1);
        _repoMock.Verify(r => r.GetAllRulesAsync(), Times.Once); // Should use cached data
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - Should not throw
        _sut.Dispose();
        _sut.Dispose();
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

    #endregion
}
