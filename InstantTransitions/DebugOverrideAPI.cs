using UnityEngine;
using UnityEngine.SceneManagement;

namespace InstantTransitions;

internal class DebugOverrideAPI : SceneManagerAPI
{
    protected override AsyncOperation LoadSceneAsyncByNameOrIndex(
        string sceneName, int sceneBuildIndex, LoadSceneParameters parameters, bool mustCompleteNextFrame)
    {
        InstantTransitionsMod.Instance.LogDebug($"LoadAsync {sceneName}");
        return base.LoadSceneAsyncByNameOrIndex(sceneName, sceneBuildIndex, parameters, mustCompleteNextFrame);
    }

    protected override AsyncOperation UnloadSceneAsyncByNameOrIndex(string sceneName, int sceneBuildIndex, bool immediately, UnloadSceneOptions options, out bool outSuccess)
    {
        InstantTransitionsMod.Instance.LogDebug($"UnloadAsync {sceneName}");
        return base.UnloadSceneAsyncByNameOrIndex(sceneName, sceneBuildIndex, immediately, options, out outSuccess);
    }
}
