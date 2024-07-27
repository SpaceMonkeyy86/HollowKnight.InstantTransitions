using Core.FsmUtil;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;

public class WorldLayout
{
    private static WorldLayout? _instance;
    public static WorldLayout Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new WorldLayout();
            }
            return _instance;
        }
    }

    public static void Dispose() => _instance = null;

    public List<string> FindNeighbors(string sceneName)
    {
        string name = GameManager.instance.sceneName;
        if (name != sceneName) return [];

        Scene scene = UnitySceneManager.GetSceneByName(name);
        if (!scene.IsValid()) return [];

        InstantTransitionsMod.Instance.LogDebug($"Finding neighbors for {scene.name}");

        HashSet<string> neighbors = [];

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            CheckGameObject(go, neighbors);
        }

        // TODO: Add support for room rando

        return neighbors.ToList();
    }

    private void CheckGameObject(GameObject go, HashSet<string> neighbors)
    {
        if (!go.activeSelf) return;

        if (go.LocateMyFSM("Door Control") is PlayMakerFSM fsm)
        {
            neighbors.Add(fsm.GetStringVariable("New Scene").Value);
        }
        else if (go.GetComponent<TransitionPoint>() is TransitionPoint tp &&
            !string.IsNullOrEmpty(tp.targetScene) && !string.IsNullOrEmpty(tp.entryPoint))
        {
            neighbors.Add(tp.targetScene);
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            CheckGameObject(go.transform.GetChild(i).gameObject, neighbors);
        }
    }
}
