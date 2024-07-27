using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;

public class Preloader
{
    private static Preloader? _instance;
    public static Preloader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new Preloader();
            }
            return _instance;
        }
    }

    public static void Dispose() => _instance = null;

    public HashSet<string> PreloadedScenes { get; } = [];

    public void Preload(string sceneName)
    {
        if (PreloadedScenes.Contains(sceneName)) return;

        Scene scene = UnitySceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded) return;

        PreloadedScenes.Add(sceneName);

        AsyncOperation? operation = UnitySceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (operation == null) return;

        operation.completed += op =>
        {
            DeactivateScene(UnitySceneManager.GetSceneByName(sceneName));
        };
    }

    public void Activate(string sceneName)
    {
        if (!PreloadedScenes.Contains(sceneName)) return;

        Scene scene = UnitySceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded) return;

        ActivateScene(scene);
    }

    public void Unload(string sceneName)
    {
        if (!PreloadedScenes.Contains(sceneName)) return;

        Scene scene = UnitySceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded) return;

        InstantTransitionsMod.Instance.LogDebug($"Unload({sceneName}) active scene {UnitySceneManager.GetActiveScene().name} stack trace: {System.Environment.StackTrace}");

        AsyncOperation? operation = UnitySceneManager.UnloadSceneAsync(sceneName);
        if (operation == null) return;

        operation.completed += op =>
        {
            PreloadedScenes.Remove(sceneName);
        };
    }

    public void Reset()
    {
        foreach (string scene in PreloadedScenes)
        {
            Unload(scene);
        }

        PreloadedScenes.Clear();
    }

    private void ActivateScene(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(true);

            foreach (PlayMakerFSM fsm in go.GetComponentsInChildren<PlayMakerFSM>())
            {
                fsm.SetState(fsm.Fsm.StartState);
            }
        }
    }

    private void DeactivateScene(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            go.SetActive(false);
        }
    }
}