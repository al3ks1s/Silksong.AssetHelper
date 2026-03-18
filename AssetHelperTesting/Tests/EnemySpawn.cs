using AssetHelperTesting;
using Silksong.AssetHelper.ManagedAssets;
using System;
using UnityEngine;

/// <summary>
/// Test that requests an enemy asset, loads when entering game, spawns when hotkey pressed
/// </summary>
public class EnemySpawn : MonoBehaviour
{
    public KeyCode SpawnHotkey { get; set; }

    public static void Prepare(KeyCode spawnHotkey = KeyCode.H)
    {
        GameObject go = new("Alita Spawner");
        DontDestroyOnLoad(go);
        EnemySpawn component = go.AddComponent<EnemySpawn>();
        component.SpawnHotkey = spawnHotkey;
    }

    private ManagedAsset<GameObject> _asset;

    void Awake()
    {
        _asset = ManagedAsset<GameObject>.FromSceneAsset(
            sceneName: "Memory_Coral_Tower",
            objPath: "Battle Scenes/Battle Scene Chamber 2/Wave 1/Coral Hunter");

        Events.OnHeroStart += () => _asset.Load();
    }

    // Code lifted from https://github.com/cometcake575/Architect-Silksong/blob/main/Behaviour/Fixers/EnemyFixers.cs
    public static void FixAlita(GameObject obj)
    {
        PlayMakerFSM fsm = obj.LocateMyFSM("Control");
        fsm.FsmVariables.FindFsmBool("Spear Spawner").Value = false;

        float ground = obj.transform.GetPositionY();
        fsm.FsmVariables.FindFsmFloat("Tele Air Y Max").Value = ground + 8;
        fsm.FsmVariables.FindFsmFloat("Tele Air Y Min").Value = ground + 2;
        fsm.FsmVariables.FindFsmFloat("Tele Ground Y").Value = ground;

        fsm.FsmVariables.FindFsmFloat("Tele X Max").Value = obj.transform.GetPositionX() + 11;
        fsm.FsmVariables.FindFsmFloat("Tele X Min").Value = obj.transform.GetPositionX() - 11;

        fsm.FsmVariables.FindFsmGameObject("Aiming Cursor").Value = new GameObject(obj.name + " Aim Cursor");
    }

    void Update()
    {
        if (Input.GetKeyDown(SpawnHotkey))
        {
            _asset.EnsureLoaded();
            GameObject alita = _asset.InstantiateAsset();

            alita.transform.position = HeroController.instance.transform.position + new Vector3(3, 0, 0);
            FixAlita(alita);

            alita.SetActive(true);
        }
    }
}
