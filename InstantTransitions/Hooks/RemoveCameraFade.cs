using Core.FsmUtil;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace InstantTransitions.Hooks;

internal static class RemoveCameraFade
{
    private static readonly List<ILHook> _hooks = [];

    internal static void Hook()
    {
        ModHooks.BeforeSceneLoadHook += BeforeSceneLoad;
        On.PlayMakerFSM.Awake += PlayMakerFSM_Awake;
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
        PlayMakerFSM fsm = GameManager.instance.cameraCtrl.gameObject.LocateMyFSM("CameraFade");

        fsm.GetState("FadingOut")
            .GetAction<SetFsmFloat>(3)
            .setValue = 0f;

        fsm.GetState("FadeIn")
            .GetAction<SetFsmFloat>(1)
            .setValue = 0f;
        fsm.GetState("FadeIn")
            .GetAction<SendEventByName>(2)
            .delay = 0f;
        fsm.GetState("FadeIn")
            .GetAction<Wait>(3)
            .time = 0f;

        return sceneName;
    }

    private static void PlayMakerFSM_Awake(On.PlayMakerFSM.orig_Awake orig, PlayMakerFSM self)
    {
        orig(self);

        if (self.FsmName == "Door Control")
        {
            var state = self.GetState("Enter");
            if (state == null) return;
            if (state.Actions.Length <= 5) return;
            var action = state.GetAction<Wait>(5);
            if (action == null) return;
            action.time = 0f;
        }

        if (self.FsmName == "Blanker Control" && self.name == "Stag Blanker")
        {
            self.GetState("Init")
                .GetAction<Wait>(4)
                .time = 0f;

            self.GetFloatVariable("Fade Time")
                .Value = 0f;
        }
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
        cursor.Emit(OpCodes.Ldc_R4, 0f);
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
