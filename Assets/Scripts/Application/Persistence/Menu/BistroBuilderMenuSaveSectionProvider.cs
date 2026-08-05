using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proveedor versionado de menu.state.
///
/// V3 persiste cartas independientes por RestaurantId, el restaurante activo,
/// las entradas no resueltas y la preparación configurable 2.1F. La aplicación
/// se realiza de forma atómica a
/// través de BistroBuilderRestaurantMenuCollectionService; el servicio de
/// carta activo sigue siendo la autoridad operativa consumida por comandas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu Save Provider")]
public sealed class BistroBuilderMenuSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "menu.state";
    public const int StableSectionVersion = 3;

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderRestaurantMenuCollectionService collectionService;

    [Header("Rendimiento")]

    [SerializeField]
    [Min(1)]
    private int captureItemsPerFrame = 64;

    [Header("Depuración")]

    [SerializeField]
    private bool logLoadSummary = true;

    private readonly List<BistroBuilderRestaurantMenuRuntimeState>
        restaurantBuffer =
            new List<BistroBuilderRestaurantMenuRuntimeState>(4);

    public string SectionId => StableSectionId;

    public int SectionVersion => StableSectionVersion;

    public int LoadOrder => 20;

    // Sigue siendo opcional para abrir partidas 366/366B sin menu.state.
    public bool IsRequired => false;

    public Type StateType => typeof(BistroBuilderMenuSaveData);

    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 20;

    public int ApplyOrder => 20;

    public int FinalizeOrder => 20;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public BistroBuilderRestaurantMenuCollectionService CollectionService =>
        collectionService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (collectionService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuCollectionService.";
            return false;
        }

        if (collectionService.MenuService != menuService ||
            collectionService.CatalogService != catalogService)
        {
            error = "La colección de cartas no comparte las dependencias canónicas.";
            return false;
        }

        if (!collectionService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        if (!collectionService.TryGetAllRestaurantSnapshots(
                restaurantBuffer,
                out string snapshotError
            ))
        {
            context.Fail(snapshotError);
            yield break;
        }

        BistroBuilderMenuSaveData data = new BistroBuilderMenuSaveData
        {
            schemaVersion = StableSectionVersion,
            activeRestaurantId = collectionService.ActiveRestaurantId,
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>(
                restaurantBuffer.Count
            )
        };

        int batchSize = Mathf.Max(1, captureItemsPerFrame);
        int capturedItems = 0;

        for (int restaurantIndex = 0;
             restaurantIndex < restaurantBuffer.Count;
             restaurantIndex++)
        {
            if (context.IsCancellationRequested)
            {
                context.Fail("La captura de menu.state fue cancelada.");
                yield break;
            }

            BistroBuilderRestaurantMenuRuntimeState source =
                restaurantBuffer[restaurantIndex];
            BistroBuilderRestaurantMenuSaveData target =
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId = source.RestaurantId,
                    revision = source.Revision
                };

            for (int index = 0; index < source.Items.Count; index++)
            {
                target.items.Add(ToSaveData(source.Items[index]));
                capturedItems++;

                if (capturedItems % batchSize == 0)
                {
                    yield return null;
                }
            }

            for (int index = 0;
                 index < source.UnresolvedItems.Count;
                 index++)
            {
                target.unresolvedItems.Add(
                    ToSaveData(source.UnresolvedItems[index])
                );
                capturedItems++;

                if (capturedItems % batchSize == 0)
                {
                    yield return null;
                }
            }

            data.restaurants.Add(target);
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderMenuSaveData data))
        {
            error = "menu.state no tiene el tipo esperado.";
            return false;
        }

        if (data.schemaVersion != StableSectionVersion)
        {
            error = "La versión interna de menu.state no coincide.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(
                data.activeRestaurantId
            ))
        {
            error = "menu.state contiene un RestaurantId activo inválido.";
            return false;
        }

        if (data.restaurants == null || data.restaurants.Count == 0)
        {
            error = "menu.state no contiene cartas por restaurante.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService para validar menu.state.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        HashSet<string> restaurantIds =
            new HashSet<string>(StringComparer.Ordinal);
        bool activeFound = false;

        for (int restaurantIndex = 0;
             restaurantIndex < data.restaurants.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                data.restaurants[restaurantIndex];

            if (restaurant == null)
            {
                error = "menu.state contiene una carta de restaurante nula.";
                return false;
            }

            if (!BistroBuilderMenuIdUtility.IsValidStableId(
                    restaurant.restaurantId
                ))
            {
                error = "menu.state contiene un RestaurantId inválido.";
                return false;
            }

            if (!restaurantIds.Add(restaurant.restaurantId))
            {
                error = "menu.state contiene el RestaurantId duplicado " +
                        restaurant.restaurantId + ".";
                return false;
            }

            if (restaurant.revision < 0)
            {
                error = "menu.state contiene una revisión negativa para " +
                        restaurant.restaurantId + ".";
                return false;
            }

            if (restaurant.items == null ||
                restaurant.unresolvedItems == null)
            {
                error = "menu.state contiene listas nulas para " +
                        restaurant.restaurantId + ".";
                return false;
            }

            HashSet<string> dishIds =
                new HashSet<string>(StringComparer.Ordinal);

            if (!ValidateItems(
                    restaurant.items,
                    dishIds,
                    restaurant.restaurantId,
                    out error
                ) ||
                !ValidateItems(
                    restaurant.unresolvedItems,
                    dishIds,
                    restaurant.restaurantId,
                    out error
                ))
            {
                return false;
            }

            activeFound |= string.Equals(
                restaurant.restaurantId,
                data.activeRestaurantId,
                StringComparison.Ordinal
            );
        }

        if (!activeFound)
        {
            error = "menu.state no contiene la carta del restaurante activo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateState(state, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        BistroBuilderMenuSaveData data = (BistroBuilderMenuSaveData)state;
        List<BistroBuilderRestaurantMenuRuntimeState> replacement =
            new List<BistroBuilderRestaurantMenuRuntimeState>(
                data.restaurants.Count
            );

        int batchSize = Mathf.Max(1, context.ObjectsPerFrame);
        int convertedItems = 0;

        for (int restaurantIndex = 0;
             restaurantIndex < data.restaurants.Count;
             restaurantIndex++)
        {
            if (context.IsCancellationRequested)
            {
                context.Fail("La aplicación de menu.state fue cancelada.");
                yield break;
            }

            BistroBuilderRestaurantMenuSaveData source =
                data.restaurants[restaurantIndex];
            List<BistroBuilderMenuItemRuntimeState> resolved =
                new List<BistroBuilderMenuItemRuntimeState>(
                    source.items.Count
                );
            List<BistroBuilderMenuItemRuntimeState> unresolved =
                new List<BistroBuilderMenuItemRuntimeState>(
                    source.unresolvedItems.Count
                );

            for (int index = 0; index < source.items.Count; index++)
            {
                resolved.Add(ToRuntimeState(source.items[index]));
                convertedItems++;

                if (convertedItems % batchSize == 0)
                {
                    yield return null;
                }
            }

            for (int index = 0;
                 index < source.unresolvedItems.Count;
                 index++)
            {
                unresolved.Add(
                    ToRuntimeState(source.unresolvedItems[index])
                );
                convertedItems++;

                if (convertedItems % batchSize == 0)
                {
                    yield return null;
                }
            }

            replacement.Add(
                new BistroBuilderRestaurantMenuRuntimeState(
                    source.restaurantId,
                    source.revision,
                    resolved,
                    unresolved
                )
            );
        }

        if (!collectionService.TryReplaceAllRestaurantStates(
                replacement,
                data.activeRestaurantId,
                true,
                out string applyError
            ))
        {
            context.Fail(applyError);
        }
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || !logLoadSummary)
        {
            return;
        }

        Debug.Log(
            "menu.state v3 restaurada con " +
            collectionService.RestaurantCount + " restaurante(s), " +
            menuService.ItemCount + " plato(s) activos y " +
            collectionService.UnresolvedItemCount +
            " entrada(s) no resuelta(s).",
            this
        );
    }

    private bool ValidateItems(
        List<BistroBuilderMenuItemSaveData> items,
        HashSet<string> ids,
        string restaurantId,
        out string error
    )
    {
        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemSaveData item = items[index];

            if (item == null)
            {
                error = "menu.state contiene una entrada nula en " +
                        restaurantId + ".";
                return false;
            }

            if (!TryValidateItemStructure(item, out error))
            {
                return false;
            }

            if (!ids.Add(item.dishId))
            {
                error = "menu.state contiene el DishId duplicado " +
                        item.dishId + " en " + restaurantId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateItemStructure(
        BistroBuilderMenuItemSaveData item,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(item.dishId))
        {
            error = "menu.state contiene un DishId inválido.";
            return false;
        }

        if (item.currentPriceCents < 0 ||
            item.currentPriceCents >
                BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "menu.state contiene un precio inválido para " +
                    item.dishId + ".";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                (BistroBuilderMealServiceAvailability)item.availableServices,
                true
            ))
        {
            error = "menu.state contiene servicios inválidos para " +
                    item.dishId + ".";
            return false;
        }

        if (item.displayOrder < 0)
        {
            error = "menu.state contiene un orden negativo para " +
                    item.dishId + ".";
            return false;
        }

        bool inheritedDifficulty =
            item.preparationDifficulty ==
                BistroBuilderMenuItemRuntimeState.InheritedPreparationValue;
        bool inheritedTime =
            item.basePreparationSeconds ==
                BistroBuilderMenuItemRuntimeState.InheritedPreparationValue;

        if (inheritedDifficulty != inheritedTime)
        {
            error = "menu.state mezcla preparación heredada y explícita para " +
                    item.dishId + ".";
            return false;
        }

        if (!inheritedDifficulty &&
            (item.preparationDifficulty <
                BistroBuilderDishDefinition.MinimumPreparationDifficulty ||
             item.preparationDifficulty >
                BistroBuilderDishDefinition.MaximumPreparationDifficulty))
        {
            error = "menu.state contiene una dificultad inválida para " +
                    item.dishId + ".";
            return false;
        }

        if (!inheritedTime &&
            (item.basePreparationSeconds <
                BistroBuilderDishDefinition.MinimumPreparationSeconds ||
             item.basePreparationSeconds >
                BistroBuilderDishDefinition.MaximumPreparationSeconds))
        {
            error = "menu.state contiene un tiempo inválido para " +
                    item.dishId + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderMenuItemSaveData ToSaveData(
        BistroBuilderMenuItemRuntimeState item
    )
    {
        return new BistroBuilderMenuItemSaveData
        {
            dishId = item.DishId,
            currentPriceCents = item.CurrentPriceCents,
            unlocked = item.Unlocked,
            enabled = item.Enabled,
            manuallySoldOut = item.ManuallySoldOut,
            signatureDish = item.SignatureDish,
            availableServices = (int)item.AvailableServices,
            displayOrder = item.DisplayOrder,
            preparationDifficulty = item.PreparationDifficulty,
            basePreparationSeconds = item.BasePreparationSeconds
        };
    }

    private static BistroBuilderMenuItemRuntimeState ToRuntimeState(
        BistroBuilderMenuItemSaveData item
    )
    {
        return new BistroBuilderMenuItemRuntimeState(
            item.dishId,
            item.currentPriceCents,
            item.unlocked,
            item.enabled,
            item.manuallySoldOut,
            item.signatureDish,
            (BistroBuilderMealServiceAvailability)item.availableServices,
            item.displayOrder,
            item.preparationDifficulty,
            item.basePreparationSeconds
        );
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (catalogService == null)
        {
            TryGetComponent(out catalogService);
        }

        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (collectionService == null)
        {
            TryGetComponent(out collectionService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
