using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.AssetHelper.ManagedAssets;
using Silksong.FsmUtil;
using UnityEngine;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Test spawning an asset which depends on an ancestor.
/// </summary>
public class DependentParentTest : MonoBehaviour
{
    public KeyCode SpawnHotkey { get; set; }

    public static void Prepare(KeyCode spawnHotkey = KeyCode.H)
    {
        GameObject go = new("MossMother Spawner");
        DontDestroyOnLoad(go);
        DependentParentTest component = go.AddComponent<DependentParentTest>();
        component.SpawnHotkey = spawnHotkey;
    }

    private ManagedAsset<GameObject> _asset;

    void Awake()
    {
        _asset = ManagedAsset<GameObject>.FromSceneAsset(
            sceneName: "Tut_03",
            objPath: "Black Thread States/Normal World/Battle Scene/Wave 1/Mossbone Mother");

        Md.HeroController.Start.Postfix(DoLoad);
    }

    private void DoLoad(HeroController self)
    {
        _asset.Load();
    }

    // Code lifted from https://github.com/cometcake575/Architect-Silksong/blob/main/Behaviour/Fixers/EnemyFixers.cs
    public static void FixMossMother(GameObject obj)
    {
        PlayMakerFSM fsm = obj.LocateMyFSM("Control");

        // Variables to align
        FsmVariables vars = fsm.FsmVariables;
        FsmFloat centreX = vars.FindFsmFloat("Centre X");
        FsmFloat leftX = vars.FindFsmFloat("Left X");
        FsmFloat rightX = vars.FindFsmFloat("Right X");
        FsmFloat swoopY = vars.FindFsmFloat("Swoop Height");
        FsmFloat maxY = vars.FindFsmFloat("Max Height");

        // Align based on self
        fsm.GetState("Init")!.InsertMethod(() => Realign(obj.transform.position), 0);

        // Wake
        obj.GetComponent<MeshRenderer>().enabled = true;
        obj.transform.GetChild(0).gameObject.SetActive(false);
        fsm.GetState("Dormant")!.InsertMethod(() =>
        {
            if (obj.GetComponent<HealthManager>().isDead) return;
            fsm.SetState("Roar");
        }, 0);

        // Disable stun
        fsm.GetState("Roar")!.GetAction<StartRoarEmitter>(9)!.stunHero = false;

        // Align based on player
        fsm.GetState("Idle")!.InsertMethod(() => Realign(HeroController.instance.transform.position), 0);

        // Fix stuck
        fsm.GetState("Slam RePos")!.InsertMethod(() => fsm.SendEvent("FINISHED"), 0);

        // Swoop follow alignment
        FsmState swoop = fsm.GetState("Swoop")!;
        swoop.DisableAction(1);
        swoop.DisableAction(2);
        swoop.DisableAction(3);

        // Disable music
        FsmState roarEnd = fsm.GetState("Roar End")!;
        roarEnd.DisableAction(3);
        roarEnd.DisableAction(4);

        // Disable death bool
        fsm.GetState("End")!.DisableAction(4);

        return;

        void Realign(Vector2 source)
        {
            centreX.value = source.x;
            leftX.value = source.x - 10.5f;
            rightX.value = source.x + 10.5f;
            swoopY.value = source.y;
            maxY.value = source.y + 6;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(SpawnHotkey))
        {
            _asset.EnsureLoaded();
            GameObject mossMom = _asset.InstantiateAsset();

            mossMom.transform.position = HeroController.instance.transform.position + new Vector3(3, 3, 0);
            FixMossMother(mossMom);

            mossMom.SetActive(true);
        }
    }
}
