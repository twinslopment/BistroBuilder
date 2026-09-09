using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketingOperationalPressureValidationResult
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
        "=== BISTRO BUILDER — MARKETING / OPERATIONAL PRESSURE VALIDACIÓN ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketingOperationalPressureValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/OperationalPressure - Validar",
        false,
        7241)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketingOperationalPressureValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report); else Debug.LogError(report);
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketingOperationalPressureValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors > 0)
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static BistroBuilderMarketingOperationalPressureValidationResult
        ValidateCurrentScene()
    {
        var result =
            new BistroBuilderMarketingOperationalPressureValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(
            scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");

        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(
            hosts.Length == 1,
            "Existe exactamente un GameSystems canónico.",
            "Se esperaba un GameSystems; hay " + hosts.Length + ".");

        BistroBuilderMarketingPreparationDurationAdjustmentProvider[] providers =
            FindSceneComponents<
                BistroBuilderMarketingPreparationDurationAdjustmentProvider>(
                    scene);
        result.Check(
            providers.Length == 1,
            "Existe un único proveedor Marketing → duración de cocina.",
            "Se esperaba un proveedor OperationalPressure; hay " +
                providers.Length + ".");

        if (providers.Length == 1)
        {
            result.Check(
                hosts.Length == 1 && providers[0].gameObject == hosts[0],
                "El proveedor vive en GameSystems.",
                "El proveedor no vive en GameSystems.");
            result.Check(
                providers[0] is
                    IBistroBuilderPreparationDurationAdjustmentProvider,
                "El proveedor usa el contrato genérico de duración.",
                "El proveedor no implementa el contrato genérico.");
            result.Check(
                providers[0].ValidateConfiguration(out _),
                "El proveedor valida sus autoridades reales.",
                "El proveedor tiene dependencias inválidas.");
        }

        BistroBuilderOrderLineExecutionService[] executions =
            FindSceneComponents<BistroBuilderOrderLineExecutionService>(scene);
        result.Check(
            executions.Length == 1,
            "Existe un único orquestador 367D de líneas.",
            "Se esperaba un OrderLineExecutionService; hay " +
                executions.Length + ".");

        BistroBuilderOrderLineExecutionService execution =
            executions.Length == 1 ? executions[0] : null;
        if (execution != null)
        {
            result.Check(
                hosts.Length == 1 && execution.gameObject == hosts[0],
                "367D y el proveedor comparten GameSystems.",
                "OrderLineExecutionService no vive en GameSystems.");
            result.Check(
                HasNoMarketingField(typeof(BistroBuilderOrderLineExecutionService)),
                "367D permanece desacoplado de tipos Marketing.",
                "OrderLineExecutionService contiene una dependencia directa de Marketing.");
            result.Check(
                execution.ValidateConfiguration(out _),
                "La configuración histórica de 367D sigue siendo válida.",
                "OrderLineExecutionService dejó de validar.");
        }

        KitchenSystem[] kitchens = FindSceneComponents<KitchenSystem>(scene);
        result.Check(
            kitchens.Length == 1,
            "Existe una única cocina provisional 367D.",
            "Se esperaba un KitchenSystem; hay " + kitchens.Length + ".");
        if (kitchens.Length == 1)
        {
            result.Check(
                execution != null &&
                ReferenceEquals(kitchens[0].LineExecutionService, execution),
                "KitchenSystem consume el orquestador 367D extendido.",
                "KitchenSystem apunta a otro orquestador de líneas.");
            result.Check(
                HasNoMarketingField(typeof(KitchenSystem)),
                "KitchenSystem permanece desacoplado de tipos Marketing.",
                "KitchenSystem contiene una dependencia directa de Marketing.");
            result.Check(
                kitchens[0].ValidateConfiguration(out _),
                "La cocina histórica sigue validando.",
                "KitchenSystem dejó de validar.");
        }

        BistroBuilderSharedCoursesValidationResult flow367F =
            BistroBuilderSharedCoursesValidator.ValidateCurrentScene();
        result.Check(
            flow367F.ErrorCount == 0,
            "El validador vigente 367F de cocina permanece verde.",
            "OperationalPressure rompió el validador 367F.\n" +
                flow367F.BuildReport());

        BistroBuilderMarketingAverageTicketValidationResult averageTicket =
            BistroBuilderMarketingAverageTicketValidator.ValidateCurrentScene();
        result.Check(
            averageTicket.Errors == 0,
            "AverageTicket permanece estructuralmente verde.",
            "OperationalPressure rompió el gate AverageTicket.");

        BistroBuilderMarketingService[] marketing =
            FindSceneComponents<BistroBuilderMarketingService>(scene);
        result.Check(
            marketing.Length == 1 && marketing[0].ValidateConfiguration(out _),
            "MarketingService conserva configuración válida.",
            "MarketingService falta, está duplicado o es inválido.");

        return result;
    }

    private static bool HasNoMarketingField(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
            if (fields[index].FieldType.Name.Contains("Marketing"))
                return false;
        return true;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var result = new List<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();

        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null &&
                string.Equals(transform.name, name, StringComparison.Ordinal))
                result.Add(transform.gameObject);
        }
        return result.ToArray();
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
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
