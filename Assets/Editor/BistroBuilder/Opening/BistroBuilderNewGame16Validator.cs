using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderNewGame16ValidationResult
{
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    private readonly List<string> lines = new();
    public void Check(bool ok, string label)
    {
        if (ok) Passed++; else Errors++;
        lines.Add((ok ? "OK - " : "ERROR - ") + label);
    }
    public string BuildReport()
    {
        var b = new StringBuilder("BLOQUE 16 - VALIDACION\n");
        for (int i = 0; i < lines.Count; i++) b.AppendLine(lines[i]);
        b.Append("Resultado: ").Append(Passed).Append(" OK / ").Append(Errors).Append(" errores.");
        return b.ToString();
    }
}

public static class BistroBuilderNewGame16Validator
{
    [MenuItem("Tools/Bistro Builder/Opening/16 - Validar", false, 16001)]
    private static void ValidateFromMenu()
    {
        BistroBuilderNewGame16ValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport()); else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderNewGame16ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderNewGame16ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == "Assets/Scenes/Prototype_Restaurant.unity",
            "Escena canonica cargada");
        var services = Object.FindObjectsByType<BistroBuilderNewGameOpeningService>(FindObjectsSortMode.None);
        var screens = Object.FindObjectsByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsSortMode.None);
        var providers = Object.FindObjectsByType<BistroBuilderNewGameOpeningSaveSectionProvider>(FindObjectsSortMode.None);
        var save = Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        result.Check(services.Length == 1, "Autoridad de nueva partida unica");
        result.Check(screens.Length == 1, "UI de nueva partida instalada");
        result.Check(providers.Length == 1, "Persistencia de apertura instalada");
        result.Check(Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>() != null, "Identidad y calendario disponibles");
        result.Check(Object.FindFirstObjectByType<BistroBuilderInventoryService>() != null, "Inventario inicial disponible");
        result.Check(Object.FindFirstObjectByType<BistroBuilderStaffService>() != null, "Personal inicial disponible");
        result.Check(Object.FindFirstObjectByType<BistroBuilderStaffScheduleService>() != null, "Horarios iniciales disponibles");
        result.Check(Object.FindFirstObjectByType<BistroBuilderRestaurantMenuService>() != null, "Carta inicial disponible");
        result.Check(Object.FindFirstObjectByType<RestaurantServiceStateService>() != null, "Primera apertura conectada");
        result.Check(Object.FindFirstObjectByType<RestaurantPlacementValidationService>() != null, "Validacion fisica conectada");
        result.Check(Object.FindFirstObjectByType<BistroBuilderAdvancedKitchenService>() != null, "Cocina avanzada conectada");
        result.Check(Object.FindFirstObjectByType<RestaurantTableRegistry>() != null, "Comedor y mesas conectados");
        result.Check(GameObject.Find("RestaurantEntrancePoint") != null, "Entrada de clientes existente");
        result.Check(save != null, "SaveGame disponible");
        if (save != null)
        {
            save.RefreshExtensions();
            result.Check(save.HasProvider(BistroBuilderNewGameOpeningSaveSectionProvider.StableSectionId),
                "Nueva partida participa en Save/Load");
        }
        else result.Check(false, "Nueva partida participa en Save/Load");
        result.Check(services.Length == 1 && services[0].ValidateConfiguration(out _),
            "Servicio de apertura configurado");
        result.Check(screens.Length == 1 && screens[0].ValidateConfiguration(out _),
            "Pantalla jugable configurada");
        result.Check(services.Length == 1 &&
            BistroBuilderNewGameEngine.TryValidateSnapshot(services[0].CreateSnapshot(), out _),
            "Snapshot de apertura valido");
        return result;
    }
}
