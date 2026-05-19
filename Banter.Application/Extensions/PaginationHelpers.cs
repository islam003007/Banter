using Banter.Application.Features.Common;

namespace Banter.Application.Extensions;

internal static class PaginationHelpers
{
    public static bool Slice<T>(
       List<T> items,
       int pageSize)
    {
        var hasMore = items.Count > pageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return hasMore;
    }

    public static string? CreateNextCursor<T>(
        IReadOnlyList<T> items,
        bool hasMore,
        Func<T, DateTime> createdAtSelector,
        Func<T, Guid> idSelector)
    {
        if (!hasMore || items.Count == 0)
        {
            return null;
        }

        var lastItem = items[^1];

        return PageCursor.Encode(
            createdAtSelector(lastItem),
            idSelector(lastItem));
    }
}
