using AssetHelperTesting.Tests;
using BepInEx;
using BepInEx.Logging;
using Silksong.AssetHelper.Dev;
using Silksong.AssetHelper.Plugin;
using UnityEngine;

namespace AssetHelperTesting
{
    // TODO - adjust the plugin guid as needed
    [BepInAutoPlugin(id: "org.silksong-modding.assethelpertesting")]
    public partial class AssetHelperTestingPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource InstanceLogger { get; private set; }

        private void Awake()
        {
            InstanceLogger = Logger;

            PrepareTests();

            AssetRequestAPI.InvokeAfterBundleCreation(
                () => DebugTools.DumpAllAddressableAssets(AssetRequestAPI.SceneAssetLocator!, "scene_locator.json")
            );

            Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
        }

        // Contributors should freely modify this method 
        private void PrepareTests()
        {
            SquirrmTest.Prepare();
        }
    }
}
