using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Autotest de dominio, integración estructural y consultas inmutables 2.1C.
/// No modifica la escena ni el estado persistente de la partida.
/// </summary>
public static class BistroBuilderMenuOffer21CSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1C Unified Menu Offer Self-Test";

    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();
    private static readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [MenuItem(MenuPath, false, 152)]
    private static void Run()
    {
        passed = 0;
        failed = 0;
        messages.Clear();
        temporaryObjects.Clear();

        try
        {
            RunContextTests();
            RunEvaluatorTests();
            RunInstalledServiceTests();
        }
        catch (Exception exception)
        {
            failed++;
            messages.Add("- ERROR NO CONTROLADO: " + exception);
        }
        finally
        {
            for (int index = temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (temporaryObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        temporaryObjects[index]
                    );
                }
            }
        }

        string report =
            "BISTRO BUILDER - AUTOTEST 2.1C OFERTA UNIFICADA\n" +
            "Pruebas superadas: " + passed + "\n" +
            "Pruebas fallidas: " + failed + "\n" +
            string.Join("\n", messages);

        if (failed == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    private static void RunContextTests()
    {
        Check(
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Breakfast,
                BistroBuilderServiceMode.TableService
            ).TryValidate(out _),
            "Desayuno y mesa forman un contexto válido."
        );
        Check(
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.BarService
            ).TryValidate(out _),
            "Comida y barra forman un contexto válido."
        );
        Check(
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Dinner,
                BistroBuilderServiceMode.WaitingAtBar
            ).TryValidate(out _),
            "Cena y espera en barra forman un contexto válido."
        );
        Check(
            !new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.All,
                BistroBuilderServiceMode.TableService
            ).TryValidate(out _),
            "La oferta rechaza una máscara múltiple como servicio actual."
        );
        Check(
            !new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Lunch,
                (BistroBuilderServiceMode)99
            ).TryValidate(out _),
            "La oferta rechaza modalidades operativas desconocidas."
        );
    }

    private static void RunEvaluatorTests()
    {
        BistroBuilderDishDefinition definition = CreateDishDefinition(
            "dish_21c_test",
            BistroBuilderMealServiceAvailability.Lunch |
                BistroBuilderMealServiceAvailability.Dinner,
            BistroBuilderDishServiceModeAvailability.TableService |
                BistroBuilderDishServiceModeAvailability.BarService
        );
        BistroBuilderMenuCommercialPolicy policy = CreatePolicy(0, 5000);
        BistroBuilderMenuItemRuntimeState baseItem =
            new BistroBuilderMenuItemRuntimeState(
                definition.DishId,
                1450,
                true,
                true,
                false,
                true,
                BistroBuilderMealServiceAvailability.Lunch |
                    BistroBuilderMealServiceAvailability.Dinner,
                3
            );
        BistroBuilderMenuOfferContext lunchTable =
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.TableService
            );
        BistroBuilderDishAvailabilitySnapshot available =
            Availability(
                definition.DishId,
                BistroBuilderDishAvailabilityState.Available,
                8,
                string.Empty
            );

        Check(
            BistroBuilderMenuOfferEvaluator.TryEvaluate(
                "restaurant_test",
                baseItem,
                definition,
                available,
                policy,
                lunchTable,
                7,
                out BistroBuilderMenuOfferItemSnapshot offer,
                out _
            ),
            "El evaluador construye una oferta válida."
        );
        Check(
            offer.IsOrderable &&
            offer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.None,
            "Una entrada válida y con stock es pedible."
        );
        Check(
            offer.PriceCents == 1450 && offer.DisplayOrder == 3,
            "La oferta conserva precio exacto y orden de carta."
        );
        Check(
            offer.SignatureDish &&
            offer.CategoryId == definition.CategoryId &&
            offer.Course == definition.Course,
            "La oferta conserva plato firma, categoría y pase."
        );
        Check(
            offer.RestaurantId == "restaurant_test" &&
            offer.OfferRevision == 7,
            "La oferta conserva restaurante y revisión de lectura."
        );

        BistroBuilderDishAvailabilitySnapshot lowStock = Availability(
            definition.DishId,
            BistroBuilderDishAvailabilityState.LowStock,
            2,
            "Últimas 2 raciones."
        );
        Evaluate(
            baseItem,
            definition,
            lowStock,
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot lowStockOffer
        );
        Check(
            lowStockOffer.IsOrderable &&
            lowStockOffer.IsLowStock &&
            lowStockOffer.RejectionMessage.Contains("2"),
            "Últimas raciones siguen pedibles y conservan diagnóstico."
        );

        Evaluate(
            baseItem,
            definition,
            Availability(
                definition.DishId,
                BistroBuilderDishAvailabilityState.OutOfStock,
                0,
                "Sin stock. Ingrediente limitante: ingredient_test."
            ),
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot outOfStock
        );
        Check(
            !outOfStock.IsOrderable &&
            outOfStock.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.OutOfStock &&
            (outOfStock.BlockFlags &
             BistroBuilderMenuOfferBlockFlags.OutOfStock) != 0,
            "El agotado por inventario se tipa sin persistirlo."
        );

        BistroBuilderMenuItemRuntimeState manual = CloneWith(
            baseItem,
            manuallySoldOut: true
        );
        Evaluate(
            manual,
            definition,
            available,
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot manualOffer
        );
        Check(
            manualOffer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.ManuallySoldOut,
            "El agotado manual se distingue del agotado por inventario."
        );

        BistroBuilderMenuItemRuntimeState disabled = CloneWith(
            baseItem,
            enabled: false,
            manuallySoldOut: true
        );
        Evaluate(
            disabled,
            definition,
            Availability(
                definition.DishId,
                BistroBuilderDishAvailabilityState.OutOfStock,
                0,
                "Sin stock."
            ),
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot disabledOffer
        );
        Check(
            disabledOffer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.Disabled &&
            (disabledOffer.BlockFlags &
             BistroBuilderMenuOfferBlockFlags.ManuallySoldOut) != 0 &&
            (disabledOffer.BlockFlags &
             BistroBuilderMenuOfferBlockFlags.OutOfStock) != 0,
            "La prioridad es estable y conserva todos los bloqueos."
        );

        BistroBuilderMenuItemRuntimeState locked = CloneWith(
            baseItem,
            unlocked: false,
            enabled: false
        );
        Evaluate(
            locked,
            definition,
            available,
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot lockedOffer
        );
        Check(
            lockedOffer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.Locked,
            "Bloqueado tiene prioridad sobre desactivado."
        );

        BistroBuilderMenuOfferContext breakfastTable =
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Breakfast,
                BistroBuilderServiceMode.TableService
            );
        Evaluate(
            baseItem,
            definition,
            available,
            policy,
            breakfastTable,
            out BistroBuilderMenuOfferItemSnapshot breakfastOffer
        );
        Check(
            breakfastOffer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason
                    .UnavailableForMealService,
            "Desayuno, comida y cena se evalúan como dimensión propia."
        );

        BistroBuilderMenuOfferContext waitingContext =
            new BistroBuilderMenuOfferContext(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.WaitingAtBar
            );
        Evaluate(
            baseItem,
            definition,
            available,
            policy,
            waitingContext,
            out BistroBuilderMenuOfferItemSnapshot modeOffer
        );
        Check(
            modeOffer.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.UnsupportedServiceMode,
            "La espera en barra no puede saltarse la modalidad del plato."
        );

        Evaluate(
            baseItem,
            definition,
            Availability(
                definition.DishId,
                BistroBuilderDishAvailabilityState.InvalidRecipe,
                0,
                "Receta inválida."
            ),
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot invalidRecipe
        );
        Check(
            invalidRecipe.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.InvalidRecipe,
            "Una receta inválida produce un bloqueo tipado."
        );

        BistroBuilderMenuCommercialPolicy restrictive = CreatePolicy(
            1500,
            5000
        );
        Evaluate(
            baseItem,
            definition,
            available,
            restrictive,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot invalidPrice
        );
        Check(
            invalidPrice.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.InvalidPrice,
            "La oferta aplica la misma política comercial que el editor."
        );

        Evaluate(
            baseItem,
            null,
            available,
            policy,
            lunchTable,
            out BistroBuilderMenuOfferItemSnapshot missingDefinition
        );
        Check(
            missingDefinition.PrimaryRejectionReason ==
                BistroBuilderMenuOfferRejectionReason.MissingDefinition,
            "Una definición ausente se diagnostica sin reinterpretar DishId."
        );

        BistroBuilderDishDefinition mismatchedDefinition =
            CreateDishDefinition(
                "dish_21c_other",
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderDishServiceModeAvailability.TableService
            );
        Check(
            !BistroBuilderMenuOfferEvaluator.TryEvaluate(
                "restaurant_test",
                baseItem,
                mismatchedDefinition,
                available,
                policy,
                lunchTable,
                4,
                out _,
                out _
            ),
            "El evaluador rechaza una definición de otro DishId."
        );
        Check(
            !BistroBuilderMenuOfferEvaluator.TryEvaluate(
                "restaurant_test",
                baseItem,
                definition,
                Availability(
                    "dish_21c_other",
                    BistroBuilderDishAvailabilityState.Available,
                    5,
                    string.Empty
                ),
                policy,
                lunchTable,
                4,
                out _,
                out _
            ),
            "El evaluador rechaza disponibilidad de otro DishId."
        );
        Check(
            !BistroBuilderMenuOfferEvaluator.TryEvaluate(
                "restaurant_test",
                baseItem,
                definition,
                available,
                policy,
                lunchTable,
                -1,
                out _,
                out _
            ),
            "El evaluador rechaza revisiones negativas."
        );

        Check(
            baseItem.CurrentPriceCents == 1450 &&
            baseItem.Enabled &&
            baseItem.SignatureDish,
            "La evaluación no muta el estado runtime original."
        );
    }

    private static void RunInstalledServiceTests()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<BistroBuilderMenuOfferService> offers =
            FindSceneComponents<BistroBuilderMenuOfferService>(scene);

        Check(offers.Count == 1, "La escena contiene una única oferta 2.1C.");

        if (offers.Count != 1)
        {
            return;
        }

        BistroBuilderMenuOfferService offer = offers[0];
        Check(
            offer.ValidateConfiguration(out _),
            "La oferta instalada valida sus dependencias."
        );
        Check(
            BistroBuilderMenuOfferContext.IsConcreteMealService(
                offer.CurrentMealService
            ),
            "La oferta instalada recibe un servicio del día concreto."
        );
        Check(
            BistroBuilderMenuIdUtility.IsValidStableId(
                offer.ActiveRestaurantId
            ),
            "La oferta instalada publica el restaurante activo."
        );

        List<BistroBuilderMenuItemRuntimeState> menu =
            new List<BistroBuilderMenuItemRuntimeState>();
        Check(
            offer.MenuService.TryGetSnapshot(menu, out _) && menu.Count > 0,
            "La carta activa expone platos para construir la oferta."
        );

        GameObject runtimePreviewRoot = null;
        BistroBuilderMenuOfferService runtimeOffer = null;
        bool runtimePreviewCreated = TryCreateRuntimePreview(
            offer,
            out runtimePreviewRoot,
            out runtimeOffer,
            out string runtimePreviewError
        );
        Check(
            runtimePreviewCreated,
            string.IsNullOrWhiteSpace(runtimePreviewError)
                ? "La oferta construye una previsualización runtime aislada."
                : "La previsualización runtime falla: " +
                    runtimePreviewError
        );

        try
        {
            if (runtimePreviewCreated)
            {
                BistroBuilderServiceMode[] modes =
                {
                    BistroBuilderServiceMode.TableService,
                    BistroBuilderServiceMode.BarService,
                    BistroBuilderServiceMode.WaitingAtBar
                };

                for (int modeIndex = 0;
                     modeIndex < modes.Length;
                     modeIndex++)
                {
                    List<BistroBuilderMenuOfferItemSnapshot> all =
                        new List<BistroBuilderMenuOfferItemSnapshot>();
                    bool allSucceeded = runtimeOffer.TryGetCurrentOffer(
                        modes[modeIndex],
                        true,
                        all,
                        out _
                    );
                    Check(
                        allSucceeded && all.Count == menu.Count,
                        "La modalidad " + modes[modeIndex] +
                        " devuelve una entrada por plato al incluir bloqueados."
                    );

                    List<BistroBuilderMenuOfferItemSnapshot> orderable =
                        new List<BistroBuilderMenuOfferItemSnapshot>();
                    bool orderableSucceeded =
                        runtimeOffer.TryGetCurrentOffer(
                            modes[modeIndex],
                            false,
                            orderable,
                            out _
                        );
                    bool onlyOrderable = orderableSucceeded;

                    for (int index = 0;
                         index < orderable.Count;
                         index++)
                    {
                        onlyOrderable &= orderable[index].IsOrderable;
                        onlyOrderable &=
                            orderable[index].ServiceMode == modes[modeIndex];
                    }

                    Check(
                        onlyOrderable,
                        "La modalidad " + modes[modeIndex] +
                        " excluye todos los artículos bloqueados."
                    );
                }

                List<BistroBuilderMenuOfferItemSnapshot> tableOffer =
                    new List<BistroBuilderMenuOfferItemSnapshot>();
                bool tableSucceeded = runtimeOffer.TryGetCurrentOffer(
                    BistroBuilderServiceMode.TableService,
                    true,
                    tableOffer,
                    out _
                );
                bool queryAgreement = tableSucceeded;
                bool exactPriceAgreement = tableSucceeded;
                Dictionary<string, BistroBuilderMenuItemRuntimeState> byId =
                    new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                        StringComparer.Ordinal
                    );

                for (int index = 0; index < menu.Count; index++)
                {
                    byId[menu[index].DishId] = menu[index];
                }

                for (int index = 0; index < tableOffer.Count; index++)
                {
                    BistroBuilderMenuOfferItemSnapshot item =
                        tableOffer[index];
                    bool serviceAnswer = runtimeOffer.IsDishOrderable(
                        item.DishId,
                        item.MealService,
                        item.ServiceMode,
                        out BistroBuilderMenuOfferRejectionReason reason,
                        out _
                    );
                    queryAgreement &= serviceAnswer == item.IsOrderable;
                    queryAgreement &= item.IsOrderable
                        ? reason ==
                            BistroBuilderMenuOfferRejectionReason.None
                        : reason == item.PrimaryRejectionReason;
                    exactPriceAgreement &= byId.TryGetValue(
                        item.DishId,
                        out BistroBuilderMenuItemRuntimeState state
                    ) && state.CurrentPriceCents == item.PriceCents;
                }

                Check(
                    queryAgreement,
                    "Lista, evaluación individual y respuesta booleana son coherentes."
                );
                Check(
                    exactPriceAgreement,
                    "La oferta conserva exactamente los precios runtime en céntimos."
                );

                bool unknownOrderable = runtimeOffer.IsDishOrderable(
                    "dish_21c_missing",
                    runtimeOffer.CurrentMealService,
                    BistroBuilderServiceMode.TableService,
                    out BistroBuilderMenuOfferRejectionReason unknownReason,
                    out _
                );
                Check(
                    !unknownOrderable &&
                    unknownReason ==
                        BistroBuilderMenuOfferRejectionReason.DishNotInMenu,
                    "Un DishId ausente se distingue de un contexto inválido."
                );
            }
        }
        finally
        {
            if (runtimePreviewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimePreviewRoot);
            }
        }

        List<BistroBuilderCanonicalOrderService> orderServices =
            FindSceneComponents<BistroBuilderCanonicalOrderService>(scene);
        List<BistroBuilderOrderCompositionService> composers =
            FindSceneComponents<BistroBuilderOrderCompositionService>(scene);
        List<BistroBuilderCanonicalOrderIntegrationService> integrations =
            FindSceneComponents<
                BistroBuilderCanonicalOrderIntegrationService
            >(scene);
        List<BistroBuilderBarServiceSystem> bars =
            FindSceneComponents<BistroBuilderBarServiceSystem>(scene);

        Check(
            orderServices.Count == 1 &&
            ReferenceEquals(orderServices[0].OfferService, offer),
            "Comandas canónicas usa la oferta 2.1C."
        );
        Check(
            composers.Count == 1 &&
            ReferenceEquals(composers[0].OfferService, offer),
            "El compositor de mesa usa la oferta 2.1C."
        );
        Check(
            integrations.Count == 1 &&
            ReferenceEquals(integrations[0].OfferService, offer),
            "La integración de comandas usa la oferta 2.1C."
        );

        bool allBarsConnected = bars.Count > 0;
        for (int index = 0; index < bars.Count; index++)
        {
            allBarsConnected &= ReferenceEquals(bars[index].OfferService, offer);
        }
        Check(
            allBarsConnected,
            "Toda la barra usa la oferta 2.1C sin filtros paralelos."
        );
    }

    private static bool TryCreateRuntimePreview(
        BistroBuilderMenuOfferService sourceOffer,
        out GameObject root,
        out BistroBuilderMenuOfferService runtimeOffer,
        out string error
    )
    {
        root = null;
        runtimeOffer = null;
        error = string.Empty;

        if (sourceOffer == null || sourceOffer.AvailabilityService == null ||
            sourceOffer.MenuService == null ||
            sourceOffer.CollectionService == null ||
            sourceOffer.CatalogService == null ||
            sourceOffer.OrderIntegration == null)
        {
            error = "La oferta instalada no expone todas sus dependencias.";
            return false;
        }

        BistroBuilderInventoryService sourceInventory =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderInventoryService
            >();
        BistroBuilderRecipeCatalogService recipes =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRecipeCatalogService
            >();

        if (sourceInventory == null || recipes == null ||
            sourceInventory.OpeningStockProfile == null)
        {
            error = "No están disponibles inventario, recetas o stock " +
                    "inicial para la previsualización 2.1C.";
            return false;
        }

        try
        {
            root = new GameObject("BB_2_1C_RuntimePreview");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderInventoryService runtimeInventory =
                root.AddComponent<BistroBuilderInventoryService>();

            if (!TrySetObjectReference(
                    runtimeInventory,
                    "recipeCatalogService",
                    recipes,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeInventory,
                    "openingStockProfile",
                    sourceInventory.OpeningStockProfile,
                    out error
                ) ||
                !TrySetBoolean(
                    runtimeInventory,
                    "logInitialization",
                    false,
                    out error
                ) ||
                !runtimeInventory.TryInitialize(out error))
            {
                return false;
            }

            BistroBuilderDishAvailabilityService runtimeAvailability =
                root.AddComponent<BistroBuilderDishAvailabilityService>();

            if (!TrySetObjectReference(
                    runtimeAvailability,
                    "recipeCatalogService",
                    recipes,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "inventoryService",
                    runtimeInventory,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "menuService",
                    sourceOffer.MenuService,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "orderIntegration",
                    sourceOffer.OrderIntegration,
                    out error
                ) ||
                !TrySetInteger(
                    runtimeAvailability,
                    "lowStockPortionThreshold",
                    sourceOffer.AvailabilityService
                        .LowStockPortionThreshold,
                    out error
                ) ||
                !TrySetBoolean(
                    runtimeAvailability,
                    "logChanges",
                    false,
                    out error
                ) ||
                !runtimeAvailability.RecalculateAll(out error))
            {
                return false;
            }

            runtimeOffer = root.AddComponent<BistroBuilderMenuOfferService>();

            if (!TrySetObjectReference(
                    runtimeOffer,
                    "menuService",
                    sourceOffer.MenuService,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeOffer,
                    "collectionService",
                    sourceOffer.CollectionService,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeOffer,
                    "catalogService",
                    sourceOffer.CatalogService,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeOffer,
                    "availabilityService",
                    runtimeAvailability,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeOffer,
                    "orderIntegration",
                    sourceOffer.OrderIntegration,
                    out error
                ) ||
                !TrySetBoolean(
                    runtimeOffer,
                    "logChanges",
                    false,
                    out error
                ))
            {
                return false;
            }

            return runtimeOffer.ValidateConfiguration(out error);
        }
        catch (Exception exception)
        {
            error = "No se pudo construir la previsualización aislada de " +
                    "2.1C: " + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(error) && root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                runtimeOffer = null;
            }
        }
    }

    private static bool TrySetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.ObjectReference)
        {
            error = "No existe la referencia serializada '" + propertyName +
                    "'.";
            return false;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static bool TrySetInteger(
        UnityEngine.Object target,
        string propertyName,
        int value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.Integer)
        {
            error = "No existe el entero serializado '" + propertyName +
                    "'.";
            return false;
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static bool TrySetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.Boolean)
        {
            error = "No existe el booleano serializado '" + propertyName +
                    "'.";
            return false;
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static void Evaluate(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderDishDefinition definition,
        BistroBuilderDishAvailabilitySnapshot availability,
        BistroBuilderMenuCommercialPolicy policy,
        BistroBuilderMenuOfferContext context,
        out BistroBuilderMenuOfferItemSnapshot snapshot
    )
    {
        if (!BistroBuilderMenuOfferEvaluator.TryEvaluate(
                "restaurant_test",
                item,
                definition,
                availability,
                policy,
                context,
                4,
                out snapshot,
                out string error
            ))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static BistroBuilderMenuItemRuntimeState CloneWith(
        BistroBuilderMenuItemRuntimeState source,
        bool? unlocked = null,
        bool? enabled = null,
        bool? manuallySoldOut = null
    )
    {
        return new BistroBuilderMenuItemRuntimeState(
            source.DishId,
            source.CurrentPriceCents,
            unlocked ?? source.Unlocked,
            enabled ?? source.Enabled,
            manuallySoldOut ?? source.ManuallySoldOut,
            source.SignatureDish,
            source.AvailableServices,
            source.DisplayOrder
        );
    }

    private static BistroBuilderDishAvailabilitySnapshot Availability(
        string dishId,
        BistroBuilderDishAvailabilityState state,
        long portions,
        string reason
    )
    {
        return new BistroBuilderDishAvailabilitySnapshot(
            dishId,
            state,
            portions,
            "ingredient_test",
            portions * 1000,
            1000,
            1,
            reason
        );
    }

    private static BistroBuilderDishDefinition CreateDishDefinition(
        string dishId,
        BistroBuilderMealServiceAvailability services,
        BistroBuilderDishServiceModeAvailability modes
    )
    {
        BistroBuilderDishDefinition definition =
            ScriptableObject.CreateInstance<BistroBuilderDishDefinition>();
        temporaryObjects.Add(definition);
        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("definitionVersion").intValue =
            BistroBuilderDishDefinition.CurrentDefinitionVersion;
        serialized.FindProperty("dishId").stringValue = dishId;
        serialized.FindProperty("displayName").stringValue = "Plato test 2.1C";
        serialized.FindProperty("category").enumValueIndex =
            (int)BistroBuilderDishCategory.MainCourse;
        serialized.FindProperty("categoryId").stringValue =
            BistroBuilderDishCategoryIdUtility.MainCourse;
        serialized.FindProperty("course").enumValueIndex =
            (int)BistroBuilderDishCourse.Main;
        serialized.FindProperty("defaultAvailability").intValue =
            (int)services;
        serialized.FindProperty("allowedServiceModes").intValue =
            (int)modes;
        serialized.FindProperty("requiredStation").enumValueIndex =
            (int)BistroBuilderKitchenStationType.HotKitchen;
        serialized.FindProperty("basePreparationSeconds").intValue = 120;
        serialized.FindProperty("complexity").intValue = 2;
        serialized.FindProperty("recipeId").stringValue = "recipe_21c_test";
        serialized.FindProperty("basePriceCents").intValue = 1450;
        serialized.FindProperty("shareable").boolValue = false;
        serialized.FindProperty("minimumConsumers").intValue = 1;
        serialized.FindProperty("maximumConsumers").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static BistroBuilderMenuCommercialPolicy CreatePolicy(
        int minimumPrice,
        int maximumPrice
    )
    {
        BistroBuilderMenuCommercialPolicy policy =
            ScriptableObject.CreateInstance<
                BistroBuilderMenuCommercialPolicy
            >();
        temporaryObjects.Add(policy);
        SerializedObject serialized = new SerializedObject(policy);
        serialized.FindProperty("minimumPriceCents").intValue = minimumPrice;
        serialized.FindProperty("maximumPriceCents").intValue = maximumPrice;
        serialized.FindProperty("maximumMenuItems").intValue = 32;
        serialized.FindProperty("maximumSignatureDishes").intValue = 3;
        serialized.FindProperty("signatureSelectionWeightBasisPoints")
            .intValue = 15000;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return policy;
    }

    private static List<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new List<T>();
        T[] all = Resources.FindObjectsOfTypeAll<T>();

        for (int index = 0; index < all.Length; index++)
        {
            T component = all[index];

            if (component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component))
            {
                result.Add(component);
            }
        }

        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            messages.Add("- OK: " + message);
        }
        else
        {
            failed++;
            messages.Add("- FALLO: " + message);
        }
    }
}
