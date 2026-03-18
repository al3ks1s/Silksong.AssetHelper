using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace AssetHelperTesting;

internal static class JsonHelper
{
    public static bool TryLoadEmbeddedJson<T>(string filename, [NotNullWhen(true)] out T? parsed)
    {
        parsed = default;
        Assembly assembly = typeof(JsonHelper).Assembly;

        string resourceName = $"AssetHelperTesting.Resources.{filename}.json";

        try
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using StreamReader reader = new(stream);
            string jsonText = reader.ReadToEnd();
            parsed = JsonConvert.DeserializeObject<T>(jsonText);
            return parsed != null;
        }
        catch
        {
            return false;
        }
    }
}
