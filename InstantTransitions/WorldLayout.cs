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

    public IEnumerable<string> FindNeighbors(string sceneName)
    {
        if (UnitySceneManager.GetActiveScene().name != sceneName) return [];

        HashSet<string> neighbors = [];

        foreach (TransitionPoint tp in TransitionPoint.TransitionPoints)
        {
            neighbors.Add(tp.targetScene);
        }

        return neighbors;
    }
}
