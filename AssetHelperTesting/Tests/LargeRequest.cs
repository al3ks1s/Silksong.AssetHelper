using Silksong.AssetHelper.Dev;
using Silksong.AssetHelper.Plugin;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Test that makes a large request (taken from existing AssetHelper clients)
/// but does nothing with the assets.
/// </summary>
public class LargeRequest : MonoBehaviour
{
    // SceneReq1, NonSceneReq are Architect; SceneReq2 is Pharlooms Glory
    public static void Prepare(bool sceneReq1 = true, bool sceneReq2 = true, bool nonSceneReq = true)
    {
        GameObject go = new("Large Request Owner");
        DontDestroyOnLoad(go);
        LargeRequest component = go.AddComponent<LargeRequest>();

        MakeRequests(sceneReq1, sceneReq2, nonSceneReq);
        AssetRequestAPI.InvokeAfterBundleCreation(() => DebugTools.SerializeAssetRequest());
    }

    private static void MakeRequests(bool sceneReq1, bool sceneReq2, bool nonSceneReq)
    {
        if (sceneReq1)
        {
            RequestSceneAssets("scenegroup1");
        }
        if (sceneReq2)
        {
            RequestSceneAssets("scenegroup2");
        }
        if (nonSceneReq)
        {
            RequestNonSceneAssets("nonscenegroup");
        }
    }

    private static void RequestSceneAssets(string filename)
    {
        if (!JsonHelper.TryLoadEmbeddedJson(filename, out Dictionary<string, List<string>>? parsed))
        {
            AssetHelperTestingPlugin.InstanceLogger.LogWarning($"Failed to parse scene assets for {filename}");
            return;
        }

        foreach ((string scene, List<string> assets) in parsed)
        {
            AssetRequestAPI.RequestSceneAssets(scene, assets);
        }
    }

    private static void RequestNonSceneAssets(string filename)
    {
        if (!JsonHelper.TryLoadEmbeddedJson(
            filename,
            out Dictionary<(string bundleName, string assetName), Type>? parsed))
        {
            AssetHelperTestingPlugin.InstanceLogger.LogWarning($"Failed to parse non-scene assets for {filename}");
            return;
        }

        foreach (((string bundleName, string assetName), Type assetType) in parsed)
        {
            AssetRequestAPI.RequestNonSceneAsset(bundleName, assetName, assetType);
        }
    }
}
