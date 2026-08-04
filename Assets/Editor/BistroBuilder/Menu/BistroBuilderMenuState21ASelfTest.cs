using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista de 2.1A. Usa objetos HideAndDontSave y no modifica
/// la escena, los assets ni las partidas reales.
/// </summary>
public static class BistroBuilderMenuState21ASelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1A Restaurant Menu State Self-Test";

    [MenuItem(MenuPath, false, 132)]
    private static void RunFromMenu()
    {
        TestReport report = new TestReport();
        GameObject root = null;
        BistroBuilderDishDefinition firstDefinition = null;
        BistroBuilderDishDefinition secondDefinition = null;
        BistroBuilderDishCatalog catalog = null;

        try
        {
            firstDefinition = CreateDefinition(
                "dish_test_first",
                "Plato de prueba uno",
                1250,
                BistroBuilderMealServiceAvailability.Lunch
            );
            secondDefinition = CreateDefinition(
                "dish_test_second",
                "Plato de prueba dos",
                840,
                BistroBuilderMealServiceAvailability.Dinner
            );
            catalog = CreateCatalog(firstDefinition, secondDefinition);

            report.Check(
                catalog.TryRebuildIndex(out _),
                "Catálogo temporal válido."
            );

            root = new GameObject("BB_Menu_21A_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderSaveGameService saveService =
                root.AddComponent<BistroBuilderSaveGameService>();
            BistroBuilderDishCatalogService catalogService =
                root.AddComponent<BistroBuilderDishCatalogService>();
            BistroBuilderRestaurantMenuService menuService =
                root.AddComponent<BistroBuilderRestaurantMenuService>();
            BistroBuilderRestaurantMenuCollectionService collectionService =
                root.AddComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >();
            BistroBuilderMenuSaveSectionProvider provider =
                root.AddComponent<BistroBuilderMenuSaveSectionProvider>();
            BistroBuilderMenuStateV1ToV2Migration migration =
                root.AddComponent<BistroBuilderMenuStateV1ToV2Migration>();

            ConfigureReference(catalogService, "catalog", catalog);
            ConfigureBool(catalogService, "logInitialization", false);
            ConfigureReference(menuService, "catalogService", catalogService);
            ConfigureBool(
                menuService,
                "initializeCatalogDishesWhenEmpty",
                true
            );
            ConfigureBool(menuService, "defaultDishEnabled", true);
            ConfigureBool(menuService, "defaultDishUnlocked", true);
            ConfigureBool(menuService, "logChanges", false);
            ConfigureReference(
                collectionService,
                "menuService",
                menuService
            );
            ConfigureReference(
                collectionService,
                "catalogService",
                catalogService
            );
            ConfigureBool(collectionService, "logChanges", false);
            ConfigureReference(provider, "saveGameService", saveService);
            ConfigureReference(provider, "menuService", menuService);
            ConfigureReference(provider, "catalogService", catalogService);
            ConfigureReference(
                provider,
                "collectionService",
                collectionService
            );
            ConfigureBool(provider, "logLoadSummary", false);

            report.Check(
                menuService.RebuildRuntimeIndexAndEnsureDefaults(out _),
                "Carta operativa inicializada."
            );
            report.Check(
                collectionService
                    .RebuildRuntimeIndexAndEnsurePrimaryRestaurant(out _),
                "Carta histórica migrada al restaurante principal."
            );
            report.Check(
                collectionService.RestaurantCount == 1 &&
                collectionService.ActiveRestaurantId ==
                    BistroBuilderRestaurantMenuCollectionService
                        .DefaultRestaurantId,
                "RestaurantId principal estable."
            );
            report.Check(
                provider.SectionVersion == 2 &&
                provider.ValidateConfiguration(out _),
                "Proveedor menu.state v2 válido."
            );

            report.Check(
                menuService.TrySetPriceCents("dish_test_first", 1495)
                    .Succeeded,
                "Precio exacto modificado en la carta principal."
            );
            report.Check(
                collectionService.TryCreateRestaurantFromCatalogDefaults(
                    "restaurant_second",
                    false,
                    out _
                ),
                "Segunda carta creada."
            );
            report.Check(
                collectionService.TryActivateRestaurant(
                    "restaurant_second",
                    out _
                ) &&
                menuService.TryGetItemSnapshot(
                    "dish_test_first",
                    out BistroBuilderMenuItemRuntimeState secondRestaurantItem
                ) &&
                secondRestaurantItem.CurrentPriceCents == 1250,
                "Las cartas no comparten precios mutables."
            );
            report.Check(
                menuService.TrySetPriceCents("dish_test_first", 1695)
                    .Succeeded &&
                collectionService.TryActivateRestaurant(
                    BistroBuilderRestaurantMenuCollectionService
                        .DefaultRestaurantId,
                    out _
                ) &&
                menuService.TryGetItemSnapshot(
                    "dish_test_first",
                    out BistroBuilderMenuItemRuntimeState primaryItem
                ) &&
                primaryItem.CurrentPriceCents == 1495,
                "El cambio de restaurante restaura el estado exacto."
            );

            BistroBuilderSaveCaptureContext captureContext =
                new BistroBuilderSaveCaptureContext(2101);
            RunEnumerator(provider.CaptureState(captureContext));
            report.Check(
                !captureContext.HasFailed &&
                captureContext.State is BistroBuilderMenuSaveData,
                "Captura v2 completada."
            );

            BistroBuilderMenuSaveData captured =
                (BistroBuilderMenuSaveData)captureContext.State;
            report.Check(
                captured.schemaVersion == 2 &&
                captured.restaurants.Count == 2,
                "Captura conserva dos restaurantes."
            );
            report.Check(
                provider.ValidateState(captured, out _),
                "Snapshot v2 validado."
            );

            string json = JsonUtility.ToJson(captured, true);
            BistroBuilderMenuSaveData roundTrip =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            report.Check(
                roundTrip.restaurants.Count == 2 &&
                roundTrip.restaurants[0].items[0].currentPriceCents ==
                    captured.restaurants[0].items[0].currentPriceCents,
                "JSON conserva estructura y céntimos."
            );

            BistroBuilderMenuSaveData invalidDuplicate =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            invalidDuplicate.restaurants[0].items.Add(
                CloneItem(invalidDuplicate.restaurants[0].items[0])
            );
            report.Check(
                !provider.ValidateState(
                    invalidDuplicate,
                    out string duplicateError
                ) && duplicateError.Contains("duplicado"),
                "Snapshot con DishId duplicado rechazado."
            );

            BistroBuilderMenuSaveData invalidActive =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            invalidActive.activeRestaurantId = "restaurant_missing";
            report.Check(
                !provider.ValidateState(invalidActive, out _),
                "Snapshot sin restaurante activo rechazado."
            );

            BistroBuilderMenuSaveData unresolved =
                JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
            unresolved.restaurants[0].items[0].dishId =
                "dish_definition_temporarily_missing";
            report.Check(
                provider.ValidateState(unresolved, out _),
                "DishId ausente se acepta para conservación."
            );
            BistroBuilderSaveLoadContext unresolvedLoad =
                new BistroBuilderSaveLoadContext(2102, false, 1);
            RunEnumerator(provider.ApplyState(unresolved, unresolvedLoad));
            report.Check(
                !unresolvedLoad.HasFailed &&
                collectionService.UnresolvedItemCount == 1 &&
                menuService.ItemCount == 1,
                "DishId ausente queda no resuelto y fuera de la oferta activa."
            );
            BistroBuilderSaveCaptureContext unresolvedCapture =
                new BistroBuilderSaveCaptureContext(2103);
            RunEnumerator(provider.CaptureState(unresolvedCapture));
            BistroBuilderMenuSaveData recaptured =
                (BistroBuilderMenuSaveData)unresolvedCapture.State;
            report.Check(
                FindUnresolvedDish(
                    recaptured,
                    "dish_definition_temporarily_missing"
                ),
                "Una captura posterior conserva íntegramente el DishId ausente."
            );

            BistroBuilderMenuSaveDataV1 legacy =
                new BistroBuilderMenuSaveDataV1
                {
                    schemaVersion = 1,
                    items = new List<BistroBuilderMenuItemSaveData>
                    {
                        new BistroBuilderMenuItemSaveData
                        {
                            dishId = "dish_test_first",
                            currentPriceCents = 1775,
                            unlocked = true,
                            enabled = false,
                            manuallySoldOut = true,
                            signatureDish = true,
                            availableServices =
                                (int)BistroBuilderMealServiceAvailability.Lunch,
                            displayOrder = 0
                        }
                    }
                };
            bool migratedSuccessfully = migration.TryMigrate(
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy)),
                out byte[] migratedPayload,
                out _
            );
            report.Check(
                migratedSuccessfully,
                "Migración v1 -> v2 completada."
            );

            BistroBuilderMenuSaveData migrated = migratedSuccessfully
                ? JsonUtility.FromJson<BistroBuilderMenuSaveData>(
                    Encoding.UTF8.GetString(migratedPayload)
                )
                : null;
            report.Check(
                migrated != null &&
                migrated.schemaVersion == 2 &&
                migrated.restaurants.Count == 1 &&
                migrated.restaurants[0].items.Count == 1,
                "Migración crea el agregado del restaurante principal."
            );
            report.Check(
                migrated != null &&
                migrated.restaurants[0].items[0].currentPriceCents == 1775 &&
                !migrated.restaurants[0].items[0].enabled &&
                migrated.restaurants[0].items[0].manuallySoldOut &&
                migrated.restaurants[0].items[0].signatureDish,
                "Migración no reinterpreta el estado histórico."
            );

            BistroBuilderMenuSaveDataV1 invalidLegacy =
                new BistroBuilderMenuSaveDataV1
                {
                    schemaVersion = 1,
                    items = new List<BistroBuilderMenuItemSaveData>
                    {
                        CloneItem(legacy.items[0]),
                        CloneItem(legacy.items[0])
                    }
                };
            report.Check(
                !migration.TryMigrate(
                    Encoding.UTF8.GetBytes(
                        JsonUtility.ToJson(invalidLegacy)
                    ),
                    out _,
                    out string legacyError
                ) && legacyError.Contains("duplicado"),
                "Migración rechaza una v1 estructuralmente corrupta."
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
            DestroyImmediateSafe(firstDefinition);
            DestroyImmediateSafe(secondDefinition);
        }

        string finalReport = report.BuildReport();
        Debug.Log(finalReport);
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            finalReport,
            "Aceptar"
        );
    }

    private static bool FindUnresolvedDish(
        BistroBuilderMenuSaveData data,
        string dishId
    )
    {
        if (data == null || data.restaurants == null)
        {
            return false;
        }

        for (int restaurantIndex = 0;
             restaurantIndex < data.restaurants.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                data.restaurants[restaurantIndex];

            if (restaurant == null || restaurant.unresolvedItems == null)
            {
                continue;
            }

            for (int index = 0;
                 index < restaurant.unresolvedItems.Count;
                 index++)
            {
                BistroBuilderMenuItemSaveData item =
                    restaurant.unresolvedItems[index];

                if (item != null && item.dishId == dishId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static BistroBuilderMenuItemSaveData CloneItem(
        BistroBuilderMenuItemSaveData source
    )
    {
        return new BistroBuilderMenuItemSaveData
        {
            dishId = source.dishId,
            currentPriceCents = source.currentPriceCents,
            unlocked = source.unlocked,
            enabled = source.enabled,
            manuallySoldOut = source.manuallySoldOut,
            signatureDish = source.signatureDish,
            availableServices = source.availableServices,
            displayOrder = source.displayOrder
        };
    }

    private static BistroBuilderDishDefinition CreateDefinition(
        string dishId,
        string displayName,
        int priceCents,
        BistroBuilderMealServiceAvailability availability
    )
    {
        BistroBuilderDishDefinition definition =
            ScriptableObject.CreateInstance<BistroBuilderDishDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serialized = new SerializedObject(definition);
        SetString(serialized, "dishId", dishId);
        SetString(serialized, "displayName", displayName);
        SetString(serialized, "description", displayName);
        SetEnum(
            serialized,
            "category",
            (int)BistroBuilderDishCategory.MainCourse
        );
        SetEnum(
            serialized,
            "course",
            (int)BistroBuilderDishCourse.Main
        );
        SetInt(serialized, "defaultAvailability", (int)availability);
        SetEnum(
            serialized,
            "requiredStation",
            (int)BistroBuilderKitchenStationType.HotKitchen
        );
        SetInt(serialized, "basePreparationSeconds", 120);
        SetInt(serialized, "complexity", 2);
        SetString(serialized, "recipeId", "recipe_" + dishId);
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
            StringBuilder builder = new StringBuilder(3072);
            builder.AppendLine(
                "BISTRO BUILDER - AUTOTEST 2.1A CARTA POR RESTAURANTE"
            );
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
                    "Aislamiento por restaurante, menu.state v2, migración y " +
                    "conservación de DishId ausentes validados."
                );
            }

            return builder.ToString().TrimEnd();
        }
    }
}
