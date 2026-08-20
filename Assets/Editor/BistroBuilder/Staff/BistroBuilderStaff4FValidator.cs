using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4FValidationResult
{
    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    private readonly List<string> lines = new List<string>();

    public void Correct(string message)
    {
        CorrectCount++;
        lines.Add("[OK] " + message);
    }

    public void Warning(string message)
    {
        WarningCount++;
        lines.Add("[AVISO] " + message);
    }

    public void Error(string message)
    {
        ErrorCount++;
        lines.Add("[ERROR] " + message);
    }

    public string BuildReport()
    {
        return "4F — VALIDACIÓN UI DE PERSONAL\n" +
               "Correctos: " + CorrectCount + "\n" +
               "Avisos: " + WarningCount + "\n" +
               "Errores: " + ErrorCount + "\n\n" +
               string.Join("\n", lines);
    }
}

/// <summary>
/// Validador estructural de la UI 4F instalada. Comprueba wiring y unicidad,
/// pero no simula clicks ni sustituye la prueba real en Play Mode.
/// </summary>
public static class BistroBuilderStaff4FValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Validar UI instalada",
        false,
        3241)]
    private static void ValidateFromMenu()
    {
        BistroBuilderStaff4FValidationResult result = ValidateCurrentScene();
        string report = result.BuildReport();
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4F Personal",
            report,
            "Aceptar");
    }

    public static BistroBuilderStaff4FValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4FValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa válida.");
            return result;
        }

        BistroBuilderStaffPlayerFacade[] facades =
            FindSceneComponents<BistroBuilderStaffPlayerFacade>(scene);
        BistroBuilderStaffPlayerScreen[] screens =
            FindSceneComponents<BistroBuilderStaffPlayerScreen>(scene);

        if (facades.Length != 1)
        {
            result.Error(
                "La escena necesita exactamente una StaffPlayerFacade; hay " +
                facades.Length + ".");
        }
        else if (!facades[0].ValidateConfiguration(out string facadeError))
        {
            result.Error("StaffPlayerFacade inválida: " + facadeError);
        }
        else
        {
            result.Correct("Existe una única fachada 4F y su wiring es válido.");
        }

        if (screens.Length != 1)
        {
            result.Error(
                "La escena necesita exactamente una StaffPlayerScreen; hay " +
                screens.Length + ".");
        }
        else if (!screens[0].ValidateConfiguration(out string screenError))
        {
            result.Error("StaffPlayerScreen inválida: " + screenError);
        }
        else
        {
            result.Correct("Existe una única pantalla 4F y todas sus referencias son válidas.");
        }

        ValidateCanonicalAuthorities<BistroBuilderStaffService>(
            scene,
            "StaffService",
            result);
        ValidateCanonicalAuthorities<BistroBuilderStaffRecruitmentService>(
            scene,
            "StaffRecruitmentService",
            result);
        ValidateCanonicalAuthorities<BistroBuilderStaffDevelopmentService>(
            scene,
            "StaffDevelopmentService",
            result);
        ValidateCanonicalAuthorities<BistroBuilderStaffSessionService>(
            scene,
            "StaffSessionService",
            result);

        if (screens.Length == 1 &&
            screens[0].GetComponent<Waiter>() != null)
        {
            result.Error(
                "La UI 4F no puede vivir en un agente Waiter operativo.");
        }
        else if (screens.Length == 1)
        {
            result.Correct(
                "La pantalla de Personal está separada del agente operativo Waiter.");
        }

        if (facades.Length == 1 && screens.Length == 1 &&
            facades[0].gameObject.scene == screens[0].gameObject.scene)
        {
            result.Correct(
                "Fachada y pantalla pertenecen a la misma escena activa.");
        }

        if (screens.Length == 1 && !screens[0].IsVisible)
        {
            result.Warning(
                "La pantalla está cerrada actualmente; la prueba visual debe abrirla en Play Mode.");
        }

        return result;
    }

    private static void ValidateCanonicalAuthorities<T>(
        Scene scene,
        string displayName,
        BistroBuilderStaff4FValidationResult result)
        where T : Component
    {
        T[] components = FindSceneComponents<T>(scene);
        if (components.Length == 1)
        {
            result.Correct(displayName + " conserva una única autoridad en escena.");
            return;
        }

        result.Error(
            displayName + " debe existir exactamente una vez; hay " +
            components.Length + ".");
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                result.Add(all[index]);
            }
        }
        return result.ToArray();
    }
}
