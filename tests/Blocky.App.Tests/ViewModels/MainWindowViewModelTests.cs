using Blocky.Core.Data;
using Blocky.Services;
using Blocky.Services.Contracts;
using Blocky.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Blocky.Tests.ViewModels;

[TestFixture]
public class MainWindowViewModelTests
{
    private Mock<IBlockyService> _blockyServiceMock = null!;
    private Mock<IApplication> _appMock = null!;
    private Mock<ILogConfig> _logConfigMock = null!;
    private Mock<IDialogService> _dialogServiceMock = null!;
    private Mock<IShellService> _shellServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        _blockyServiceMock = new Mock<IBlockyService>();
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync([]);
        _appMock = new Mock<IApplication>();
        _logConfigMock = new Mock<ILogConfig>();
        _dialogServiceMock = new Mock<IDialogService>();
        _shellServiceMock = new Mock<IShellService>();
    }

    private MainWindowViewModel CreateSut() => new(
        Mock.Of<ILogger<MainWindowViewModel>>(),
        _blockyServiceMock.Object,
        _appMock.Object,
        _logConfigMock.Object,
        _dialogServiceMock.Object,
        _shellServiceMock.Object);

    private static BlockyRule Rule(string domain) => new()
    {
        Id = Guid.NewGuid(),
        Domain = domain
    };

    private void SetupDialogResult(bool result, Action<RuleDialogViewModel>? mutate = null)
    {
        _dialogServiceMock
            .Setup(d => d.ShowRuleDialog(It.IsAny<RuleDialogViewModel>()))
            .Callback<RuleDialogViewModel>(vm => mutate?.Invoke(vm))
            .Returns(result);
    }

    [Test]
    public async Task Initialization_LoadsRulesIntoCollection()
    {
        var rules = new List<BlockyRule> { Rule("a.com"), Rule("b.com") };
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync(rules);

        var sut = CreateSut();
        await sut.InitializationTask;

        sut.Rules.Should().BeEquivalentTo(rules);
        sut.LoadError.Should().BeNull();
    }

    [Test]
    public async Task Initialization_WhenLoadFails_SetsLoadErrorInsteadOfThrowing()
    {
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ThrowsAsync(new IOException("db unreachable"));

        var sut = CreateSut();
        await sut.InitializationTask;

        sut.Rules.Should().BeEmpty();
        sut.LoadError.Should().Contain("db unreachable");
    }

    [Test]
    public async Task AddRule_WhenDialogSaved_AddsRuleViaServiceAndCollection()
    {
        SetupDialogResult(true, vm => vm.Domain = "example.com");
        var sut = CreateSut();

        await sut.AddRuleCommand.ExecuteAsync(null);

        _blockyServiceMock.Verify(s => s.AddRuleAsync(It.Is<BlockyRule>(r => r.Domain == "example.com")), Times.Once);
        sut.Rules.Should().ContainSingle(r => r.Domain == "example.com");
    }

    [Test]
    public async Task AddRule_WhenDialogCancelled_DoesNothing()
    {
        SetupDialogResult(false);
        var sut = CreateSut();

        await sut.AddRuleCommand.ExecuteAsync(null);

        _blockyServiceMock.Verify(s => s.AddRuleAsync(It.IsAny<BlockyRule>()), Times.Never);
        sut.Rules.Should().BeEmpty();
    }

    [Test]
    public async Task AddRule_WhenServiceFails_ShowsErrorAndDoesNotAddToCollection()
    {
        SetupDialogResult(true, vm => vm.Domain = "example.com");
        _blockyServiceMock.Setup(s => s.AddRuleAsync(It.IsAny<BlockyRule>())).ThrowsAsync(new IOException("boom"));
        var sut = CreateSut();

        await sut.AddRuleCommand.ExecuteAsync(null);

        _dialogServiceMock.Verify(d => d.ShowError("Add Rule", It.Is<string>(m => m.Contains("boom"))), Times.Once);
        sut.Rules.Should().BeEmpty();
    }

    [Test]
    public async Task EditRule_WhenDialogSaved_UpdatesServiceAndReplacesInCollection()
    {
        var existing = Rule("old.com");
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync([existing]);
        SetupDialogResult(true, vm => vm.Domain = "new.com");
        var sut = CreateSut();
        await sut.InitializationTask;

        await sut.EditRuleCommand.ExecuteAsync(existing);

        _blockyServiceMock.Verify(s => s.UpdateRuleAsync(It.Is<BlockyRule>(r => r.Domain == "new.com" && r.Id == existing.Id)), Times.Once);
        sut.Rules.Should().ContainSingle(r => r.Domain == "new.com");
    }

    [Test]
    public async Task EditRule_WhenDialogCancelled_DoesNothing()
    {
        var existing = Rule("old.com");
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync([existing]);
        SetupDialogResult(false);
        var sut = CreateSut();
        await sut.InitializationTask;

        await sut.EditRuleCommand.ExecuteAsync(existing);

        _blockyServiceMock.Verify(s => s.UpdateRuleAsync(It.IsAny<BlockyRule>()), Times.Never);
        sut.Rules.Should().ContainSingle(r => r.Domain == "old.com");
    }

    [Test]
    public async Task RemoveRule_RemovesViaServiceAndCollection()
    {
        var existing = Rule("gone.com");
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync([existing]);
        var sut = CreateSut();
        await sut.InitializationTask;

        await sut.RemoveRuleCommand.ExecuteAsync(existing);

        _blockyServiceMock.Verify(s => s.RemoveRuleAsync(existing.Id), Times.Once);
        sut.Rules.Should().BeEmpty();
    }

    [Test]
    public async Task RemoveRule_WhenServiceFails_ShowsErrorAndKeepsRule()
    {
        var existing = Rule("keep.com");
        _blockyServiceMock.Setup(s => s.GetAllRulesAsync()).ReturnsAsync([existing]);
        _blockyServiceMock.Setup(s => s.RemoveRuleAsync(existing.Id)).ThrowsAsync(new IOException("boom"));
        var sut = CreateSut();
        await sut.InitializationTask;

        await sut.RemoveRuleCommand.ExecuteAsync(existing);

        _dialogServiceMock.Verify(d => d.ShowError("Remove Rule", It.IsAny<string>()), Times.Once);
        sut.Rules.Should().ContainSingle();
    }

    [Test]
    public void OpenLog_OpensCurrentLogFileViaShell()
    {
        _logConfigMock.Setup(l => l.GetCurrentLogFilePath()).Returns(@"C:\logs\blocky.log");
        var sut = CreateSut();

        sut.OpenLogCommand.Execute(null);

        _shellServiceMock.Verify(s => s.OpenFile(@"C:\logs\blocky.log"), Times.Once);
    }

    [Test]
    public void OpenLog_WhenShellFails_ShowsError()
    {
        _logConfigMock.Setup(l => l.GetCurrentLogFilePath()).Returns(@"C:\logs\blocky.log");
        _shellServiceMock.Setup(s => s.OpenFile(It.IsAny<string>())).Throws(new IOException("no app"));
        var sut = CreateSut();

        sut.OpenLogCommand.Execute(null);

        _dialogServiceMock.Verify(d => d.ShowError("Open Log", It.Is<string>(m => m.Contains("no app"))), Times.Once);
    }

    [Test]
    public void Quit_ShutsDownTheApplication()
    {
        var sut = CreateSut();

        sut.QuitCommand.Execute(null);

        _appMock.Verify(a => a.Shutdown(), Times.Once);
    }
}
