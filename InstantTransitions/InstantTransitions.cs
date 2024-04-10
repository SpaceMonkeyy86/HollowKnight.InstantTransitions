using Modding;
using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

    private static List<string> ILGetPreloadedScenes()
    {
        return Instance.preloadedScenes;
    }

    private List<ILHook> hooks = [];

    private List<string> preloadedScenes = [];

    internal static InstantTransitionsMod Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException($"An instance of {nameof(InstantTransitionsMod)} was never constructed");
            }
            return _instance;
        }
    }

    public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

    public InstantTransitionsMod() : base("Instant Transitions")
    {
        _instance = this;
    }

    public override void Initialize()
    {
        Log("Begin Initialization");
        
        ModHooks.BeforeSceneLoadHook += BeforeSceneLoadHook;
        
        On.GameManager.OnNextLevelReady += GameManagerOnOnNextLevelReady;
        
        IL.GameManager.FindTransitionPoint += GameManagerOnFindTransitionPoint;
        
        hooks.Add(new ILHook(
            typeof(SceneLoad)
                    .GetNestedType("<BeginRoutine>d__35", BindingFlags.NonPublic)
                    .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance), 
            SceneLoadOnBeginRoutine));

        Log("End Initialization");
    }

    private string BeforeSceneLoadHook(string sceneName)
    {
        foreach (string scene in preloadedScenes)
        {
            if (scene != sceneName) Unload(scene);
        }
        
        return sceneName;
    }

    private void GameManagerOnOnNextLevelReady(On.GameManager.orig_OnNextLevelReady orig, GameManager self)
    {
        orig(self);
        if (self.IsNonGameplayScene()) return;

        preloadedScenes.Remove(UnitySceneManager.GetActiveScene().name);
        
        Preload("Crossroads_15");
    }
    
    private void GameManagerOnFindTransitionPoint(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);
        
        // Replace if (transitionPoint.name == entryPointName && ...) with
        // if (transitionPoint.name == entryPointName && transitionPoint.isActiveAndEnabled && ...)
        ILLabel? skipLabel = null;
        cursor.GotoNext(MoveType.After, i => i.MatchBrfalse(out skipLabel));
        cursor.Emit(OpCodes.Ldloc_2);
        cursor.Emit(OpCodes.Call, typeof(TransitionPoint)
            .GetProperty(nameof(TransitionPoint.isActiveAndEnabled),
                BindingFlags.Public | BindingFlags.Instance)!
            .GetGetMethod());
        cursor.Emit(OpCodes.Brfalse_S, skipLabel);
    }

    private void SceneLoadOnBeginRoutine(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        Type stateMachineType = typeof(SceneLoad)
            .GetNestedType("<BeginRoutine>d__35", BindingFlags.NonPublic);

        // Insert before AsyncOperation loadOperation = UnitySceneManager.LoadSceneAsync(...)
        // if (InstantTransitionsMod.ILGetPreloadedScenes().Contains(targetSceneName))
        // {
        //     loadOperation = null;
        //     goto <RecordEndTime(SceneLoad.Phases.Fetch)>;
        // }
        ILLabel skipFetchLabel;
        ILLabel loadNormallyLabel;
        FieldReference targetSceneNameField = null!;
        FieldReference loadOperationField = null!;
        cursor.GotoNext(MoveType.Before,
            i => i.MatchLdloc(1),
            i => i.MatchLdcI4(1),
            i => i.MatchCallvirt<SceneLoad>("RecordEndTime"));
        skipFetchLabel = cursor.MarkLabel();
        cursor.Goto(0);
        cursor.GotoNext(MoveType.Before,
            i => i.MatchLdarg(0),
            i => i.MatchLdloc(1),
            i => i.MatchLdfld<SceneLoad>("targetSceneName")
                 && i.MatchLdfld(out targetSceneNameField),
            i => i.MatchLdcI4(1),
            i => i.MatchCall<UnitySceneManager>("LoadSceneAsync"),
            i => i.MatchStfld(stateMachineType, "<loadOperation>5__2")
                 && i.MatchStfld(out loadOperationField));
        loadNormallyLabel = cursor.MarkLabel();
        cursor.Emit(OpCodes.Call, typeof(InstantTransitionsMod)
            .GetMethod(nameof(ILGetPreloadedScenes), BindingFlags.NonPublic | BindingFlags.Static));
        cursor.Emit(OpCodes.Ldloc_1);
        cursor.Emit(OpCodes.Ldfld, targetSceneNameField);
        cursor.Emit(OpCodes.Call, typeof(List<string>)
            .GetMethod(nameof(List<string>.Contains), BindingFlags.Public | BindingFlags.Instance));
        cursor.Emit(OpCodes.Brfalse_S, loadNormallyLabel);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldnull);
        cursor.Emit(OpCodes.Stfld, loadOperationField);
        cursor.Emit(OpCodes.Br_S, skipFetchLabel);

        // Replace loadOperation.allowSceneActivation = true with
        // if (loadOperation == null)
        // {
        //     InstantTransitionsMod.Activate(targetSceneName);
        // }
        // else
        // {
        //     loadOperation.allowSceneActivation = true;
        // }
        ILLabel allowActivationLabel;
        ILLabel yieldReturnLabel;
        cursor.GotoNext(MoveType.After, 
            i => i.MatchLdarg(0),
            i => i.MatchLdfld(loadOperationField),
            i => i.MatchLdcI4(1),
            i => i.MatchCallvirt(typeof(AsyncOperation)
                .GetProperty(nameof(AsyncOperation.allowSceneActivation), 
                    BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod()));
        yieldReturnLabel = cursor.MarkLabel();
        cursor.Goto(cursor.Index - 4);
        allowActivationLabel = cursor.MarkLabel();
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldfld, loadOperationField);
        cursor.Emit(OpCodes.Brtrue_S, allowActivationLabel);
        Log(cursor.Prev.Operand);
        cursor.Emit(OpCodes.Ldfld, targetSceneNameField);
        cursor.Emit(OpCodes.Call, typeof(InstantTransitionsMod)
            .GetMethod(nameof(Activate), BindingFlags.Public | BindingFlags.Static));
        cursor.Emit(OpCodes.Br_S, yieldReturnLabel);
    }

    private void Preload(string sceneName)
    {
        if (preloadedScenes.Contains(sceneName)) return;
        if (UnitySceneManager.GetSceneByName(sceneName).isLoaded) return;
        
        preloadedScenes.Add(sceneName);
        
        AsyncOperation? op = UnitySceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null) return;
        op.completed += _ =>
        {
            Deactivate(UnitySceneManager.GetSceneByName(sceneName));
        };
    }

    private void Unload(string sceneName)
    {
        if (!preloadedScenes.Contains(sceneName)) return;
        if (!UnitySceneManager.GetSceneByName(sceneName).isLoaded) return;
        
        AsyncOperation? op = UnitySceneManager.UnloadSceneAsync(sceneName);
        if (op == null) return;
        op.completed += _ =>
        {
            preloadedScenes.Remove(sceneName);
        };
    }

    public static void Activate(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(true);
        }
    }
    
    public static void Deactivate(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(false);
        }
    }
}
