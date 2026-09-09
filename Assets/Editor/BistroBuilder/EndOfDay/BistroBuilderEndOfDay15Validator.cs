using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderEndOfDay15ValidationResult
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
        var b = new StringBuilder("BLOQUE 15 - VALIDACION\n");
        for (int i = 0; i < lines.Count; i++) b.AppendLine(lines[i]);
        b.Append("Resultado: ").Append(Passed).Append(" OK / ").Append(Errors).Append(" errores.");
        return b.ToString();
    }
}

public static class BistroBuilderEndOfDay15Validator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/End Of Day/15 - Validar", false, 15001)]
    private static void ValidateFromMenu()
    {
        BistroBuilderEndOfDay15ValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderEndOfDay15ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderEndOfDay15ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == ScenePath,
            "Escena canonica cargada");

        BistroBuilderEndOfDayService[] services =
            Object.FindObjectsByType<BistroBuilderEndOfDayService>(FindObjectsSortMode.None);
        BistroBuilderEndOfDayPlayerScreen[] screens =
            Object.FindObjectsByType<BistroBuilderEndOfDayPlayerScreen>(FindObjectsSortMode.None);
        BistroBuilderEndOfDaySaveSectionProvider[] providers =
            Object.FindObjectsByType<BistroBuilderEndOfDaySaveSectionProvider>(FindObjectsSortMode.None);

        RestaurantServiceStateService serviceState = Object.FindFirstObjectByType<RestaurantServiceStateService>();
        BistroBuilderGeneralGameStateService calendar = Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        GameClock clock = Object.FindFirstObjectByType<GameClock>();
        BistroBuilderFinancialResultsService finance = Object.FindFirstObjectByType<BistroBuilderFinancialResultsService>();
        BistroBuilderInventoryService inventory = Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderReputationService reputation = Object.FindFirstObjectByType<BistroBuilderReputationService>();
        BistroBuilderCustomerExperienceTrackingService experience = Object.FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>();
        BistroBuilderAdvancedFrontOfHouseService room = Object.FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
        BistroBuilderAdvancedOrderService orders = Object.FindFirstObjectByType<BistroBuilderAdvancedOrderService>();
        BistroBuilderSaveGameService save = Object.FindFirstObjectByType<BistroBuilderSaveGameService>();

        result.Check(services.Length == 1, "Autoridad de cierre unica");
        result.Check(screens.Length == 1, "UI jugable de resumen instalada");
        result.Check(providers.Length == 1, "Persistencia de cierres instalada");
        result.Check(serviceState != null && calendar != null && clock != null,
            "Estado de servicio y calendario conectados");
        result.Check(finance != null, "Finanzas y resultados diarios conectados");
        result.Check(inventory != null, "Inventario conectado");
        result.Check(reputation != null && experience != null,
            "Satisfaccion y reputacion conectadas");
        result.Check(room != null, "Sala avanzada 14 conectada");
        result.Check(orders != null, "Comandas avanzadas 11 conectadas");
        result.Check(save != null, "SaveGame disponible");

        BistroBuilderEndOfDayService service = services.Length == 1 ? services[0] : null;
        result.Check(service != null && service.ValidateConfiguration(out _),
            "Cierre operativo configurado");
        result.Check(screens.Length == 1 && screens[0].ValidateConfiguration(out _),
            "Pantalla de fin de dia configurada");
        result.Check(providers.Length == 1 && providers[0].ValidateConfiguration(out _),
            "Persistencia de resultados configurada");
        result.Check(service != null && service.CreateSnapshot() != null &&
            BistroBuilderEndOfDayEngine.TryValidateSnapshot(service.CreateSnapshot(), out _),
            "Snapshot de cierres valido");
        result.Check(serviceState != null && !serviceState.AcceptsNewCustomers,
            "Escena canonica arranca sin nuevas entradas");
        result.Check(typeof(BistroBuilderEndOfDayService).GetMethod("TryAdvanceToNextDay") != null,
            "Avance de calendario y preparacion siguiente dia disponible");
        result.Check(typeof(BistroBuilderEndOfDayService).GetMethod("TryBeginEndOfService") != null,
            "Inicio de cierre operativo disponible");
        return result;
    }
}
