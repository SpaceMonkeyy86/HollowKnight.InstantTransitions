using Modding.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InstantTransitions;

public class TransitionPreload : MonoBehaviour
{
    // Have a second, larger collider that unloads the scene when exited. This prevents breaking things by quickly
    // entering and exiting the collider's radius.
    public bool Unload { get; set; }

    private float Margin => Unload ? 15f : 10f;

    private BoxCollider2D? collider;

    public void Awake()
    {
        collider = gameObject.GetOrAddComponent<BoxCollider2D>();
        collider.size = transform.parent.GetComponent<BoxCollider2D>().size
            + new Vector2(Margin * 2 / transform.parent.GetScaleX(), Margin * 2 / transform.parent.GetScaleY());
        collider.isTrigger = true;
    }

    public void OnTriggerEnter2D(Collider2D c)
    {
        void StartLoad(string target)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByName(target).isLoaded)
            {
                InstantTransitionsMod.Instance.Log($"Scene {target} is already loaded, skipping preload");
                return;
            }

            InstantTransitionsMod.Instance.Log($"Preloading {target}");

            InstantTransitionsMod.Instance.UnloadingScenes.Remove(target);

            AsyncOperation result = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
            InstantTransitionsMod.Instance.LoadingScenes.Add(target, result);
            result.allowSceneActivation = false;
            result.completed += (op) =>
            {
                InstantTransitionsMod.Instance.LoadingScenes.Remove(target);
            };
        }

        if (Unload || c.gameObject.tag != "Player" || InstantTransitionsMod.Instance.LoadCooldown > 0) return;

        InstantTransitionsMod.Instance.Log($"Entered load trigger {gameObject.scene.name}:{transform.parent.gameObject.name}");

        string target = transform.parent.GetComponent<TransitionPoint>().targetScene;

        if (InstantTransitionsMod.Instance.UnloadingScenes.TryGetValue(target, out AsyncOperation result) && !result.isDone)
        {
            result.completed += _ =>
            {
                InstantTransitionsMod.Instance.Log($"Queueing preload for {target}");
                StartLoad(target);
            };
        }
        else
        {
            StartLoad(target);
        }
    }

    public void OnTriggerExit2D(Collider2D c)
    {
        void StartUnload(string target)
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(target).isLoaded) return;

            InstantTransitionsMod.Instance.Log($"Unloading {target}");

            InstantTransitionsMod.Instance.LoadingScenes.Remove(target);

            AsyncOperation result = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(target);
            InstantTransitionsMod.Instance.UnloadingScenes.Add(target, result);
            result.completed += (op) =>
            {
                InstantTransitionsMod.Instance.UnloadingScenes.Remove(target);
            };
        }

        if (!Unload || c.gameObject.tag != "Player" || InstantTransitionsMod.Instance.LoadCooldown > 0
            || gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;

        InstantTransitionsMod.Instance.Log($"Exited unload trigger {gameObject.scene.name}:{transform.parent.gameObject.name}");

        string target = transform.parent.GetComponent<TransitionPoint>().targetScene;

        if (InstantTransitionsMod.Instance.LoadingScenes.TryGetValue(target, out AsyncOperation result) && !result.isDone)
        {
            result.completed += _ =>
            {
                StartUnload(target);
            };
        }
        else
        {
            StartUnload(target);
        }
    }
}
