using System.Text.Json;
using Blocky.Core.Protocol;
using Serilog;

namespace Blocky.Host;

/// <summary>
/// One native-messaging session: replies to get-rules requests and pushes the rule set
/// when the change monitor fires and the content hash actually changed. Runs until the
/// peer closes stdin.
/// </summary>
public sealed class HostSession(IRulesSource rulesSource, ILogger logger)
{
    readonly SemaphoreSlim _writeLock = new(1, 1);
    Stream _output = Stream.Null;
    CancellationToken _ct;
    string? _lastSentRev;

    public async Task RunAsync(Stream input, Stream output, IChangeMonitor? monitor, CancellationToken ct)
    {
        _output = output;
        _ct = ct;

        if (monitor is not null)
        {
            monitor.Changed += OnChanged;
            monitor.Start();
        }

        try
        {
            while (true)
            {
                var frame = await StdioFraming.ReadMessageAsync(input, ct);
                if (frame is null)
                {
                    logger.Information("stdin closed; ending session");
                    break;
                }

                await HandleClientFrameAsync(frame);
            }
        }
        finally
        {
            if (monitor is not null)
            {
                monitor.Changed -= OnChanged;
            }
        }
    }

    void OnChanged() => _ = PushIfChangedAsync();

    async Task PushIfChangedAsync()
    {
        try
        {
            var message = await rulesSource.GetCurrentAsync(_ct);
            if (message.Rev == _lastSentRev)
            {
                return;
            }

            logger.Information("Pushing rules rev {Rev} ({Count} rules, dbMissing: {DbMissing})",
                message.Rev, message.Payload.Rules.Count, message.DbMissing);
            await SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJsonContext.Default.RulesMessage), message.Rev);
        }
        catch (OperationCanceledException)
        {
            // session shutting down
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to push rules update");
        }
    }

    async Task HandleClientFrameAsync(byte[] frame)
    {
        ClientMessage? request = null;
        try
        {
            request = JsonSerializer.Deserialize(frame, ProtocolJsonContext.Default.ClientMessage);
        }
        catch (JsonException ex)
        {
            logger.Warning(ex, "Ignoring malformed client frame");
        }

        if (request?.Type != ProtocolConstants.GetRulesRequestType)
        {
            logger.Warning("Ignoring unknown client message type {Type}", request?.Type);
            return;
        }

        try
        {
            var message = await rulesSource.GetCurrentAsync(_ct);
            logger.Information("Answering get-rules with rev {Rev} ({Count} rules, dbMissing: {DbMissing})",
                message.Rev, message.Payload.Rules.Count, message.DbMissing);
            await SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJsonContext.Default.RulesMessage), message.Rev);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to read rules for get-rules request");
            var error = new ErrorMessage(ProtocolConstants.Version, ProtocolConstants.ErrorMessageType, "db-read-failed", ex.Message);
            await SendAsync(JsonSerializer.SerializeToUtf8Bytes(error, ProtocolJsonContext.Default.ErrorMessage), rev: null);
        }
    }

    async Task SendAsync(byte[] payload, string? rev)
    {
        await _writeLock.WaitAsync(_ct);
        try
        {
            await StdioFraming.WriteMessageAsync(_output, payload, _ct);
            if (rev is not null)
            {
                _lastSentRev = rev;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
