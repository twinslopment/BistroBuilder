using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3DValidator
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3D - Validar costes y márgenes", false, 3031)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3D",
            "Validación de costes: " + passed + " correctos, " + failed + " errores.",
            "Aceptar"
        );
        if (!ok)
        {
            Debug.LogError("3D — La validación de costes de producto ha fallado.");
        }
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report
    )
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida.", ref passed, ref failed, builder);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        bool finance3CValid = BistroBuilderFinance3CValidator.ValidateCurrentScene(
            out _, out int finance3CErrors, out _);
        Check(finance3CValid && finance3CErrors == 0,
            "3A/3B/3C permanecen íntegros y válidos.",
            ref passed, ref failed, builder);

        GameObject gameSystems = FindGameSystems(scene);
        Check(gameSystems != null,
            "Existe GameSystems canónico.", ref passed, ref failed, builder);
        if (gameSystems == null)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        BistroBuilderProductCostService[] services =
            UnityEngine.Object.FindObjectsByType<BistroBuilderProductCostService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(services.Length == 1,
            "Existe una única autoridad analítica de costes 3D.",
            ref passed, ref failed, builder);

        BistroBuilderProductCostService service =
            services.Length == 1 ? services[0] : null;
        Check(service != null && service.gameObject == gameSystems,
            "La autoridad 3D pertenece a GameSystems.",
            ref passed, ref failed, builder);

        BistroBuilderProductCostSaveSectionProvider[] providers =
            UnityEngine.Object.FindObjectsByType<BistroBuilderProductCostSaveSectionProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(providers.Length == 1 && providers[0].gameObject == gameSystems,
            "Existe un único proveedor finance.product_cost.runtime en GameSystems.",
            ref passed, ref failed, builder);

        bool referencesValid = service != null &&
            GetReference(service, "inventoryService") is BistroBuilderInventoryService &&
            GetReference(service, "recipeCatalogService") is BistroBuilderRecipeCatalogService &&
            GetReference(service, "orderSystem") is OrderSystem &&
            GetReference(service, "canonicalOrderService") is BistroBuilderCanonicalOrderService &&
            GetReference(service, "supplierReceivingBridge") is BistroBuilderSupplierReceivingBridge23L &&
            GetReference(service, "generalGameStateService") is BistroBuilderGeneralGameStateService &&
            GetReference(service, "gameClock") is GameClock &&
            GetReference(service, "saveGameService") is BistroBuilderSaveGameService;
        Check(referencesValid,
            "3D referencia exclusivamente autoridades canónicas existentes.",
            ref passed, ref failed, builder);

        string serviceError = string.Empty;
        Check(service != null && service.ValidateConfiguration(out serviceError),
            "Configuración de 3D válida" + FormatError(serviceError) + ".",
            ref passed, ref failed, builder);

        BistroBuilderProductCostSaveSectionProvider provider =
            providers.Length == 1 ? providers[0] : null;
        string providerError = string.Empty;
        Check(provider != null && provider.ValidateConfiguration(out providerError),
            "Proveedor de persistencia 3D válido" + FormatError(providerError) + ".",
            ref passed, ref failed, builder);

        BistroBuilderSaveGameService save =
            GetReference(service, "saveGameService") as BistroBuilderSaveGameService;
        bool saveRegistered = false;
        bool authoritiesSeparate = false;
        if (save != null)
        {
            save.RefreshExtensions();
            saveRegistered = save.HasProvider(
                BistroBuilderProductCostSaveSectionProvider.StableSectionId);
            authoritiesSeparate =
                save.HasProvider(BistroBuilderInventorySaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderFinanceSaveSectionProvider.StableSectionId) &&
                saveRegistered;
        }
        Check(saveRegistered,
            "SaveGameService registra finance.product_cost.runtime.",
            ref passed, ref failed, builder);
        Check(authoritiesSeparate,
            "Inventario, Proveedores, Caja y Coste de producto persisten como autoridades separadas.",
            ref passed, ref failed, builder);

        bool emptySnapshotValid =
            BistroBuilderProductCostEngine.TryValidateSnapshot(
                new BistroBuilderProductCostSnapshot(),
                out string snapshotError);
        Check(emptySnapshotValid,
            "Snapshot finance.product_cost.runtime v1 inicial válido" +
            FormatError(snapshotError) + ".",
            ref passed, ref failed, builder);

        report = BuildReport(passed, failed, builder);
        return failed == 0;
    }

    private static UnityEngine.Object GetReference(
        UnityEngine.Object target,
        string fieldName
    )
    {
        if (target == null) return null;
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(roots[index].name, "GameSystems", StringComparison.Ordinal))
            {
                return roots[index];
            }
        }
        return null;
    }

    private static string BuildReport(int passed, int failed, StringBuilder builder)
    {
        builder.Insert(0,
            "3D — VALIDACIÓN COSTES DE PRODUCTO Y MÁRGENES\n" +
            "Correctos: " + passed + "  Errores: " + failed + "\n\n");
        return builder.ToString();
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder
    )
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }

    private static string FormatError(string error)
    {
        return string.IsNullOrWhiteSpace(error) ? string.Empty : ": " + error;
    }
}
