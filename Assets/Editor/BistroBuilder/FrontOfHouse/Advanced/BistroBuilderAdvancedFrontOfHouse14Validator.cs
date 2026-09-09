using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedFrontOfHouse14ValidationResult
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
        var b = new StringBuilder("BLOQUE 14 - VALIDACION\n");
        for (int i = 0; i < lines.Count; i++) b.AppendLine(lines[i]);
        b.Append("Resultado: ").Append(Passed).Append(" OK / ").Append(Errors).Append(" errores.");
        return b.ToString();
    }
}

public static class BistroBuilderAdvancedFrontOfHouse14Validator
{
    [MenuItem("Tools/Bistro Builder/Front Of House/14 - Validar", false, 14001)]
    private static void ValidateFromMenu()
    {
        BistroBuilderAdvancedFrontOfHouse14ValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport()); else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderAdvancedFrontOfHouse14ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedFrontOfHouse14ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == "Assets/Scenes/Prototype_Restaurant.unity",
            "Escena canonica cargada");
        BistroBuilderAdvancedFrontOfHouseService[] services =
            Object.FindObjectsByType<BistroBuilderAdvancedFrontOfHouseService>(FindObjectsSortMode.None);
        BistroBuilderAdvancedFrontOfHousePlayerScreen[] screens =
            Object.FindObjectsByType<BistroBuilderAdvancedFrontOfHousePlayerScreen>(FindObjectsSortMode.None);
        TableAssignmentSystem tables = Object.FindFirstObjectByType<TableAssignmentSystem>();
        CustomerWaitingAreaSystem waiting = Object.FindFirstObjectByType<CustomerWaitingAreaSystem>();
        BistroBuilderBarServiceSystem bar = Object.FindFirstObjectByType<BistroBuilderBarServiceSystem>();
        BistroBuilderReservationService reservations = Object.FindFirstObjectByType<BistroBuilderReservationService>();
        BistroBuilderAdvancedWaiterService waiters = Object.FindFirstObjectByType<BistroBuilderAdvancedWaiterService>();
        BistroBuilderAdvancedCustomerProfileService customers = Object.FindFirstObjectByType<BistroBuilderAdvancedCustomerProfileService>();
        BistroBuilderActiveServiceSaveSectionProvider persistence =
            Object.FindFirstObjectByType<BistroBuilderActiveServiceSaveSectionProvider>();

        result.Check(services.Length == 1, "Autoridad avanzada de sala unica");
        result.Check(screens.Length == 1, "UI/feedback de sala instalada");
        result.Check(tables != null && waiting != null, "Entrada y cola canonicas conectadas");
        result.Check(bar != null, "Barra canonica conectada");
        result.Check(reservations != null, "Reservas conectadas");
        result.Check(waiters != null, "Camareros 13 conectados");
        result.Check(customers != null, "Clientes avanzados conectados");
        result.Check(persistence != null, "Persistencia service.runtime disponible");

        if (services.Length == 1)
            result.Check(services[0].ValidateConfiguration(out _), "Gestion avanzada de sala operativa");
        else result.Check(false, "Gestion avanzada de sala operativa");
        if (screens.Length == 1)
            result.Check(screens[0].ValidateConfiguration(out _), "Pantalla jugable configurada");
        else result.Check(false, "Pantalla jugable configurada");

        BistroBuilderAdvancedFrontOfHouseService service = services.Length == 1 ? services[0] : null;
        result.Check(tables != null && ReadObject(tables, "advancedFrontOfHouseService") == service,
            "Asignacion de mesas delega criterios avanzados");
        result.Check(waiting != null && ReadObject(waiting, "advancedFrontOfHouseService") == service,
            "Cola visual usa la priorizacion avanzada");
        result.Check(persistence != null && ReadObject(persistence, "advancedFrontOfHouseService") == service,
            "Rotacion estrategica persistente");
        result.Check(typeof(BistroBuilderBarServiceSystem).GetMethod("TryOfferWaitingAtBar") != null,
            "Envio manual a barra disponible");
        result.Check(service != null && service.TryCaptureRuntimeSnapshot(out var snapshot, out _) &&
                     snapshot != null && snapshot.TryValidate(out _),
            "Snapshot avanzado de sala valido");
        return result;
    }

    private static Object ReadObject(Object target, string field)
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(field);
        return p != null ? p.objectReferenceValue : null;
    }
}
