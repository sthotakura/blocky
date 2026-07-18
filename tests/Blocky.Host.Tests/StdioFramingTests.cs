using System.Text;
using Blocky.Host;

namespace Blocky.Host.Tests;

[TestFixture]
public class StdioFramingTests
{
    [Test]
    public async Task WriteThenRead_RoundTripsPayload()
    {
        var payload = Encoding.UTF8.GetBytes("""{"v":1,"type":"get-rules"}""");
        using var stream = new MemoryStream();

        await StdioFraming.WriteMessageAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        var result = await StdioFraming.ReadMessageAsync(stream, CancellationToken.None);

        result.Should().Equal(payload);
    }

    [Test]
    public async Task Read_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        var result = await StdioFraming.ReadMessageAsync(stream, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Read_TruncatedPayload_ReturnsNull()
    {
        using var stream = new MemoryStream();
        // Length says 100 bytes but only 3 follow.
        stream.Write([100, 0, 0, 0]);
        stream.Write([1, 2, 3]);
        stream.Position = 0;

        var result = await StdioFraming.ReadMessageAsync(stream, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Read_OversizedLength_Throws()
    {
        using var stream = new MemoryStream();
        stream.Write(BitConverter.GetBytes(StdioFraming.MaxMessageBytes + 1));
        stream.Position = 0;

        var act = async () => await StdioFraming.ReadMessageAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Test]
    public async Task Read_MultipleFrames_ReadsThemInOrder()
    {
        var first = Encoding.UTF8.GetBytes("one");
        var second = Encoding.UTF8.GetBytes("two");
        using var stream = new MemoryStream();
        await StdioFraming.WriteMessageAsync(stream, first, CancellationToken.None);
        await StdioFraming.WriteMessageAsync(stream, second, CancellationToken.None);
        stream.Position = 0;

        (await StdioFraming.ReadMessageAsync(stream, CancellationToken.None)).Should().Equal(first);
        (await StdioFraming.ReadMessageAsync(stream, CancellationToken.None)).Should().Equal(second);
        (await StdioFraming.ReadMessageAsync(stream, CancellationToken.None)).Should().BeNull();
    }
}
