using Blocky.Core.Data;
using Blocky.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace Blocky.Tests.ViewModels;

[TestFixture]
public class RuleDialogViewModelTests
{
    private static RuleDialogViewModel CreateSut(BlockyRule? rule = null) => new(rule ?? new BlockyRule());

    private static bool? Save(RuleDialogViewModel sut)
    {
        bool? closeResult = null;
        sut.CloseRequested += result => closeResult = result;
        sut.SaveCommand.Execute(null);
        return closeResult;
    }

    [Test]
    public void Constructor_NullRule_Throws()
    {
        var act = () => new RuleDialogViewModel(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_CopiesRuleValues()
    {
        var rule = new BlockyRule
        {
            Domain = "example.com",
            HasTimeRestriction = true,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(18, 0, 0)
        };

        var sut = CreateSut(rule);

        sut.Domain.Should().Be("example.com");
        sut.HasTimeRestriction.Should().BeTrue();
        sut.StartTime.Should().Be(new TimeSpan(9, 0, 0));
        sut.EndTime.Should().Be(new TimeSpan(18, 0, 0));
    }

    [TestCase("example.com")]
    [TestCase("sub.example.com")]
    [TestCase("a-b.example.co.uk")]
    public void Save_ValidDomain_RequestsCloseWithTrue(string domain)
    {
        var sut = CreateSut();
        sut.Domain = domain;

        Save(sut).Should().BeTrue();
        sut.HasErrors.Should().BeFalse();
    }

    [TestCase("", Description = "required")]
    [TestCase("ab", Description = "below minimum length")]
    [TestCase("http://example.com", Description = "protocol")]
    [TestCase("https://example.com", Description = "protocol")]
    [TestCase("example.com/path", Description = "path")]
    [TestCase("example.com?q=1", Description = "query string")]
    [TestCase("example.com#frag", Description = "fragment")]
    [TestCase("example.com:8080", Description = "port")]
    [TestCase("exa mple.com", Description = "whitespace inside")]
    [TestCase("-example.com", Description = "leading hyphen")]
    public void Save_InvalidDomain_DoesNotClose(string domain)
    {
        var sut = CreateSut();
        sut.Domain = domain;

        Save(sut).Should().BeNull();
        sut.HasErrors.Should().BeTrue();
    }

    [Test]
    public void Save_TimeRestrictionWithEqualTimes_DoesNotClose()
    {
        var sut = CreateSut();
        sut.Domain = "example.com";
        sut.HasTimeRestriction = true;
        sut.StartTime = new TimeSpan(8, 0, 0);
        sut.EndTime = new TimeSpan(8, 0, 0);

        Save(sut).Should().BeNull();
        sut.HasErrors.Should().BeTrue();
    }

    [Test]
    public void Save_OvernightTimeRestriction_IsValid()
    {
        var sut = CreateSut();
        sut.Domain = "example.com";
        sut.HasTimeRestriction = true;
        sut.StartTime = new TimeSpan(22, 0, 0);
        sut.EndTime = new TimeSpan(6, 0, 0);

        Save(sut).Should().BeTrue();
    }

    [Test]
    public void DisablingTimeRestriction_ClearsTimes()
    {
        var sut = CreateSut();
        sut.HasTimeRestriction = true;
        sut.HasTimeRestriction = false;

        sut.StartTime.Should().BeNull();
        sut.EndTime.Should().BeNull();
    }

    [Test]
    public void ReEnablingTimeRestriction_RestoresDefaults()
    {
        var sut = CreateSut();
        sut.HasTimeRestriction = true;
        sut.HasTimeRestriction = false;
        sut.HasTimeRestriction = true;

        sut.StartTime.Should().Be(new TimeSpan(8, 0, 0));
        sut.EndTime.Should().Be(new TimeSpan(19, 0, 0));
    }

    [Test]
    public void ToBlockyRule_TrimsDomainAndPreservesIdentity()
    {
        var original = new BlockyRule { Domain = "old.com", CreatedAt = new DateTime(2024, 1, 1) };
        var sut = CreateSut(original);
        sut.Domain = "  new.com  ";

        var result = sut.ToBlockyRule();

        result.Id.Should().Be(original.Id);
        result.CreatedAt.Should().Be(original.CreatedAt);
        result.Domain.Should().Be("new.com");
        result.IsEnabled.Should().BeTrue();
    }

    [Test]
    public void ToBlockyRule_WithoutTimeRestriction_NullsTimes()
    {
        var sut = CreateSut();
        sut.Domain = "example.com";
        sut.HasTimeRestriction = true;
        sut.StartTime = new TimeSpan(8, 0, 0);
        sut.EndTime = new TimeSpan(17, 0, 0);
        sut.HasTimeRestriction = false;

        var result = sut.ToBlockyRule();

        result.HasTimeRestriction.Should().BeFalse();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
    }
}
