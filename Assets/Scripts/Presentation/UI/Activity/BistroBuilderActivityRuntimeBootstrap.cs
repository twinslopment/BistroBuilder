using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala el runtime de ACTIVIDAD sin depender de prefabs ni de una escena
/// concreta. Es idempotente y repara instalaciones parciales.
/// </summary>
public static class BistroBuilderActivityRuntimeBootstrap
{
    private const string HostName = "__BB_ActivityRuntime__";
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        if (subscribed)
            return;
        subscribed = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterSceneLoad()
    {
        Install(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

    private static void Install(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        ActivityFeedService existing =
            Object.FindFirstObjectByType<ActivityFeedService>(
                FindObjectsInactive.Include);

        GameObject host = null;
        if (existing != null && existing.gameObject.scene == scene)
        {
            host = existing.gameObject;
        }
        else
        {
            BistroBuilderUiShell shell =
                Object.FindFirstObjectByType<BistroBuilderUiShell>(
                    FindObjectsInactive.Include);

            if (shell != null && shell.gameObject.scene == scene)
                host = shell.gameObject;
            else
            {
                host = new GameObject(HostName);
                SceneManager.MoveGameObjectToScene(host, scene);
            }
        }

        ActivityFeedService feed =
            GetOrAdd<ActivityFeedService>(host);
        GetOrAdd<BistroBuilderActivityRuntimeBridge>(host);
        GetOrAdd<ActivityTargetRouter>(host);
        GetOrAdd<ActivityPanelController>(host);

        BistroBuilderActivityPersistenceInstaller.TryInstall(feed);
    }

    private static T GetOrAdd<T>(GameObject host)
        where T : Component
    {
        T component = host.GetComponent<T>();
        return component != null ? component : host.AddComponent<T>();
    }
}
