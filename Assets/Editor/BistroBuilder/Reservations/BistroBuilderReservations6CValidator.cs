using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Resultado estructural de 6C.</summary>
public sealed class BistroBuilderReservations6CValidationResult
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport()
    {
        return "=== BISTRO BUILDER — 6C / VALIDACIÓN ===\n" +
               string.Join("\n", Lines) +
               "\nResultado: " + Correct + " OK / " + Warnings +
               " avisos / " + Errors + " errores.";
    }
}

/// <summary>
/// Comprueba que Reservas reutiliza el flujo real de clientes y mesas.
/// </summary>
public static class BistroBuilderReservations6CValidator
{
    [MenuItem("Tools/Bistro Builder/Reservations/6C - Validar integración", false, 631)]
    private static void RunFromMenu()
    {
        BistroBuilderReservations6CValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderReservations6CValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderReservations6CValidationResult();
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

        BistroBuilderReservationServiceIntegration[] integrations =
            FindSceneComponents<BistroBuilderReservationServiceIntegration>(scene);
        Check(result, integrations.Length == 1,
            "Existe una única integración runtime de Reservas.");
        if (integrations.Length != 1)
            return result;

        BistroBuilderReservationServiceIntegration integration = integrations[0];
        Check(result, integration.gameObject == gameSystems,
            "La integración 6C vive en GameSystems.");

        Check(result, integration.ValidateConfiguration(out string configError),
            string.IsNullOrWhiteSpace(configError)
                ? "La integración 6C está completamente cableada."
                : configError);

        Check(result,
            FindSceneComponents<CustomerGroupSpawner>(scene).Length == 1,
            "Se reutiliza un único CustomerGroupSpawner canónico.");
        Check(result,
            FindSceneComponents<TableAssignmentSystem>(scene).Length == 1,
            "Se reutiliza un único TableAssignmentSystem canónico.");
        Check(result,
            FindSceneComponents<RestaurantTableRegistry>(scene).Length == 1,
            "Se reutiliza un único RestaurantTableRegistry.");
        Check(result,
            FindSceneComponents<RestaurantSeatRegistry>(scene).Length == 1,
            "Se reutiliza un único RestaurantSeatRegistry.");

        MethodInfo preferred = typeof(TableAssignmentSystem).GetMethod(
            "TryReservePreferredTable",
            BindingFlags.Instance | BindingFlags.Public);
        Check(result, preferred != null,
            "TableAssignmentSystem expone reserva preferente sin duplicar asignador.");

        MethodInfo spawn = typeof(CustomerGroupSpawner).GetMethod(
            "TrySpawnExternalTableServiceGroup",
            BindingFlags.Instance | BindingFlags.Public);
        Check(result, spawn != null,
            "CustomerGroupSpawner expone llegada externa reutilizando su pipeline.");

        BistroBuilderReservations6AValidationResult gate6A =
            BistroBuilderReservations6AValidator.ValidateCurrentScene();
        Check(result, gate6A.Errors == 0,
            "Regresión 6A limpia.");

        BistroBuilderReservations6BValidationResult gate6B =
            BistroBuilderReservations6BValidator.ValidateCurrentScene();
        Check(result, gate6B.Errors == 0,
            "Regresión 6B limpia.");

        BistroBuilderBlock6CapacityValidation capacity =
            BistroBuilderBlock6CapacityValidator.ValidateCurrentScene();
        Check(result, capacity.Errors == 0,
            "La ampliación 10 mesas / 28 plazas / 4 camareros sigue válida.");

        Check(result,
            FindSceneComponents<WaiterTaskCoordinator>(scene).Length == 1,
            "6C no duplica WaiterTaskCoordinator.");
        Check(result,
            FindSceneComponents<RestaurantServiceStateService>(scene).Length == 1,
            "6C reutiliza el estado de servicio canónico.");

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

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
                if (values[index] != null) result.Add(values[index]);
        }
        return result.ToArray();
    }

    private static void Check(
        BistroBuilderReservations6CValidationResult result,
        bool condition,
        string message)
    {
        if (condition) Pass(result, message);
        else Fail(result, message);
    }

    private static void Pass(
        BistroBuilderReservations6CValidationResult result,
        string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }

    private static void Fail(
        BistroBuilderReservations6CValidationResult result,
        string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
