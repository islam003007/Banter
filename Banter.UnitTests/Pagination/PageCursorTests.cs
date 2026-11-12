using Banter.Application.Features.Common;
using Xunit;

namespace Banter.UnitTests.Pagination;

public class PageCursorTests
{
    [Fact]
    public void EncodeAndDecode_RoundTripsTheTimestampAndId()
    {
        var createdAt = new DateTime(2026, 6, 1, 12, 34, 56, DateTimeKind.Utc);
        var id = Guid.NewGuid();

        var cursor = PageCursor.Encode(createdAt, id);
        var decoded = PageCursor.Decode(cursor);

        Assert.NotNull(decoded);
        Assert.Equal(createdAt, decoded!.CreatedAt);
        Assert.Equal(id, decoded.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decode_ReturnsNullForAnEmptyCursor(string? cursor)
    {
        Assert.Null(PageCursor.Decode(cursor));
    }

    [Fact]
    public void Decode_ReturnsNullForMalformedBase64()
    {
        Assert.Null(PageCursor.Decode("not-a-valid-cursor"));
    }
}
