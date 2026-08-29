using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BistroBuilderProgression9EValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    public void Check(bool condition, string ok, string fail)
    {
        if (condition) { Passed++; lines.Add("[OK] " + ok); }
        else { Errors++; lines.Add("[ERROR] " + fail); }
    }
    public string BuildReport() =>
        "=== BISTRO BUILDER — 9E / UI JUGABLE ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderProgression9EValidator
{
    [MenuItem("Tools/Bistro Builder/Progression/9E - Validar", false, 9041)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderProgression9EValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderProgression9EValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena no está activa o permanece Dirty.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems no es único.");

        var facades = FindScene<BistroBuilderProgressionPlayerFacade>(scene);
        result.Check(facades.Length == 1 && host != null && facades[0].gameObject == host,
            "La fachada 9E es única y vive en GameSystems.",
            "La fachada 9E falta, está duplicada o mal ubicada.");
        if (facades.Length == 1)
            result.Check(facades[0].ValidateConfiguration(out _),
                "La fachada 9E valida autoridades y Finanzas.",
                "La fachada 9E no valida su configuración.");

        var screens = FindScene<BistroBuilderProgressionPlayerScreen>(scene);
        result.Check(screens.Length == 1,
            "Existe una única pantalla jugable 9E.",
            "La pantalla 9E falta o está duplicada.");
        if (screens.Length == 1)
            result.Check(screens[0].ValidateConfiguration(out _),
                "La pantalla 9E tiene todos sus controles enlazados.",
                "La pantalla 9E tiene referencias incompletas.");

        GameObject uiRoot = FindDirectRoot(scene, "BistroBuilderProgressionUI");
        result.Check(uiRoot != null,
            "Existe el Canvas jugable de Mejoras y Progresión.",
            "Falta BistroBuilderProgressionUI.");
        if (uiRoot != null)
        {
            Button[] buttons = uiRoot.GetComponentsInChildren<Button>(true);
            result.Check(buttons.Length >= 12,
                "La UI expone filtros, navegación, compra, cierre y lanzador.",
                "La UI 9E no contiene todos los controles esperados.");
        }

        BistroBuilderProgression9DValidationResult previous =
            BistroBuilderProgression9DValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "9A-9D permanecen estructuralmente verdes.",
            "9E rompe un gate anterior del Bloque 9.");
        return result;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == "GameSystems")
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && root.name == name) return root;
        return null;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
