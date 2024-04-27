using Modding;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions.Hooks;

internal static class SceneLoadLogic
{
    private static readonly List<ILHook> _hooks = [];

    internal static void Hook()
    {
        ModHooks.BeforeSceneLoadHook += ModHooks_BeforeSceneLoadHook;
        ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
        On.GameManager.OnNextLevelReady += GameManager_OnNextLevelReady;
        _hooks.Add(new ILHook(
            typeof(SceneLoad)
                .GetNestedType("<BeginRoutine>d__35", BindingFlags.NonPublic)
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
            SceneLoad_BeginRoutine));
    }

    internal static void Unhook()
    {
        ModHooks.BeforeSceneLoadHook -= ModHooks_BeforeSceneLoadHook;
        ModHooks.SavegameLoadHook -= ModHooks_SavegameLoadHook;
        On.GameManager.OnNextLevelReady -= GameManager_OnNextLevelReady;
        _hooks.Clear();
    }

    private static string ModHooks_BeforeSceneLoadHook(string sceneName)
    {
        foreach (string scene in Preloader.Instance.PreloadedScenes)
        {
            if (scene != sceneName) Preloader.Instance.Unload(scene);
        }

        return sceneName;
    }

    private static void ModHooks_SavegameLoadHook(int slot)
    {
        Preloader.Instance.Reset();
    }

    private static void GameManager_OnNextLevelReady(On.GameManager.orig_OnNextLevelReady orig, GameManager self)
    {
        orig(self);
        if (self.IsNonGameplayScene()) return;

        string name = UnitySceneManager.GetActiveScene().name;
        Preloader.Instance.PreloadedScenes.Remove(name);

        foreach (string scene in WorldLayout.Instance.FindNeighbors(name))
        {
            Preloader.Instance.Preload(scene);
        }
    }

    private static void SceneLoad_BeginRoutine(ILContext il)
    {
        ILCursor cursor = new ILCursor(il).Goto(0);

        Type stateMachineType = typeof(SceneLoad)
            .GetNestedType("<BeginRoutine>d__35", BindingFlags.NonPublic);
        FieldInfo stateField = stateMachineType
            .GetField("<>1__state", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo currentField = stateMachineType
            .GetField("<>2__current", BindingFlags.NonPublic | BindingFlags.Instance);

        // Insert before AsyncOperation loadOperation = UnitySceneManager.LoadSceneAsync(...)
        // if (InstantTransitionsMod.ILIsTargetPreloaded(targetSceneName))
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
            i => i.MatchLdcI4((int)SceneLoad.Phases.Fetch),
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
        cursor.Goto(cursor.Index);
        cursor.Emit(OpCodes.Ldloc_1);
        cursor.Emit(OpCodes.Ldfld, targetSceneNameField);
        cursor.EmitDelegate(ILCallbacks.IsTargetPreloaded);
        cursor.Emit(OpCodes.Brfalse_S, loadNormallyLabel);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldnull);
        cursor.Emit(OpCodes.Stfld, loadOperationField);
        cursor.Emit(OpCodes.Br_S, skipFetchLabel);

        // Replace loadOperation.allowSceneActivation = true with
        // if (loadOperation == null)
        // {
        //     InstantTransitionsMod.ILActivateByName(targetSceneName);
        // }
        // else
        // {
        //     loadOperation.allowSceneActivation = true;
        // }
        ILLabel allowActivationLabel;
        ILLabel yieldReturnLabel;
        ILLabel nullCheckLabel;
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
        cursor.Goto(cursor.Index); // makes allowActivationLabel stay on the instruction it's currently on
        nullCheckLabel = cursor.MarkLabel();
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldfld, loadOperationField);
        cursor.Emit(OpCodes.Brtrue_S, allowActivationLabel);
        cursor.Emit(OpCodes.Ldloc_1);
        cursor.Emit(OpCodes.Ldfld, targetSceneNameField);
        cursor.EmitDelegate(ILCallbacks.ActivateByName);
        cursor.Emit(OpCodes.Br_S, yieldReturnLabel);
        for (int j = 0; j < 3; j++)
        {
            ILLabel? label = null;
            cursor.GotoPrev(MoveType.Before,
                i => i.MatchLeaveS(out label) || i.MatchBrfalse(out label));
            label!.Target = nullCheckLabel.Target;
        }

        cursor.GotoNext(MoveType.Before,
            i => i.MatchStfld(currentField),
            i => i.MatchLdarg(0),
            i => i.MatchLdcI4(8),
            i => i.MatchStfld(stateField));
        cursor.Emit(OpCodes.Pop);
        cursor.Emit(OpCodes.Ldnull);

        cursor.GotoNext(MoveType.After,
            i => i.MatchCallvirt(typeof(SceneLoad)
                .GetMethod("set_IsFinished", BindingFlags.NonPublic | BindingFlags.Instance)));
        cursor.Emit(OpCodes.Ldloc_1);
        cursor.EmitDelegate(ILCallbacks.LogSceneLoadInfo);
    }
}
