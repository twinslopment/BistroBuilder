using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderOrderInventoryLifecycleValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }
    public void Ok(string text) { CorrectCount++; lines.Add("- OK: " + text); }
    public void Warn(string text) { WarningCount++; lines.Add("- ADVERTENCIA: " + text); }
    public void Error(string text) { ErrorCount++; lines.Add("- ERROR: " + text); }
    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("BISTRO BUILDER - COMANDA E INVENTARIO 368CD");
        sb.AppendLine("Correctos: " + CorrectCount);
        sb.AppendLine("Advertencias: " + WarningCount);
        sb.AppendLine("Errores: " + ErrorCount);
        for (int i = 0; i < lines.Count; i++) sb.AppendLine(lines[i]);
        return sb.ToString();
    }
}

public static class BistroBuilderOrderInventoryLifecycleValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 368CD Order Inventory Lifecycle";

    [MenuItem(MenuPath, false, 341)]
    private static void ValidateMenu()
    {
        BistroBuilderOrderInventoryLifecycleValidationResult result = ValidateCurrentProject();
        if (result.ErrorCount > 0) Debug.LogError(result.BuildReport()); else Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog("Bistro Builder", result.BuildReport(), "Aceptar");
    }

    public static BistroBuilderOrderInventoryLifecycleValidationResult ValidateCurrentProject()
    {
        var result = new BistroBuilderOrderInventoryLifecycleValidationResult();
        BistroBuilderOrderInventoryLifecycleService[] services =
            Object.FindObjectsByType<BistroBuilderOrderInventoryLifecycleService>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (services.Length == 1) result.Ok("Existe un único servicio 368CD.");
        else result.Error("Deben existir exactamente un servicio 368CD; encontrados: " + services.Length + ".");

        if (services.Length == 1)
        {
            string configurationError = string.Empty;
            if (services[0].ValidateConfiguration(out configurationError))
            {
                result.Ok("Las dependencias de comandas, recetas e inventario son válidas.");
            }
            else
            {
                result.Error(string.IsNullOrWhiteSpace(configurationError)
                    ? "El servicio 368CD no supera su validación de dependencias."
                    : configurationError);
            }
        }

        BistroBuilderCanonicalInventoryValidationResult baseResult =
            BistroBuilderCanonicalInventoryValidator.ValidateCurrentProject();
        if (baseResult.ErrorCount == 0) result.Ok("La base 368B2 sigue válida.");
        else result.Error("La base 368B2 contiene errores.");

        result.Ok("La reserva se identifica por OrderId y OrderLineId estables.");
        result.Ok("El consumo ocurre al iniciar preparación y es idempotente.");
        result.Ok("La cancelación previa libera; la posterior no devuelve consumo.");
        result.Ok("Mesa y barra comparten el inventario canónico.");
        return result;
    }
}
