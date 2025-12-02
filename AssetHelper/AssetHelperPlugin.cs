using BepInEx;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UObject = UnityEngine.Object;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    private static readonly Dictionary<string, string> Keys = [];
    
    public static bool IsLoaded { get; private set; }

    private static readonly string BundleSuffix = @"_[0-9a-fA-F]{32}\.bundle+$";
    private static readonly Regex BundleSuffixRegex = new (BundleSuffix, RegexOptions.Compiled);

    public static AssetHelperPlugin Instance { get;private set; }

    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }


    private bool TryStrip(string key, [MaybeNullWhen(false)] out string stripped)
    {
        if (BundleSuffixRegex.IsMatch(key))
        {
            stripped = BundleSuffixRegex.Replace(key, "");
            return true;
        }
        else
        {
            stripped = key;
            return false;
        }
    }

    private IEnumerator Start()
    {
        // For some reason we need to wait to load the asset list
        yield return null;
        yield return null;

        Stopwatch sw = Stopwatch.StartNew();
        IResourceLocator locator = Addressables.InitializeAsync().WaitForCompletion();

        foreach (string key in locator.Keys)
        {
            if (!TryStrip(key, out string? stripped)) continue;

            Keys[stripped] = key;
        }

        sw.Stop();
        Logger.LogInfo($"Loaded asset list in {sw.ElapsedMilliseconds} ms");

        IsLoaded = true;
    }

    private static T? LoadAsset<T>(string bundleName, string name, List<string>? extraDependencies = null)
        where T : UObject
    {
        if (!IsLoaded)
        {
            Instance.Logger.LogWarning($"Cannot load asset {name} from {bundleName}: too early");
        }

        extraDependencies ??= [];

        List<IAssetBundleResource> loadedDependencies = [];
        foreach (string key in extraDependencies)
        {
            loadedDependencies.Add(
                Addressables.LoadAssetAsync<IAssetBundleResource>(Keys[key]).WaitForCompletion()
                );
        }

        IAssetBundleResource rsc = Addressables
            .LoadAssetAsync<IAssetBundleResource>(Keys[bundleName]).WaitForCompletion();

        AssetBundle bundle = rsc.GetAssetBundle();

        string objName = bundle.GetAllAssetNames().FirstOrDefault(x => x.Contains(name));
        if (objName == null)
        {
            Instance.Logger.LogError($"Could not find name {name} in bundle {bundleName}");
            Instance.Logger.LogError("Available names:\n" + string.Join(", ", bundle.GetAllAssetNames().ToArray()));

            return default;
        }

        T loaded = bundle.LoadAsset<T>(objName);
        return loaded;
    }
}
