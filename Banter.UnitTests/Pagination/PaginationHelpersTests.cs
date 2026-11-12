using Banter.Application.Extensions;
using Xunit;

namespace Banter.UnitTests.Pagination;

public class PaginationHelpersTests
{
    private record Item(DateTime CreatedAt, Guid Id);

    private static List<Item> Items(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new Item(
                new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(index),
                Guid.NewGuid()))
            .ToList();

    [Fact]
    public void Slice_RemovesTheExtraRowAndReportsMore()
    {
        var items = Items(11);

        var hasMore = PaginationHelpers.Slice(items, 10);

        Assert.True(hasMore);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void Slice_KeepsThePageWhenThereIsNoExtraRow()
    {
        var items = Items(10);

        var hasMore = PaginationHelpers.Slice(items, 10);

        Assert.False(hasMore);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void CreateNextCursor_ReturnsNullWhenThereIsNoNextPage()
    {
        var cursor = PaginationHelpers.CreateNextCursor(
            Items(10),
            hasMore: false,
            item => item.CreatedAt,
            item => item.Id);

        Assert.Null(cursor);
    }

    [Fact]
    public void CreateNextCursor_EncodesTheLastItem()
    {
        var items = Items(10);

        var cursor = PaginationHelpers.CreateNextCursor(
            items,
            hasMore: true,
            item => item.CreatedAt,
            item => item.Id);

        Assert.NotNull(cursor);
        var decoded = Banter.Application.Features.Common.PageCursor.Decode(cursor);
        Assert.NotNull(decoded);
        Assert.Equal(items[^1].CreatedAt, decoded!.CreatedAt);
        Assert.Equal(items[^1].Id, decoded.Id);
    }
}
