using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Resultado estructural de la ampliación de capacidad del Bloque 6.</summary>
public sealed class BistroBuilderBlock6CapacityValidation
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport()
    {
        var builder = new StringBuilder();
        foreach (string line in Lines) builder.AppendLine(line);
        builder.Append("Resultado: ").Append(Correct).Append(" OK / ")
            .Append(Warnings).Append(" avisos / ").Append(Errors).Append(" errores.");
        return builder.ToString();
    }
}

/// <summary>
/// Comprueba capacidad, identidades y geometría sin sustituir registros canónicos.
/// </summary>
public static class BistroBuilderBlock6CapacityValidator
{    [MenuItem("Tools/Bistro Builder/Reservations/6X - Validate dining capacity", false, 601)]
    private static void ValidateFromMenu()
    {
        BistroBuilderBlock6CapacityValidation result = ValidateCurrentScene();
        if (result.Errors > 0) Debug.LogError(result.BuildReport());
        else if (result.Warnings > 0) Debug.LogWarning(result.BuildReport());
        else Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog("Bistro Builder — Capacidad Bloque 6",
            result.BuildReport(), "Aceptar");
    }

    public static BistroBuilderBlock6CapacityValidation ValidateCurrentScene()
    {
        var result = new BistroBuilderBlock6CapacityValidation();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Error(result, "La escena activa no es válida.");
            return result;
        }

        RestaurantArea dining = FindDiningArea(scene);
        Check(result, dining != null, "Existe una única área dining_main.");
        RestaurantTable[] tables = FindSceneComponents<RestaurantTable>(scene);
        RestaurantSeat[] seats = FindSceneComponents<RestaurantSeat>(scene);
        Waiter[] waiters = FindSceneComponents<Waiter>(scene);
        Check(result, tables.Length >= 10, "La sala expone al menos 10 mesas: " + tables.Length + ".");
        Check(result, seats.Length >= 28, "La sala expone al menos 28 sillas: " + seats.Length + ".");
        Check(result, waiters.Length >= 4, "Hay al menos 4 Waiter operativos reales: " + waiters.Length + ".");
        var tableIds = new HashSet<int>();
        int totalCapacity = 0;
        foreach (RestaurantTable table in tables)
        {
            if (table == null) continue;
            if (!tableIds.Add(table.TableId))
                Error(result, "TableId duplicado: " + table.TableId + ".");
            totalCapacity += Math.Max(0, table.Capacity);
            RestaurantTableSeatingConfiguration config =
                table.GetComponent<RestaurantTableSeatingConfiguration>();
            string error = string.Empty;
            if (config == null)
            {
                Error(result, table.name + " no tiene configuración de seating.");
            }
            else if (!config.ValidateConfiguration(out error))
            {
                Error(result, table.name + " tiene seating inválido: " + error);
            }
        }
        Check(result, tableIds.Count == tables.Length,
            "Todos los TableId son únicos.");
        Check(result, totalCapacity >= 28,
            "Capacidad total de comedor >= 28 clientes: " + totalCapacity + ".");

        var waiterIds = new HashSet<int>();
        foreach (Waiter waiter in waiters)
        {
            if (waiter == null) continue;
            if (!waiterIds.Add(waiter.WaiterId))
                Error(result, "WaiterId duplicado: " + waiter.WaiterId + ".");
        }
        Check(result, waiterIds.Count == waiters.Length,
            "Todos los WaiterId son únicos.");

        for (int tableId = 5; tableId <= 10; tableId++)
            ValidateExpansionTable(result, scene, dining, tableId);
        RestaurantTableSeatingConfigurationDefinition four =
            AssetDatabase.LoadAssetAtPath<
                RestaurantTableSeatingConfigurationDefinition>(
                BistroBuilderBlock6CapacityInstaller.FourSeatConfigPath);
        Check(result,
            four != null && four.MaximumCustomers == 4 &&
            four.ValidateConfiguration(2f, 1f, out _),
            "La definición de mesa de 4 plazas es canónica y válida.");

        BistroBuilderSaveDefinitionCatalog[] saveCatalogs =
            FindSceneComponents<BistroBuilderSaveDefinitionCatalog>(scene);
        bool saveCatalogOk = saveCatalogs.Length == 1 &&
            saveCatalogs[0] != null;
        if (saveCatalogOk)
        {
            saveCatalogs[0].RebuildIndex();
            saveCatalogOk = saveCatalogs[0].TryGetDefinition(
                "table_basic_4",
                out RestaurantPlaceableItemDefinition savedFourSeat) &&
                savedFourSeat != null && savedFourSeat.HasValidPrefab;
        }
        Check(result, saveCatalogOk,
            "SaveGame conoce la definición persistible table_basic_4.");

        RestaurantPlacementValidationService[] placementServices =
            FindSceneComponents<RestaurantPlacementValidationService>(scene);
        if (placementServices.Length != 1 || placementServices[0] == null)
        {
            Error(result, "No existe una autoridad única de validación de colocación.");
        }
        else
        {
            RestaurantPlacementValidationSummary placement =
                placementServices[0].ValidateAllRegisteredPlacements(true);
            Check(result, placement.IsValid,
                "Placement global limpio: " + placement.ValidCount + "/" +
                placement.TotalCount + " válidos; overlap=" +
                placement.PhysicalOverlapCount + ", clearance=" +
                placement.ClearanceViolationCount + ", constraints=" +
                placement.ConstraintViolationCount + ".");
        }
        BistroBuilderSeatingFoundationValidationResult seating =
            BistroBuilderSeatingFoundationValidator.ValidateCurrentProject();
        if (seating.ErrorCount > 0)
            Error(result, "Regresión Seating Foundation: " + seating.BuildReport());
        else
            Ok(result, "Seating Foundation permanece sin errores estructurales.");

        return result;
    }

    private static void ValidateExpansionTable(
        BistroBuilderBlock6CapacityValidation result,
        Scene scene,
        RestaurantArea dining,
        int tableId)
    {
        RestaurantTable table = null;
        foreach (RestaurantTable candidate in FindSceneComponents<RestaurantTable>(scene))
            if (candidate != null && candidate.TableId == tableId) { table = candidate; break; }
        Check(result, table != null, "Existe mesa ampliada TableId " + tableId + ".");
        if (table == null) return;

        if (dining != null)
            Check(result, dining.ContainsPosition(table.transform.position),
                table.name + " está dentro de dining_main.");
        RestaurantTableSeatingConfiguration config =
            table.GetComponent<RestaurantTableSeatingConfiguration>();
        if (config == null) return;
        var slots = new List<RestaurantTableSeatSlot>(table.Capacity);
        config.WriteCurrentSlots(slots);
        Check(result, slots.Count == table.Capacity,
            table.name + " genera exactamente " + table.Capacity + " plazas.");

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            string name = "BB_B6_Chair_T" + tableId.ToString("00") +
                          "_S" + slotIndex.ToString("00");
            RestaurantSeat seat = FindSeat(scene, name);
            Check(result, seat != null, "Existe " + name + ".");
            if (seat == null) continue;

            bool matches = config.TryEvaluateSeatAgainstSlot(
                seat,
                seat.transform.position,
                seat.transform.rotation,
                slots[slotIndex],
                out RestaurantSeatSlotMatch match);
            Check(result, matches && match.IsValid,
                name + " coincide físicamente con su plaza canónica.");
            if (dining != null)
                Check(result, dining.ContainsPosition(seat.transform.position),
                    name + " está dentro de dining_main.");
        }
    }

    private static RestaurantSeat FindSeat(Scene scene, string name)
    {        foreach (RestaurantSeat seat in FindSceneComponents<RestaurantSeat>(scene))
            if (seat != null && string.Equals(seat.name, name, StringComparison.Ordinal))
                return seat;
        return null;
    }

    private static RestaurantArea FindDiningArea(Scene scene)
    {
        RestaurantArea found = null;
        foreach (RestaurantArea area in FindSceneComponents<RestaurantArea>(scene))
        {
            if (area == null || !string.Equals(
                    area.AreaId, "dining_main", StringComparison.Ordinal)) continue;
            if (found != null) return null;
            found = area;
        }
        return found;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results.ToArray();
    }

    private static void Check(
        BistroBuilderBlock6CapacityValidation result,
        bool condition,
        string message)
    {        if (condition) Ok(result, message);
        else Error(result, message);
    }

    private static void Ok(BistroBuilderBlock6CapacityValidation result, string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }

    private static void Error(BistroBuilderBlock6CapacityValidation result, string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
