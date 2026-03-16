using Silksong.AssetHelper.ManagedAssets;
using Silksong.AssetHelper.Plugin;
using UnityEngine;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Test spawning a child game object whose ancestor was requested.
/// </summary>
public class SpawnRequestedChild : MonoBehaviour
{
    public KeyCode SpawnHotkey { get; set; }

    private static ManagedAsset<GameObject> _asset;

    public static void Prepare(KeyCode spawnHotkey = KeyCode.H)
    {
        GameObject go = new("Group Spawner");
        DontDestroyOnLoad(go);
        SpawnRequestedChild component = go.AddComponent<SpawnRequestedChild>();
        component.SpawnHotkey = spawnHotkey;
    }

    void Awake()
    {
        _asset = ManagedAsset<GameObject>.FromSceneAsset(sceneName: "Memory_Coral_Tower", objPath: "Fish/Pt Exit/BG (2)/Pt Fish Shiny (5)");
        AssetRequestAPI.RequestSceneAsset(sceneName: "Memory_Coral_Tower", assetPath: "Fish/Pt Exit");
        Events.OnHeroStart += () => _asset.Load();
    }

    void Update()
    {
        if (!Input.GetKeyDown(SpawnHotkey)) return;

        _asset.EnsureLoaded();

        GameObject spawnedAsset = _asset.InstantiateAsset();
        spawnedAsset.transform.position = HeroController.instance.transform.position + new Vector3(5, 5, 0);
        spawnedAsset.SetActive(true);
    }
}
