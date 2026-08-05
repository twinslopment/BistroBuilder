using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Fachada de aplicación del editor jugable de carta 2.1E.
///
/// No contiene widgets ni escribe directamente en la carta operativa. Abre
/// una BistroBuilderMenuEditSessionService, construye fotografías inmutables
/// para presentación y canaliza todas las mutaciones a través del borrador
/// transaccional de 2.1B.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Editor Service")]
public sealed class BistroBuilderMenuEditorService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1E";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderMenuEditSessionService editSessionService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderRestaurantMenuCollectionService collectionService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderDishCategoryCatalogService categoryCatalogService;

    [SerializeField]
    private BistroBuilderMenuOfferService offerService;

    [SerializeField]
    private BistroBuilderDishAvailabilityService availabilityService;

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderMenuCommercialPolicy commercialPolicy;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges;

    private readonly List<BistroBuilderMenuItemRuntimeState> draftBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<BistroBuilderMenuItemRuntimeState> operationalBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<BistroBuilderDishDefinition> definitionBuffer =
        new List<BistroBuilderDishDefinition>(32);

    private readonly List<BistroBuilderDishCategoryDefinition> categoryBuffer =
        new List<BistroBuilderDishCategoryDefinition>(16);

    private readonly Dictionary<string, BistroBuilderMenuItemRuntimeState>
        draftByDishId =
            new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderMenuItemRuntimeState>
        operationalByDishId =
            new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, BistroBuilderDishCategoryDefinition>
        categoryById =
            new Dictionary<string, BistroBuilderDishCategoryDefinition>(
                StringComparer.Ordinal
            );

    private readonly StringBuilder recipeBuilder = new StringBuilder(512);

    private bool subscribed;
    private bool editorOpen;
    private bool externalConflict;
    private bool internalTransition;
    private int revision;

    private BistroBuilderMealServiceAvailability previewMealService =
        BistroBuilderMealServiceAvailability.Lunch;

    private BistroBuilderServiceMode previewServiceMode =
        BistroBuilderServiceMode.TableService;

    public event Action<BistroBuilderMenuEditorChangedEvent> EditorChanged;

    public BistroBuilderMenuEditSessionService EditSessionService =>
        editSessionService;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderRestaurantMenuCollectionService CollectionService =>
        collectionService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public BistroBuilderDishCategoryCatalogService CategoryCatalogService =>
        categoryCatalogService;

    public BistroBuilderMenuOfferService OfferService => offerService;

    public BistroBuilderDishAvailabilityService AvailabilityService =>
        availabilityService;

    public BistroBuilderRecipeCatalogService RecipeCatalogService =>
        recipeCatalogService;

    public BistroBuilderMenuCommercialPolicy CommercialPolicy =>
        commercialPolicy;

    public bool IsOpen => editorOpen;

    public bool HasPendingChanges =>
        editorOpen && editSessionService != null &&
        editSessionService.HasPendingChanges;

    public bool HasExternalConflict => externalConflict;

    public int Revision => revision;

    public BistroBuilderMealServiceAvailability PreviewMealService =>
        previewMealService;

    public BistroBuilderServiceMode PreviewServiceMode => previewServiceMode;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        if (editorOpen)
        {
            TryClose(true, out _);
        }

        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (editSessionService == null)
        {
            error = "Falta BistroBuilderMenuEditSessionService.";
            return false;
        }

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (collectionService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuCollectionService.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (categoryCatalogService == null)
        {
            error = "Falta BistroBuilderDishCategoryCatalogService.";
            return false;
        }

        if (offerService == null)
        {
            error = "Falta BistroBuilderMenuOfferService.";
            return false;
        }

        if (availabilityService == null)
        {
            error = "Falta BistroBuilderDishAvailabilityService.";
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (commercialPolicy == null)
        {
            error = "Falta BistroBuilderMenuCommercialPolicy.";
            return false;
        }

        if (!editSessionService.ValidateConfiguration(out error) ||
            !offerService.ValidateConfiguration(out error) ||
            !recipeCatalogService.ValidateConfiguration(out error) ||
            !commercialPolicy.TryValidate(out error))
        {
            return false;
        }

        if (!ReferenceEquals(editSessionService.MenuService, menuService) ||
            !ReferenceEquals(
                editSessionService.CollectionService,
                collectionService
            ) ||
            !ReferenceEquals(
                editSessionService.CatalogService,
                catalogService
            ) ||
            !ReferenceEquals(
                editSessionService.CategoryCatalogService,
                categoryCatalogService
            ) ||
            !ReferenceEquals(
                editSessionService.CommercialPolicy,
                commercialPolicy
            ))
        {
            error = "El editor no comparte la sesión canónica de 2.1B.";
            return false;
        }

        if (!ReferenceEquals(offerService.MenuService, menuService) ||
            !ReferenceEquals(offerService.CollectionService, collectionService) ||
            !ReferenceEquals(offerService.CatalogService, catalogService) ||
            !ReferenceEquals(
                offerService.AvailabilityService,
                availabilityService
            ))
        {
            error = "El editor no comparte la oferta canónica de 2.1C.";
            return false;
        }

        if (!ReferenceEquals(
                recipeCatalogService.DishCatalogService,
                catalogService
            ))
        {
            error = "El editor no comparte el catálogo de recetas canónico.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryOpen(out string error)
    {
        if (editorOpen)
        {
            error = string.Empty;
            return true;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (editSessionService.HasOpenSession)
        {
            error = "Existe otra sesión de edición de carta abierta.";
            return false;
        }

        internalTransition = true;

        try
        {
            if (!editSessionService.TryBeginActiveSession(out error))
            {
                return false;
            }

            previewMealService =
                BistroBuilderMenuOfferContext.IsConcreteMealService(
                    offerService.CurrentMealService
                )
                    ? offerService.CurrentMealService
                    : BistroBuilderMealServiceAvailability.Lunch;
            previewServiceMode = BistroBuilderServiceMode.TableService;
            externalConflict = false;
            editorOpen = true;
            Publish(
                BistroBuilderMenuEditorChangeType.Opened,
                string.Empty,
                "Editor de carta abierto."
            );
            error = string.Empty;
            return true;
        }
        finally
        {
            internalTransition = false;
        }
    }

    public bool TryClose(bool discardPendingChanges, out string error)
    {
        if (!editorOpen)
        {
            error = string.Empty;
            return true;
        }

        if (HasPendingChanges && !discardPendingChanges)
        {
            error = "La carta contiene cambios pendientes.";
            return false;
        }

        internalTransition = true;

        try
        {
            if (editSessionService.HasOpenSession &&
                !editSessionService.TryDiscard(out error))
            {
                return false;
            }

            editorOpen = false;
            externalConflict = false;
            Publish(
                BistroBuilderMenuEditorChangeType.Closed,
                string.Empty,
                "Editor de carta cerrado."
            );
            error = string.Empty;
            return true;
        }
        finally
        {
            internalTransition = false;
        }
    }

    public bool TryApplyAndContinue(
        out BistroBuilderMenuEditCommitResult result,
        out string error
    )
    {
        result = default(BistroBuilderMenuEditCommitResult);

        if (!EnsureOpen(out error))
        {
            return false;
        }

        if (externalConflict)
        {
            error = "La carta cambió fuera del editor. Recarga el borrador.";
            return false;
        }

        internalTransition = true;

        try
        {
            if (!editSessionService.TryCommit(out result, out error))
            {
                if (editSessionService.State ==
                    BistroBuilderMenuEditSessionState.Conflict)
                {
                    externalConflict = true;
                    Publish(
                        BistroBuilderMenuEditorChangeType.ConflictDetected,
                        string.Empty,
                        error
                    );
                }

                return false;
            }

            if (!editSessionService.TryBeginActiveSession(
                    out string reopenError
                ))
            {
                editorOpen = false;
                externalConflict = false;
                error = "La carta se aplicó, pero el editor no pudo reabrir " +
                        "un borrador limpio: " + reopenError;
                Publish(
                    BistroBuilderMenuEditorChangeType.Applied,
                    string.Empty,
                    error
                );
                return true;
            }

            externalConflict = false;
            Publish(
                BistroBuilderMenuEditorChangeType.Applied,
                string.Empty,
                result.Message
            );
            error = string.Empty;
            return true;
        }
        finally
        {
            internalTransition = false;
        }
    }

    public bool TryDiscardAndContinue(out string error)
    {
        if (!EnsureOpen(out error))
        {
            return false;
        }

        internalTransition = true;

        try
        {
            if (!editSessionService.TryDiscard(out error))
            {
                return false;
            }

            if (!editSessionService.TryBeginActiveSession(
                    out string reopenError
                ))
            {
                editorOpen = false;
                externalConflict = false;
                error = "Los cambios se descartaron, pero no pudo abrirse " +
                        "un borrador limpio: " + reopenError;
                Publish(
                    BistroBuilderMenuEditorChangeType.Closed,
                    string.Empty,
                    error
                );
                return false;
            }

            externalConflict = false;
            Publish(
                BistroBuilderMenuEditorChangeType.Discarded,
                string.Empty,
                "Cambios descartados."
            );
            error = string.Empty;
            return true;
        }
        finally
        {
            internalTransition = false;
        }
    }

    public bool TryReloadAfterConflict(out string error)
    {
        if (!EnsureOpen(out error))
        {
            return false;
        }

        internalTransition = true;

        try
        {
            if (editSessionService.HasOpenSession &&
                !editSessionService.TryDiscard(out error))
            {
                return false;
            }

            if (!editSessionService.TryBeginActiveSession(
                    out string reopenError
                ))
            {
                editorOpen = false;
                externalConflict = false;
                error = "La sesión en conflicto se cerró, pero no pudo " +
                        "recargarse: " + reopenError;
                Publish(
                    BistroBuilderMenuEditorChangeType.Closed,
                    string.Empty,
                    error
                );
                return false;
            }

            externalConflict = false;
            Publish(
                BistroBuilderMenuEditorChangeType.Reloaded,
                string.Empty,
                "Carta recargada desde el estado operativo."
            );
            error = string.Empty;
            return true;
        }
        finally
        {
            internalTransition = false;
        }
    }

    public bool TrySetPreviewContext(
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        out string error
    )
    {
        BistroBuilderMenuOfferContext context =
            new BistroBuilderMenuOfferContext(mealService, serviceMode);

        if (!context.TryValidate(out error))
        {
            return false;
        }

        if (previewMealService == mealService &&
            previewServiceMode == serviceMode)
        {
            error = string.Empty;
            return true;
        }

        previewMealService = mealService;
        previewServiceMode = serviceMode;
        Publish(
            BistroBuilderMenuEditorChangeType.ContextChanged,
            string.Empty,
            "Contexto de previsualización actualizado."
        );
        error = string.Empty;
        return true;
    }

    public BistroBuilderMenuMutationResult TryAddDish(string dishId)
    {
        return ApplyMutation(editSessionService.TryAddDish(dishId), dishId);
    }

    public BistroBuilderMenuMutationResult TryRemoveDish(string dishId)
    {
        return ApplyMutation(editSessionService.TryRemoveDish(dishId), dishId);
    }

    public BistroBuilderMenuMutationResult TrySetEnabled(
        string dishId,
        bool value
    )
    {
        return ApplyMutation(
            editSessionService.TrySetEnabled(dishId, value),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetPriceCents(
        string dishId,
        int value
    )
    {
        return ApplyMutation(
            editSessionService.TrySetPriceCents(dishId, value),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetPreparationDifficulty(
        string dishId,
        int difficulty
    )
    {
        return ApplyMutation(
            editSessionService.TrySetPreparationDifficulty(
                dishId,
                difficulty
            ),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetBasePreparationSeconds(
        string dishId,
        int preparationSeconds
    )
    {
        return ApplyMutation(
            editSessionService.TrySetBasePreparationSeconds(
                dishId,
                preparationSeconds
            ),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetAvailability(
        string dishId,
        BistroBuilderMealServiceAvailability value
    )
    {
        return ApplyMutation(
            editSessionService.TrySetAvailability(dishId, value),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetManuallySoldOut(
        string dishId,
        bool value
    )
    {
        return ApplyMutation(
            editSessionService.TrySetManuallySoldOut(dishId, value),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TrySetSignatureDish(
        string dishId,
        bool value
    )
    {
        return ApplyMutation(
            editSessionService.TrySetSignatureDish(dishId, value),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TryRestoreDishDefaults(
        string dishId
    )
    {
        return ApplyMutation(
            editSessionService.TryRestoreDishDefaults(dishId),
            dishId
        );
    }

    public BistroBuilderMenuMutationResult TryMoveDishWithinCategory(
        string dishId,
        int direction
    )
    {
        return ApplyMutation(
            editSessionService.TryMoveDishWithinCategory(dishId, direction),
            dishId
        );
    }

    public bool TryBuildSnapshot(
        List<BistroBuilderMenuEditorDishSnapshot> destination,
        out BistroBuilderMenuEditorSummarySnapshot summary,
        out string error
    )
    {
        summary = default(BistroBuilderMenuEditorSummarySnapshot);

        if (destination == null)
        {
            error = "El destino de la vista es nulo.";
            return false;
        }

        destination.Clear();

        if (!EnsureOpen(out error) || !ValidateConfiguration(out error))
        {
            return false;
        }

        if (!editSessionService.TryGetDraftSnapshot(
                draftBuffer,
                out error
            ) ||
            !menuService.TryGetSnapshot(operationalBuffer, out error))
        {
            return false;
        }

        catalogService.CopyDefinitionsTo(definitionBuffer);
        categoryCatalogService.CopyDefinitionsTo(categoryBuffer);
        RebuildIndexes();

        bool inventoryReady =
            availabilityService.ValidateRuntimeReadiness(
                out string inventoryStatus
            );

        int includedCount = 0;
        int activeCount = 0;
        int signatureCount = 0;
        int attentionCount = 0;
        string restaurantId = collectionService.ActiveRestaurantId;

        for (int index = 0; index < definitionBuffer.Count; index++)
        {
            BistroBuilderDishDefinition definition = definitionBuffer[index];

            if (definition == null)
            {
                error = "El catálogo contiene una definición nula.";
                destination.Clear();
                return false;
            }

            draftByDishId.TryGetValue(
                definition.DishId,
                out BistroBuilderMenuItemRuntimeState draftItem
            );
            operationalByDishId.TryGetValue(
                definition.DishId,
                out BistroBuilderMenuItemRuntimeState operationalItem
            );
            categoryById.TryGetValue(
                definition.CategoryId,
                out BistroBuilderDishCategoryDefinition category
            );

            bool included = draftItem != null;
            int currentPrice = included
                ? draftItem.CurrentPriceCents
                : definition.BasePriceCents;
            bool isModified = !AreEquivalent(draftItem, operationalItem);

            bool hasRecipe = recipeCatalogService.TryGetRecipeByDishId(
                definition.DishId,
                out BistroBuilderRecipeDefinition recipe
            ) && recipe != null && recipe.TryValidate(out _);
            string recipeSummary = BuildRecipeSummary(recipe, hasRecipe);

            BistroBuilderRecipeEconomicsSnapshot economics =
                default(BistroBuilderRecipeEconomicsSnapshot);
            bool hasEconomics = false;

            if (hasRecipe)
            {
                hasEconomics = BistroBuilderRecipeEconomics.TryBuildSnapshot(
                    definition,
                    recipe,
                    currentPrice,
                    out economics,
                    out _
                );
            }

            BistroBuilderMenuOfferBlockFlags blockFlags;
            BistroBuilderMenuOfferRejectionReason primaryReason;
            string availabilityMessage;
            bool isOrderable;
            bool isLowStock;
            long availablePortions;

            if (!included)
            {
                blockFlags =
                    BistroBuilderMenuOfferBlockFlags.DishNotInMenu;
                primaryReason =
                    BistroBuilderMenuOfferRejectionReason.DishNotInMenu;
                availabilityMessage =
                    "El plato no forma parte de la carta del restaurante.";
                isOrderable = false;
                isLowStock = false;
                availablePortions = 0L;
            }
            else if (!inventoryReady)
            {
                BuildAvailabilityWithoutRuntimeInventory(
                    restaurantId,
                    draftItem,
                    definition,
                    inventoryStatus,
                    out blockFlags,
                    out primaryReason,
                    out availabilityMessage
                );
                isOrderable = false;
                isLowStock = false;
                availablePortions = 0L;
            }
            else if (!availabilityService.TryEvaluateMenuItem(
                    draftItem,
                    previewMealService,
                    out BistroBuilderDishAvailabilitySnapshot availability,
                    out error
                ) ||
                !BistroBuilderMenuOfferEvaluator.TryEvaluate(
                    restaurantId,
                    draftItem,
                    definition,
                    availability,
                    commercialPolicy,
                    new BistroBuilderMenuOfferContext(
                        previewMealService,
                        previewServiceMode
                    ),
                    offerService.Revision,
                    out BistroBuilderMenuOfferItemSnapshot offer,
                    out error
                ))
            {
                destination.Clear();
                return false;
            }
            else
            {
                blockFlags = offer.BlockFlags;
                primaryReason = offer.PrimaryRejectionReason;
                availabilityMessage = offer.IsOrderable
                    ? offer.IsLowStock
                        ? availability.Reason
                        : "Disponible para nuevos pedidos."
                    : offer.RejectionMessage;
                isOrderable = offer.IsOrderable;
                isLowStock = offer.IsLowStock;
                availablePortions = availability.AvailablePortions;
            }

            int preparationDifficulty = included
                ? draftItem.ResolvePreparationDifficulty(definition)
                : definition.Complexity;
            int preparationSeconds = included
                ? draftItem.ResolveBasePreparationSeconds(definition)
                : definition.BasePreparationSeconds;

            BistroBuilderMenuEditorDishSnapshot snapshot =
                new BistroBuilderMenuEditorDishSnapshot(
                    definition.DishId,
                    definition.DisplayName,
                    definition.Description,
                    definition.CategoryId,
                    category != null
                        ? category.DisplayName
                        : definition.CategoryId,
                    category != null ? category.DisplayOrder : int.MaxValue,
                    definition.Course,
                    definition.RequiredStation,
                    definition.AllowedServiceModes,
                    definition.BasePriceCents,
                    currentPrice,
                    definition.Complexity,
                    preparationDifficulty,
                    definition.BasePreparationSeconds,
                    preparationSeconds,
                    included,
                    included && draftItem.Unlocked,
                    included && draftItem.Enabled,
                    included && draftItem.ManuallySoldOut,
                    included && draftItem.SignatureDish,
                    included
                        ? draftItem.AvailableServices
                        : definition.DefaultAvailability,
                    included ? draftItem.DisplayOrder : int.MaxValue,
                    isModified,
                    hasRecipe,
                    recipeSummary,
                    hasEconomics,
                    hasEconomics ? economics.CostPerPortionCents : 0,
                    hasEconomics ? economics.GrossMarginCents : 0,
                    hasEconomics ? economics.GrossMarginBasisPoints : 0,
                    hasEconomics
                        ? economics.MarginBand
                        : BistroBuilderRecipeMarginBand.Loss,
                    isOrderable,
                    isLowStock,
                    availablePortions,
                    blockFlags,
                    primaryReason,
                    availabilityMessage
                );

            destination.Add(snapshot);

            if (included)
            {
                includedCount++;

                if (draftItem.Enabled && draftItem.Unlocked)
                {
                    activeCount++;
                }

                if (draftItem.SignatureDish)
                {
                    signatureCount++;
                }
            }

            if (snapshot.NeedsAttention)
            {
                attentionCount++;
            }
        }

        destination.Sort(CompareSnapshots);
        summary = new BistroBuilderMenuEditorSummarySnapshot(
            restaurantId,
            previewMealService,
            previewServiceMode,
            editSessionService.State,
            editSessionService.DraftChangeCount,
            definitionBuffer.Count,
            includedCount,
            activeCount,
            signatureCount,
            attentionCount,
            externalConflict,
            inventoryReady,
            inventoryReady ? string.Empty : inventoryStatus
        );
        error = string.Empty;
        return true;
    }

    private void BuildAvailabilityWithoutRuntimeInventory(
        string restaurantId,
        BistroBuilderMenuItemRuntimeState draftItem,
        BistroBuilderDishDefinition definition,
        string inventoryStatus,
        out BistroBuilderMenuOfferBlockFlags flags,
        out BistroBuilderMenuOfferRejectionReason primary,
        out string message
    )
    {
        BistroBuilderDishAvailabilitySnapshot optimisticAvailability =
            new BistroBuilderDishAvailabilitySnapshot(
                draftItem.DishId,
                BistroBuilderDishAvailabilityState.Available,
                0L,
                string.Empty,
                0L,
                0L,
                availabilityService != null
                    ? availabilityService.Revision
                    : 0,
                string.Empty
            );

        if (BistroBuilderMenuOfferEvaluator.TryEvaluate(
                restaurantId,
                draftItem,
                definition,
                optimisticAvailability,
                commercialPolicy,
                new BistroBuilderMenuOfferContext(
                    previewMealService,
                    previewServiceMode
                ),
                offerService != null ? offerService.Revision : 0,
                out BistroBuilderMenuOfferItemSnapshot offer,
                out _
            ))
        {
            flags = offer.BlockFlags |
                    BistroBuilderMenuOfferBlockFlags.AvailabilityUnknown;
            primary = BistroBuilderMenuOfferEvaluator.ResolvePrimaryReason(
                flags
            );
            message = primary ==
                BistroBuilderMenuOfferRejectionReason.AvailabilityUnknown
                    ? string.IsNullOrWhiteSpace(inventoryStatus)
                        ? "El inventario runtime todavía no está disponible."
                        : inventoryStatus
                    : BistroBuilderMenuOfferEvaluator.BuildMessage(
                        primary,
                        optimisticAvailability
                    );
            return;
        }

        flags = BistroBuilderMenuOfferBlockFlags.AvailabilityUnknown;
        primary = BistroBuilderMenuOfferRejectionReason.AvailabilityUnknown;
        message = string.IsNullOrWhiteSpace(inventoryStatus)
            ? "El inventario runtime todavía no está disponible."
            : inventoryStatus;
    }

    private BistroBuilderMenuMutationResult ApplyMutation(
        BistroBuilderMenuMutationResult result,
        string dishId
    )
    {
        if (result.Succeeded)
        {
            Publish(
                BistroBuilderMenuEditorChangeType.DraftChanged,
                dishId,
                result.Message
            );
        }

        return result;
    }

    private bool EnsureOpen(out string error)
    {
        if (!editorOpen || editSessionService == null ||
            !editSessionService.HasOpenSession)
        {
            error = "El editor de carta no está abierto.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RebuildIndexes()
    {
        draftByDishId.Clear();
        operationalByDishId.Clear();
        categoryById.Clear();

        for (int index = 0; index < draftBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = draftBuffer[index];

            if (item != null && !draftByDishId.ContainsKey(item.DishId))
            {
                draftByDishId.Add(item.DishId, item);
            }
        }

        for (int index = 0; index < operationalBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = operationalBuffer[index];

            if (item != null &&
                !operationalByDishId.ContainsKey(item.DishId))
            {
                operationalByDishId.Add(item.DishId, item);
            }
        }

        for (int index = 0; index < categoryBuffer.Count; index++)
        {
            BistroBuilderDishCategoryDefinition category =
                categoryBuffer[index];

            if (category != null && category.Visible &&
                !categoryById.ContainsKey(category.CategoryId))
            {
                categoryById.Add(category.CategoryId, category);
            }
        }
    }

    private string BuildRecipeSummary(
        BistroBuilderRecipeDefinition recipe,
        bool valid
    )
    {
        recipeBuilder.Clear();

        if (!valid || recipe == null)
        {
            return "No existe una receta canónica válida.";
        }

        recipeBuilder.Append("Rendimiento: ");
        recipeBuilder.Append(recipe.YieldPortions);
        recipeBuilder.AppendLine(" ración(es)");

        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount line =
                recipe.Ingredients[index];

            if (line == null || line.Ingredient == null)
            {
                continue;
            }

            recipeBuilder.Append("• ");
            recipeBuilder.Append(line.Ingredient.DisplayName);
            recipeBuilder.Append(": ");
            recipeBuilder.Append(
                line.Amount.ToString(
                    "0.###",
                    CultureInfo.GetCultureInfo("es-ES")
                )
            );
            recipeBuilder.Append(" ");
            recipeBuilder.Append(GetUnitLabel(line.Unit));

            if (index < recipe.Ingredients.Count - 1)
            {
                recipeBuilder.AppendLine();
            }
        }

        return recipeBuilder.ToString();
    }

    private static string GetUnitLabel(BistroBuilderMeasurementUnit unit)
    {
        switch (unit)
        {
            case BistroBuilderMeasurementUnit.Gram:
                return "g";
            case BistroBuilderMeasurementUnit.Kilogram:
                return "kg";
            case BistroBuilderMeasurementUnit.Milliliter:
                return "ml";
            case BistroBuilderMeasurementUnit.Liter:
                return "l";
            case BistroBuilderMeasurementUnit.Unit:
                return "ud";
            case BistroBuilderMeasurementUnit.Portion:
                return "ración";
            default:
                return unit.ToString();
        }
    }

    private static bool AreEquivalent(
        BistroBuilderMenuItemRuntimeState left,
        BistroBuilderMenuItemRuntimeState right
    )
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return string.Equals(
                   left.DishId,
                   right.DishId,
                   StringComparison.Ordinal
               ) &&
               left.CurrentPriceCents == right.CurrentPriceCents &&
               left.Unlocked == right.Unlocked &&
               left.Enabled == right.Enabled &&
               left.ManuallySoldOut == right.ManuallySoldOut &&
               left.SignatureDish == right.SignatureDish &&
               left.AvailableServices == right.AvailableServices &&
               left.DisplayOrder == right.DisplayOrder &&
               left.PreparationDifficulty == right.PreparationDifficulty &&
               left.BasePreparationSeconds == right.BasePreparationSeconds;
    }

    private static int CompareSnapshots(
        BistroBuilderMenuEditorDishSnapshot left,
        BistroBuilderMenuEditorDishSnapshot right
    )
    {
        int category = left.CategoryDisplayOrder.CompareTo(
            right.CategoryDisplayOrder
        );

        if (category != 0)
        {
            return category;
        }

        int included = right.Included.CompareTo(left.Included);

        if (included != 0)
        {
            return included;
        }

        int order = left.DisplayOrder.CompareTo(right.DisplayOrder);

        if (order != 0)
        {
            return order;
        }

        int name = string.Compare(
            left.DisplayName,
            right.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );

        return name != 0
            ? name
            : string.Compare(
                left.DishId,
                right.DishId,
                StringComparison.Ordinal
            );
    }

    private void HandleSessionChanged(
        BistroBuilderMenuEditSessionChangedEvent change
    )
    {
        if (!editorOpen || internalTransition)
        {
            return;
        }

        Publish(
            BistroBuilderMenuEditorChangeType.DraftChanged,
            change.DishId,
            "El borrador de carta cambió."
        );
    }

    private void HandleMenuChanged(BistroBuilderMenuChangedEvent change)
    {
        if (!editorOpen || internalTransition ||
            editSessionService == null ||
            !editSessionService.HasOpenSession)
        {
            return;
        }

        if (menuService.Revision != editSessionService.BaseMenuRevision)
        {
            externalConflict = true;
            Publish(
                BistroBuilderMenuEditorChangeType.ConflictDetected,
                change != null ? change.DishId : string.Empty,
                "La carta cambió fuera del editor."
            );
        }
    }

    private void HandleActiveRestaurantChanged(
        string previousRestaurantId,
        string currentRestaurantId
    )
    {
        if (!editorOpen || internalTransition)
        {
            return;
        }

        externalConflict = true;
        Publish(
            BistroBuilderMenuEditorChangeType.ConflictDetected,
            string.Empty,
            "El restaurante activo cambió fuera del editor."
        );
    }

    private void HandleOfferChanged(BistroBuilderMenuOfferChangedEvent change)
    {
        if (!editorOpen || internalTransition)
        {
            return;
        }

        Publish(
            BistroBuilderMenuEditorChangeType.AvailabilityChanged,
            change.DishId,
            "La disponibilidad de la carta cambió."
        );
    }

    private void Publish(
        BistroBuilderMenuEditorChangeType changeType,
        string dishId,
        string message
    )
    {
        revision++;
        BistroBuilderMenuEditorChangedEvent change =
            new BistroBuilderMenuEditorChangedEvent(
                changeType,
                dishId,
                message,
                revision
            );
        EditorChanged?.Invoke(change);

        if (logChanges)
        {
            Debug.Log(
                "Editor de carta 2.1E: " + changeType + ". " + message,
                this
            );
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        ResolveDependencies();

        if (editSessionService != null)
        {
            editSessionService.SessionChanged += HandleSessionChanged;
        }

        if (menuService != null)
        {
            menuService.MenuChanged += HandleMenuChanged;
        }

        if (collectionService != null)
        {
            collectionService.ActiveRestaurantChanged +=
                HandleActiveRestaurantChanged;
        }

        if (offerService != null)
        {
            offerService.OfferChanged += HandleOfferChanged;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (editSessionService != null)
        {
            editSessionService.SessionChanged -= HandleSessionChanged;
        }

        if (menuService != null)
        {
            menuService.MenuChanged -= HandleMenuChanged;
        }

        if (collectionService != null)
        {
            collectionService.ActiveRestaurantChanged -=
                HandleActiveRestaurantChanged;
        }

        if (offerService != null)
        {
            offerService.OfferChanged -= HandleOfferChanged;
        }

        subscribed = false;
    }

    private void ResolveDependencies()
    {
        if (editSessionService == null)
        {
            TryGetComponent(out editSessionService);
        }

        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (collectionService == null)
        {
            TryGetComponent(out collectionService);
        }

        if (catalogService == null)
        {
            TryGetComponent(out catalogService);
        }

        if (categoryCatalogService == null)
        {
            TryGetComponent(out categoryCatalogService);
        }

        if (offerService == null)
        {
            TryGetComponent(out offerService);
        }

        if (availabilityService == null)
        {
            TryGetComponent(out availabilityService);
        }

        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }

        if (commercialPolicy == null && menuService != null)
        {
            commercialPolicy = menuService.CommercialPolicy;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
