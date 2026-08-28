using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderReservations6DValidationResult
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport()
    {
        return "=== BISTRO BUILDER — 6D / PERSISTENCIA RESERVAS ===\n" +
               string.Join("\n", Lines) +
               "\nResultado: " + Correct + " OK / " + Warnings +
               " avisos / " + Errors + " errores.";
    }
}

public static class BistroBuilderReservations6DValidator
{
    [MenuItem("Tools/Bistro Builder/Reservations/6D - Validar persistencia", false, 641)]
    private static void RunFromMenu()
    {
        BistroBuilderReservations6DValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderReservations6DValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderReservations6DValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Fail(result, "No hay escena activa válida.");
            return result;
        }

        GameObject gameSystems = FindUniqueGameSystems(scene);
        if (gameSystems == null)
        {
            Fail(result, "Debe existir exactamente un GameSystems canónico.");
            return result;
        }
        Pass(result, "GameSystems canónico único.");

        BistroBuilderReservationsSaveSectionProvider[] providers =
            FindSceneComponents<BistroBuilderReservationsSaveSectionProvider>(scene);
        Check(result, providers.Length == 1,
            "Existe exactamente un provider reservations.state.");
        if (providers.Length != 1)
            return result;

        BistroBuilderReservationsSaveSectionProvider provider = providers[0];
        Check(result, provider.gameObject == gameSystems,
            "Provider 6D vive en GameSystems.");
        Check(result,
            provider.SectionId == BistroBuilderReservationsSaveSectionProvider.StableSectionId,
            "SectionId estable reservations.state.");
        Check(result,
            provider.SectionVersion == BistroBuilderReservationsSaveSectionProvider.StableSectionVersion,
            "Versión estable de reservations.state.");
        Check(result, !provider.IsRequired,
            "reservations.state es opcional para saves anteriores al Bloque 6.");

        string providerError = string.Empty;
        Check(result, provider.ValidateConfiguration(out providerError),
            string.IsNullOrWhiteSpace(providerError)
                ? "Provider 6D configurado correctamente."
                : providerError);

        BistroBuilderSaveGameService[] saveServices =
            FindSceneComponents<BistroBuilderSaveGameService>(scene);
        Check(result, saveServices.Length == 1,
            "Existe un único SaveGame canónico.");
        if (saveServices.Length == 1)
        {
            BistroBuilderSaveGameService save = saveServices[0];
            save.RefreshExtensions();
            Check(result, save.HasProvider(
                    BistroBuilderReservationsSaveSectionProvider.StableSectionId),
                "SaveGame registra reservations.state.");
            string saveError = string.Empty;
            Check(result, save.ValidateConfiguration(out saveError),
                string.IsNullOrWhiteSpace(saveError)
                    ? "SaveGame sigue siendo válido tras registrar 6D."
                    : saveError);
        }

        Check(result,
            FindSceneComponents<BistroBuilderReservationService>(scene).Length == 1,
            "ReservationService sigue siendo autoridad única.");
        Check(result,
            FindSceneComponents<BistroBuilderReservationServiceIntegration>(scene).Length == 1,
            "Integración 6C sigue siendo única.");
        Check(result,
            FindSceneComponents<BistroBuilderActiveServiceSaveSectionProvider>(scene).Length == 1,
            "service.runtime canónico sigue instalado para reservas activas.");

        return result;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && transform.name == "GameSystems")
            {
                found = transform.gameObject;
                count++;
            }
        }
        return count == 1 ? found : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null)
                    result.Add(values[index]);
            }
        }
        return result.ToArray();
    }

    private static void Check(
        BistroBuilderReservations6DValidationResult result,
        bool condition,
        string message)
    {
        if (condition) Pass(result, message);
        else Fail(result, message);
    }

    private static void Pass(
        BistroBuilderReservations6DValidationResult result,
        string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }

    private static void Fail(
        BistroBuilderReservations6DValidationResult result,
        string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
