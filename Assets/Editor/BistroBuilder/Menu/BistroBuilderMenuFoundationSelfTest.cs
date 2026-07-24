using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista de BistroBuilder 367A.
///
/// Crea objetos temporales HideAndDontSave, no modifica la escena ni las
/// partidas reales y valida dominio, catálogo, operaciones atómicas,
/// serialización y proveedor menu.state.
/// </summary>
public static class BistroBuilderMenuFoundationSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 367A Dish & Menu Self-Test";

    [MenuItem(MenuPath, false, 120)]
    private static void RunFromMenu()
    {
        TestReport report = new TestReport();
        GameObject root = null;
        BistroBuilderDishDefinition fabada = null;
        BistroBuilderDishDefinition merluza = null;
        BistroBuilderDishDefinition tarta = null;
        BistroBuilderDishCatalog catalog = null;
        BistroBuilderDishCatalog duplicateCatalog = null;

        try
        {
            TestIdUtility(report);

            fabada = CreateDefinition(
                "dish_fabada_asturiana",
                "Fabada asturiana",
                BistroBuilderDishCategory.MainCourse,
                BistroBuilderDishCourse.Main,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.HotKitchen,
                1850,
                840,
                "recipe_fabada_asturiana"
            );
            merluza = CreateDefinition(
                "dish_merluza_plancha",
                "Merluza a la plancha",
                BistroBuilderDishCategory.MainCourse,
                BistroBuilderDishCourse.Main,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.Grill,
                2100,
                720,
                "recipe_merluza_plancha"
            );
            tarta = CreateDefinition(
                "dish_tarta_queso",
                "Tarta de queso",
                BistroBuilderDishCategory.Dessert,
                BistroBuilderDishCourse.Dessert,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderKitchenStationType.Pastry,
                750,
                180,
                "recipe_tarta_queso"
            );

            report.Check(fabada.TryValidate(out _), "Fabada válida.");
            report.Check(merluza.TryValidate(out _), "Merluza válida.");
            report.Check(tarta.TryValidate(out _), "Tarta válida.");
            report.Check(
                fabada.BasePriceCents == 1850,
                "El dinero canónico se conserva en céntimos enteros."
            );
            report.Check(
                fabada.RequiredStation ==
                    BistroBuilderKitchenStationType.HotKitchen,
                "La estación de cocina queda declarada."
            );
            report.Check(
                fabada.RecipeId == "recipe_fabada_asturiana",
                "La referencia futura a receta queda estable."
            );

            catalog = CreateCatalog(fabada, merluza, tarta);
            report.Check(
                catalog.TryRebuildIndex(out _),
                "Catálogo válido reconstruido."
            );
            report.Check(
                catalog.DefinitionCount == 3,
                "Catálogo contiene tres platos."
            );
            report.Check(
                catalog.TryGetDefinition(
                    "dish_fabada_asturiana",
                    out BistroBuilderDishDefinition resolvedFabada
                ) && ReferenceEquals(resolvedFabada, fabada),
                "Búsqueda O(1) por DishId."
            );
            report.Check(
                !catalog.TryGetDefinition("dish_missing", out _),
                "DishId inexistente se rechaza."
            );

            duplicateCatalog = CreateCatalog(fabada, fabada);
            report.Check(
                !duplicateCatalog.TryRebuildIndex(out string duplicateError) &&
                duplicateError.Contains("duplicado"),
                "Catálogo rechaza DishId duplicado."
            );

            root = new GameObject("BB367A_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderSaveGameService saveGameService =
                root.AddComponent<BistroBuilderSaveGameService>();
            BistroBuilderDishCatalogService catalogService =
                root.AddComponent<BistroBuilderDishCatalogService>();
            BistroBuilderRestaurantMenuService menuService =
                root.AddComponent<BistroBuilderRestaurantMenuService>();
            BistroBuilderMenuSaveSectionProvider provider =
                root.AddComponent<BistroBuilderMenuSaveSectionProvider>();

            ConfigureReference(catalogService, "catalog", catalog);
            ConfigureBool(catalogService, "logInitialization", false);
            ConfigureReference(
                menuService,
                "catalogService",
                catalogService
            );
            ConfigureBool(
                menuService,
                "initializeCatalogDishesWhenEmpty",
                true
            );
            ConfigureBool(menuService, "defaultDishEnabled", true);
            ConfigureBool(menuService, "defaultDishUnlocked", true);
            ConfigureBool(menuService, "logChanges", false);
            ConfigureReference(
                provider,
                "saveGameService",
                saveGameService
            );
            ConfigureReference(provider, "menuService", menuService);
            ConfigureReference(
                provider,
                "catalogService",
                catalogService
            );
            ConfigureBool(provider, "logLoadSummary", false);

            report.Check(
                catalogService.ValidateConfiguration(out _),
                "Servicio de catálogo válido."
            );
            report.Check(
                menuService.RebuildRuntimeIndexAndEnsureDefaults(out _),
                "Carta runtime inicializada."
            );
            report.Check(
                menuService.ItemCount == 3,
                "Carta inicial incluye todo el catálogo."
            );
            report.Check(
                menuService.ValidateConfiguration(out _),
                "Configuración de carta válida."
            );
            report.Check(
                provider.ValidateConfiguration(out _),
                "Proveedor menu.state válido."
            );
            report.Check(
                provider.SectionId == "menu.state",
                "Identidad estable de sección."
            );
            report.Check(
                provider.SectionVersion == 1,
                "Versión inicial de sección."
            );
            report.Check(
                provider.LoadOrder == 20,
                "Orden de carga anterior a estructura."
            );
            report.Check(
                !provider.IsRequired,
                "Compatibilidad con partidas 366B."
            );

            int eventCount = 0;
            BistroBuilderMenuChangedEvent lastEvent = null;
            menuService.MenuChanged += change =>
            {
                eventCount++;
                lastEvent = change;
            };

            BistroBuilderMenuMutationResult priceResult =
                menuService.TrySetPriceCents(
                    "dish_fabada_asturiana",
                    1995
                );
            report.Check(priceResult.Succeeded, "Cambio de precio aceptado.");
            report.Check(eventCount == 1, "Cambio publica un único evento.");
            report.Check(
                lastEvent != null &&
                lastEvent.ChangeType ==
                    BistroBuilderMenuChangeType.PriceChanged,
                "Evento de precio tipado."
            );
            report.Check(
                menuService.TryGetItemSnapshot(
                    "dish_fabada_asturiana",
                    out BistroBuilderMenuItemRuntimeState fabadaState
                ) && fabadaState.CurrentPriceCents == 1995,
                "Nuevo precio visible en snapshot."
            );

            BistroBuilderMenuMutationResult invalidPrice =
                menuService.TrySetPriceCents(
                    "dish_fabada_asturiana",
                    -1
                );
            report.Check(
                !invalidPrice.Succeeded &&
                invalidPrice.FailureReason ==
                    BistroBuilderMenuMutationFailureReason.InvalidPrice,
                "Precio negativo rechazado de forma controlada."
            );
            report.Check(
                eventCount == 1,
                "Operación rechazada no publica eventos."
            );

            report.Check(
                menuService.TrySetEnabled(
                    "dish_merluza_plancha",
                    false
                ).Succeeded,
                "Desactivación de plato aceptada."
            );
            report.Check(
                !menuService.IsDishOrderable(
                    "dish_merluza_plancha",
                    BistroBuilderMealServiceAvailability.Lunch,
                    out string disabledReason
                ) && disabledReason.Contains("desactivado"),
                "Plato desactivado no puede pedirse."
            );
            report.Check(
                menuService.TrySetEnabled(
                    "dish_merluza_plancha",
                    true
                ).Succeeded,
                "Reactivación de plato aceptada."
            );
            report.Check(
                menuService.TrySetManuallySoldOut(
                    "dish_merluza_plancha",
                    true
                ).Succeeded,
                "Agotado manual aceptado."
            );
            report.Check(
                !menuService.IsDishOrderable(
                    "dish_merluza_plancha",
                    BistroBuilderMealServiceAvailability.Dinner,
                    out string soldOutReason
                ) && soldOutReason.Contains("agotado"),
                "Plato agotado no puede pedirse."
            );
            report.Check(
                menuService.TrySetManuallySoldOut(
                    "dish_merluza_plancha",
                    false
                ).Succeeded,
                "Agotado manual puede liberarse."
            );
            report.Check(
                menuService.TrySetAvailability(
                    "dish_tarta_queso",
                    BistroBuilderMealServiceAvailability.Dinner
                ).Succeeded,
                "Disponibilidad por servicio actualizada."
            );
            report.Check(
                !menuService.IsDishOrderable(
                    "dish_tarta_queso",
                    BistroBuilderMealServiceAvailability.Lunch,
                    out _
                ),
                "Plato no disponible en comida se rechaza."
            );
            report.Check(
                menuService.IsDishOrderable(
                    "dish_tarta_queso",
                    BistroBuilderMealServiceAvailability.Dinner,
                    out _
                ),
                "Plato disponible en cena se acepta."
            );
            report.Check(
                menuService.TrySetSignatureDish(
                    "dish_fabada_asturiana",
                    true
                ).Succeeded,
                "Plato firma actualizado."
            );
            report.Check(
                menuService.TrySetUnlocked(
                    "dish_tarta_queso",
                    false
                ).Succeeded,
                "Bloqueo de plato actualizado."
            );
            report.Check(
                !menuService.IsDishOrderable(
                    "dish_tarta_queso",
                    BistroBuilderMealServiceAvailability.Dinner,
                    out string lockedReason
                ) && lockedReason.Contains("desbloqueado"),
                "Plato bloqueado no puede pedirse."
            );
            report.Check(
                menuService.TrySetUnlocked(
                    "dish_tarta_queso",
                    true
                ).Succeeded,
                "Desbloqueo de plato actualizado."
            );

            report.Check(
                menuService.TryMoveDish(
                    "dish_tarta_queso",
                    0
                ).Succeeded,
                "Reordenación de carta aceptada."
            );
            report.Check(
                menuService.TryGetItemSnapshot(
                    "dish_tarta_queso",
                    out BistroBuilderMenuItemRuntimeState tartaState
                ) && tartaState.DisplayOrder == 0,
                "Orden normalizado tras mover."
            );

            List<BistroBuilderMenuItemRuntimeState> snapshot =
                new List<BistroBuilderMenuItemRuntimeState>();
            report.Check(
                menuService.TryGetSnapshot(snapshot, out _),
                "Snapshot independiente capturado."
            );
            int menuCountBeforeExternalMutation = menuService.ItemCount;
            snapshot.Clear();
            report.Check(
                menuService.ItemCount == menuCountBeforeExternalMutation,
                "Modificar el snapshot no altera la carta."
            );

            List<BistroBuilderMenuItemRuntimeState> validReplacement =
                new List<BistroBuilderMenuItemRuntimeState>
                {
                    new BistroBuilderMenuItemRuntimeState(
                        "dish_fabada_asturiana",
                        2200,
                        true,
                        true,
                        false,
                        true,
                        BistroBuilderMealServiceAvailability.Dinner,
                        0
                    ),
                    new BistroBuilderMenuItemRuntimeState(
                        "dish_tarta_queso",
                        800,
                        true,
                        true,
                        false,
                        false,
                        BistroBuilderMealServiceAvailability.Dinner,
                        1
                    )
                };
            report.Check(
                menuService.TryReplaceAll(
                    validReplacement,
                    true,
                    out _
                ),
                "Reemplazo atómico válido aceptado."
            );
            report.Check(
                menuService.ItemCount == 2,
                "Reemplazo válido cambia el número de platos."
            );

            int revisionBeforeInvalidReplace = menuService.Revision;
            List<BistroBuilderMenuItemRuntimeState> invalidReplacement =
                new List<BistroBuilderMenuItemRuntimeState>
                {
                    validReplacement[0].Clone(),
                    validReplacement[0].Clone()
                };
            report.Check(
                !menuService.TryReplaceAll(
                    invalidReplacement,
                    true,
                    out string invalidReplaceError
                ) && invalidReplaceError.Contains("duplicado"),
                "Reemplazo con duplicados rechazado."
            );
            report.Check(
                menuService.ItemCount == 2 &&
                menuService.Revision == revisionBeforeInvalidReplace,
                "Reemplazo inválido no modifica estado ni revisión."
            );

            BistroBuilderSaveCaptureContext captureContext =
                new BistroBuilderSaveCaptureContext(77);
            RunEnumerator(provider.CaptureState(captureContext));
            report.Check(
                !captureContext.HasFailed,
                "Captura menu.state completada."
            );
            report.Check(
                captureContext.State is BistroBuilderMenuSaveData,
                "Captura produce el tipo persistente esperado."
            );

            BistroBuilderMenuSaveData saveData =
                (BistroBuilderMenuSaveData)captureContext.State;
            report.Check(
                saveData.items.Count == 2,
                "Captura conserva todas las entradas."
            );
            report.Check(
                provider.ValidateState(saveData, out _),
                "Estado persistente validado."
            );

            string json = JsonUtility.ToJson(saveData, true);
            BistroBuilderMenuSaveData roundTrip =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            report.Check(
                roundTrip != null && roundTrip.items.Count == 2,
                "Round-trip JSON conserva la lista."
            );
            report.Check(
                roundTrip.items[0].currentPriceCents ==
                    saveData.items[0].currentPriceCents,
                "Round-trip JSON conserva céntimos exactos."
            );
            report.Check(
                roundTrip.items[0].dishId == saveData.items[0].dishId,
                "Round-trip JSON conserva DishId."
            );

            BistroBuilderMenuSaveData corrupted =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            corrupted.items[0].dishId = "dish_missing";
            report.Check(
                !provider.ValidateState(corrupted, out string missingError) &&
                missingError.Contains("inexistente"),
                "Persistencia rechaza referencias rotas."
            );

            menuService.ResetToCatalogDefaults();
            BistroBuilderSaveLoadContext loadContext =
                new BistroBuilderSaveLoadContext(77, false, 1);
            RunEnumerator(provider.PrepareForLoad(loadContext));
            RunEnumerator(provider.ApplyState(roundTrip, loadContext));
            report.Check(
                !loadContext.HasFailed,
                "Aplicación menu.state completada."
            );
            report.Check(
                menuService.ItemCount == 2,
                "Carga restaura el número exacto de entradas."
            );
            report.Check(
                menuService.TryGetItemSnapshot(
                    roundTrip.items[0].dishId,
                    out BistroBuilderMenuItemRuntimeState restoredItem
                ) &&
                restoredItem.CurrentPriceCents ==
                    roundTrip.items[0].currentPriceCents,
                "Carga restaura precio e identidad."
            );

            BistroBuilderMenuValidationResult projectValidation =
                BistroBuilderMenuFoundationValidator
                    .ValidateCurrentProject();
            report.Check(
                projectValidation.ErrorCount == 0,
                "Proyecto instalado supera el validador 367A."
            );
        }
        catch (Exception exception)
        {
            report.Fail("Excepción no controlada: " + exception);
        }
        finally
        {
            DestroyImmediateSafe(root);
            DestroyImmediateSafe(catalog);
            DestroyImmediateSafe(duplicateCatalog);
            DestroyImmediateSafe(fabada);
            DestroyImmediateSafe(merluza);
            DestroyImmediateSafe(tarta);
        }

        string finalReport = report.BuildReport();
        Debug.Log(finalReport);

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            finalReport,
            "Aceptar"
        );
    }

    private static void TestIdUtility(TestReport report)
    {
        report.Check(
            BistroBuilderMenuIdUtility.NormalizeStableId(
                "  Dish Fabada Asturiana  "
            ) == "dish_fabada_asturiana",
            "Normalización de ID con espacios."
        );
        report.Check(
            BistroBuilderMenuIdUtility.NormalizeStableId(
                "dish---tarta...queso"
            ) == "dish_tarta_queso",
            "Normalización compacta separadores."
        );
        report.Check(
            BistroBuilderMenuIdUtility.IsValidStableId(
                "dish_fabada_asturiana"
            ),
            "ID estable válido."
        );
        report.Check(
            !BistroBuilderMenuIdUtility.IsValidStableId(
                "Dish Fabada"
            ),
            "ID con mayúsculas y espacios rechazado."
        );
        report.Check(
            !BistroBuilderMenuIdUtility.IsValidStableId("1dish"),
            "ID que comienza por número rechazado."
        );
        report.Check(
            BistroBuilderMenuIdUtility.IsValidServiceMask(
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                false
            ),
            "Máscara de servicios combinada válida."
        );
        report.Check(
            !BistroBuilderMenuIdUtility.IsValidServiceMask(
                (BistroBuilderMealServiceAvailability)64,
                true
            ),
            "Bits de servicio desconocidos rechazados."
        );
    }

    private static BistroBuilderDishDefinition CreateDefinition(
        string dishId,
        string displayName,
        BistroBuilderDishCategory category,
        BistroBuilderDishCourse course,
        BistroBuilderMealServiceAvailability availability,
        BistroBuilderKitchenStationType station,
        int priceCents,
        int preparationSeconds,
        string recipeId
    )
    {
        BistroBuilderDishDefinition definition =
            ScriptableObject.CreateInstance<BistroBuilderDishDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;

        SerializedObject serialized = new SerializedObject(definition);
        SetString(serialized, "dishId", dishId);
        SetString(serialized, "displayName", displayName);
        SetString(serialized, "description", displayName);
        SetEnum(serialized, "category", (int)category);
        SetEnum(serialized, "course", (int)course);
        SetInt(serialized, "defaultAvailability", (int)availability);
        SetEnum(serialized, "requiredStation", (int)station);
        SetInt(serialized, "basePreparationSeconds", preparationSeconds);
        SetInt(serialized, "complexity", 3);
        SetString(serialized, "recipeId", recipeId);
        SetInt(serialized, "basePriceCents", priceCents);
        SetBool(serialized, "shareable", false);
        SetInt(serialized, "minimumConsumers", 1);
        SetInt(serialized, "maximumConsumers", 1);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return definition;
    }

    private static BistroBuilderDishCatalog CreateCatalog(
        params BistroBuilderDishDefinition[] definitions
    )
    {
        BistroBuilderDishCatalog catalog =
            ScriptableObject.CreateInstance<BistroBuilderDishCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty list = serialized.FindProperty("definitions");
        list.arraySize = definitions.Length;

        for (int index = 0; index < definitions.Length; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return catalog;
    }

    private static void ConfigureReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBool(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value
    )
    {
        serialized.FindProperty(propertyName).stringValue = value;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        serialized.FindProperty(propertyName).intValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        serialized.FindProperty(propertyName).enumValueIndex = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value
    )
    {
        serialized.FindProperty(propertyName).boolValue = value;
    }

    private static void RunEnumerator(IEnumerator routine)
    {
        if (routine == null)
        {
            return;
        }

        while (routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
            {
                RunEnumerator(nested);
            }
        }
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private sealed class TestReport
    {
        private readonly List<string> failures = new List<string>();

        public int Passed { get; private set; }

        public int Failed => failures.Count;

        public void Check(bool condition, string description)
        {
            if (condition)
            {
                Passed++;
            }
            else
            {
                Fail(description);
            }
        }

        public void Fail(string description)
        {
            failures.Add(description ?? "Fallo sin descripción.");
        }

        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 367A");
            builder.AppendLine("Pruebas superadas: " + Passed);
            builder.AppendLine("Pruebas fallidas: " + Failed);

            for (int index = 0; index < failures.Count; index++)
            {
                builder.Append("- FALLO: ");
                builder.AppendLine(failures[index]);
            }

            if (Failed == 0)
            {
                builder.Append(
                    "Catálogo, carta runtime y menu.state validados."
                );
            }

            return builder.ToString().TrimEnd();
        }
    }
}
