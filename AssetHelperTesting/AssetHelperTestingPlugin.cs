using BepInEx;
using Silksong.AssetHelper.Dev;
using Silksong.AssetHelper.Plugin;

namespace AssetHelperTesting
{
    // TODO - adjust the plugin guid as needed
    [BepInAutoPlugin(id: "org.silksong-modding.assethelpertesting")]
    public partial class AssetHelperTestingPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            EnemySpawn.Prepare();

            // Put your initialization logic here
            Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

            AssetRequestAPI.InvokeAfterBundleCreation(
                () => DebugTools.DumpAllAddressableAssets(AssetRequestAPI.SceneAssetLocator!, "scene_locator.json")
            );
        }
    }
}
