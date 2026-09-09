using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedKitchen12ValidationResult
{
    public int Passed;
    public int Errors;
    public readonly List<string> Messages = new List<string>();

    public string BuildReport()
    {
        var text = new StringBuilder("BLOQUE 12 — VALIDACIÓN\n");
        for (int i = 0; i < Messages.Count; i++) text.AppendLine(Messages[i]);
        text.Append("Resultado: ").Append(Passed).Append(" OK / ")
            .Append(Errors).Append(" errores.");
        return text.ToString();
    }
}

public static class BistroBuilderAdvancedKitchen12Validator
{
    [MenuItem("Tools/Bistro Builder/Kitchen/12 - Validar", false, 12001)]
    private static void ValidateFromMenu()
    {
        BistroBuilderAdvancedKitchen12ValidationResult result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderAdvancedKitchen12ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedKitchen12ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        Check(scene.IsValid() && scene.isLoaded &&
            scene.path == "Assets/Scenes/Prototype_Restaurant.unity",
            "Escena canónica cargada", result);

        BistroBuilderAdvancedKitchenService advanced = Unique<BistroBuilderAdvancedKitchenService>(scene);
        KitchenSystem kitchen = Unique<KitchenSystem>(scene);
        BistroBuilderStaffService staff = Unique<BistroBuilderStaffService>(scene);
        BistroBuilderAdvancedKitchenPlayerFacade facade = Unique<BistroBuilderAdvancedKitchenPlayerFacade>(scene);
        BistroBuilderAdvancedKitchenPlayerScreen screen = Unique<BistroBuilderAdvancedKitchenPlayerScreen>(scene);
        BistroBuilderCustomerExperienceTrackingService experience =
            Unique<BistroBuilderCustomerExperienceTrackingService>(scene);

        Check(advanced != null, "Autoridad de cocina avanzada única", result);
        Check(kitchen != null && ReferenceEquals(kitchen.AdvancedKitchenService, advanced),
            "KitchenSystem delegado a Cocina 12", result);
        Check(advanced != null && advanced.ValidateConfiguration(out _),
            "Configuración operativa válida", result);
        Check(facade != null && facade.ValidateConfiguration(out _),
            "Fachada jugable válida", result);
        Check(screen != null && screen.ValidateConfiguration(out _),
            "Pantalla jugable válida", result);
        Check(experience != null && experience.ValidateConfiguration(out _),
            "Experiencia/Reputación sigue válida", result);

        BistroBuilderKitchenStationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderKitchenStationCatalog>(
                BistroBuilderAdvancedKitchen12Seed.CatalogPath);
        Check(catalog != null && catalog.TryValidate(out _),
            "Catálogo de estaciones válido", result);
        Check(catalog != null && catalog.Stations.Count >= 7,
            "Estaciones operativas instaladas", result);

        int dishCount = AssetDatabase.FindAssets("t:BistroBuilderDishDefinition").Length;
        Check(catalog != null && dishCount > 0 && catalog.Routes.Count == dishCount,
            "Todos los platos tienen ruta de cocina", result);

        Check(staff != null && staff.TryGetRoleDefinition(
                "cook", out BistroBuilderStaffRoleDefinition cookRole) &&
            cookRole != null && cookRole.active &&
            string.Equals(cookRole.operationalAdapterId,
                BistroBuilderStaffOperationalAdapterIds.CookAgent,
                StringComparison.Ordinal),
            "Rol persistente Cocinero/a instalado", result);

        BistroBuilderStaffRecruitmentProfile recruitment = Resources.Load<
            BistroBuilderStaffRecruitmentProfile>("BistroBuilder/Staff/StaffRecruitmentProfile");
        bool cookCandidate = false;
        if (recruitment != null)
            for (int i = 0; i < recruitment.EnabledRoleIds.Count; i++)
                cookCandidate |= string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(recruitment.EnabledRoleIds[i]),
                    "cook", StringComparison.Ordinal);
        Check(cookCandidate, "Cocineros disponibles en contratación", result);

        if (advanced != null && advanced.TryBuildSnapshot(
                out BistroBuilderAdvancedKitchenSnapshot snapshot, out _))
        {
            Check(snapshot.stations.Count == catalog?.Stations.Count,
                "Snapshot expone todas las estaciones", result);
            Check(snapshot.totalCapacity >= snapshot.stations.Count,
                "Capacidad de cocina coherente", result);
        }
        else
        {
            Check(false, "Snapshot expone todas las estaciones", result);
            Check(false, "Capacidad de cocina coherente", result);
        }

        if (kitchen != null && kitchen.TryCaptureRuntimeSnapshot(
                out BistroBuilderKitchenRuntimeSnapshot runtime, out _))
            Check(runtime != null && runtime.advancedEnabled && runtime.TryValidate(out _),
                "service.runtime captura Cocina 12", result);
        else
            Check(false, "service.runtime captura Cocina 12", result);

        return result;
    }

    private static void Check(
        bool condition,
        string name,
        BistroBuilderAdvancedKitchen12ValidationResult result)
    {
        if (condition) { result.Passed++; result.Messages.Add("OK — " + name); }
        else { result.Errors++; result.Messages.Add("ERROR — " + name); }
    }

    private static T Unique<T>(Scene scene) where T : Component
    {
        T found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;
                found = values[i];
                count++;
            }
        }
        return count == 1 ? found : null;
    }
}
