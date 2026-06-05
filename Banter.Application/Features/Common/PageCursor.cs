using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Json;

namespace Banter.Application.Features.Common;

public class PageCursor
{
    public DateTime CreatedAt { get; }
    public Guid Id { get; }
    public PageCursor(DateTime createdAt, Guid id)
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
}
