using AssetsTools.NET;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Plugin;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    public static AssetHelperPlugin Instance { get; private set; }
    #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    internal static ManualLogSource InstanceLogger { get; private set; }

    private static readonly Dictionary<string, string> Keys = [];

    private ILHook _atHook;

    private void Awake()
    {
        Instance = this;
        InstanceLogger = this.Logger;
        
        BundleDeps.Setup();

        GameEvents.Hook();

        Addressables.ResourceManager.ResourceProviders.Add(new ChildGameObjectProvider());

        // TODO - activate this
        // SceneAssetRepackManager.Hook();

        // TODO - remove this when assetstools.net gets updated
        _atHook = new ILHook(typeof(AssetTypeValueIterator).GetMethod(nameof(AssetTypeValueIterator.ReadNext)), PatchATVI);

        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

        // AssetsData.InvokeAfterAddressablesLoaded(TestExecutor.CustomBundle);
        AssetsData.InvokeAfterAddressablesLoaded(() => {
            var p = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, "AssetHelper-repackedSceneCatalog.bin"));
            var w = p.WaitForCompletion();
            DebugTools.DumpAllAddressableAssets(w, "test2.json", true);
            });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            StartCoroutine(LoadThingy());
        }
    }

    IEnumerator LoadThingy()
    {
        string pkey = "AssetHelper/RepackedAssets/memory_coral_tower/Battle Scenes[Battle Scene Chamber 2/Wave 1/Coral Hunter]";
        var locn = Addressables.ResourceLocators.Skip(2).First().AllLocations.Last();
        Logger.LogInfo($"{locn.PrimaryKey} | {locn.ProviderId} | {locn.InternalId}");
        var op = Addressables.LoadAssetAsync<GameObject>(locn);

        yield return op;

        Logger.LogInfo(op.OperationException);
        Logger.LogInfo(op.Result.name);

        GameObject alita = Instantiate(op.Result);

        var fsm = alita.LocateMyFSM("Control");
        fsm.FsmVariables.FindFsmBool("Spear Spawner").Value = false;

        var ground = alita.transform.GetPositionY();
        fsm.FsmVariables.FindFsmFloat("Tele Air Y Max").Value = ground + 8;
        fsm.FsmVariables.FindFsmFloat("Tele Air Y Min").Value = ground + 2;
        fsm.FsmVariables.FindFsmFloat("Tele Ground Y").Value = ground;

        fsm.FsmVariables.FindFsmFloat("Tele X Max").Value = alita.transform.GetPositionX() + 11;
        fsm.FsmVariables.FindFsmFloat("Tele X Min").Value = alita.transform.GetPositionX() - 11;

        fsm.FsmVariables.FindFsmGameObject("Aiming Cursor").Value = new GameObject(alita.name + " Aim Cursor");

        alita.transform.position = HeroController.instance.transform.position + new Vector3(5, 0, 0);

        alita.SetActive(true);
    }

    /// <summary>
    /// Fixes a bug with AssetTypeValueIterator where it moves 4 bytes forward when reading a double rather than 8
    /// </summary>
    private void PatchATVI(ILContext il)
    {
        ILCursor cursor = new(il);

        if (!cursor.TryGotoNext(MoveType.After,
            i => i.MatchCallvirt<AssetTypeTemplateField>($"get_{nameof(AssetTypeTemplateField.ValueType)}"),
            i => i.MatchStloc(out _),
            i => i.MatchLdloc(out _),
            i => i.MatchLdcI4(out _),
            i => i.MatchSub(),
            i => i.MatchSwitch(out _)
            ))
        {
            return;
        }

        ILLabel[] switchArgs = (ILLabel[])cursor.Prev.Operand;
        switchArgs[(int)AssetValueType.Double - 1] = switchArgs[(int)AssetValueType.Int64 - 1];
    }

    private IEnumerator Start()
    {
        SceneAssetAPI.RequestApiAvailable = false;

        // Addressables isn't initialized until the next frame
        yield return null;

        while (true)
        {
            // Check this just in case
            bool b = AssetsData.TryLoadBundleKeys();
            if (b)
            {
                break;
            }

            yield return null;
        }
    }

    private void OnApplicationQuit()
    {
        GameEvents.AfterQuitApplication();
    }
}
