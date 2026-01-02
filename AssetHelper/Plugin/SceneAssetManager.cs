using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.BundleTools.RepackedBundleData>;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class managing the scene repacking.
/// </summary>
internal static class SceneAssetManager
{
    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");

    private static RepackDataCollection? _repackData;

    internal static event Action? SingleRepackOperationCompleted;

    /// <summary>
    /// Run a repacking procedure so that by the end, anything in toRepack which could be repacked has been.
    /// </summary>
    /// <param name="toRepack"></param>
    internal static void Run(Dictionary<string, HashSet<string>> toRepack)
    {
        string repackDataPath = Path.Combine(AssetPaths.RepackedSceneBundleDir, "repack_data.json");

        if (JsonExtensions.TryLoadFromFile<RepackDataCollection>(repackDataPath, out RepackDataCollection? repackData))
        {
            _repackData = repackData;
        }
        else
        {
            _repackData = [];
        }

        Dictionary<string, HashSet<string>> updatedToRepack = [];

        foreach ((string scene, HashSet<string> request) in toRepack)
        {
            if (!_repackData.TryGetValue(scene, out RepackedBundleData existingBundleData))
            {
                updatedToRepack[scene] = request;
                continue;
            }

            // TODO - accept silksong version changes if the bundle hasn't changed
            Version current = Version.Parse(AssetHelperPlugin.Version);
            if (existingBundleData.SilksongVersion != AssetPaths.SilksongVersion
                || !Version.TryParse(existingBundleData.PluginVersion ?? string.Empty, out Version oldPluginVersion)
                || oldPluginVersion > current
                || oldPluginVersion < _lastAcceptablePluginVersion
                )
            {
                updatedToRepack[scene] = request;
                continue;
            }

            if (request.All(x => existingBundleData.TriedToRepack(x)))
            {
                // No need to re-repack as there's nothing new to try
                continue;
            }

            updatedToRepack[scene] = new(request
                .Union(existingBundleData.GameObjectAssets?.Values ?? Enumerable.Empty<string>())
                .Union(existingBundleData.NonRepackedAssets ?? Enumerable.Empty<string>())
                );
        }

        SceneRepacker repacker = new StrippedSceneRepacker();

        foreach ((string scene, HashSet<string> request) in updatedToRepack)
        {
            RepackedBundleData newData = repacker.Repack(scene, request.ToList(), Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{scene}.bundle"));
            _repackData[scene] = newData;
            _repackData.SerializeToFile(repackDataPath);
            SingleRepackOperationCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Check whether the given scene object is loadable using AssetHelper.
    /// 
    /// This function assumes that the game object existed in the original scene.
    /// </summary>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="objName">The hierarchical name of the given game object.</param>
    /// <param name="assetPath">The path within the asset bundle.</param>
    /// <param name="relativePath">The path to the game object relative to the asset, or null
    /// if the asset and the requested game object are the same.</param>
    private static bool TryGetSceneAssetData(
        string sceneName,
        string objName,
        [MaybeNullWhen(false)] out string assetPath,
        out string? relativePath
        )
    {
        if (_repackData == null
            || !_repackData.TryGetValue(sceneName, out RepackedBundleData data))
        {
            assetPath = null;
            relativePath = null;
            return false;
        }

        return data.CanLoad(objName, out assetPath, out relativePath);
    }
}
