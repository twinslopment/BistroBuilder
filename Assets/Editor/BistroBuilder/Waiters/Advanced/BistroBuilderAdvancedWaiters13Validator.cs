using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedWaiters13ValidationResult
{
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    private readonly List<string> lines = new();
    public void Check(bool ok, string name)
    {
        if (ok) Passed++; else Errors++;
        lines.Add((ok ? "OK - " : "ERROR - ") + name);
    }
    public string BuildReport()
    {
        var b = new StringBuilder("BLOQUE 13 - VALIDACION\n");
        for (int i = 0; i < lines.Count; i++) b.AppendLine(lines[i]);
        b.Append("Resultado: ").Append(Passed).Append(" OK / ").Append(Errors).Append(" errores.");
        return b.ToString();
    }
}

public static class BistroBuilderAdvancedWaiters13Validator
{
    [MenuItem("Tools/Bistro Builder/Waiters/13 - Validar", false, 13001)]
    private static void ValidateFromMenu()
    {
        BistroBuilderAdvancedWaiters13ValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport()); else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderAdvancedWaiters13ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedWaiters13ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == "Assets/Scenes/Prototype_Restaurant.unity",
            "Escena canonica cargada");

        BistroBuilderAdvancedWaiterService[] services =
            Object.FindObjectsByType<BistroBuilderAdvancedWaiterService>(FindObjectsSortMode.None);
        BistroBuilderWaiterRoutingService[] routing =
            Object.FindObjectsByType<BistroBuilderWaiterRoutingService>(FindObjectsSortMode.None);
        BistroBuilderAdvancedWaiterPlayerScreen[] screens =
            Object.FindObjectsByType<BistroBuilderAdvancedWaiterPlayerScreen>(FindObjectsSortMode.None);
        WaiterTaskCoordinator coordinator = Object.FindFirstObjectByType<WaiterTaskCoordinator>();
        BistroBuilderCustomerExperienceTrackingService experience =
            Object.FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>();
        Waiter[] waiters = Object.FindObjectsByType<Waiter>(FindObjectsSortMode.None);

        result.Check(services.Length == 1, "Autoridad IA de camareros unica");
        result.Check(routing.Length == 1, "Motor de rutas unico");
        result.Check(screens.Length == 1, "UI/feedback de camareros instalada");
        result.Check(coordinator != null, "Coordinador central existente");
        result.Check(waiters.Length > 0, "Agentes camarero disponibles");
        result.Check(experience != null, "Acciones contextuales conectadas a experiencia real");

        if (services.Length == 1)
        {
            result.Check(services[0].ValidateConfiguration(out _), "IA operativa configurada");
            result.Check(ReadObjectReference(services[0], "experienceTrackingService") == experience,
                "Acciones de camarero afectan satisfaccion persistente");
        }
        else
        {
            result.Check(false, "IA operativa configurada");
            result.Check(false, "Acciones de camarero afectan satisfaccion persistente");
        }

        if (screens.Length == 1)
            result.Check(screens[0].ValidateConfiguration(out _), "Pantalla jugable configurada");
        else result.Check(false, "Pantalla jugable configurada");

        int profiled = 0;
        int routed = 0;
        for (int i = 0; i < waiters.Length; i++)
        {
            if (waiters[i].GetComponent<BistroBuilderAdvancedWaiterProfile>() != null) profiled++;
            WaiterMovementView movement = waiters[i].GetComponent<WaiterMovementView>();
            if (movement == null || ReadObjectReference(movement, "routingService") == routing.GetValueOrDefault(0)) routed++;
        }
        result.Check(profiled == waiters.Length, "Perfiles de responsabilidad y zonas instalados");
        result.Check(routed == waiters.Length, "Movimiento conectado al motor de mejores rutas");

        if (coordinator != null)
        {
            result.Check(ReadBool(coordinator, "manageTakeOrderTasks") &&
                         ReadBool(coordinator, "manageFoodDeliveryTasks") &&
                         ReadBool(coordinator, "manageBillTasks") &&
                         ReadBool(coordinator, "manageCleaningTasks"),
                "Tareas de sala unificadas en el coordinador central");
            result.Check(ReadObjectReference(coordinator, "advancedWaiterService") == services.GetValueOrDefault(0),
                "Coordinador delegado a IA avanzada");
        }
        else
        {
            result.Check(false, "Tareas de sala unificadas en el coordinador central");
            result.Check(false, "Coordinador delegado a IA avanzada");
        }

        result.Check(AllLegacyAssignmentSystemsDisabled(), "Asignadores legacy desactivados sin doble autoridad");
        result.Check(typeof(IBistroBuilderWaiterRouteProvider).IsAssignableFrom(typeof(BistroBuilderWaiterRoutingService)),
            "Contrato preparado para navegacion profesional futura");
        return result;
    }

    private static bool AllLegacyAssignmentSystemsDisabled()
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is WaiterAssignmentSystem || all[i] is FoodDeliveryAssignmentSystem ||
                all[i] is BillAssignmentSystem || all[i] is TableCleaningAssignmentSystem)
            {
                if (all[i].enabled) return false;
            }
        }
        return true;
    }

    private static bool ReadBool(Object target, string name)
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(name);
        return p != null && p.boolValue;
    }

    private static Object ReadObjectReference(Object target, string name)
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(name);
        return p != null ? p.objectReferenceValue : null;
    }

    private static T GetValueOrDefault<T>(this T[] values, int index) where T : class
    {
        return values != null && index >= 0 && index < values.Length ? values[index] : null;
    }
}
