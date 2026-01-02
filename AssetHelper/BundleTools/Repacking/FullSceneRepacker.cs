using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using HutongGames.PlayMaker.Actions;
using Mono.WebBrowser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Silksong.AssetHelper.BundleTools.Repacking;

public class FullSceneRepacker : SceneRepacker
{


    /// <inheritdoc />
    public override RepackedBundleData Repack(string sceneBundlePath, List<string> objectNames, string outBundlePath) {

        GetDefaultBundleNames(sceneBundlePath, new List<string> { "All the objects" }, outBundlePath, out string newCabName, out string newBundleName);
        AssetsManager mgr = BundleUtils.CreateDefaultManager();
        BundleFileInstance sceneBun = mgr.LoadBundleFile(sceneBundlePath);

        if (!TryFindAssetsFiles(mgr, sceneBun, out AssetsFileInstance? mainSceneAfileInst, out AssetsFileInstance? sceneSharedAssetsFileInst, out int mainAfileIdx))
        {
            throw new NotSupportedException($"Could not find assets files for {sceneBundlePath}");
        }

        AssetsFile sceneAfile = mainSceneAfileInst.file;
        AssetsFile sharedAssetsAfile = sceneSharedAssetsFileInst.file;
        AssetFileInfo assetInfosSA = sceneAfile.GetAssetsOfType(AssetClassID.AssetBundle).First();

        var bundleBaseSA = mgr.GetBaseField(sceneSharedAssetsFileInst, assetInfosSA);

        List<AssetFileInfo> assetInfos = sceneAfile.GetAssetsOfType(AssetClassID.GameObject);
        HashSet<string> names = new HashSet<string>();

        bundleBaseSA["m_Container.Array"].Children.RemoveAt(0);

        AssetTypeValueField arrayEntry;
        AssetTypeValueField bundleBase;
        AssetExternal extasset;

        List<ContentCatalogDataEntry> locationEntries = new List<ContentCatalogDataEntry>();
        int currentPreloadIndex = 0;
        foreach (var assetInfo in assetInfos)
        {
            bundleBase = mgr.GetBaseField(mainSceneAfileInst, assetInfo);
            extasset = mgr.GetExtAsset(mainSceneAfileInst, bundleBase["m_Component.Array"][0]["component"]);

            if (extasset.baseField["m_Father.m_PathID"].AsLong == 0)
            {

                if (!names.Contains(bundleBase["m_Name"].AsString))
                {

                    arrayEntry = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleBaseSA["m_Container.Array"]);
                    arrayEntry["first"].AsString = $"AssetHelper/Scenes/{scene}/{bundleBase["m_Name"].AsString}";
                    arrayEntry["second.asset.m_FileID"].AsInt = 0;
                    arrayEntry["second.asset.m_PathID"].AsLong = assetInfo.PathId;


                    List<long> internalPaths = new List<long>();
                    List<(int fileId, long pathId)> externalPaths = new List<(int fileId, long pathId)>();
                    Stopwatch swDeps = Stopwatch.StartNew();
                    swDeps.Start();
                    mgr.FindBundleDependentObjects(mainSceneAfileInst, assetInfo.PathId);
                    swDeps.Stop();

                    arrayEntry["second.preloadIndex"].AsInt = currentPreloadIndex;
                    arrayEntry["second.preloadSize"].AsInt = externalPaths.Count;

                    Stopwatch swPreloadFill = Stopwatch.StartNew();
                    foreach (var preloadPtr in externalPaths)
                    {
                        var preloadEntry = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleBaseSA["m_PreloadTable.Array"]);
                        preloadEntry["m_FileID"].AsInt = preloadPtr.fileId;
                        preloadEntry["m_PathID"].AsLong = preloadPtr.pathId;
                        bundleBaseSA["m_PreloadTable.Array"].Children.Add(preloadEntry);
                        currentPreloadIndex += 1;
                    }

                    bundleBaseSA["m_Container.Array"].Children.Add(arrayEntry);

                    string internalId = $"AssetHelper/Scenes/{scene}/{bundleBase["m_Name"].AsString}";
                    string providerId = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

                    List<string> keys = new List<string>();
                    keys.Add($"AssetHelper/Scenes/{scene}/{bundleBase["m_Name"].AsString}");

                    List<string> dependencies = new List<string>();
                    dependencies.Add($"repacked-{scene.ToLower()}-{newBundleName}.bundle");
                    ContentCatalogDataEntry entry = new ContentCatalogDataEntry(typeof(GameObject), internalId, providerId, keys, dependencies);
                    locationEntries.Add(entry);

                    keysToLoad.Add(internalId);

                }

                names.Add(bundleBase["m_Name"].AsString);
            }
        }

        bundleBaseSA["m_IsStreamedSceneAssetBundle"].AsBool = false;
        assetInfosSA.SetNewData(bundleBaseSA);

        bundleBaseSA["m_Name"].AsString = newBundleName + ".bundle";
        bundleBaseSA["m_AssetBundleName"].AsString = bundleBaseSA["m_Name"].AsString;
        bundleBaseSA["m_SceneHashes.Array"].Children.RemoveAt(0);
        //Log.LogInfo(bundleBaseSA["m_Name"].AsString);

        AssetFileInfo firstAssetInfo = fileInst.file.GetAssetInfo(1);
        fileInst.file.Metadata.RemoveAssetInfo(firstAssetInfo);
        firstAssetInfo.PathId = 1000000;
        var n = AssetFileInfo.Create(fileInst.file, 1, (int)AssetClassID.AssetBundle, cdf);

        n.SetNewData(bundleBaseSA);

        fileInst.file.Metadata.AddAssetInfo(n);
        fileInst.file.Metadata.AddAssetInfo(firstAssetInfo);
        bundle.file.BlockAndDirInfo.DirectoryInfos[1].SetNewData(fileInst.file);
        bundle.file.BlockAndDirInfo.DirectoryInfos[1].Name = $"CAB-{hash(Encoding.ASCII.GetBytes(bundle.file.BlockAndDirInfo.DirectoryInfos[1].Name)).ToString()}";
        bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetRemoved();

        Stopwatch swWrite = Stopwatch.StartNew();
        swWrite.Start();
        using (AssetsFileWriter writer = new AssetsFileWriter(Path.Combine(pluginPath, "repackedBundles", scene.ToLower() + ".bundle")))
        {
            bundle.file.Write(writer);
        }

        return null;
    }

    
}
