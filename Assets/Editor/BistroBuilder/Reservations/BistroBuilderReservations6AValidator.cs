using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado estructural de 6A — Fundación de Reservas.
/// </summary>
public sealed class BistroBuilderReservations6AValidationResult
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport()
    {
        return "=== BISTRO BUILDER — 6A / VALIDACIÓN ===\n" +
               string.Join("\n", Lines) +
               "\nResultado: " + Correct + " OK / " + Warnings +
               " avisos / " + Errors + " errores.";
    }
}

/// <summary>
/// Valida una única autoridad de Reservas en GameSystems.
/// </summary>
public static class BistroBuilderReservations6AValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Reservations/6A - Validar fundación",
        false,
        611)]
    private static void RunFromMenu()
    {
        BistroBuilderReservations6AValidationResult result =
            ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderReservations6AValidationResult
        ValidateCurrentScene()
    {
        var result = new BistroBuilderReservations6AValidationResult();
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
        BistroBuilderReservationService[] services =
            FindSceneComponents<BistroBuilderReservationService>(scene);
        if (services.Length != 1)
        {
            Fail(result,
                "Debe existir exactamente un ReservationService; hay " +
                services.Length + ".");
            return result;
        }

        BistroBuilderReservationService service = services[0];
        Pass(result, "Existe una única autoridad ReservationService.");
        Check(
            result,
            service.gameObject == gameSystems,
            "ReservationService vive en GameSystems.");

        string error = string.Empty;
        Check(
            result,
            service.ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "ReservationService expone reservations.state válido."
                : error);

        BistroBuilderReservationsSnapshot snapshot = service.CreateSnapshot();
        Check(
            result,
            snapshot != null &&
            snapshot.schemaId ==
                BistroBuilderReservationsSnapshot.CurrentSchemaId &&
            snapshot.schemaVersion ==
                BistroBuilderReservationsSnapshot.CurrentSchemaVersion,
            "Snapshot reservations.state V1 disponible.");
        Check(
            result,
            FindSceneComponents<RestaurantTableRegistry>(scene).Length == 1,
            "Reservas reutiliza el TableRegistry canónico existente.");
        Check(
            result,
            FindSceneComponents<RestaurantSeatRegistry>(scene).Length == 1,
            "Reservas reutiliza el SeatRegistry canónico existente.");
        Check(
            result,
            FindSceneComponents<WaiterTaskCoordinator>(scene).Length == 1,
            "No se ha duplicado WaiterTaskCoordinator.");

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
        BistroBuilderReservations6AValidationResult result,
        bool condition,
        string message)
    {
        if (condition) Pass(result, message);
        else Fail(result, message);
    }

    private static void Pass(
        BistroBuilderReservations6AValidationResult result,
        string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }
    private static void Fail(
        BistroBuilderReservations6AValidationResult result,
        string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
