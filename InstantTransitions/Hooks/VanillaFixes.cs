using Core.FsmUtil;
using HutongGames.PlayMaker.Actions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions.Hooks;

internal static class VanillaFixes
{
    internal static void Hook()
    {
        On.GameManager.OnNextLevelReady += GameManager_OnNextLevelReady;
        On.PlayMakerFSM.Awake += PlayMakerFSM_Awake;
        On.Climber.Start += Climber_Start;
        IL.GameManager.FindTransitionPoint += GameManager_FindTransitionPoint;
    }

    internal static void Unhook()
    {
        On.GameManager.OnNextLevelReady -= GameManager_OnNextLevelReady;
        On.PlayMakerFSM.Awake -= PlayMakerFSM_Awake;
        On.Climber.Start -= Climber_Start;
        IL.GameManager.FindTransitionPoint -= GameManager_FindTransitionPoint;
    }

    private static void GameManager_OnNextLevelReady(On.GameManager.orig_OnNextLevelReady orig, GameManager self)
    {
        orig(self);
        if (self.IsNonGameplayScene()) return;

        if (UnitySceneManager.GetActiveScene().IsValid())
        {
            foreach (GameObject go in UnitySceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name.Contains("SceneBorder"))
                {
                    go.SetActive(false);
                }
            }
        }
    }

    private static void PlayMakerFSM_Awake(On.PlayMakerFSM.orig_Awake orig, PlayMakerFSM self)
    {
        orig(self);

        if (self.FsmName == "chaser" && self.name.Contains("Buzzer"))
        {
            self.GetState("Initiate")
                .InsertAction(new Wait() { time = 0.2f }, 0);
        }
    }

    private static void Climber_Start(On.Climber.orig_Start orig, Climber self)
    {
        self.StartCoroutine(StartRoutine());
        IEnumerator StartRoutine()
        {
            // TODO: Figure out why this works
            yield return null;
            orig(self);
        }
    }

    private static void GameManager_FindTransitionPoint(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        // Replace if (transitionPoint.name == entryPointName && ...) with
        // if (transitionPoint.name == entryPointName && transitionPoint.isActiveAndEnabled && ...)
        ILLabel? skipLabel = null;
        cursor.GotoNext(MoveType.After, i => i.MatchBrfalse(out skipLabel));
        cursor.Emit(OpCodes.Ldloc_2);
        cursor.EmitDelegate<Func<TransitionPoint, bool>>(tp => tp.isActiveAndEnabled);
        cursor.Emit(OpCodes.Brfalse_S, skipLabel);
    }
}
