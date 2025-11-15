using Blocky.Data;
using Blocky.Services.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace Blocky.Tests.Data;

[TestFixture]
public class BlockyRuleRepoTests
{
    private DbContextOptions<AppDbContext> _dbContextOptions = null!;
    private IDbContextFactory<AppDbContext> _dbContextFactory = null!;
    private Mock<ITimerService> _timerServiceMock = null!;
    private Mock<ITimer> _timerMock = null!;
    private BlockyRuleRepo _sut = null!;

    [SetUp]
    public void Setup()
    {
        // Create a unique database name for each test to ensure isolation
        var dbName = $"TestDb_{Guid.NewGuid()}";
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _dbContextFactory = new TestDbContextFactory(_dbContextOptions);
        _timerServiceMock = new Mock<ITimerService>();
        _timerMock = new Mock<ITimer>();
        _timerServiceMock.Setup(t => t.Create(It.IsAny<string>())).Returns(_timerMock.Object);

        _sut = new BlockyRuleRepo(_dbContextFactory, _timerServiceMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        // Clean up database
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
    }

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_ValidRule_AddsToDatabase()
    {
        // Arrange
        var rule = CreateTestRule("example.com");

        // Act
        await _sut.AddAsync(rule);

        // Assert
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var savedRule = await context.Rules.FindAsync(rule.Id);
        savedRule.Should().NotBeNull();
        savedRule!.Domain.Should().Be("example.com");
    }

    [Test]
    public void AddAsync_NullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.AddAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task AddAsync_RuleWithTimeRestriction_SavesTimeSpans()
    {
        // Arrange
        var rule = CreateTestRuleWithTime("example.com",
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(17));

        // Act
        await _sut.AddAsync(rule);

        // Assert
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var savedRule = await context.Rules.FindAsync(rule.Id);
        savedRule.Should().NotBeNull();
        savedRule!.HasTimeRestriction.Should().BeTrue();
        savedRule.StartTime.Should().Be(TimeSpan.FromHours(8));
        savedRule.EndTime.Should().Be(TimeSpan.FromHours(17));
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingRule_ReturnsRule()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        await _sut.AddAsync(rule);

        // Act
        var result = await _sut.GetByIdAsync(rule.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(rule.Id);
        result.Domain.Should().Be("example.com");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentRule_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllRulesAsync Tests

    [Test]
    public async Task GetAllRulesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetAllRulesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllRulesAsync_WithMultipleRules_ReturnsAllRules()
    {
        // Arrange
        var rule1 = CreateTestRule("example.com");
        var rule2 = CreateTestRule("test.com");
        var rule3 = CreateTestRule("another.com");

        await _sut.AddAsync(rule1);
        await _sut.AddAsync(rule2);
        await _sut.AddAsync(rule3);

        // Act
        var result = await _sut.GetAllRulesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Domain).Should().BeEquivalentTo(new[] { "example.com", "test.com", "another.com" });
    }

    [Test]
    public async Task GetAllRulesAsync_ReturnsEnabledAndDisabledRules()
    {
        // Arrange
        var enabledRule = CreateTestRule("enabled.com", isEnabled: true);
        var disabledRule = CreateTestRule("disabled.com", isEnabled: false);

        await _sut.AddAsync(enabledRule);
        await _sut.AddAsync(disabledRule);

        // Act
        var result = await _sut.GetAllRulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Domain == "enabled.com" && r.IsEnabled);
        result.Should().Contain(r => r.Domain == "disabled.com" && !r.IsEnabled);
    }

    #endregion

    #region GetActiveRulesAsync Tests

    [Test]
    public async Task GetActiveRulesAsync_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var enabledRule1 = CreateTestRule("enabled1.com", isEnabled: true);
        var enabledRule2 = CreateTestRule("enabled2.com", isEnabled: true);
        var disabledRule = CreateTestRule("disabled.com", isEnabled: false);

        await _sut.AddAsync(enabledRule1);
        await _sut.AddAsync(enabledRule2);
        await _sut.AddAsync(disabledRule);

        // Act
        var result = await _sut.GetActiveRulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.IsEnabled);
        result.Select(r => r.Domain).Should().BeEquivalentTo(new[] { "enabled1.com", "enabled2.com" });
    }

    [Test]
    public async Task GetActiveRulesAsync_NoEnabledRules_ReturnsEmpty()
    {
        // Arrange
        var disabledRule = CreateTestRule("disabled.com", isEnabled: false);
        await _sut.AddAsync(disabledRule);

        // Act
        var result = await _sut.GetActiveRulesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ExistingRule_UpdatesRule()
    {
        // Arrange
        var originalRule = CreateTestRule("example.com");
        await _sut.AddAsync(originalRule);

        // Modify the rule
        originalRule.Domain = "updated.com";
        originalRule.IsEnabled = false;

        // Act
        await _sut.UpdateAsync(originalRule);

        // Assert
        var updatedRule = await _sut.GetByIdAsync(originalRule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.Domain.Should().Be("updated.com");
        updatedRule.IsEnabled.Should().BeFalse();
    }

    [Test]
    public async Task UpdateAsync_SetsLastUpdatedTimestamp()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        await _sut.AddAsync(rule);

        var originalLastUpdated = rule.LastUpdated;

        // Wait a tiny bit to ensure time difference
        await Task.Delay(10);

        // Modify the rule
        rule.Domain = "updated.com";

        // Act
        await _sut.UpdateAsync(rule);

        // Assert
        var updatedRule = await _sut.GetByIdAsync(rule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.LastUpdated.Should().BeAfter(originalLastUpdated!.Value);
    }

    [Test]
    public void UpdateAsync_NonExistentRule_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentRule = CreateTestRule("example.com");

        // Act & Assert
        var act = async () => await _sut.UpdateAsync(nonExistentRule);
        act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Rule not found with id: {nonExistentRule.Id}");
    }

    [Test]
    public void UpdateAsync_NullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sut.UpdateAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateAsync_TimeRestriction_UpdatesCorrectly()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        await _sut.AddAsync(rule);

        // Add time restriction
        rule.HasTimeRestriction = true;
        rule.StartTime = TimeSpan.FromHours(9);
        rule.EndTime = TimeSpan.FromHours(18);

        // Act
        await _sut.UpdateAsync(rule);

        // Assert
        var updatedRule = await _sut.GetByIdAsync(rule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.HasTimeRestriction.Should().BeTrue();
        updatedRule.StartTime.Should().Be(TimeSpan.FromHours(9));
        updatedRule.EndTime.Should().Be(TimeSpan.FromHours(18));
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingRule_RemovesFromDatabase()
    {
        // Arrange
        var rule = CreateTestRule("example.com");
        await _sut.AddAsync(rule);

        // Act
        await _sut.DeleteAsync(rule.Id);

        // Assert
        var deletedRule = await _sut.GetByIdAsync(rule.Id);
        deletedRule.Should().BeNull();
    }

    [Test]
    public void DeleteAsync_NonExistentRule_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _sut.DeleteAsync(nonExistentId);
        act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Rule not found with id: {nonExistentId}");
    }

    [Test]
    public void DeleteAsync_EmptyGuid_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _sut.DeleteAsync(Guid.Empty);
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid rule id");
    }

    #endregion

    #region Helper Methods

    private static BlockyRule CreateTestRule(string domain, bool isEnabled = true)
    {
        return new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = domain,
            IsEnabled = isEnabled,
            HasTimeRestriction = false,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };
    }

    private static BlockyRule CreateTestRuleWithTime(string domain, TimeSpan startTime, TimeSpan endTime)
    {
        return new BlockyRule
        {
            Id = Guid.NewGuid(),
            Domain = domain,
            IsEnabled = true,
            HasTimeRestriction = true,
            StartTime = startTime,
            EndTime = endTime,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };
    }

    #endregion

    #region Test DbContext Factory

    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            return context;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var context = new AppDbContext(_options);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            return context;
        }
    }

    #endregion
}
