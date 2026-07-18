using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Blocky.Core.Protocol;
using Serilog;
using Serilog.Core;

namespace Blocky.Host.Tests;

[TestFixture]
public class HostSessionTests
{
    static readonly ILogger SilentLogger = Logger.None;

    sealed class FakeRulesSource : IRulesSource
    {
        public RulesMessage Message { get; set; } = RulesMessageFactory.Create([], dbMissing: false, DateTimeOffset.UnixEpoch);
        public Exception? ThrowOnRead { get; set; }

        public Task<RulesMessage> GetCurrentAsync(CancellationToken ct) =>
            ThrowOnRead is not null ? Task.FromException<RulesMessage>(ThrowOnRead) : Task.FromResult(Message);
    }

    sealed class FakeMonitor : IChangeMonitor
    {
        public event Action? Changed;
        public void Start() { }
        public void Raise() => Changed?.Invoke();
        public void Dispose() { }
    }

    static byte[] Frame(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var frame = new byte[4 + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
        payload.CopyTo(frame, 4);
        return frame;
    }

    static async Task<List<JsonDocument>> ReadAllFramesAsync(byte[] outputBytes)
    {
        var messages = new List<JsonDocument>();
        using var stream = new MemoryStream(outputBytes);
        while (await StdioFraming.ReadMessageAsync(stream, CancellationToken.None) is { } frame)
        {
            messages.Add(JsonDocument.Parse(frame));
        }
        return messages;
    }

    static Core.Data.BlockyRule Rule(string domain) => new()
    {
        Id = Guid.NewGuid(),
        Domain = domain,
        IsEnabled = true
    };

    [Test]
    public async Task GetRulesRequest_RepliesWithRulesMessage()
    {
        var source = new FakeRulesSource
        {
            Message = RulesMessageFactory.Create([Rule("example.com")], dbMissing: false, DateTimeOffset.UnixEpoch)
        };
        var pipe = new Pipe();
        var output = new MemoryStream();
        var session = new HostSession(source, SilentLogger);

        await pipe.Writer.WriteAsync(Frame("""{"v":1,"type":"get-rules"}"""));
        await pipe.Writer.CompleteAsync();
        await session.RunAsync(pipe.Reader.AsStream(), output, monitor: null, CancellationToken.None);

        var messages = await ReadAllFramesAsync(output.ToArray());
        messages.Should().HaveCount(1);
        var root = messages[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("rules");
        root.GetProperty("v").GetInt32().Should().Be(1);
        root.GetProperty("dbMissing").GetBoolean().Should().BeFalse();
        root.GetProperty("payload").GetProperty("rules").GetArrayLength().Should().Be(1);
        root.GetProperty("payload").GetProperty("rules")[0].GetProperty("domain").GetString().Should().Be("example.com");
    }

    [Test]
    public async Task UnknownMessageType_IsIgnored()
    {
        var source = new FakeRulesSource();
        var pipe = new Pipe();
        var output = new MemoryStream();
        var session = new HostSession(source, SilentLogger);

        await pipe.Writer.WriteAsync(Frame("""{"v":1,"type":"something-new"}"""));
        await pipe.Writer.WriteAsync(Frame("""not json at all"""));
        await pipe.Writer.CompleteAsync();
        await session.RunAsync(pipe.Reader.AsStream(), output, monitor: null, CancellationToken.None);

        output.Length.Should().Be(0);
    }

    [Test]
    public async Task DbReadFailure_RepliesWithErrorMessage()
    {
        var source = new FakeRulesSource { ThrowOnRead = new IOException("database is locked") };
        var pipe = new Pipe();
        var output = new MemoryStream();
        var session = new HostSession(source, SilentLogger);

        await pipe.Writer.WriteAsync(Frame("""{"v":1,"type":"get-rules"}"""));
        await pipe.Writer.CompleteAsync();
        await session.RunAsync(pipe.Reader.AsStream(), output, monitor: null, CancellationToken.None);

        var messages = await ReadAllFramesAsync(output.ToArray());
        messages.Should().HaveCount(1);
        var root = messages[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("code").GetString().Should().Be("db-read-failed");
    }

    [Test]
    public async Task MonitorChange_PushesRules_AndSuppressesSameRev()
    {
        var source = new FakeRulesSource
        {
            Message = RulesMessageFactory.Create([Rule("example.com")], dbMissing: false, DateTimeOffset.UnixEpoch)
        };
        var monitor = new FakeMonitor();
        var pipe = new Pipe();
        var output = new MemoryStream();
        var session = new HostSession(source, SilentLogger);

        var run = session.RunAsync(pipe.Reader.AsStream(), output, monitor, CancellationToken.None);

        monitor.Raise();
        await WaitForOutputAsync(output, minimumFrames: 1);

        // Same content again — must not push a second frame.
        monitor.Raise();
        await Task.Delay(200);

        // Changed content — must push again.
        source.Message = RulesMessageFactory.Create([Rule("example.com"), Rule("other.com")], dbMissing: false, DateTimeOffset.UnixEpoch);
        monitor.Raise();
        await WaitForOutputAsync(output, minimumFrames: 2);

        await pipe.Writer.CompleteAsync();
        await run;

        var messages = await ReadAllFramesAsync(output.ToArray());
        messages.Should().HaveCount(2);
        messages[0].RootElement.GetProperty("payload").GetProperty("rules").GetArrayLength().Should().Be(1);
        messages[1].RootElement.GetProperty("payload").GetProperty("rules").GetArrayLength().Should().Be(2);
    }

    static async Task WaitForOutputAsync(MemoryStream output, int minimumFrames)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await ReadAllFramesAsync(output.ToArray())).Count >= minimumFrames)
                {
                    return;
                }
            }
            catch (InvalidDataException)
            {
                // Frame partially written; retry.
            }
            await Task.Delay(25);
        }

        throw new TimeoutException($"Expected at least {minimumFrames} frames in the output");
    }
}
