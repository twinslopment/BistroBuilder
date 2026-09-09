using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BistroBuilderMarketingPlayerUiValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }

    public void Check(bool condition, string ok, string fail)
    {
        if (condition)
        {
            Passed++;
            lines.Add("[OK] " + ok);
        }
        else
        {
            Errors++;
            lines.Add("[ERROR] " + fail);
        }
    }

    public string BuildReport() =>
        "=== BISTRO BUILDER — MARKETING / UI JUGABLE VALIDACIÓN ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketingPlayerUiValidator
{
    [MenuItem("Tools/Bistro Builder/Marketing/UI jugable - Validar", false, 7261)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketingPlayerUiValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketingPlayerUiValidationResult result = ValidateCurrentScene();
        if (result.Errors > 0)
            throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderMarketingPlayerUiValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketingPlayerUiValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(
            scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");

        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(
            hosts.Length == 1,
            "Existe un único GameSystems canónico.",
            "GameSystems es inexistente o está duplicado.");

        BistroBuilderMarketingPlayerFacade[] facades =
            FindSceneComponents<BistroBuilderMarketingPlayerFacade>(scene);
        result.Check(
            facades.Length == 1 && hosts.Length == 1 &&
            facades[0].gameObject == hosts[0],
            "Existe una única fachada UI de Marketing en GameSystems.",
            "La fachada UI de Marketing falta, está duplicada o mal ubicada.");
        if (facades.Length == 1)
        {
            result.Check(
                facades[0].ValidateConfiguration(out _),
                "La fachada UI valida todas sus autoridades.",
                "La fachada UI tiene dependencias inválidas.");
        }

        BistroBuilderMarketingPlayerScreen[] screens =
            FindSceneComponents<BistroBuilderMarketingPlayerScreen>(scene);
        result.Check(
            screens.Length == 1,
            "Existe una única pantalla jugable de Marketing.",
            "La pantalla jugable de Marketing falta o está duplicada.");
        if (screens.Length == 1)
        {
            result.Check(
                screens[0].ValidateConfiguration(out _),
                "La pantalla jugable valida todo su wiring.",
                "La pantalla jugable contiene referencias inválidas.");
        }

        GameObject[] uiRoots = FindNamedObjects(scene, "BistroBuilderMarketingUI");
        result.Check(
            uiRoots.Length == 1,
            "Existe un único Canvas raíz de Marketing.",
            "El Canvas raíz de Marketing falta o está duplicado.");
        if (uiRoots.Length == 1)
        {
            result.Check(
                uiRoots[0].GetComponent<Canvas>() != null &&
                uiRoots[0].GetComponent<GraphicRaycaster>() != null,
                "El Canvas de Marketing dispone de render y raycast.",
                "El Canvas de Marketing no tiene infraestructura UI completa.");
        }

        GameObject[] launchers = FindNamedObjects(scene, "OpenMarketingButton");
        result.Check(
            launchers.Length == 1 && launchers[0].GetComponent<Button>() != null,
            "Existe un único acceso jugable a Marketing.",
            "El botón de acceso a Marketing falta o está duplicado.");

        BistroBuilderMarketingGuestRelationsValidationResult relations =
            BistroBuilderMarketingGuestRelationsValidator.ValidateCurrentScene();
        result.Check(
            relations.Errors == 0,
            "GuestRelations permanece estructuralmente verde.",
            "La UI de Marketing rompió GuestRelations.");

        BistroBuilderMarketingOperationalPressureValidationResult pressure =
            BistroBuilderMarketingOperationalPressureValidator.ValidateCurrentScene();
        result.Check(
            pressure.Errors == 0,
            "OperationalPressure permanece estructuralmente verde.",
            "La UI de Marketing rompió OperationalPressure.");

        BistroBuilderMarketing7CValidationResult persistence =
            BistroBuilderMarketing7CValidator.ValidateCurrentScene();
        result.Check(
            persistence.Errors == 0,
            "La persistencia Marketing 7C permanece verde.",
            "La UI de Marketing rompió la persistencia 7C.");

        result.Check(
            FindSceneComponents<BistroBuilderMarketingService>(scene).Length == 1,
            "MarketingService conserva una única autoridad.",
            "MarketingService falta o está duplicado.");
        result.Check(
            FindSceneComponents<BistroBuilderGuestRelationsService>(scene).Length == 1,
            "GuestRelations conserva una única autoridad.",
            "GuestRelations falta o está duplicado.");
        result.Check(
            FindSceneComponents<BistroBuilderSaveGameService>(scene).Length == 1,
            "SaveGameService conserva una única autoridad.",
            "SaveGameService falta o está duplicado.");

        return result;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var result = new List<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && string.Equals(
                    transform.name,
                    name,
                    StringComparison.Ordinal))
                result.Add(transform.gameObject);
        }
        return result.ToArray();
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < found.Length; index++)
                if (found[index] != null) result.Add(found[index]);
        }
        return result.ToArray();
    }
}
