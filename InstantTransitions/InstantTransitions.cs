using Modding;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

    public Dictionary<string, AsyncOperation> LoadingScenes { get; } = new();
    public Dictionary<string, AsyncOperation> UnloadingScenes { get; } = new();

    public float LoadCooldown { get; set; }

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

        On.GameManager.BeginSceneTransitionRoutine += GameManager_BeginSceneTransitionRoutine;
        On.TransitionPoint.Awake += TransitionPoint_Awake;
        On.SceneLoad.Begin += SceneLoad_Begin;

        ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;

        ILHooks = new()
        {
            /*
             * IEnumerator GameManager::BeginSceneTransitionRoutine(...)
             * {
             *     ...
             *     sceneLoad.ActivationComplete += () =>
             *     {
             *         // Hooks into this function
             *     }
             *     ...
             * }
             */
            new ILHook(typeof(GameManager).GetNestedType("<>c__DisplayClass174_0", BindingFlags.NonPublic)
                .GetMethod("<BeginSceneTransitionRoutine>b__2", BindingFlags.NonPublic | BindingFlags.Instance),
                GameManager_BeginSceneTransitionRoutine_ActivationCompletedAnonymous)
        };

        Log("Initialized");
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
        void SetupTrigger(bool unload)
        {
            GameObject preloadTrigger = new($"Preload Trigger ({(unload ? "Unload" : "Load")})");
            preloadTrigger.layer = 13; // Hero Detector
            preloadTrigger.transform.parent = self.gameObject.transform;
            preloadTrigger.transform.localPosition = Vector3.zero;

            TransitionPreload tp = preloadTrigger.AddComponent<TransitionPreload>();
            tp.Unload = unload;
        }

        if (self.targetScene != null && self.targetScene != "")
        {
            SetupTrigger(false);
            SetupTrigger(true);
        }

        orig(self);
    }

    private void SceneLoad_Begin(On.SceneLoad.orig_Begin orig, SceneLoad self)
    {
        // Heavily stripped-down version of SceneLoad::BeginCoroutine()
        IEnumerator Coroutine(SceneLoad self, MonoBehaviour runner)
        {
            void InvokeEvent(string name)
            {
                ((MulticastDelegate)typeof(SceneLoad).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self))?.DynamicInvoke();
            }

            Log($"Entering {self.TargetSceneName}");

            LoadCooldown = 1f;

            SceneAdditiveLoadConditional.loadInSequence = true;
            yield return runner.StartCoroutine(ScenePreloader.FinishPendingOperations());

            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(self.TargetSceneName);

            if (scene.isLoaded)
            {
                Log($"Scene {self.TargetSceneName} already loaded");

                InvokeEvent(nameof(SceneLoad.FetchComplete));
                InvokeEvent(nameof(SceneLoad.WillActivate));
            }
            else
            {
                AsyncOperation operation;

                if (LoadingScenes.TryGetValue(self.TargetSceneName, out AsyncOperation op))
                {
                    Log($"Scene {self.TargetSceneName} successfully preloaded");
                    LoadingScenes.Remove(self.TargetSceneName);
                    operation = op;
                }
                else
                {
                    Log($"Scene {self.TargetSceneName} did not preload");
                    operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(self.TargetSceneName);
                }

                while (operation.progress < 0.9f)
                {
                    yield return null;
                }

                InvokeEvent(nameof(SceneLoad.FetchComplete));
                InvokeEvent(nameof(SceneLoad.WillActivate));

                operation.allowSceneActivation = true;
                yield return operation;

                scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(self.TargetSceneName);
            }

            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                go.SetActive(false);
            }

            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                go.SetActive(true);
            }

            InvokeEvent(nameof(SceneLoad.ActivationComplete));

            if (self.IsUnloadAssetsRequired) yield return Resources.UnloadUnusedAssets();
            if (self.IsGarbageCollectRequired) GCManager.Collect();

            InvokeEvent(nameof(SceneLoad.Complete));
            yield return null;
            InvokeEvent(nameof(SceneLoad.StartCalled));

            if (SceneAdditiveLoadConditional.ShouldLoadBoss)
            {
                yield return runner.StartCoroutine(SceneAdditiveLoadConditional.LoadAll());
                InvokeEvent(nameof(SceneLoad.BossLoaded));
                GameManager.instance?.LoadedBoss();
            }

            ScenePreloader.Cleanup();
            InvokeEvent(nameof(SceneLoad.Finish));

            Log($"Finished entering {self.TargetSceneName}");
        }

        if (LoadCooldown > 0) return;

        MonoBehaviour monoBehaviour = (MonoBehaviour)typeof(SceneLoad).GetField("runner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);

        monoBehaviour.StartCoroutine(Coroutine(self, monoBehaviour));
    }

    private void GameManager_BeginSceneTransitionRoutine_ActivationCompletedAnonymous(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);
        cursor.RemoveRange(4);
    }

    private void ModHooks_HeroUpdateHook()
    {
        LoadCooldown -= Time.deltaTime;
        if (LoadCooldown < 0) LoadCooldown = 0;
    }

    private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
    {
        Log($"Scene changed from {arg0.name} to {arg1.name}");
    }
}
