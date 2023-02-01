using Modding;
using System;
using System.Collections;

namespace InstantTransitions;
public class InstantTransitionsMod : Mod
{
    private static InstantTransitionsMod? _instance;

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

    public InstantTransitionsMod() : base("InstantTransitions")
    {
        _instance = this;
    }

    public override void Initialize()
    {
        Log("Initializing");

        On.GameManager.BeginSceneTransitionRoutine += GameManager_BeginSceneTransitionRoutine;

        Log("Initialized");
    }

    private IEnumerator GameManager_BeginSceneTransitionRoutine(On.GameManager.orig_BeginSceneTransitionRoutine orig, GameManager self, GameManager.SceneLoadInfo info)
    {
        info.WaitForSceneTransitionCameraFade = false;
        info.PreventCameraFadeOut = true;

        IEnumerator original = orig(self, info);
        while (original.MoveNext())
        {
            yield return original.Current;
        }
    }
}
