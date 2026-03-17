using Silksong.AssetHelper.ManagedAssets;
using UnityEngine;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Loading and instantiating this asset causes concerning warning logs to be emitted - this test
/// is here to support investigation into this issue.
/// </summary>
internal class SquirrmTest : MonoBehaviour
{
    public KeyCode LoadHotkey { get; set; }
    public KeyCode InstantiateHotkey { get; set; }

    public static void Prepare(KeyCode loadHotkey = KeyCode.G, KeyCode instantiateHotkey = KeyCode.H)
    {
        GameObject go = new("Squirrm loader");
        DontDestroyOnLoad(go);
        SquirrmTest component = go.AddComponent<SquirrmTest>();
        component.LoadHotkey = loadHotkey;
        component.InstantiateHotkey = instantiateHotkey;
    }

    private ManagedAsset<GameObject> _asset;

    void Awake()
    {
        _asset = ManagedAsset<GameObject>.FromSceneAsset(
            sceneName: "Coral_36",
            objPath: "Judge Child (1)");

        Events.OnHeroStart += () => _asset.Load();
    }

    void Update()
    {
        if (Input.GetKeyDown(LoadHotkey))
        {
            _asset.Load();
            _asset.Handle.Completed += _ => AssetHelperTestingPlugin.InstanceLogger.LogInfo($"Squirrm loaded");
        }

        if (Input.GetKeyDown(InstantiateHotkey))
        {
            GameObject go = _asset.InstantiateAsset();
        }
    }
}
