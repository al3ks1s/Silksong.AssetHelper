using BepInEx.Logging;
using System;
using System.IO;

namespace Silksong.AssetHelper.Internal;

internal static class CacheManager
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetHelper)}.{nameof(CacheManager)}");

    /// <summary>
    /// Write an object to the cache, regardless of whether or not is already there.
    /// </summary>
    public static void WriteObj<T>(T obj, string filename) where T : class
    {
        string filePath = Path.Combine(AssetPaths.CacheDirectory, filename);

        VersionedObject<T> toCache = new(AssetHelperPlugin.Version, obj);
        toCache?.SerializeToFile(filePath);
    }

    private static bool VersionMatches(string? self, string? other)
    {
        if (self is null || other is null)
        {
            return false;
        }

        if (!Version.TryParse(self, out Version selfV) || !Version.TryParse(other, out Version otherV))
        {
            return false;
        }

        return selfV.Major == otherV.Major && selfV.Minor == otherV.Minor;
    }

    /// <summary>
    /// If the filename exists in the cache folder, load the object using Newtonsoft.Json.
    /// If the filename does not exist, compute the object, store it in the file and return it.
    /// 
    /// The function will be recalculated each time the Silksong version or
    /// <see cref="AssetHelperPlugin"/> major/minor version changes.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="generator">The function used to generate the object.</param>
    /// <param name="filename">The name of the cache file.</param>
    /// <returns>The object.</returns>
    public static T GetCached<T>(
        Func<T> generator,
        string filename) where T : class
    {
        string filePath = Path.Combine(AssetPaths.CacheDirectory, filename);

        if (JsonExtensions.TryLoadFromFile<VersionedObject<T>>(filePath, out VersionedObject<T>? fromCache))
        {
            if (fromCache.Value is not null && VersionMatches(fromCache.Version, AssetHelperPlugin.Version))
            {
                return fromCache.Value;
            }
        }

        T generated = generator();
        WriteObj(generated, filename);

        return generated;
    }
}
