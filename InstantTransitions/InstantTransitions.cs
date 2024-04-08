using Modding;
using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

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
        
        On.GameManager.OnNextLevelReady += GameManagerOnOnNextLevelReady;
        On.HeroController.HeroJump += HeroControllerOnHeroJump;
        
        IL.GameManager.FindTransitionPoint += GameManagerOnFindTransitionPoint;

        Log("End Initialization");
    }

    private void GameManagerOnOnNextLevelReady(On.GameManager.orig_OnNextLevelReady orig, GameManager self)
    {
        orig(self);
        if (self.IsNonGameplayScene()) return;
        
        Preload("Crossroads_15");
    }

    private void HeroControllerOnHeroJump(On.HeroController.orig_HeroJump orig, HeroController self)
    {
        orig(self);
        
        // Preload("Crossroads_15");
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
                BindingFlags.Public | BindingFlags.Instance)
            !.GetGetMethod());
        cursor.Emit(OpCodes.Brfalse_S, skipLabel);
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
        UnitySceneManager.UnloadSceneAsync(sceneName);
    }

    private static void Activate(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(true);
        }
    }
    
    private static void Deactivate(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(false);
        }
    }
}
