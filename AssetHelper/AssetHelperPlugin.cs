using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Plugin;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
[BepInDependency("io.github.flibber-hk.filteredlogs", BepInDependency.DependencyFlags.SoftDependency)]
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
        new Hook(typeof(Net35Polyfill).GetMethod(nameof(Net35Polyfill.CopyToCompat)), PatchC2C);
        //FilteredLogs.API.ApplyFilter(Name);
        BundleDeps.Setup();

        GameEvents.Hook();

        Addressables.ResourceManager.ResourceProviders.Add(new ChildGameObjectProvider());
        
        // TODO - activate this
        //SceneAssetRepackManager.Hook();

        // TODO - remove this when assetstools.net gets updated
        _atHook = new ILHook(typeof(AssetTypeValueIterator).GetMethod(nameof(AssetTypeValueIterator.ReadNext)), PatchATVI);

        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

        AssetsData.InvokeAfterAddressablesLoaded(TestExecutor.CustomBundle);

        //LoadThingy2();
    }
    private void PatchC2C(Action<Stream, Stream, long, int> orig, Stream input, Stream output, long bytes, int bufferSize)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int read;

        // set to largest value so we always go over buffer (hopefully)
        if (bytes == -1)
            bytes = long.MaxValue;

        // bufferSize will always be an int so if bytes is larger, it's also under the size of an int
        while (bytes > 0 && (read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, bytes))) > 0)
        {
            output.Write(buffer, 0, read);
            bytes -= read;
        }
        ArrayPool<byte>.Shared.Return(buffer);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            StartCoroutine(LoadThingy());
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            TestExecutor.RunArchitectTest();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            TestExecutor.GenFromFile();
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            LoadThingy2();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            TestExecutor.CustomBundle();
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            TestExecutor.TestCatalogSerialization2();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            TestExecutor.CreateFullNonSceneCatalog();
        }
    }

    void LoadThingy2()
    {
        AssetsData.InvokeAfterAddressablesLoaded(() =>
        {
            
            var p = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, "AssetHelper-testCatalog.bin"));
            var w = p.WaitForCompletion();
            DebugTools.DumpAllAddressableAssets(w, "test2.json", true);

            Logger.LogInfo($"");
            var op = Addressables.LoadAssetAsync<GameObject>("AssetHelper/Addressables/Assets/Prefabs/Hornet Enemies/Song Pilgrim 03.prefab");
            op.WaitForCompletion();

            Logger.LogMessage($"Result: {op.Status}");
            Logger.LogMessage($"StackT: {op.OperationException}");
            Logger.LogMessage($"Object: {op.Result}");
            GameObject.Instantiate(op.Result);
        });
    }

    void LoadThingy3()
    {
        AssetsData.InvokeAfterAddressablesLoaded(() =>
        {

            var p = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, "AssetHelper-repackedSceneCatalog.bin"));
            var w = p.WaitForCompletion();
            DebugTools.DumpAllAddressableAssets(w, "test2.json", true);

            foreach (var nonbundlelocation in w.AllLocations.Where(f => f.ResourceType == typeof(GameObject)))
            {
                Logger.LogInfo($"");
                Logger.LogInfo($"{nonbundlelocation.PrimaryKey}");
                Logger.LogInfo($"{nonbundlelocation.InternalId}");
                Logger.LogInfo($"{nonbundlelocation.ProviderId}");
                var op = Addressables.LoadAssetAsync<GameObject>(nonbundlelocation.PrimaryKey);
                op.WaitForCompletion();

                Logger.LogMessage($"Result: {op.Status}");
                Logger.LogMessage($"StackT: {op.OperationException}");
                Logger.LogMessage($"Object: {op.Result}");
            }

        });
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
