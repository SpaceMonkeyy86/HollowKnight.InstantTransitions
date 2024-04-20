using Core.FsmUtil;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace InstantTransitions.Hooks;

internal static class RemoveCameraFade
{
    private static readonly List<ILHook> _hooks = [];

    internal static void Hook()
    {
        ModHooks.BeforeSceneLoadHook += BeforeSceneLoad;
        _hooks.Add(new ILHook(
            typeof(GameManager)
                .GetNestedType("<BeginSceneTransitionRoutine>d__174", BindingFlags.NonPublic)
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
            GameManager_BeginSceneTransitionRoutine));
        _hooks.Add(new ILHook(
            typeof(HeroController)
                .GetNestedType("<EnterScene>d__469", BindingFlags.NonPublic)
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
            HeroController_EnterScene));
    }

    internal static void Unhook()
    {
        ModHooks.BeforeSceneLoadHook -= BeforeSceneLoad;
        _hooks.Clear();
    }

    private static string BeforeSceneLoad(string sceneName)
    {
        if (InstantTransitionsMod.Instance.Settings.RemoveCameraFade)
        {
            PlayMakerFSM fsm = GameManager.instance.cameraCtrl.gameObject.LocateMyFSM("CameraFade");

            fsm.GetState("FadingOut")
                .GetAction<SetFsmFloat>(3)
                .setValue = 0f;

            fsm.GetState("FadeIn")
                .GetAction<SetFsmFloat>(1)
                .setValue = 0f;
            fsm.GetState("FadeIn")
                .GetAction<Wait>(3)
                .time = 0.1f;

            PlayMakerFSM blanker = fsm.GetGameObjectVariable("Blanker").Value.LocateMyFSM("Blanker Control");

            blanker.GetColorVariable("Start Colour").Value = Color.white;
            blanker.GetColorVariable("End Colour").Value = Color.white;

            FsmOwnerDefault owner = blanker.GetState("Fade In")
                .GetAction<SetSpriteRenderer>(3)
                .gameObject;
            SetSpriteRendererSprite action = new()
            {
                gameObject = owner,
                sprite = new Sprite()
            };
            blanker.GetState("Fade In")
                .AddAction(action);
        }

        return sceneName;
    }

    private static void GameManager_BeginSceneTransitionRoutine(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        Type stateMachineType = typeof(GameManager)
                .GetNestedType("<BeginSceneTransitionRoutine>d__174", BindingFlags.NonPublic);

        cursor.GotoNext(MoveType.Before,
            i => i.MatchLdarg(0),
            i => i.MatchLdcR4(0.5f),
            i => i.MatchStfld(stateMachineType, "<cameraFadeTimer>5__2"));
        cursor.GotoNext(MoveType.Before, i => i.MatchLdcR4(out _));
        cursor.Remove();
        cursor.EmitDelegate(ILCallbacks.FadeTimer);
    }

    private static void HeroController_EnterScene(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        while (cursor.TryGotoNext(i => i.MatchLdcR4(out float val) && (val == 0.165f || val == 0.4f)))
        {
            cursor.Next.Operand = 0f;
        }
    }
}
