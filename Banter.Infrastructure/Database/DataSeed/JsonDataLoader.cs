using Banter.Infrastructure.Database.DataSeed;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Text;

namespace Banter.Infrastructure.Database.Seed;

internal static class JsonDataLoader
{
    private static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings()
    {
        ContractResolver = new PrivateSetterContractResolver(),
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include
    });

    private static readonly Assembly _assembly = typeof(DataSeeder).Assembly;

    private static readonly string _rootNameSpace = _assembly.GetName().Name ?? "";

    public static List<T> LoadAsync<T>(string fileName)
    {
        var resourceName = $"{_rootNameSpace}.Database.DataSeed.Data.{fileName}";

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");

        using var streamReader = new StreamReader(stream, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);

        return _serializer.Deserialize<List<T>>(jsonReader)
               ?? new List<T>();
    }

    private class PrivateSetterContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property.Writable)
                return property;

            if (member is PropertyInfo prop)
            {
                var hasPrivateSetter = prop.GetSetMethod(true) != null;
                property.Writable = hasPrivateSetter;
            }

            return property;
        }
    }
}