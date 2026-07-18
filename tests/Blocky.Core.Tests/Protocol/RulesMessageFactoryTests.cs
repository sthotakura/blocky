using Blocky.Core.Data;
using Blocky.Core.Protocol;

namespace Blocky.Core.Tests.Protocol;

[TestFixture]
public class RulesMessageFactoryTests
{
    static readonly DateTimeOffset SomeTime = new(2026, 7, 17, 9, 0, 0, TimeSpan.Zero);

    static BlockyRule Rule(string domain, Guid? id = null, bool isEnabled = true) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Domain = domain,
        IsEnabled = isEnabled
    };

    [Test]
    public void Create_SetsEnvelopeFields()
    {
        var message = RulesMessageFactory.Create([Rule("example.com")], dbMissing: false, SomeTime);

        message.V.Should().Be(ProtocolConstants.Version);
        message.Type.Should().Be(ProtocolConstants.RulesMessageType);
        message.GeneratedAt.Should().Be(SomeTime);
        message.DbMissing.Should().BeFalse();
        message.Rev.Should().HaveLength(16);
    }

    [Test]
    public void Create_SameRulesInDifferentOrder_ProduceSameRev()
    {
        var a = Rule("a.com", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var b = Rule("b.com", Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var first = RulesMessageFactory.Create([a, b], dbMissing: false, SomeTime);
        var second = RulesMessageFactory.Create([b, a], dbMissing: false, DateTimeOffset.UnixEpoch);

        first.Rev.Should().Be(second.Rev, "rev is a content hash independent of ordering and timestamp");
    }

    [Test]
    public void Create_DifferentContent_ProducesDifferentRev()
    {
        var first = RulesMessageFactory.Create([Rule("a.com")], dbMissing: false, SomeTime);
        var second = RulesMessageFactory.Create([Rule("b.com")], dbMissing: false, SomeTime);

        first.Rev.Should().NotBe(second.Rev);
    }

    [Test]
    public void Create_DbMissing_ChangesRevEvenWithSamePayload()
    {
        var present = RulesMessageFactory.Create([], dbMissing: false, SomeTime);
        var missing = RulesMessageFactory.Create([], dbMissing: true, SomeTime);

        missing.DbMissing.Should().BeTrue();
        missing.Rev.Should().NotBe(present.Rev);
    }

    [Test]
    public void Create_ValidSchedule_MapsToMinutes()
    {
        var rule = Rule("work.com");
        rule.HasTimeRestriction = true;
        rule.StartTime = new TimeSpan(8, 0, 0);
        rule.EndTime = new TimeSpan(17, 0, 0);

        var message = RulesMessageFactory.Create([rule], dbMissing: false, SomeTime);

        var dto = message.Payload.Rules.Single();
        dto.Enabled.Should().BeTrue();
        dto.Schedule.Should().Be(new ScheduleDto(480, 1020));
    }

    [Test]
    public void Create_TimeRestrictionWithMissingTimes_IsDisabledNotAlwaysOn()
    {
        var rule = Rule("broken.com");
        rule.HasTimeRestriction = true;
        rule.StartTime = new TimeSpan(8, 0, 0);
        rule.EndTime = null;

        var message = RulesMessageFactory.Create([rule], dbMissing: false, SomeTime);

        var dto = message.Payload.Rules.Single();
        dto.Enabled.Should().BeFalse("an invalid time restriction is inactive and must not degrade to always-on");
        dto.Schedule.Should().BeNull();
    }

    [Test]
    public void Create_RulesAreSortedById()
    {
        var first = Rule("z.com", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = Rule("a.com", Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var message = RulesMessageFactory.Create([second, first], dbMissing: false, SomeTime);

        message.Payload.Rules.Select(r => r.Domain).Should().ContainInOrder("z.com", "a.com");
    }
}
