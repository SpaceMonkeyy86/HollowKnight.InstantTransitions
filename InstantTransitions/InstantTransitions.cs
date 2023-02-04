using Modding;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

    public bool LoadCooldown { get; set; }

    private List<ILHook>? ILHooks { get; set; }

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
        Log("Initializing");

        LoadTimePredictions.Load();

        ILHooks = new()
        {
            // MoveNext() for HeroController::EnterScene()
            new ILHook(typeof(HeroController).GetNestedType("<EnterScene>d__469", BindingFlags.NonPublic)
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
                HeroController_EnterScene_StateMachineMoveNext),

            // MoveNext() for SceneLoad::BeginRoutine()
            new ILHook(typeof(SceneLoad).GetNestedType("<BeginRoutine>d__35", BindingFlags.NonPublic)
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
                SceneLoad_BeginRoutine_StateMachineMoveNext)
        };

        On.GameManager.BeginSceneTransitionRoutine += GameManager_BeginSceneTransitionRoutine;
        On.TransitionPoint.Awake += TransitionPoint_Awake;
        On.HeroController.EnterScene += HeroController_EnterScene;
        On.TransitionPoint.OnTriggerEnter2D += TransitionPoint_OnTriggerEnter2D;

        ModHooks.ApplicationQuitHook += ModHooks_ApplicationQuitHook;

        Log("Initialized");
    }

    private void HeroController_EnterScene_StateMachineMoveNext(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        while (cursor.TryGotoNext(
            i => i.MatchLdloc(1),
            i => i.MatchLdfld<HeroController>("gm"),
            i => i.MatchCallvirt<GameManager>(nameof(GameManager.FadeSceneIn))))
        {
            cursor.RemoveRange(3);
        }
    }

    private void SceneLoad_BeginRoutine_StateMachineMoveNext(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        cursor.TryGotoNext(
            i => i.MatchLdloc(1),
            i => i.MatchLdcI4(1),
            i => i.MatchCallvirt<SceneLoad>("RecordEndTime"));
        for (int i = 0; i < 3; i++) cursor.GotoNext();
        cursor.Emit(OpCodes.Ldloc_1);
        cursor.Emit(OpCodes.Call, typeof(InstantTransitionsMod).GetMethod(nameof(SceneLoad_BeginRoutine_StateMachineMoveNext_Injected),
            BindingFlags.NonPublic | BindingFlags.Static));
    }

    private static void SceneLoad_BeginRoutine_StateMachineMoveNext_Injected(SceneLoad sceneLoad)
    {
        LoadTimePredictions.LoadTimes[sceneLoad.TargetSceneName] = sceneLoad.GetDuration(SceneLoad.Phases.Fetch)!.Value;
    }

    private IEnumerator GameManager_BeginSceneTransitionRoutine(On.GameManager.orig_BeginSceneTransitionRoutine orig, GameManager self, GameManager.SceneLoadInfo info)
    {
        info.WaitForSceneTransitionCameraFade = false;
        info.PreventCameraFadeOut = true;
        info.forceWaitFetch = false;

        IEnumerator original = orig(self, info);
        while (original.MoveNext())
        {
            yield return original.Current;
        }
    }

    private void TransitionPoint_Awake(On.TransitionPoint.orig_Awake orig, TransitionPoint self)
    {
        if (self.targetScene != null && self.targetScene != "" && !self.isADoor)
        {
            self.gameObject.AddComponent<TransitionStretch>();
        }

        orig(self);
    }

    private IEnumerator HeroController_EnterScene(On.HeroController.orig_EnterScene orig, HeroController self, TransitionPoint enterGate, float delayBeforeEnter)
    {
        Time.timeScale = 3;

        LoadCooldown = true;
        List<WaitForSeconds> wait = new();

        IEnumerator enumerator = orig(self, enterGate, delayBeforeEnter);

        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            /*
            if (item is WaitForSeconds w)
            {
                wait.Add(w);

                yield return null;
            }
            else
            {
                yield return item;
            }
            */
            yield return item;
        }

        Time.timeScale = 1;
        LoadCooldown = false;

        // GameManager.instance.StartCoroutine(WaitCoroutine());

        IEnumerator WaitCoroutine()
        {
            foreach (WaitForSeconds w in wait)
            {
                yield return w;
            }

            LoadCooldown = false;
        }
    }

    private void TransitionPoint_OnTriggerEnter2D(On.TransitionPoint.orig_OnTriggerEnter2D orig, TransitionPoint self, Collider2D movingObj)
    {
        if (LoadCooldown) return;

        orig(self, movingObj);
    }

    private void ModHooks_ApplicationQuitHook()
    {
        LoadTimePredictions.Save();
    }
}
