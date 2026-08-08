using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderInventoryLots22AValidationResult
{
    private readonly List<string> lines = new List<string>();

    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Ok(string text)
    {
        CorrectCount++;
        lines.Add("- OK: " + text);
    }

    public void Warn(string text)
    {
        WarningCount++;
        lines.Add("- ADVERTENCIA: " + text);
    }

    public void Error(string text)
    {
        ErrorCount++;
        lines.Add("- ERROR: " + text);
    }

    public string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "BISTRO BUILDER - 2.2A LOTES, CADUCIDAD DIARIA Y FEFO"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        for (int index = 0; index < lines.Count; index++)
        {
            builder.AppendLine(lines[index]);
        }

        return builder.ToString();
    }
}

public static class BistroBuilderInventoryLots22AValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 2.2A Internal Lots, Expiration and FEFO";

    [MenuItem(MenuPath, false, 361)]
    private static void ValidateMenu()
    {
        BistroBuilderInventoryLots22AValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();
        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderInventoryLots22AValidationResult
        ValidateCurrentProject()
    {
        var result = new BistroBuilderInventoryLots22AValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        Check(
            result,
            scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "La escena activa está cargada y guardada."
        );

        BistroBuilderAvailabilityPersistenceValidationResult baseResult =
            BistroBuilderAvailabilityPersistenceValidator
                .ValidateCurrentProject();
        Check(
            result,
            baseResult.ErrorCount == 0,
            "La base validada 368EF sigue siendo compatible con 2.2A."
        );

        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(scene);
        Check(result, gameSystems != null, "Existe GameSystems.");

        BistroBuilderInventoryService[] inventories =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderInventoryService
            >(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(
            result,
            inventories.Length == 1,
            "Existe un único inventario canónico por restaurante/escena."
        );

        BistroBuilderInventoryService inventory = inventories.Length == 1
            ? inventories[0]
            : null;
        string error = string.Empty;
        Check(
            result,
            inventory != null && inventory.ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "El inventario tiene configuración válida."
                : error
        );

        BistroBuilderGeneralGameStateService[] generalStates =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderGeneralGameStateService
            >(FindObjectsInactive.Include, FindObjectsSortMode.None);
        error = string.Empty;
        Check(
            result,
            generalStates.Length == 1 &&
            generalStates[0].ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "El calendario diario autoritativo es único y válido."
                : error
        );

        BistroBuilderRecipeCatalogService[] recipeServices =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderRecipeCatalogService
            >(FindObjectsInactive.Include, FindObjectsSortMode.None);
        error = string.Empty;
        Check(
            result,
            recipeServices.Length == 1 &&
            recipeServices[0].ValidateConfiguration(out error) &&
            recipeServices[0].IngredientCount == 22,
            string.IsNullOrWhiteSpace(error)
                ? "El catálogo conserva los 22 ingredientes canónicos."
                : error
        );

        bool shelfLifeValid = recipeServices.Length == 1 &&
            ValidateShelfLives(recipeServices[0], out error);
        Check(
            result,
            shelfLifeValid,
            string.IsNullOrWhiteSpace(error)
                ? "Las vidas útiles y marcas de perecedero son coherentes."
                : error
        );

        bool hasExpiring = false;
        bool hasNonExpiring = false;
        if (recipeServices.Length == 1 &&
            recipeServices[0].IngredientCatalog != null)
        {
            IReadOnlyList<BistroBuilderIngredientDefinition> definitions =
                recipeServices[0].IngredientCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                BistroBuilderIngredientDefinition definition =
                    definitions[index];
                if (definition == null)
                {
                    continue;
                }

                hasExpiring |= definition.DefaultShelfLifeDays > 0;
                hasNonExpiring |= definition.DefaultShelfLifeDays == 0;
            }
        }
        Check(
            result,
            hasExpiring && hasNonExpiring,
            "El catálogo soporta existencias con y sin caducidad."
        );

        Check(
            result,
            Enum.IsDefined(
                typeof(BistroBuilderInventoryTransactionType),
                BistroBuilderInventoryTransactionType.Expiration
            ),
            "El libro de movimientos incorpora la salida por caducidad."
        );

        Check(
            result,
            Enum.GetValues(typeof(BistroBuilderInventoryFreshnessState))
                .Length == 5,
            "El dominio expone cinco estados de frescura comprensibles."
        );

        MethodInfo lotCopy = typeof(BistroBuilderInventoryService).GetMethod(
            "CopyLotSnapshotsTo",
            BindingFlags.Instance | BindingFlags.Public
        );
        MethodInfo shelfLifeProcess =
            typeof(BistroBuilderInventoryService).GetMethod(
                "TryProcessShelfLifeForCurrentDay",
                BindingFlags.Instance | BindingFlags.Public
            );
        Check(
            result,
            lotCopy != null && shelfLifeProcess != null,
            "La fachada de inventario expone diagnóstico de lotes y proceso diario."
        );

        Check(
            result,
            BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion == 2,
            "El snapshot de inventario usa esquema v2."
        );

        BistroBuilderInventorySaveSectionProvider[] providers =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderInventorySaveSectionProvider
            >(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(
            result,
            providers.Length == 1,
            "Existe un único proveedor inventory.canonical."
        );

        Check(
            result,
            providers.Length == 1 &&
            providers[0].SectionId ==
                BistroBuilderInventorySaveSectionProvider.StableSectionId &&
            providers[0].SectionVersion == 2 &&
            providers[0].StateType ==
                typeof(BistroBuilderInventoryRuntimeSnapshot),
            "inventory.canonical conserva identidad y eleva su versión a 2."
        );

        error = string.Empty;
        Check(
            result,
            providers.Length == 1 &&
            providers[0].ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "El proveedor de inventario puede capturar y restaurar lotes."
                : error
        );

        BistroBuilderInventoryStateV1ToV2Migration[] migrations =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderInventoryStateV1ToV2Migration
            >(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(
            result,
            migrations.Length == 1,
            "Existe una única migración inventory.canonical v1->v2."
        );

        Check(
            result,
            migrations.Length == 1 &&
            migrations[0].SectionId ==
                BistroBuilderInventorySaveSectionProvider.StableSectionId &&
            migrations[0].FromVersion == 1 &&
            migrations[0].ToVersion == 2 &&
            migrations[0].FromSerializerId ==
                BistroBuilderJsonSaveSerializer.StableSerializerId &&
            migrations[0].ToSerializerId ==
                BistroBuilderJsonSaveSerializer.StableSerializerId,
            "La migración v1->v2 es consecutiva y mantiene JSON."
        );

        BistroBuilderSaveGameService[] saveServices =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSaveGameService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        Check(
            result,
            saveServices.Length == 1,
            "Existe una única plataforma de guardado."
        );

        error = string.Empty;
        bool saveValid = false;
        if (saveServices.Length == 1)
        {
            saveServices[0].RefreshExtensions();
            saveValid = saveServices[0].ValidateConfiguration(out error) &&
                saveServices[0].HasProvider(
                    BistroBuilderInventorySaveSectionProvider.StableSectionId
                );
        }
        Check(
            result,
            saveValid,
            string.IsNullOrWhiteSpace(error)
                ? "Guardado registra inventory.canonical v2 y su migración."
                : error
        );

        BistroBuilderDishAvailabilityService availability =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderDishAvailabilityService
            >();
        error = string.Empty;
        Check(
            result,
            availability != null &&
            availability.ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "La disponibilidad de platos sigue ligada al inventario."
                : error
        );

        BistroBuilderOrderInventoryLifecycleService lifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOrderInventoryLifecycleService
            >();
        error = string.Empty;
        Check(
            result,
            lifecycle != null && lifecycle.ValidateConfiguration(out error),
            string.IsNullOrWhiteSpace(error)
                ? "Cocina y comandas conservan la fachada 368CD."
                : error
        );

        Check(
            result,
            inventory != null && inventory.OpeningStockProfile != null &&
            inventory.OpeningStockProfile.TryValidate(out error),
            "El stock inicial sigue siendo válido y podrá materializar lotes internos."
        );

        bool runtimeOrContractValid = false;
        error = string.Empty;
        if (inventory != null && inventory.IsInitialized)
        {
            runtimeOrContractValid =
                inventory.ValidateRuntimeState(out error) &&
                inventory.TryCaptureRuntimeSnapshot(
                    out BistroBuilderInventoryRuntimeSnapshot snapshot,
                    out error
                ) &&
                snapshot != null &&
                snapshot.schemaVersion == 2 &&
                snapshot.TryValidateBasic(out error);
        }
        else
        {
            runtimeOrContractValid =
                BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion ==
                    2;
        }
        Check(
            result,
            runtimeOrContractValid,
            string.IsNullOrWhiteSpace(error)
                ? "El runtime/snapshot de lotes cumple el contrato 2.2A."
                : error
        );

        Check(
            result,
            inventories.Length <= 1,
            "2.2A no introduce almacenes ni inventarios jugables adicionales."
        );

        return result;
    }

    private static bool ValidateShelfLives(
        BistroBuilderRecipeCatalogService service,
        out string error
    )
    {
        error = string.Empty;
        if (service == null || service.IngredientCatalog == null)
        {
            error = "No existe catálogo de ingredientes.";
            return false;
        }

        IReadOnlyList<BistroBuilderIngredientDefinition> definitions =
            service.IngredientCatalog.Definitions;
        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderIngredientDefinition definition = definitions[index];
            if (definition == null || !definition.TryValidate(out error))
            {
                return false;
            }

            if (definition.Perishable &&
                definition.DefaultShelfLifeDays <= 0)
            {
                error = "El perecedero " + definition.IngredientId +
                        " no tiene vida útil positiva.";
                return false;
            }
        }

        return true;
    }

    private static void Check(
        BistroBuilderInventoryLots22AValidationResult result,
        bool condition,
        string text
    )
    {
        if (condition)
        {
            result.Ok(text);
        }
        else
        {
            result.Error(text);
        }
    }
}
