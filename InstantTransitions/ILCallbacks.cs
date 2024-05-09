using System;
using System.Text;

namespace InstantTransitions;

internal static class ILCallbacks
{
    internal static bool IsTargetPreloaded(string targetSceneName)
    {
        return Preloader.Instance.PreloadedScenes.Contains(targetSceneName);
    }

    internal static void ActivateByName(string targetSceneName)
    {
        Preloader.Instance.Activate(targetSceneName);
    }

    internal static void LogSceneLoadInfo(SceneLoad load)
    {
#if DEBUG
        StringBuilder builder = new();
        builder.Append($"Finished loading {load.TargetSceneName}\n");
        for (int i = 0; i < SceneLoad.PhaseCount; i++)
        {
            SceneLoad.Phases phase = (SceneLoad.Phases)i;
            string name = Enum.GetName(typeof(SceneLoad.Phases), phase) ?? "Unknown phase";
            builder.Append($"{name}: {load.GetDuration(phase)}\n");
        }
        InstantTransitionsMod.Instance.LogDebug(builder.ToString());
#endif
    }

    internal static bool ParentSceneIsActive(SceneAdditiveLoadConditional loadConditional)
    {
        return GameManager.instance.nextSceneName == loadConditional.gameObject.scene.name;
    }
}
