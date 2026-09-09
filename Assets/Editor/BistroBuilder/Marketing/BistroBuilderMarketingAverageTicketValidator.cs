using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketingAverageTicketValidationResult
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
        "=== BISTRO BUILDER — MARKETING / AVERAGE TICKET VALIDACIÓN ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketingAverageTicketValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/AverageTicket - Validar",
        false,
        7231)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketingAverageTicketValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report); else Debug.LogError(report);
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketingAverageTicketValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors > 0)
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static BistroBuilderMarketingAverageTicketValidationResult
        ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketingAverageTicketValidationResult();
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

        BistroBuilderMarketingSalesPaymentAdjustmentProvider[] providers =
            FindSceneComponents<
                BistroBuilderMarketingSalesPaymentAdjustmentProvider>(scene);
        result.Check(
            providers.Length == 1,
            "Existe un único proveedor Marketing → cobro.",
            "Se esperaba un proveedor AverageTicket; hay " +
                providers.Length + ".");

        if (providers.Length == 1)
        {
            result.Check(
                hosts.Length == 1 && providers[0].gameObject == hosts[0],
                "El proveedor vive en GameSystems.",
                "El proveedor no vive en GameSystems.");
            result.Check(
                providers[0] is IBistroBuilderSalesPaymentAdjustmentProvider,
                "El proveedor usa el contrato genérico de ajuste de cobro.",
                "El proveedor no implementa el contrato genérico.");
            result.Check(
                providers[0].ValidateConfiguration(out _),
                "El proveedor valida Marketing y portfolio reales.",
                "El proveedor tiene dependencias inválidas.");
        }

        BistroBuilderSalesRevenueBridge[] bridges =
            FindSceneComponents<BistroBuilderSalesRevenueBridge>(scene);
        result.Check(
            bridges.Length == 1,
            "Existe un único bridge de ingresos 3B.",
            "Se esperaba un SalesRevenueBridge; hay " + bridges.Length + ".");
        if (bridges.Length == 1)
        {
            result.Check(
                hosts.Length == 1 && bridges[0].gameObject == hosts[0],
                "3B y el proveedor comparten GameSystems.",
                "SalesRevenueBridge no vive en GameSystems.");
            result.Check(
                HasNoMarketingField(typeof(BistroBuilderSalesRevenueBridge)),
                "Finanzas 3B permanece desacoplado de tipos Marketing.",
                "SalesRevenueBridge contiene una dependencia directa de Marketing.");
            result.Check(
                bridges[0].ValidateConfiguration(out _),
                "La configuración histórica de 3B sigue siendo válida.",
                "SalesRevenueBridge dejó de validar.");
        }

        bool financeOk = BistroBuilderFinance3BValidator.ValidateCurrentScene(
            out int financePassed,
            out int financeErrors,
            out _);
        result.Check(
            financeOk && financeErrors == 0 && financePassed > 0,
            "El validador histórico Finanzas 3B permanece verde.",
            "AverageTicket rompió el validador 3B.");

        BistroBuilderMarketingTargetDemandValidationResult target =
            BistroBuilderMarketingTargetDemandValidator.ValidateCurrentScene();
        result.Check(
            target.Errors == 0,
            "TargetDemand permanece estructuralmente verde.",
            "AverageTicket rompió el gate TargetDemand.");

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
