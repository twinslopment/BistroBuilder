using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista de 2.1B. Trabaja únicamente con objetos temporales
/// HideAndDontSave y no modifica escena, assets ni partidas reales.
/// </summary>
public static class BistroBuilderMenuFoundation21BSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1B Categories and Transactional Editing Self-Test";

    [MenuItem(MenuPath, false, 142)]
    private static void RunFromMenu()
    {
        TestReport report = new TestReport();
        GameObject root = null;
        BistroBuilderDishCatalog dishCatalog = null;
        BistroBuilderDishCategoryCatalog categoryCatalog = null;
        BistroBuilderMenuCommercialPolicy policy = null;
        BistroBuilderDishDefinition[] dishes = null;
        BistroBuilderDishCategoryDefinition[] categories = null;
        BistroBuilderDishDefinition futureDefinition = null;

        try
        {
            categories = new[]
            {
                CreateCategory(
                    BistroBuilderDishCategoryIdUtility.MainCourse,
                    "Platos principales",
                    10,
                    true,
                    BistroBuilderDishCategory.MainCourse
                ),
                CreateCategory(
                    BistroBuilderDishCategoryIdUtility.Dessert,
                    "Postres",
                    20,
                    true,
                    BistroBuilderDishCategory.Dessert
                ),
                CreateCategory(
                    BistroBuilderDishCategoryIdUtility.Beverage,
                    "Bebidas",
                    30,
                    true,
                    BistroBuilderDishCategory.Beverage
                ),
                CreateCategory(
                    "category_seasonal_test",
                    "Temporada",
                    40,
                    false,
                    BistroBuilderDishCategory.MainCourse
                )
            };
            categoryCatalog = CreateCategoryCatalog(categories);

            report.Check(
                categoryCatalog.TryRebuildIndex(out _),
                "Catálogo temporal de categorías válido."
            );
            report.Check(
                BistroBuilderDishCategoryIdUtility.FromLegacyCategory(
                    BistroBuilderDishCategory.MainCourse
                ) == BistroBuilderDishCategoryIdUtility.MainCourse,
                "El enum histórico resuelve un CategoryId estable."
            );
            report.Check(
                BistroBuilderDishCategoryIdUtility.TryGetLegacyCategory(
                    BistroBuilderDishCategoryIdUtility.Dessert,
                    out BistroBuilderDishCategory resolvedLegacy
                ) && resolvedLegacy == BistroBuilderDishCategory.Dessert,
                "El CategoryId canónico conserva el puente histórico."
            );
            report.Check(
                categoryCatalog.TryGetDefinition(
                    "category_seasonal_test",
                    out BistroBuilderDishCategoryDefinition customCategory
                ) && !customCategory.HasLegacyMapping,
                "El catálogo admite categorías nuevas sin ampliar el enum."
            );

            dishes = new[]
            {
                CreateDefinition(
                    "dish_21b_main_one",
                    "Principal uno",
                    BistroBuilderDishCategory.MainCourse,
                    BistroBuilderDishCategoryIdUtility.MainCourse,
                    1200,
                    BistroBuilderMealServiceAvailability.Lunch
                ),
                CreateDefinition(
                    "dish_21b_main_two",
                    "Principal dos",
                    BistroBuilderDishCategory.MainCourse,
                    BistroBuilderDishCategoryIdUtility.MainCourse,
                    1450,
                    BistroBuilderMealServiceAvailability.Dinner
                ),
                CreateDefinition(
                    "dish_21b_dessert",
                    "Postre",
                    BistroBuilderDishCategory.Dessert,
                    BistroBuilderDishCategoryIdUtility.Dessert,
                    700,
                    BistroBuilderMealServiceAvailability.All
                ),
                CreateDefinition(
                    "dish_21b_beverage",
                    "Bebida",
                    BistroBuilderDishCategory.Beverage,
                    BistroBuilderDishCategoryIdUtility.Beverage,
                    350,
                    BistroBuilderMealServiceAvailability.All
                )
            };
            dishCatalog = CreateDishCatalog(dishes);
            policy = CreatePolicy(100, 5000, 4, 2, 15000);

            report.Check(
                dishCatalog.TryRebuildIndex(out _),
                "Catálogo temporal de platos válido."
            );
            report.Check(
                policy.TryValidate(out _) &&
                policy.SignatureSelectionWeightBasisPoints == 15000,
                "Política comercial temporal válida y exacta."
            );

            for (int index = 0; index < dishes.Length; index++)
            {
                report.Check(
                    dishes[index].TryValidate(out _) &&
                    dishes[index].HasExplicitCategoryId &&
                    dishes[index].DefinitionVersion ==
                        BistroBuilderDishDefinition.CurrentDefinitionVersion,
                    "Definición versionada y categorizada: " +
                    dishes[index].DishId + "."
                );
            }

            futureDefinition = CreateDefinition(
                "dish_21b_future",
                "Plato futuro",
                BistroBuilderDishCategory.MainCourse,
                BistroBuilderDishCategoryIdUtility.MainCourse,
                1000,
                BistroBuilderMealServiceAvailability.Lunch
            );
            SetPrivateInt(
                futureDefinition,
                "definitionVersion",
                BistroBuilderDishDefinition.CurrentDefinitionVersion + 1
            );
            report.Check(
                !futureDefinition.TryValidate(out string futureError) &&
                futureError.Contains("no está soportada"),
                "Una versión futura desconocida no se interpreta en silencio."
            );

            root = new GameObject("BB_Menu_21B_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderDishCatalogService dishCatalogService =
                root.AddComponent<BistroBuilderDishCatalogService>();
            BistroBuilderRestaurantMenuService menuService =
                root.AddComponent<BistroBuilderRestaurantMenuService>();
            BistroBuilderRestaurantMenuCollectionService collectionService =
                root.AddComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >();
            BistroBuilderDishCategoryCatalogService categoryService =
                root.AddComponent<BistroBuilderDishCategoryCatalogService>();
            BistroBuilderMenuEditSessionService editSessionService =
                root.AddComponent<BistroBuilderMenuEditSessionService>();

            ConfigureReference(
                dishCatalogService,
                "catalog",
                dishCatalog
            );
            ConfigureBool(
                dishCatalogService,
                "logInitialization",
                false
            );
            ConfigureReference(
                menuService,
                "catalogService",
                dishCatalogService
            );
            ConfigureReference(
                menuService,
                "commercialPolicy",
                policy
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
                collectionService,
                "menuService",
                menuService
            );
            ConfigureReference(
                collectionService,
                "catalogService",
                dishCatalogService
            );
            ConfigureBool(collectionService, "logChanges", false);
            ConfigureReference(
                categoryService,
                "catalog",
                categoryCatalog
            );
            ConfigureBool(
                categoryService,
                "logInitialization",
                false
            );
            ConfigureReference(
                editSessionService,
                "menuService",
                menuService
            );
            ConfigureReference(
                editSessionService,
                "collectionService",
                collectionService
            );
            ConfigureReference(
                editSessionService,
                "catalogService",
                dishCatalogService
            );
            ConfigureReference(
                editSessionService,
                "categoryCatalogService",
                categoryService
            );
            ConfigureReference(
                editSessionService,
                "commercialPolicy",
                policy
            );
            ConfigureBool(editSessionService, "logChanges", false);

            report.Check(
                menuService.RebuildRuntimeIndexAndEnsureDefaults(out _),
                "Carta operativa inicializada con política comercial."
            );
            report.Check(
                collectionService
                    .RebuildRuntimeIndexAndEnsurePrimaryRestaurant(out _),
                "Colección por restaurante inicializada."
            );
            report.Check(
                categoryService.ValidateConfiguration(out _) &&
                categoryService.CategoryCount == categories.Length,
                "Servicio runtime de categorías válido."
            );
            report.Check(
                editSessionService.ValidateConfiguration(out _),
                "Servicio de edición transaccional válido."
            );
            report.Check(
                menuService.ItemCount == 4 &&
                collectionService.RestaurantCount == 1,
                "Carta inicial completa y vinculada al restaurante principal."
            );

            int sessionEventCount = 0;
            int menuEventCount = 0;
            BistroBuilderMenuChangeType lastMenuChange =
                BistroBuilderMenuChangeType.Initialized;
            editSessionService.SessionChanged += _ => sessionEventCount++;
            menuService.MenuChanged += change =>
            {
                menuEventCount++;
                lastMenuChange = change.ChangeType;
            };

            report.Check(
                editSessionService.TryBeginActiveSession(out _) &&
                editSessionService.State ==
                    BistroBuilderMenuEditSessionState.OpenClean &&
                !editSessionService.HasPendingChanges,
                "La sesión abre un borrador limpio."
            );
            report.Check(
                !editSessionService.TryBeginActiveSession(out _),
                "No pueden coexistir dos borradores locales."
            );

            int livePriceBeforeDraft = GetPrice(
                menuService,
                "dish_21b_main_one"
            );
            report.Check(
                editSessionService.TrySetPriceCents(
                    "dish_21b_main_one",
                    1375
                ).Succeeded &&
                GetPrice(menuService, "dish_21b_main_one") ==
                    livePriceBeforeDraft,
                "El precio del borrador no contamina la carta operativa."
            );
            report.Check(
                editSessionService.TrySetPriceCents(
                    "dish_21b_main_one",
                    99
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason.InvalidPrice,
                "La sesión rechaza precios fuera de la política."
            );
            report.Check(
                editSessionService.TrySetAvailability(
                    "dish_21b_main_one",
                    (BistroBuilderMealServiceAvailability)8
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason
                        .InvalidAvailability,
                "La sesión rechaza máscaras de servicio desconocidas."
            );

            report.Check(
                editSessionService.TrySetEnabled(
                    "dish_21b_beverage",
                    false
                ).Succeeded &&
                editSessionService.TrySetSignatureDish(
                    "dish_21b_beverage",
                    true
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason.PolicyViolation &&
                editSessionService.TrySetEnabled(
                    "dish_21b_beverage",
                    true
                ).Succeeded,
                "Un candidato desactivado no puede convertirse en plato firma."
            );

            report.Check(
                editSessionService.TrySetSignatureDish(
                    "dish_21b_main_one",
                    true
                ).Succeeded &&
                editSessionService.TrySetSignatureDish(
                    "dish_21b_main_two",
                    true
                ).Succeeded,
                "Se admiten platos firma válidos hasta el límite."
            );
            report.Check(
                editSessionService.TrySetSignatureDish(
                    "dish_21b_dessert",
                    true
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason
                        .SignatureLimitReached,
                "El límite de platos firma se aplica en el borrador."
            );
            report.Check(
                editSessionService.TrySetEnabled(
                    "dish_21b_main_one",
                    false
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason.PolicyViolation &&
                editSessionService.TrySetAvailability(
                    "dish_21b_main_one",
                    BistroBuilderMealServiceAvailability.None
                ).FailureReason ==
                    BistroBuilderMenuMutationFailureReason.PolicyViolation,
                "Un plato firma no puede quedar inactivo ni sin servicio."
            );
            report.Check(
                editSessionService.TrySetManuallySoldOut(
                    "dish_21b_main_one",
                    true
                ).Succeeded,
                "El agotado temporal no elimina la identidad de plato firma."
            );

            report.Check(
                editSessionService.TryRemoveDish(
                    "dish_21b_beverage"
                ).Succeeded &&
                editSessionService.DraftItemCount == 3 &&
                editSessionService.TryAddDish(
                    "dish_21b_beverage"
                ).Succeeded &&
                editSessionService.DraftItemCount == 4,
                "Añadir y retirar platos solo modifica el borrador."
            );
            report.Check(
                editSessionService.TryMoveDish(
                    "dish_21b_beverage",
                    0
                ).Succeeded,
                "El orden de presentación puede editarse en la sesión."
            );

            List<BistroBuilderMenuItemRuntimeState> draftSnapshot =
                new List<BistroBuilderMenuItemRuntimeState>();
            report.Check(
                editSessionService.TryGetDraftSnapshot(
                    draftSnapshot,
                    out _
                ) &&
                draftSnapshot.Count == 4 &&
                draftSnapshot[0].DishId == "dish_21b_beverage" &&
                editSessionService.TryGetDraftItemSnapshot(
                    "dish_21b_beverage",
                    out BistroBuilderMenuItemRuntimeState isolatedItem
                ) &&
                !ReferenceEquals(draftSnapshot[0], isolatedItem),
                "Los snapshots del borrador son copias ordenadas e inmutables."
            );

            int baseRestaurantRevision =
                editSessionService.BaseRestaurantRevision;
            int baseMenuRevision = editSessionService.BaseMenuRevision;
            int eventsBeforeCommit = menuEventCount;
            int pendingChanges = editSessionService.DraftChangeCount;

            report.Check(
                editSessionService.TryCommit(
                    out BistroBuilderMenuEditCommitResult commit,
                    out _
                ) &&
                commit.Succeeded &&
                commit.HadChanges &&
                commit.AppliedChangeCount == pendingChanges &&
                commit.PreviousRestaurantRevision ==
                    baseRestaurantRevision &&
                commit.CurrentRestaurantRevision ==
                    baseRestaurantRevision + 1,
                "El commit aplica el borrador como una única transacción."
            );
            report.Check(
                menuService.Revision == baseMenuRevision + 1 &&
                menuEventCount == eventsBeforeCommit + 1 &&
                lastMenuChange ==
                    BistroBuilderMenuChangeType.StateReplaced,
                "El commit produce una revisión y un evento operativo."
            );
            report.Check(
                !editSessionService.HasOpenSession &&
                editSessionService.State ==
                    BistroBuilderMenuEditSessionState.Committed &&
                GetPrice(menuService, "dish_21b_main_one") == 1375 &&
                GetItem(menuService, "dish_21b_main_one").SignatureDish &&
                GetItem(menuService, "dish_21b_main_one").ManuallySoldOut,
                "El estado confirmado queda disponible en la carta activa."
            );
            report.Check(
                collectionService.TryGetRestaurantSnapshot(
                    collectionService.ActiveRestaurantId,
                    out BistroBuilderRestaurantMenuRuntimeState committedState,
                    out _
                ) &&
                committedState.Revision == baseRestaurantRevision + 1,
                "El agregado 2.1A se sincroniza en la misma transacción."
            );

            int noChangeRestaurantRevision = committedState.Revision;
            int noChangeMenuRevision = menuService.Revision;
            report.Check(
                editSessionService.TryBeginActiveSession(out _) &&
                editSessionService.TryCommit(
                    out BistroBuilderMenuEditCommitResult noChangeCommit,
                    out _
                ) &&
                noChangeCommit.Succeeded &&
                !noChangeCommit.HadChanges &&
                menuService.Revision == noChangeMenuRevision &&
                collectionService.TryGetRestaurantSnapshot(
                    collectionService.ActiveRestaurantId,
                    out BistroBuilderRestaurantMenuRuntimeState noChangeState,
                    out _
                ) &&
                noChangeState.Revision == noChangeRestaurantRevision,
                "Confirmar sin cambios no genera revisiones artificiales."
            );

            int priceBeforeDiscard = GetPrice(
                menuService,
                "dish_21b_dessert"
            );
            report.Check(
                editSessionService.TryBeginActiveSession(out _) &&
                editSessionService.TrySetPriceCents(
                    "dish_21b_dessert",
                    825
                ).Succeeded &&
                editSessionService.TryDiscard(out _) &&
                GetPrice(menuService, "dish_21b_dessert") ==
                    priceBeforeDiscard &&
                editSessionService.State ==
                    BistroBuilderMenuEditSessionState.Discarded,
                "Descartar elimina el borrador sin tocar la carta operativa."
            );

            report.Check(
                editSessionService.TryBeginActiveSession(out _),
                "Sesión preparada para probar concurrencia optimista."
            );
            BistroBuilderMenuMutationResult externalMutation =
                menuService.TrySetPriceCents(
                    "dish_21b_dessert",
                    799
                );
            bool staleCommit = editSessionService.TryCommit(
                out _,
                out string staleError
            );
            report.Check(
                externalMutation.Succeeded &&
                !staleCommit &&
                editSessionService.State ==
                    BistroBuilderMenuEditSessionState.Conflict &&
                staleError.Contains("obsoleto") &&
                GetPrice(menuService, "dish_21b_dessert") == 799,
                "Una revisión externa bloquea un commit obsoleto."
            );
            report.Check(
                editSessionService.TryDiscard(out _),
                "Un borrador en conflicto puede descartarse de forma segura."
            );

            report.Check(
                collectionService.TryCreateRestaurantFromCatalogDefaults(
                    "restaurant_21b_second",
                    false,
                    out _
                ) &&
                editSessionService.TryBeginActiveSession(out _) &&
                collectionService.TryActivateRestaurant(
                    "restaurant_21b_second",
                    out _
                ) &&
                !editSessionService.TryCommit(out _, out _) &&
                editSessionService.State ==
                    BistroBuilderMenuEditSessionState.Conflict,
                "Cambiar de restaurante invalida el borrador anterior."
            );
            editSessionService.TryDiscard(out _);
            collectionService.TryActivateRestaurant(
                BistroBuilderRestaurantMenuCollectionService
                    .DefaultRestaurantId,
                out _
            );

            if (!collectionService.TryGetRestaurantSnapshot(
                    collectionService.ActiveRestaurantId,
                    out BistroBuilderRestaurantMenuRuntimeState beforeStaleReplace,
                    out string beforeStaleError
                ))
            {
                throw new InvalidOperationException(beforeStaleError);
            }

            List<BistroBuilderMenuItemRuntimeState> replacement =
                CloneItems(beforeStaleReplace.Items);
            int stalePriceBefore = GetPrice(
                menuService,
                "dish_21b_main_two"
            );
            report.Check(
                !collectionService.TryReplaceActiveRestaurantItems(
                    replacement,
                    beforeStaleReplace.Revision + 1,
                    menuService.Revision,
                    true,
                    out _,
                    out _,
                    out _
                ) &&
                GetPrice(menuService, "dish_21b_main_two") ==
                    stalePriceBefore,
                "El reemplazo transaccional rechaza revisiones esperadas " +
                "obsoletas sin mutar el estado."
            );

            BistroBuilderMenuItemRuntimeState invalidSignature =
                new BistroBuilderMenuItemRuntimeState(
                    "dish_21b_main_one",
                    1200,
                    true,
                    false,
                    false,
                    true,
                    BistroBuilderMealServiceAvailability.Lunch,
                    0
                );
            report.Check(
                !BistroBuilderMenuPolicyEvaluator.TryValidateMenu(
                    new List<BistroBuilderMenuItemRuntimeState>
                    {
                        invalidSignature
                    },
                    policy,
                    out string invalidSignatureError
                ) &&
                invalidSignatureError.Contains("permanecer activo"),
                "La política detecta estados de plato firma incoherentes."
            );
            report.Check(
                sessionEventCount >= 10,
                "La sesión publica cambios observables sin exponer su estado."
            );
        }
        catch (Exception exception)
        {
            report.Fail("Excepción no controlada: " + exception);
        }
        finally
        {
            DestroyImmediateSafe(root);
            DestroyImmediateSafe(dishCatalog);
            DestroyImmediateSafe(categoryCatalog);
            DestroyImmediateSafe(policy);
            DestroyImmediateSafe(futureDefinition);
            DestroyAll(dishes);
            DestroyAll(categories);
        }

        string finalReport = report.BuildReport();

        if (report.Failed > 0)
        {
            Debug.LogError(finalReport);
        }
        else
        {
            Debug.Log(finalReport);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            finalReport,
            "Aceptar"
        );
    }

    private static BistroBuilderDishCategoryDefinition CreateCategory(
        string categoryId,
        string displayName,
        int displayOrder,
        bool hasLegacyMapping,
        BistroBuilderDishCategory legacyCategory
    )
    {
        BistroBuilderDishCategoryDefinition definition =
            ScriptableObject.CreateInstance<
                BistroBuilderDishCategoryDefinition
            >();
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serialized = new SerializedObject(definition);
        SetString(serialized, "categoryId", categoryId);
        SetString(serialized, "displayName", displayName);
        SetBool(serialized, "hasLegacyMapping", hasLegacyMapping);
        SetEnum(serialized, "legacyCategory", (int)legacyCategory);
        SetInt(serialized, "displayOrder", displayOrder);
        SetBool(serialized, "visible", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static BistroBuilderDishCategoryCatalog CreateCategoryCatalog(
        params BistroBuilderDishCategoryDefinition[] definitions
    )
    {
        BistroBuilderDishCategoryCatalog catalog =
            ScriptableObject.CreateInstance<
                BistroBuilderDishCategoryCatalog
            >();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        ConfigureObjectList(catalog, "definitions", definitions);
        return catalog;
    }

    private static BistroBuilderDishDefinition CreateDefinition(
        string dishId,
        string displayName,
        BistroBuilderDishCategory legacyCategory,
        string categoryId,
        int priceCents,
        BistroBuilderMealServiceAvailability availability
    )
    {
        BistroBuilderDishDefinition definition =
            ScriptableObject.CreateInstance<BistroBuilderDishDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serialized = new SerializedObject(definition);
        SetInt(
            serialized,
            "definitionVersion",
            BistroBuilderDishDefinition.CurrentDefinitionVersion
        );
        SetString(serialized, "dishId", dishId);
        SetString(serialized, "displayName", displayName);
        SetString(serialized, "description", displayName);
        SetEnum(serialized, "category", (int)legacyCategory);
        SetString(serialized, "categoryId", categoryId);
        SetEnum(
            serialized,
            "course",
            legacyCategory == BistroBuilderDishCategory.Dessert
                ? (int)BistroBuilderDishCourse.Dessert
                : (int)BistroBuilderDishCourse.Main
        );
        SetInt(serialized, "defaultAvailability", (int)availability);
        SetInt(
            serialized,
            "allowedServiceModes",
            (int)BistroBuilderDishServiceModeAvailability.All
        );
        SetEnum(
            serialized,
            "requiredStation",
            (int)BistroBuilderKitchenStationType.HotKitchen
        );
        SetInt(serialized, "basePreparationSeconds", 60);
        SetInt(serialized, "complexity", 1);
        SetString(serialized, "recipeId", string.Empty);
        SetInt(serialized, "basePriceCents", priceCents);
        SetBool(serialized, "shareable", false);
        SetInt(serialized, "minimumConsumers", 1);
        SetInt(serialized, "maximumConsumers", 1);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static BistroBuilderDishCatalog CreateDishCatalog(
        params BistroBuilderDishDefinition[] definitions
    )
    {
        BistroBuilderDishCatalog catalog =
            ScriptableObject.CreateInstance<BistroBuilderDishCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        ConfigureObjectList(catalog, "definitions", definitions);
        return catalog;
    }

    private static BistroBuilderMenuCommercialPolicy CreatePolicy(
        int minimumPriceCents,
        int maximumPriceCents,
        int maximumMenuItems,
        int maximumSignatureDishes,
        int signatureWeightBasisPoints
    )
    {
        BistroBuilderMenuCommercialPolicy policy =
            ScriptableObject.CreateInstance<
                BistroBuilderMenuCommercialPolicy
            >();
        policy.hideFlags = HideFlags.HideAndDontSave;
        ConfigureInt(policy, "minimumPriceCents", minimumPriceCents);
        ConfigureInt(policy, "maximumPriceCents", maximumPriceCents);
        ConfigureInt(policy, "maximumMenuItems", maximumMenuItems);
        ConfigureInt(
            policy,
            "maximumSignatureDishes",
            maximumSignatureDishes
        );
        ConfigureBool(policy, "requireSignatureDishEnabled", true);
        ConfigureBool(policy, "requireSignatureDishUnlocked", true);
        ConfigureBool(
            policy,
            "requireSignatureDishServiceAvailability",
            true
        );
        ConfigureInt(
            policy,
            "signatureSelectionWeightBasisPoints",
            signatureWeightBasisPoints
        );
        return policy;
    }

    private static int GetPrice(
        BistroBuilderRestaurantMenuService service,
        string dishId
    )
    {
        return GetItem(service, dishId).CurrentPriceCents;
    }

    private static BistroBuilderMenuItemRuntimeState GetItem(
        BistroBuilderRestaurantMenuService service,
        string dishId
    )
    {
        if (!service.TryGetItemSnapshot(
                dishId,
                out BistroBuilderMenuItemRuntimeState item
            ))
        {
            throw new InvalidOperationException(
                "No se pudo resolver " + dishId + " en el autotest."
            );
        }

        return item;
    }

    private static List<BistroBuilderMenuItemRuntimeState> CloneItems(
        IReadOnlyList<BistroBuilderMenuItemRuntimeState> source
    )
    {
        List<BistroBuilderMenuItemRuntimeState> result =
            new List<BistroBuilderMenuItemRuntimeState>(source.Count);

        for (int index = 0; index < source.Count; index++)
        {
            result.Add(source[index].Clone());
        }

        return result;
    }

    private static void ConfigureObjectList<T>(
        UnityEngine.Object target,
        string propertyName,
        T[] values
    ) where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty list = RequireProperty(serialized, propertyName);
        list.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                values[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateInt(
        object target,
        string fieldName,
        int value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (field == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene el campo " +
                fieldName + "."
            );
        }

        field.SetValue(target, value);
    }

    private static void ConfigureReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        RequireProperty(serialized, propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBool(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        RequireProperty(serialized, propertyName).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInt(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        RequireProperty(serialized, propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value
    )
    {
        RequireProperty(serialized, propertyName).stringValue = value;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        RequireProperty(serialized, propertyName).intValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        RequireProperty(serialized, propertyName).enumValueIndex = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value
    )
    {
        RequireProperty(serialized, propertyName).boolValue = value;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " no contiene la propiedad " + propertyName + "."
            );
        }

        return property;
    }

    private static void DestroyAll<T>(T[] targets)
        where T : UnityEngine.Object
    {
        if (targets == null)
        {
            return;
        }

        for (int index = 0; index < targets.Length; index++)
        {
            DestroyImmediateSafe(targets[index]);
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
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine(
                "BISTRO BUILDER - AUTOTEST 2.1B CATEGORÍAS Y EDICIÓN " +
                "TRANSACCIONAL"
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
                    "CategoryId estables, política comercial, borradores " +
                    "aislados, commit atómico y conflictos de revisión " +
                    "validados."
                );
            }

            return builder.ToString().TrimEnd();
        }
    }
}
