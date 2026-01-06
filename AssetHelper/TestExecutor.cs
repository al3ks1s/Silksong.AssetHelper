using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.LoadedAssets;
using Silksong.AssetHelper.Plugin;
using Silksong.UnityHelper.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.BundleTools.RepackedBundleData>;

namespace Silksong.AssetHelper;

// TODO - probably remove this class before making a release
/// <summary>
/// Utility methods to test aspects of the codebase.
/// </summary>
internal static class TestExecutor
{
    public static int Completed { get;private set; }

    public static void TestCatalogSerialization()
    {
        // Create a new catalog with all non-scene bundles and all container assets within those bundles

        Stopwatch gatherSw = Stopwatch.StartNew();
        AssetsManager mgr = new();

        List<ContentCatalogDataEntry> bundleLocs = new();
        List<ContentCatalogDataEntry> assetLocs = new();

        foreach (IResourceLocation locn in Addressables.ResourceLocators.First().AllLocations)
        {
            if (locn.ResourceType != typeof(IAssetBundleResource)) continue;
            if (locn.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;
            
            bundleLocs.Add(CatalogEntryUtils.CreateEntryFromLocation(locn, out string primaryKey));

            using (MemoryStream ms = new(File.ReadAllBytes(locn.InternalId)))
            {
                BundleFileInstance bun = mgr.LoadBundleFile(ms, locn.PrimaryKey);
                AssetsFileInstance afi = mgr.LoadAssetsFileFromBundle(bun, 0);
                AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);

                foreach (AssetTypeValueField ctrEntry in iBundle["m_Container.Array"].Children)
                {
                    string name = ctrEntry["first"].AsString;

                    // TODO - should figure out the object type properly...
                    assetLocs.Add(CatalogEntryUtils.CreateAssetEntry($"{name}", typeof(UObject), new List<string> { $"{nameof(AssetHelper)}:{locn.PrimaryKey}" }, out string pk));
                }
                mgr.UnloadAll();
            }
        }
        gatherSw.Stop();

        //AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered catalog entries in {gatherSw.ElapsedMilliseconds} ms");
        List<ContentCatalogDataEntry> catalog = [.. bundleLocs, .. assetLocs];
        AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered {catalog.Count} catalog entries in {gatherSw.ElapsedMilliseconds} ms");

        Stopwatch writeSw = Stopwatch.StartNew();
        CatalogUtils.WriteCatalog(catalog, "testCatalog");
        writeSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Wrote catalog in {writeSw.ElapsedMilliseconds} ms");
        AssetBundle.UnloadAllAssetBundles(false);
        var p = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, "AssetHelper-testCatalog.bin"));
        var locr = p.WaitForCompletion();

        DebugTools.DumpAllAddressableAssets(locr, "nonscene.txt",true);

        var locnn = locr.AllLocations.Where(f => f.PrimaryKey.Contains("Assets/Prefabs/UI/Fast Travel Map.prefab")).First();
        var handle = Addressables.LoadAssetAsync<GameObject>(locnn);
        handle.WaitForCompletion();

        AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.Status}");
        AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.OperationException}");

    }

    public static void CreateFullNonSceneCatalog()
    {
        // Create a new catalog with all non-scene bundles and all container assets within those bundles

        Stopwatch gatherSw = Stopwatch.StartNew();
        AssetsManager mgr = new();

        // TODO - clean up primary keys
        List<ContentCatalogDataEntry> bundleLocs = new();
        List<ContentCatalogDataEntry> assetLocs = new();

        Dictionary<string, string> cab2key = new();
        foreach ((string cab, string name) in BundleDeps.CabLookup)
        {
            string origPrimaryKey = AssetsData.ToBundleKey(name);
            cab2key[cab] = nameof(AssetHelper) + ":" + origPrimaryKey;
        }

        foreach (IResourceLocation locn in Addressables.ResourceLocators.First().AllLocations)
        {
            if (locn.ResourceType != typeof(IAssetBundleResource)) continue;
            if (locn.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            bundleLocs.Add(CatalogEntryUtils.CreateEntryFromLocation(locn, nameof(AssetHelper) + ":" + locn.PrimaryKey));

            using (MemoryStream ms = new(File.ReadAllBytes(locn.InternalId)))
            {
                BundleFileInstance bun = mgr.LoadBundleFile(ms, locn.PrimaryKey);
                AssetsFileInstance afi = mgr.LoadAssetsFileFromBundle(bun, 0);
                AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);

                List<string> deps = afi.file.Metadata.Externals
                    .Select(x => x.OriginalPathName.Split("/")[^1].ToLowerInvariant())
                    .Where(x => x.StartsWith("cab"))
                    .Select(x => cab2key[x])
                    .Prepend(nameof(AssetHelper) + ":" + locn.PrimaryKey)
                    .ToList();

                foreach (AssetTypeValueField ctrEntry in iBundle["m_Container.Array"].Children)
                {
                    string name = ctrEntry["first"].AsString;

                    // TODO - should figure out the object type properly...
                    assetLocs.Add(CatalogEntryUtils.CreateAssetEntry($"{name}", typeof(UObject), deps, out _));
                }
                mgr.UnloadAll();
            }
        }
        gatherSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered catalog entries in {gatherSw.ElapsedMilliseconds} ms");

        List<ContentCatalogDataEntry> catalog = [.. bundleLocs, .. assetLocs];

        Stopwatch writeSw = Stopwatch.StartNew();
        string catalogPath = CatalogUtils.WriteCatalog(catalog, "testCatalog");
        writeSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Wrote catalog in {writeSw.ElapsedMilliseconds} ms");

        Stopwatch loadSw = Stopwatch.StartNew();
        IResourceLocator lr = Addressables.LoadContentCatalogAsync(catalogPath).WaitForCompletion();
        loadSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Loaded catalog in {loadSw.ElapsedMilliseconds} ms");
        DebugTools.DumpAllAddressableAssets(lr, "full_non_scene.json", true);

        AssetHelperPlugin.InstanceLogger.LogInfo($"starting lookup");
        //AssetBundle.UnloadAllAssetBundles(false);
        //DebugTools.GetLoadedBundleNames(out List<string> names, out List<string> unknown);
      
        AssetHelperPlugin.InstanceLogger.LogInfo($"Loaded: {AssetBundle.GetAllLoadedAssetBundles().Count()}");
        
        var locnn = lr.AllLocations.Where(f => f.PrimaryKey.Contains("AssetHelper/Addressables/Assets/Prefabs/"));
        foreach (var loc in locnn)
        {
            
            var handle = Addressables.LoadAssetAsync<GameObject>(loc);
            handle.WaitForCompletion();

            AssetHelperPlugin.InstanceLogger.LogInfo($"Loaded: {AssetBundle.GetAllLoadedAssetBundles().Count()}");
            AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.Status}");
            AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.OperationException}");
            AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.Result}");

        }

    }

    public static void TestCatalogSerialization2()
    {
        // Create a new catalog with all non-scene bundles and all container assets within those bundles

        Stopwatch gatherSw = Stopwatch.StartNew();
        long lastMem = 0;
        void Log(string msg, [CallerLineNumber] int lineno = -1) => AssetHelperPlugin.InstanceLogger.LogInfo($"{msg} [@{lineno}] [{gatherSw.ElapsedMilliseconds} ms] GC:{GC.GetTotalMemory(true) - lastMem}");
        AssetsManager mgr = new();

        List<ContentCatalogDataEntry> bundleLocs = new();
        List<ContentCatalogDataEntry> assetLocs = new();

        Dictionary<string, string> cab2key = new();
        foreach ((string cab, string name) in BundleDeps.CabLookup)
        {
            string origPrimaryKey = AssetsData.ToBundleKey(name);
            cab2key[cab] = nameof(AssetHelper) + ":" + origPrimaryKey;
        }

        //Log($"Blabla"); lastMem = GC.GetTotalMemory(true);
        foreach (IResourceLocation locn in Addressables.ResourceLocators.First().AllLocations)
        {
            if (locn.ResourceType != typeof(IAssetBundleResource)) continue;
            if (locn.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            //Log($"Bundle: {locn.PrimaryKey}"); lastMem = GC.GetTotalMemory(true);
            bundleLocs.Add(CatalogEntryUtils.CreateEntryFromLocation(locn, out _));

            using (MemoryStream ms = new(File.ReadAllBytes(locn.InternalId)))
            {
                BundleFileInstance bun = mgr.LoadBundleFile(ms, locn.PrimaryKey);
                //Log($"Bundle loaded"); lastMem = GC.GetTotalMemory(true);
                AssetsFileInstance afi = mgr.LoadAssetsFileFromBundle(bun, 0);
                //Log($"FileInstance"); lastMem = GC.GetTotalMemory(true);
                AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);
                //Log($"Bundle base field"); lastMem = GC.GetTotalMemory(true);

                List<string> deps = afi.file.Metadata.Externals
                    .Select(x => x.OriginalPathName.Split("/")[^1].ToLowerInvariant())
                    .Where(x => x.StartsWith("cab"))
                    .Select(x => cab2key[x])
                    .Prepend($"{nameof(AssetHelper)}:{locn.PrimaryKey}")
                    .ToList();

                foreach (AssetTypeValueField ctrEntry in iBundle["m_Container.Array"].Children)
                {
                    string name = ctrEntry["first"].AsString;

                    // TODO - should figure out the object type properly...
                    if (!name.StartsWith("Assets/"))
                    {
                        AssetHelperPlugin.InstanceLogger.LogMessage(name);
                    }
                    assetLocs.Add(CatalogEntryUtils.CreateAssetEntry($"{name}", typeof(UObject), deps, out _));
                }

                //Log($"Catalog entry"); lastMem = GC.GetTotalMemory(true);
                mgr.UnloadAll();
            }
        }
        gatherSw.Stop();
        //AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered catalog entries in {gatherSw.ElapsedMilliseconds} ms");

        List<ContentCatalogDataEntry> catalog = [.. bundleLocs, .. assetLocs];
        AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered {catalog.Count} catalog entries in {gatherSw.ElapsedMilliseconds} ms");
        
        Stopwatch writeSw = Stopwatch.StartNew();
        CatalogUtils.WriteCatalog(catalog, "testCatalog");

        writeSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Wrote catalog in {writeSw.ElapsedMilliseconds} ms");
        AssetBundle.UnloadAllAssetBundles(false);
        var p = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, "AssetHelper-testCatalog.bin"));
        var locr = p.WaitForCompletion();

        DebugTools.DumpAllAddressableAssets(locr, "nonscene.txt", true);

        var locnn = locr.AllLocations.Where(f => f.PrimaryKey.Contains("Assets/Prefabs/UI/Fast Travel Map.prefab")).First();
        var handle = Addressables.LoadAssetAsync<GameObject>(locnn);
        handle.WaitForCompletion();

        AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.Status}");
        AssetHelperPlugin.InstanceLogger.LogInfo($"{handle.OperationException}");

    }

    internal static string CreateCatalog(RepackDataCollection data)
    {
        Stopwatch sw = Stopwatch.StartNew();
        void Log(string msg, [CallerLineNumber] int lineno = -1) => AssetHelperPlugin.InstanceLogger.LogInfo($"{msg} [@{lineno}] [{sw.ElapsedMilliseconds} ms]");
        Log("Started");

        List<ContentCatalogDataEntry> addedEntries = [];
        Dictionary<string, ContentCatalogDataEntry> bundleLookup = [];
        Dictionary<string, string> pkLookup = [];
        HashSet<string> bundlesToInclude = [];

        foreach (IResourceLocation location in Addressables.ResourceLocators.First().AllLocations)
        {
            if (location.ResourceType != typeof(IAssetBundleResource)) continue;
            if (location.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            if (!AssetsData.TryStrip(location.PrimaryKey, out string? stripped)) continue;
            bundleLookup[stripped] = CatalogEntryUtils.CreateEntryFromLocation(location, out string newPrimaryKey);
            pkLookup[stripped] = newPrimaryKey;
        }

        Log($"Created {bundleLookup.Count} existing bundle entries");

        foreach ((string scene, RepackedBundleData bundleData) in data)
        {
            // Create an entry for the bundle
            string repackedSceneBundleKey = $"AssetHelper/RepackedScenes/{scene}";

            ContentCatalogDataEntry bundleEntry = CatalogEntryUtils.CreateBundleEntry(
                repackedSceneBundleKey,
                Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"),
                bundleData.BundleName!,
                []);
            addedEntries.Add(bundleEntry);

            List<string> dependencyKeys = [repackedSceneBundleKey];
            foreach (string dep in BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{scene}.bundle"))
            {
                string depKey = dep.Replace(".bundle", "");
                bundlesToInclude.Add(depKey);
                dependencyKeys.Add(pkLookup[depKey]);
            }

            foreach ((string containerPath, string objPath) in bundleData.GameObjectAssets ?? [])
            {
                ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                    $"AssetHelper/RepackedAssets/{scene}/{objPath}",
                    typeof(GameObject),
                    dependencyKeys,
                    out string pki);
                addedEntries.Add(entry);
            }
        }

        List<ContentCatalogDataEntry> allEntries = new();
        allEntries.AddRange(bundlesToInclude.Select(x => bundleLookup[x]));
        allEntries.AddRange(addedEntries);

        Log($"Placed {allEntries.Count} entries in catalog list");

        string catalogPath = CatalogUtils.WriteCatalog(allEntries, "repackedSceneCatalog");

        Log("Wrote catalog");

        return catalogPath;
    }

    internal static string CreateCatalog2(RepackDataCollection data)
    {
        // TODO - clean this up

        Stopwatch sw = Stopwatch.StartNew();
        void Log(string msg, [CallerLineNumber] int lineno = -1) => AssetHelperPlugin.InstanceLogger.LogInfo($"{msg} [@{lineno}] [{sw.ElapsedMilliseconds} ms]");
        Log("Started");

        List<ContentCatalogDataEntry> addedEntries = [];
        Dictionary<string, ContentCatalogDataEntry> bundleLookup = [];
        Dictionary<string, string> pkLookup = [];
        HashSet<string> bundlesToInclude = [];

        foreach (IResourceLocation location in Addressables.ResourceLocators.First().AllLocations)
        {
            if (location.ResourceType != typeof(IAssetBundleResource)) continue;
            if (location.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            if (!AssetsData.TryStrip(location.PrimaryKey, out string? stripped)) continue;
            bundleLookup[stripped] = CatalogEntryUtils.CreateEntryFromLocation(location, out string newPrimaryKey);
            pkLookup[stripped] = newPrimaryKey;
        }

        Log($"Created {bundleLookup.Count} existing bundle entries");

        foreach ((string scene, RepackedBundleData bundleData) in data)
        {
            // Create an entry for the bundle
            string repackedSceneBundleKey = $"AssetHelper/RepackedScenes/{scene}";

            ContentCatalogDataEntry bundleEntry = CatalogEntryUtils.CreateBundleEntry(
                repackedSceneBundleKey,
                Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"),
                bundleData.BundleName!,
                []);
            addedEntries.Add(bundleEntry);

            List<string> dependencyKeys = [repackedSceneBundleKey];
            foreach (string dep in BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{scene}.bundle"))
            {
                string depKey = dep.Replace(".bundle", "");
                bundlesToInclude.Add(depKey);
                dependencyKeys.Add(pkLookup[depKey]);
            }

            foreach ((string containerPath, string objPath) in bundleData.GameObjectAssets ?? [])
            {
                ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                    containerPath,
                    typeof(GameObject),
                    dependencyKeys,
                    $"AssetHelper/RepackedAssets/{scene}/{objPath}"
                    );
                addedEntries.Add(entry);
            }
        }

        List<ContentCatalogDataEntry> allEntries = new();
        allEntries.AddRange(bundlesToInclude.Select(x => bundleLookup[x]));
        allEntries.AddRange(addedEntries);

        allEntries.Add(CatalogEntryUtils.CreateChildGameObjectEntry(
            "AssetHelper/RepackedAssets/memory_coral_tower/Battle Scenes",
            "Battle Scene Chamber 2/Wave 1/Coral Hunter",
            out _
            ));

        Log($"Placed {allEntries.Count} entries in catalog list");

        string catalogPath = CatalogUtils.WriteCatalog(allEntries, "repackedSceneCatalog");

        Log("Wrote catalog");

        return catalogPath;
    }


    public static void GenFromFile()
    {
        if (!JsonExtensions.TryLoadFromFile(Path.Combine(AssetPaths.AssemblyFolder, "serialization_data.json"), out Dictionary<string, List<string>>? data))
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"No serialization_data.json found next to this assembly");
            return;
        }

        Gen(data);
    }

    public static void Gen(Dictionary<string, List<string>> rpData)
    {
        Directory.CreateDirectory(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump"));

        SceneRepacker r = new StrippedSceneRepacker();
        Dictionary<string, RepackedBundleData> data = [];

        CustomCatalogBuilder ccb = new CustomCatalogBuilder("repackedSceneCatalog");
        
        Stopwatch sw = Stopwatch.StartNew();
        foreach ((string scene, List<string> objs) in rpData!)
        {
            try
            {
                Stopwatch miniSw = Stopwatch.StartNew();
                RepackedBundleData dat = r.Repack(AssetPaths.GetScenePath(scene), objs, Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"));
                ccb.AddRepackedSceneData(scene, dat, Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"));
                data[scene] = dat;
                miniSw.Stop();
                AssetHelperPlugin.InstanceLogger.LogInfo($"Scene {scene} complete {miniSw.ElapsedMilliseconds} ms");
                Completed += 1;
            }
            catch (Exception ex)
            {
                AssetHelperPlugin.InstanceLogger.LogError($"Scene {scene} error\n" + ex);
            }
        }
        
        data.SerializeToFile(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", "repack_data.json"));
        AssetHelperPlugin.InstanceLogger.LogInfo($"All scenes complete {sw.ElapsedMilliseconds} ms");
        AssetHelperPlugin.InstanceLogger.LogInfo($"Starting architect catalog");

        ccb.Build();

    }


    public static void CustomBundle()
    {
        // Create bundle
        StrippedSceneRepacker repacker = new();
        RepackedBundleData data = repacker.Repack(
            AssetPaths.GetScenePath("Memory_coral_tower"),
            ["Battle Scenes"],
            Path.Combine(AssetPaths.AssemblyFolder, "battlescenes.bundle"));

        // Create catalog
        Dictionary<string, RepackedBundleData> lookup = new();
        lookup["memory_coral_tower"] = data;

        CustomCatalogBuilder ccb = new CustomCatalogBuilder("repackedSceneCatalog");
        ccb.AddRepackedSceneData("memory_coral_tower", data, Path.Combine(AssetPaths.AssemblyFolder, "battlescenes.bundle"));
        
        ccb.addEntry(CatalogEntryUtils.CreateChildGameObjectEntry(
            "repackedSceneCatalog/RepackedAssets/memory_coral_tower/Battle Scenes",
            "Battle Scene Chamber 2/Wave 1/Coral Hunter",
            out _
            ));
        string path  = ccb.Build();

    }


    public static void RunArchitectTest()
    {
        if (!JsonExtensions.TryLoadFromFile<Dictionary<string, RepackedBundleData>>(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", "repack_data.json"), out var data))
        {
            return;
        }

        List<string> sceneNames = data.Keys.ToList();

        Stopwatch sw = Stopwatch.StartNew();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Determining deps for {sceneNames.Count} scenes");
        foreach (string sceneName in sceneNames)
        {
            BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{sceneName}.bundle");
        }
        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Determined deps in {sw.ElapsedMilliseconds} ms");
    }

    public static IEnumerator InstantiateAll()
    {
        Stopwatch sw = Stopwatch.StartNew();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Start with loaded bundle count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load serda
        if (!JsonExtensions.TryLoadFromFile<Dictionary<string, RepackedBundleData>>(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", "repack_data.json"), out var data))
        {
            yield break;
        }
        List<string> allDependencies = new();
        foreach (string scene in data.Keys)
        {
            string key = $"scenes_scenes_scenes/{scene.ToLowerInvariant()}";
            allDependencies.AddRange(BundleDeps.DetermineDirectDeps(key).Where(x => x != key));
        }
        AssetHelperPlugin.InstanceLogger.LogInfo($"Build deps: {allDependencies.Count}: {sw.ElapsedMilliseconds} ms");
        AssetBundleGroup abg = new(allDependencies);
        yield return abg.LoadAsync();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Deps loaded, new count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        Dictionary<(string, string), GameObject> allGos = [];

        foreach ((string scene, RepackedBundleData dat) in data)
        {
            Stopwatch miniSw = Stopwatch.StartNew();
            var req = AssetBundle.LoadFromFileAsync(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"));
            yield return req;
            AssetBundle loadedModBundle = req.assetBundle;
            AssetHelperPlugin.InstanceLogger.LogInfo($"Bundle loaded {scene}: {miniSw.ElapsedMilliseconds} ms");

            var assetsReq = loadedModBundle.LoadAllAssetsAsync<GameObject>();
            yield return assetsReq;
            AssetHelperPlugin.InstanceLogger.LogInfo($"AssetReq completed: {miniSw.ElapsedMilliseconds} ms");

            int ct = assetsReq.allAssets.Where(x => x != null).Count();
            AssetHelperPlugin.InstanceLogger.LogInfo($"Loaded {ct} assets, expected {dat.GameObjectAssets?.Count ?? 0}");

            yield return null;
        }
    }

    public static IEnumerator InstantiateAssets(string bundleFile, string sceneName, string assetPath)
    {
        Stopwatch sw = Stopwatch.StartNew();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Start with loaded bundle count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load dependencies
        AssetBundleGroup? dependencyGrp = AssetBundleGroup.CreateForScene(sceneName, false);  // Set to true for shallow bundle

        yield return dependencyGrp.LoadAsync();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Deps loaded, new count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load bundle
        var req = AssetBundle.LoadFromFileAsync(bundleFile);
        yield return req;
        AssetBundle loadedModBundle = req.assetBundle;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Main Bundle loaded: {sw.ElapsedMilliseconds} ms");

        // Spawn mask shard
        GameObject theAsset = loadedModBundle.LoadAsset<GameObject>(assetPath);
        AssetHelperPlugin.InstanceLogger.LogInfo($"Asset loaded: {sw.ElapsedMilliseconds} ms");

        GameObject go = UObject.Instantiate(theAsset);
        go.name = $"SpawnedAsset-{GetRandomString()}";

        if (HeroController.instance != null)
        {
            go.transform.position = HeroController.instance.transform.position + new Vector3(0, 3, 0);
        }

        go.SetActive(true);

        AssetHelperPlugin.InstanceLogger.LogInfo($"Spawned: {sw.ElapsedMilliseconds} ms");

        yield return null;

        static string GetRandomString()
        {
            string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            char[] res = new char[10];
            System.Random rng = new();

            for (int i = 0; i < 10; i++)
                res[i] = chars[rng.Next(chars.Length)];

            return new string(res);
        }

    }


}
