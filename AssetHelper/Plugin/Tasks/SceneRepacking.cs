using AssetHelperLib.Repacking;
using Silksong.AssetHelper.Internal;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Silksong.AssetHelper.Core;
using AssetHelperLib.PreloadTable;
using System;
using Silksong.AssetHelper.Plugin.LoadingPage;
using AssetHelperLib.BundleTools;
using CPPCache = System.Collections.Generic.Dictionary<string, AssetHelperLib.PreloadTable.ContainerPointerPreloadsBundleData>;
using GoInfo = AssetHelperLib.BundleTools.GameObjectLookup.GameObjectInfo;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.Plugin.RepackedSceneBundleData>;

namespace Silksong.AssetHelper.Plugin.Tasks;

internal class SceneRepacking : BaseStartupTask
{
    private static string SceneCatalogPath => Path.Combine(AssetPaths.CatalogFolder, $"{CatalogKeys.SceneCatalogId}.bin");

    private static string CatalogMetadataPath => Path.ChangeExtension(SceneCatalogPath, ".json");

    // Data about the repacked assets in the bundles on disk
    private RepackDataCollection _repackData = [];

    public override IEnumerator Run(ILoadingScreen loadingScreen)
    {
        if (AssetRequestAPI.Request.SceneAssets.Count == 0)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo("Not running scene repack operation: no scenes in request");
        }

        // Prepare operation
        if (JsonExtensions.TryLoadFromFile(AssetPaths.RepackedSceneBundleMetadataPath, out RepackDataCollection? repackData))
        {
            _repackData = repackData;
        }
        else
        {
            _repackData = [];
        }

        // This block sets _repackData to include only the data for scenes that do not need to be repacked
        _repackData = _repackData
            // If some data is missing, we must repack
            .Where(kvp => kvp.Value.CatalogInfo is not null && kvp.Value.Data is not null)
            // If the metadata (plugin version, bundle hash) changes, we must repack
            .Where(kvp => !MetadataMismatch(kvp.Key, kvp.Value))
            // If the bundle does not exist, we must repack
            .Where(kvp => File.Exists(GetBundlePathForScene(kvp.Key)))
            // If the existing data does not support everything in the request, we repack
            .Where(kvp => CanLoadAll(kvp.Value.CatalogInfo!, kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        List<string> scenesToRepack;

        // Repack scenes that need to be repacked
        {
            loadingScreen.Reset();
            loadingScreen.SetText(LanguageKeys.REPACKING_SCENE.GetLocalized());

            CachedObject<CPPCache> SyncedCppCache = CachedObject<CPPCache>.CreateSynced(
                "container_pointer_preloads_cache.json", () => new(), mutable: true, out IDisposable? cppSyncHandle);

            ContainerPointerPreloads cpp = new(ResolveCab) { Cache = SyncedCppCache.Value };
            PreloadTableResolver resolver = new([new DefaultPreloadTableResolver(), cpp]);
            SceneRepacker repacker = new StrippedSceneRepacker(resolver);

            scenesToRepack = AssetRequestAPI.Request.SceneAssets.Keys
                .Where(x => !_repackData.ContainsKey(x))
                .ToList();

            Stopwatch mainSw = Stopwatch.StartNew();

            int total = scenesToRepack.Count;
            int count = 0;

            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {total} scenes");

            foreach (string scene in scenesToRepack)
            {
                HashSet<string> request = AssetRequestAPI.Request.SceneAssets[scene];
                Stopwatch sw = Stopwatch.StartNew();
                AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {request.Count} objects in scene {scene}");
                loadingScreen.SetSubtext(scene);

                RepackedSceneBundleData sceneRepackData = DoRepack(scene, request, repacker);

                sw.Stop();
                _repackData[scene] = sceneRepackData;
                // Serialize after each step so that interrupted operations can continue
                _repackData.SerializeToFile(AssetPaths.RepackedSceneBundleMetadataPath);
                AssetHelperPlugin.InstanceLogger.LogInfo($"Repacked {scene} in {sw.ElapsedMilliseconds} ms");

                count += 1;
                loadingScreen.SetProgress((float)count / (float)total);

                yield return null;
            }

            mainSw.Stop();
            AssetHelperPlugin.InstanceLogger.LogInfo($"Finished scene repacking after {mainSw.ElapsedMilliseconds} ms");

            loadingScreen.Reset();

            // Save and dispose the cpp cache
            cppSyncHandle?.Dispose();
            yield return null;
        }

        // Write catalog
        // We can skip the catalog writing if nothing was freshly repacked
        // and the catalog exists with the correct metadata.
        {
            bool shouldWriteCatalog = (scenesToRepack.Count > 0) || MustWriteCatalog();

            if (shouldWriteCatalog)
            {
                loadingScreen.Reset();
                loadingScreen.SetText(LanguageKeys.BULDING_SCENE.GetLocalized());

                AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");
                Stopwatch sw = Stopwatch.StartNew();

                CustomCatalogBuilder cbr = new(CatalogKeys.SceneCatalogId);

                foreach ((string scene, RepackedSceneBundleData data) in _repackData)
                {
                    if (data.Data == null || data.CatalogInfo == null) continue;
                    string bundlePath = GetBundlePathForScene(scene);
                    string bundleFileName = Path.GetFileName(bundlePath);
                    string serializedBundlePath = $"{GetSerializedBundleDirPrefix()}/{bundleFileName}";

                    cbr.AddRepackedSceneData(scene, data.Data, data.CatalogInfo, bundlePath, serializedBundlePath);
                }
                sw.Stop();
                AssetHelperPlugin.InstanceLogger.LogInfo($"Prepared catalog in {sw.ElapsedMilliseconds} ms");

                loadingScreen.SetText(LanguageKeys.WRITING_SCENE.GetLocalized());
                loadingScreen.SetProgress(0);
                yield return null;

                sw = Stopwatch.StartNew();

                int catCount = 0;
                using IEnumerator<float> serializationRoutine = cbr.BuildRoutine();
                while (serializationRoutine.MoveNext())
                {
                    float progress = serializationRoutine.Current;
                    catCount++;
                    if (catCount % 10 == 0)
                    {
                        loadingScreen.SetProgress(progress);
                        yield return null;
                    }
                }

                sw.Stop();
                AssetHelperPlugin.InstanceLogger.LogInfo($"Finished writing catalog in {sw.ElapsedMilliseconds} ms");

                SceneCatalogMetadata metadata = new();
                metadata.SerializeToFile(CatalogMetadataPath);
            }
            else
            {
                AssetHelperPlugin.InstanceLogger.LogInfo($"Not creating catalog");
            }
        }

        yield return null;

        // Load catalog
        loadingScreen.SetText(LanguageKeys.LOADING_SCENE.GetLocalized());
        yield return null;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Loading scene catalog");
        AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(SceneCatalogPath);
        yield return catalogLoadOp;
        AssetRequestAPI.SceneAssetLocator = catalogLoadOp.Result;

        yield return null;
    }

    private RepackedSceneBundleData DoRepack(
        string scene,
        HashSet<string> request,
        SceneRepacker repacker
        )
    {
        Dictionary<string, List<List<long>>> transformSeqs = null!;

        string containerPrefix = $"{nameof(AssetHelper)}/{scene}";

        RepackingParams rParams = new()
        {
            SceneBundlePath = AssetPaths.GetScenePath(scene),
            ObjectNames = request.ToList(),
            ContainerPrefix = containerPrefix,
            OutBundlePath = GetBundlePathForScene(scene),
            LateCallback = (ctx, data) => transformSeqs = BuildTransformSequences(ctx, data, request),
        };
        RepackedBundleData repackData = repacker.Repack(rParams);

        string? hash = null;
        if (AddressablesData.TryGetLocationForScene(scene, out IResourceLocation? location) && location.Data is AssetBundleRequestOptions opts)
        {
            hash = opts.Hash;
        }

        SceneCatalogInfo catInfo = BuildSceneCatalogInfo(repackData, transformSeqs, containerPrefix);

        RepackedSceneBundleData sceneRepackData = new()
        {
            SceneName = scene,
            BundleHash = hash,
            Data = repackData,
            CatalogInfo = catInfo,
        };

        return sceneRepackData;
    }

    private SceneCatalogInfo BuildSceneCatalogInfo(
        RepackedBundleData repackData, Dictionary<string, List<List<long>>> transformSeqs, string containerPrefix)
    {
        SceneCatalogInfo info = new();

        HashSet<string> rootGos = repackData.GameObjectAssets?.Values.Distinct().ToHashSet() ?? [];
        Dictionary<long, string> rootTransformPathIds = [];
        foreach (string rootGo in rootGos)
        {
            foreach (List<long> transformSeq in transformSeqs[rootGo])
            {
                string containerPath = repackData.GameObjectAssets!
                    .First(kvp => kvp.Key.StartsWith($"{containerPrefix}/{transformSeq[0]}") && kvp.Value == rootGo)
                    .Key;
                info.RootGameObjects.Add(new(rootGo, containerPath, transformSeq[0]));
                rootTransformPathIds.Add(transformSeq[0], rootGo);
            }
        }
        
        foreach (string goPath in transformSeqs.Keys)
        {
            if (rootGos.Contains(goPath)) continue;

            foreach (List<long> transformSeq in transformSeqs[goPath])
            {
                long ancestorPathId = transformSeq.FirstOrDefault(x => rootTransformPathIds.ContainsKey(x));
                if (ancestorPathId == 0)
                {
                    throw new Exception($"Unexpectedly failed to find ancestor for {goPath} [{transformSeq[0]}]");
                }
                string ancestor = rootTransformPathIds[ancestorPathId];
                if (!goPath.StartsWith(ancestor + "/"))
                {
                    throw new Exception($"Object {goPath} unexpectedly matched ancestor {ancestor}");
                }
                string relativePath = goPath.Substring(ancestor.Length + 1);

                info.ChildGameObjects.Add(new(
                    goPath,
                    transformSeq[0],
                    ancestor,
                    relativePath,
                    ancestorPathId
                    ));
            }
        }

        return info;
    }

    /// <summary>
    /// For each game object path, compute all transform path sequences for that game object path.
    /// 
    /// A transform path sequence is a list of transform path IDs [id0, id1, ..., idn], where id0 is the
    /// transform path id for the game object, id1 is its parent, ..., idn is the root.
    /// 
    /// The number of path sequences for each game object path will match the number of game objects with
    /// that game object path.
    /// </summary>
    private Dictionary<string, List<List<long>>> BuildTransformSequences(
        RepackingContext ctx, RepackedBundleData data, HashSet<string> request)
    {
        GameObjectLookup goLookup = ctx.GameObjLookup
            ?? GameObjectLookup.CreateFromFile(ctx.SceneAssetsManager, ctx.MainAssetsFileInstance);

        Dictionary<string, List<List<long>>> transformSeqs = [];

        // We need to find transform sequences for all game objects in the request,
        // and also all game objects in the bundle. The latter will only matter if there is a root game object
        // in the repacked bundle that wasn't in the request, but has been included due to being a dependency
        // for a requested asset.
        HashSet<string> requiredPaths = [
            .. request,
            .. data.GameObjectAssets?.Values ?? Enumerable.Empty<string>()
            ];

        foreach (string objPath in requiredPaths)
        {
            List<List<long>> objTransformSeqs = [];
            
            if (!goLookup.TryLookupName(objPath, out List<GoInfo>? infos))
            {
                throw new Exception($"Failed to find {objPath} in bundle");
            }

            foreach (GoInfo info in infos)
            {
                GoInfo currentInfo = info;
                List<long> tPathSeq = [];
                long tPathId = currentInfo.TransformPathId;

                tPathSeq.Add(tPathId);

                while (currentInfo.ParentPathId != 0)
                {
                    currentInfo = goLookup.LookupTransform(currentInfo.ParentPathId);
                    tPathSeq.Add(currentInfo.TransformPathId);
                }

                objTransformSeqs.Add(tPathSeq);
            }

            transformSeqs[objPath] = objTransformSeqs;
        }

        return transformSeqs;
    }

    private bool MustWriteCatalog()
    {
        if (!JsonExtensions.TryLoadFromFile(CatalogMetadataPath, out SceneCatalogMetadata? oldMeta)
            || oldMeta.Metadata == null
            || oldMeta.Metadata.SilksongVersion != VersionData.SilksongVersion
            || !VersionData.EarliestAcceptableSceneRepackVersion.AllowCachedData(oldMeta.Metadata.PluginVersion)
            || oldMeta.Metadata.OSFolderName != AssetPaths.OSFolderName
            )
        {
            return true;
        }

        if (!File.Exists(SceneCatalogPath))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if we have to completely repack because of a metadata change.
    /// </summary>
    private static bool MetadataMismatch(string scene, RepackedSceneBundleData existingData)
    {
        if (existingData.Metadata == null)
        {
            return true;
        }

        if (!VersionData.EarliestAcceptableSceneRepackVersion.AllowCachedData(existingData.Metadata.PluginVersion))
        {
            // Mismatch: the version of the plugin used to repack needs to be after the last acceptable version.
            // We do not accept versions from the future.
            return true;
        }

        if (existingData.Metadata.OSFolderName != AssetPaths.OSFolderName)
        {
            // Different OS strings mean the base game bundles may be different
            return true;
        }

        if (existingData.Metadata.SilksongVersion == VersionData.SilksongVersion)
        {
            // If the Silksong version matches, then we're definitely fine.
            return false;
        }

        if (AddressablesData.TryGetLocationForScene(scene, out IResourceLocation? location)
            && location.Data is AssetBundleRequestOptions opts
            && !string.IsNullOrEmpty(opts.Hash)
            && !string.IsNullOrEmpty(existingData.BundleHash)
            && opts.Hash == existingData.BundleHash)
        {
            // Hash matches, so we can accept the mismatched silksong version
            return false;
        }

        return true;
    }

    /// <summary>
    /// Return true if the provided catalog info is capable of loading all assets requested
    /// for the given scene.
    /// </summary>
    /// <param name="catalogInfo"></param>
    /// <param name="sceneName"></param>
    /// <returns></returns>
    private bool CanLoadAll(SceneCatalogInfo catalogInfo, string sceneName)
    {
        HashSet<string> existingAssets = new(catalogInfo.LoadableAssets);

        if (!AssetRequestAPI.Request.SceneAssets.TryGetValue(sceneName, out HashSet<string> requested))
        {
            // If nothing was requested, then certainly everything requested can be loaded.
            return true;
        }

        return requested.IsSubsetOf(existingAssets);
    }

    private static string GetSerializedBundleDirPrefix()
    {
        return $$"""{Silksong.{{nameof(AssetHelper)}}.Core.{{nameof(AssetPaths)}}.{{nameof(AssetPaths.RepackedSceneBundleDir)}}}""";
    }

    private static string GetBundlePathForScene(string sceneName)
    {
        return Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
    }

    private static bool ResolveCab(string cabName, out string? bundlePath)
    {
        bundlePath = null;

        if (cabName.Contains("unity"))
        {
            // This isn't a game bundle (not a cab name) so we should silently skip
            return true;
        }

        if (!BundleMetadata.CabLookup.TryGetValue(cabName.ToLowerInvariant(), out string? bundleName))
        {
            // Surprisingly failed to resolve a cab, so we should ensure a warning is emitted
            return false;
        }

        if (bundleName.Contains("monoscripts") || bundleName.Contains("builtinassets"))
        {
            // Silently skip these because we shouldn't follow any deps there
            return true;
        }

        bundlePath = Path.Combine(AssetPaths.BundleFolder, bundleName);
        return true;
    }
}
