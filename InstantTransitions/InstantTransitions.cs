using Core.FsmUtil;
using Modding;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

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

        ModHooks.ApplicationQuitHook += ModHooks_ApplicationQuitHook;

        UnitySceneManager.activeSceneChanged += UnitySceneManager_activeSceneChanged;

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
        LoadTimePredictions.Update(sceneLoad.TargetSceneName, sceneLoad.GetDuration(SceneLoad.Phases.Fetch)!.Value, LoadTimePredictions.Confidence.VeryConfident);
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
        if (self.targetScene != null && self.targetScene != string.Empty && !self.isADoor)
        {
            self.gameObject.AddComponent<TransitionStretch>();
        }

        // Remove camera fadeout from door FSM
        if (self.isADoor)
        {
            self.gameObject.LocateMyFSM("Door Control").GetState("Enter").RemoveAction(4);
        }

        orig(self);
    }

    private IEnumerator HeroController_EnterScene(On.HeroController.orig_EnterScene orig, HeroController self, TransitionPoint enterGate, float delayBeforeEnter)
    {
        Time.timeScale = 5;

        IEnumerator enumerator = orig(self, enterGate, delayBeforeEnter);
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }

        Time.timeScale = 1;
    }

    private void ModHooks_ApplicationQuitHook()
    {
        LoadTimePredictions.Save();
    }

    private void UnitySceneManager_activeSceneChanged(Scene arg0, Scene arg1)
    {
        GameManager.instance.StartCoroutine(GeneratePredictionsRoutine());

        IEnumerator GeneratePredictionsRoutine()
        {
            foreach (GameObject go in arg1.GetRootGameObjects())
            {
                foreach (TransitionPoint gate in go.GetComponentsInChildren<TransitionPoint>())
                {
                    string target = gate.targetScene;

                    if (target != null && target != string.Empty && LoadTimePredictions.GetConfidence(target) < LoadTimePredictions.Confidence.Confident && !UnitySceneManager.GetSceneByName(target).isLoaded)
                    {
                        float start = Time.realtimeSinceStartup;

                        AsyncOperation operation = UnitySceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
                        operation.allowSceneActivation = false;

                        while (operation.progress < 0.9f)
                        {
                            yield return null;
                        }

                        float elapsed = Time.realtimeSinceStartup - start;

                        LoadTimePredictions.Update(target, elapsed, LoadTimePredictions.Confidence.Confident);

                        operation.allowSceneActivation = true;
                        yield return operation;

                        // Using the async version would cause the scene to appear on the screen for a single frame
#pragma warning disable CS0618 // Type or member is obsolete
                        UnitySceneManager.UnloadScene(target);
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                }
            }
        }
    }
}
