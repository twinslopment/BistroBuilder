using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instala 21A al cargar escenas sin serializar cambios masivos en las escenas canónicas.
/// </summary>
public static class BistroBuilderUiRuntimeBootstrap
{
    public const string RuntimeRevision = "21A-UIUX-BOOTSTRAP-V1.0";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene(scene);
    }

    public static bool InstallForScene(Scene scene)
    {
        Canvas canvas = FindCanonicalCanvas(scene);
        if (canvas == null) return false;
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        BistroBuilderUiDesignSystem design = GetOrAdd<BistroBuilderUiDesignSystem>(canvas.gameObject);
        BistroBuilderUiShell shell = GetOrAdd<BistroBuilderUiShell>(canvas.gameObject);
        GetOrAdd<BistroBuilderUnifiedUiInteractionService>(canvas.gameObject);

        shell.EnsureShell();
        design.ApplyAllNow(true);
        return true;
    }

    private static Canvas FindCanonicalCanvas(Scene scene)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            Transform parent = canvas.transform.parent;
            if (canvas.name == "Canvas" && parent != null && parent.name == "MainHUD")
                return canvas;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T current = target.GetComponent<T>();
        return current != null ? current : target.AddComponent<T>();
    }
}
