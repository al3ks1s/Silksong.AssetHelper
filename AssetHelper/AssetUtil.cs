using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Logger = BepInEx.Logging.Logger;
using UObject = UnityEngine.Object;

namespace Silksong.AssetHelper;

public static class AssetUtil
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource(nameof(AssetUtil));

    /// <summary>
    /// Load the specified asset from the specified asset bundle.
    /// </summary>
    /// <typeparam name="T">The type of the asset to be loaded.</typeparam>
    /// <param name="bundleName">The name of the .bundle file containing the asset.</param>
    /// <param name="assetName">The name of the asset within the file.</param>
    /// <param name="onLoaded"></param>
    /// <param name="extraDependencies"></param>
    /// <returns></returns>
    public static IEnumerator LoadAsset<T>(
        string bundleName,
        string assetName,
        Action<LoadedAsset<T>> onLoaded,
        List<string>? extraDependencies = null
        )
        where T : UObject
    {
        if (Data.BundleKeys is null)
        {
            Log.LogError($"Cannot load asset {assetName} from {bundleName}: too early.");
            yield break;
        }

        extraDependencies ??= [];

        List<string> allBundles = [bundleName, .. extraDependencies];

        AsyncOperationHandle<IList<IAssetBundleResource>> opHandle =
            Addressables.LoadAssetsAsync<IAssetBundleResource>(allBundles, null, Addressables.MergeMode.Union);

        yield return opHandle;

        IAssetBundleResource resource = opHandle.Result[0];

        AssetBundle bundle = resource.GetAssetBundle();

        string objName = bundle.GetAllAssetNames().FirstOrDefault(x => x.Contains(assetName));
        if (objName == null)
        {
            Log.LogError($"Could not find name {assetName} in bundle {bundleName}");
            Log.LogError("Available names:\n" + string.Join(", ", bundle.GetAllAssetNames().ToArray()));

            yield break;
        }

        Log.LogInfo($"Loading asset {objName} from bundle {bundleName}");

        T loaded = bundle.LoadAsset<T>(objName);

        LoadedAsset<T> wrapped = new(loaded, opHandle);
        onLoaded?.Invoke(wrapped);

        yield break;
    }
}
