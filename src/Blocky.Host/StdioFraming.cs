using System.Buffers.Binary;

namespace Blocky.Host;

/// <summary>
/// Chrome native-messaging framing: a 4-byte little-endian length prefix followed by
/// that many bytes of UTF-8 JSON.
/// </summary>
public static class StdioFraming
{
    // Chrome caps host → browser messages at 1 MB; our messages are far smaller,
    // so anything larger on either direction indicates a broken peer.
    public const int MaxMessageBytes = 1024 * 1024;

    /// <summary>Returns null when the stream ends (Chrome closed the pipe).</summary>
    public static async Task<byte[]?> ReadMessageAsync(Stream input, CancellationToken ct)
    {
        var lengthBuffer = new byte[4];
        if (!await TryReadExactAsync(input, lengthBuffer, ct))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length is < 0 or > MaxMessageBytes)
        {
            throw new InvalidDataException($"Invalid native-messaging frame length: {length}");
        }

        var payload = new byte[length];
        if (!await TryReadExactAsync(input, payload, ct))
        {
            return null;
        }

        return payload;
    }

    public static async Task WriteMessageAsync(Stream output, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length > MaxMessageBytes)
        {
            throw new InvalidDataException($"Refusing to send oversized frame: {payload.Length} bytes");
        }

        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
        await output.WriteAsync(lengthBuffer, ct);
        await output.WriteAsync(payload, ct);
        await output.FlushAsync(ct);
    }

    static async Task<bool> TryReadExactAsync(Stream input, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await input.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
