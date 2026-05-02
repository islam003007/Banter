using System.Collections.ObjectModel;

namespace Banter.Application.Abstractions;

public class AppException : Exception
{
    public IReadOnlyDictionary<string, object?> Extensions { get; } // This works for anonymous objects
    public string Code { get; }
    public AppException(string code, string message, object? metadata = null) : base(message)
    {
        var dict = new Dictionary<string, object?>();
        if (metadata is not null)
        {
            foreach (var property in metadata.GetType().GetProperties())
            {
                dict[property.Name] = property.GetValue(metadata);
            }
        }
        Extensions = new ReadOnlyDictionary<string, object?>(dict);
        Code = code;
    }
}
