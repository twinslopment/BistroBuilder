using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderReservations6BValidationResult
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport()
    {
        return "=== BISTRO BUILDER — 6B / VALIDACIÓN ===\n" +
               string.Join("\n", Lines) +
               "\nResultado: " + Correct + " OK / " + Warnings +
               " avisos / " + Errors + " errores.";
    }
}

/// <summary>
/// Gate estructural de disponibilidad/asignación de mesas para Reservas.
/// </summary>
public static class BistroBuilderReservations6BValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Reservations/6B - Validar disponibilidad",
        false,
        621)]
    private static void RunFromMenu()
    {        BistroBuilderReservations6BValidationResult result =
            ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderReservations6BValidationResult
        ValidateCurrentScene()
    {
        var result = new BistroBuilderReservations6BValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Fail(result, "No hay escena activa válida.");
            return result;
        }

        BistroBuilderReservations6AValidationResult baseGate =
            BistroBuilderReservations6AValidator.ValidateCurrentScene();
        Check(result, baseGate.Errors == 0,
            "La fundación 6A permanece válida.");

        GameObject gameSystems = FindUniqueGameSystems(scene);
        if (gameSystems == null)
        {
            Fail(result, "Debe existir exactamente un GameSystems canónico.");
            return result;
        }
        Pass(result, "GameSystems canónico único.");

        BistroBuilderReservationAvailabilityService[] services =
            FindSceneComponents<BistroBuilderReservationAvailabilityService>(scene);        if (services.Length != 1)
        {
            Fail(result,
                "Debe existir exactamente un ReservationAvailabilityService; hay " +
                services.Length + ".");
            return result;
        }

        BistroBuilderReservationAvailabilityService service = services[0];
        Pass(result, "Existe una única autoridad de disponibilidad 6B.");
        Check(result,
            service.gameObject == gameSystems,
            "ReservationAvailabilityService vive en GameSystems.");
        Check(result,
            service.ReservationService != null,
            "6B referencia ReservationService 6A.");
        Check(result,
            service.TableRegistry != null,
            "6B reutiliza RestaurantTableRegistry.");
        Check(result,
            service.SeatRegistry != null,
            "6B reutiliza RestaurantSeatRegistry.");
        Check(result,
            service.GeneralGameStateService != null,
            "6B reutiliza el calendario global.");
        Check(result,
            service.AvailabilityProfile != null,
            "6B tiene perfil de disponibilidad.");

        string error = string.Empty;
        Check(result,
            service.ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "Configuración 6B válida."
                : error);
        RestaurantTable[] tables = FindSceneComponents<RestaurantTable>(scene);
        RestaurantSeat[] seats = FindSceneComponents<RestaurantSeat>(scene);
        Check(result,
            tables.Length >= 10,
            "La escena mantiene al menos 10 mesas reales: " + tables.Length + ".");
        Check(result,
            seats.Length >= 28,
            "La escena mantiene al menos 28 sillas reales: " + seats.Length + ".");
        Check(result,
            FindSceneComponents<RestaurantTableRegistry>(scene).Length == 1,
            "No se ha duplicado RestaurantTableRegistry.");
        Check(result,
            FindSceneComponents<RestaurantSeatRegistry>(scene).Length == 1,
            "No se ha duplicado RestaurantSeatRegistry.");

        bool selfOk = BistroBuilderReservations6BAvailabilitySelfTest.Run(
            out int passed,
            out int failed,
            out _);
        Check(result,
            selfOk && failed == 0,
            "Autotest puro 6B: " + passed + " OK / " + failed + " fallos.");

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
        BistroBuilderReservations6BValidationResult result,
        bool condition,
        string message)
    {
        if (condition) Pass(result, message);
        else Fail(result, message);
    }

    private static void Pass(
        BistroBuilderReservations6BValidationResult result,
        string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }

    private static void Fail(
        BistroBuilderReservations6BValidationResult result,
        string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
