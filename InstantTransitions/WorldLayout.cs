using System.Collections.Generic;
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

    public IEnumerable<string> FindNeighbors(string sceneName)
    {
        if (UnitySceneManager.GetActiveScene().name != sceneName) return [];

        HashSet<string> neighbors = [];

        foreach (TransitionPoint tp in TransitionPoint.TransitionPoints)
        {
            if (!tp.isActiveAndEnabled) continue;
            if (string.IsNullOrEmpty(tp.targetScene) || string.IsNullOrEmpty(tp.entryPoint)) continue;
            neighbors.Add(tp.targetScene);
        }

        return neighbors;
    }
}
