using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Json;

namespace Banter.Application.Features.Common;

internal class PageCursor
{
    public DateTime CreatedAt { get; }
    public Guid Id { get; }
    private PageCursor(DateTime createdAt, Guid id)
    {
        CreatedAt = createdAt;
        Id = id;
    }

    public static string Encode(DateTime createdAt, Guid id)
    {
        var cursor = new PageCursor(createdAt, id);

        string json = JsonSerializer.Serialize(cursor);

        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    public static PageCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        try
        {
            string json = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));

            return JsonSerializer.Deserialize<PageCursor>(json);
        }
        catch
        {
            return null;
        }
    }

    public static IQueryable<T> ApplyCursor<T>(
        this IQueryable<T> query,
        PageCursor? cursor)
        where T : ICursorPageItem
    {
        if (cursor is null)
            return query;

        return query.Where(x =>
            x.CreatedAt < cursor.CreatedAt
            || (x.CreatedAt == cursor.CreatedAt && x.Id < cursor.Id));
    }

    public static async Task<CursorPage<T>> ToCursorPageAsync<T>(
        this IQueryable<T> query,
        int pageSize,
        CancellationToken cancellationToken = default)
        where T : ICursorPageItem
    {
        var items = await query
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var lastItem = items.LastOrDefault();

        var nextCursor =
            hasMore && lastItem is not null
                ? PageCursor.Encode(lastItem.CreatedAt, lastItem.Id)
                : null;

        return new CursorPage<T>(
            items,
            nextCursor,
            hasMore);
    }
}
