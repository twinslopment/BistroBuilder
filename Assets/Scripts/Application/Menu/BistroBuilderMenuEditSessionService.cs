using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sesión transaccional runtime para editar la carta del restaurante activo.
///
/// Todas las operaciones modifican un borrador aislado. El estado operativo
/// solo cambia al confirmar, mediante un único TryReplaceAll y una única
/// revisión observable. El commit usa control de concurrencia optimista para
/// impedir que una UI obsoleta sobrescriba cambios externos o una carga.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Edit Session Service")]
public sealed class BistroBuilderMenuEditSessionService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1B";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderRestaurantMenuCollectionService collectionService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderDishCategoryCatalogService categoryCatalogService;

    [SerializeField]
    private BistroBuilderMenuCommercialPolicy commercialPolicy;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges = true;

    private readonly List<BistroBuilderMenuItemRuntimeState> draftItems =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly Dictionary<string, BistroBuilderMenuItemRuntimeState>
        draftByDishId =
            new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderDishDefinition> definitionBuffer =
        new List<BistroBuilderDishDefinition>(32);

    private string sessionRestaurantId = string.Empty;
    private int baseRestaurantRevision;
    private int baseMenuRevision;
    private int draftChangeCount;
    private bool sessionOpen;
    private bool dirty;
    private BistroBuilderMenuEditSessionState state =
        BistroBuilderMenuEditSessionState.Closed;

    public event Action<BistroBuilderMenuEditSessionChangedEvent>
        SessionChanged;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderRestaurantMenuCollectionService CollectionService =>
        collectionService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public BistroBuilderDishCategoryCatalogService CategoryCatalogService =>
        categoryCatalogService;

    public BistroBuilderMenuCommercialPolicy CommercialPolicy =>
        commercialPolicy;

    public bool HasOpenSession => sessionOpen;

    public bool HasPendingChanges => sessionOpen && dirty;

    public string SessionRestaurantId => sessionRestaurantId;

    public int BaseRestaurantRevision => baseRestaurantRevision;

    public int BaseMenuRevision => baseMenuRevision;

    public int DraftChangeCount => draftChangeCount;

    public int DraftItemCount => draftItems.Count;

    public BistroBuilderMenuEditSessionState State => state;

    private void Awake()
    {
        ResolveDependencies();

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

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

        if (commercialPolicy == null)
        {
            error = "Falta BistroBuilderMenuCommercialPolicy.";
            return false;
        }

        if (!commercialPolicy.TryValidate(out error) ||
            !catalogService.ValidateConfiguration(out error) ||
            !categoryCatalogService.ValidateConfiguration(out error) ||
            !menuService.ValidateConfiguration(out error) ||
            !collectionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (collectionService.MenuService != menuService ||
            collectionService.CatalogService != catalogService)
        {
            error = "La sesión no comparte los servicios canónicos de 2.1A.";
            return false;
        }

        if (menuService.CatalogService != catalogService)
        {
            error = "La carta activa no comparte el catálogo canónico.";
            return false;
        }

        if (menuService.CommercialPolicy != null &&
            menuService.CommercialPolicy != commercialPolicy)
        {
            error = "La sesión y la carta activa usan políticas distintas.";
            return false;
        }

        catalogService.CopyDefinitionsTo(definitionBuffer);

        for (int index = 0; index < definitionBuffer.Count; index++)
        {
            BistroBuilderDishDefinition definition = definitionBuffer[index];

            if (definition == null ||
                !categoryCatalogService.TryGetDefinition(
                    definition.CategoryId,
                    out _
                ))
            {
                error = "La definición " +
                        (definition != null
                            ? definition.DishId
                            : "<nula>") +
                        " no tiene una categoría canónica registrada.";
                return false;
            }
        }

        if (sessionOpen && !TryValidateDraft(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Abre una copia aislada de la carta activa. Solo puede existir una
    /// sesión para evitar dos borradores locales competidores.
    /// </summary>
    public bool TryBeginActiveSession(out string error)
    {
        if (sessionOpen)
        {
            error = "Ya existe una sesión de edición abierta.";
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        string activeRestaurantId = collectionService.ActiveRestaurantId;

        if (!collectionService.TryGetRestaurantSnapshot(
                activeRestaurantId,
                out BistroBuilderRestaurantMenuRuntimeState snapshot,
                out error
            ))
        {
            return false;
        }

        draftItems.Clear();
        draftByDishId.Clear();

        for (int index = 0; index < snapshot.Items.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = snapshot.Items[index];

            if (item == null)
            {
                error = "La carta activa contiene una entrada nula.";
                ResetSessionStorage();
                return false;
            }

            BistroBuilderMenuItemRuntimeState clone = item.Clone();
            draftItems.Add(clone);

            if (draftByDishId.ContainsKey(clone.DishId))
            {
                error = "La carta activa contiene el DishId duplicado " +
                        clone.DishId + ".";
                ResetSessionStorage();
                return false;
            }

            draftByDishId.Add(clone.DishId, clone);
        }

        NormalizeDisplayOrder(draftItems);
        sessionRestaurantId = snapshot.RestaurantId;
        baseRestaurantRevision = snapshot.Revision;
        baseMenuRevision = menuService.Revision;
        draftChangeCount = 0;
        dirty = false;
        sessionOpen = true;
        state = BistroBuilderMenuEditSessionState.OpenClean;

        if (!TryValidateDraft(out error))
        {
            ResetSessionStorage();
            return false;
        }

        Publish(
            BistroBuilderMenuDraftChangeType.SessionOpened,
            string.Empty
        );
        error = string.Empty;
        return true;
    }

    public bool TryGetDraftItemSnapshot(
        string dishId,
        out BistroBuilderMenuItemRuntimeState snapshot
    )
    {
        snapshot = null;

        if (!sessionOpen)
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!draftByDishId.TryGetValue(
                normalized,
                out BistroBuilderMenuItemRuntimeState item
            ))
        {
            return false;
        }

        snapshot = item.Clone();
        return true;
    }

    public bool TryGetDraftSnapshot(
        List<BistroBuilderMenuItemRuntimeState> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino del borrador es nulo.";
            return false;
        }

        if (!sessionOpen)
        {
            error = "No existe una sesión de edición abierta.";
            return false;
        }

        destination.Clear();

        for (int index = 0; index < draftItems.Count; index++)
        {
            destination.Add(draftItems[index].Clone());
        }

        destination.Sort(CompareItems);
        error = string.Empty;
        return true;
    }

    public BistroBuilderMenuMutationResult TryAddDish(string dishId)
    {
        if (!EnsureSession(out BistroBuilderMenuMutationResult failure))
        {
            return failure;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.InvalidDishId,
                "El DishId indicado no es válido."
            );
        }

        if (draftByDishId.ContainsKey(normalized))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.DishAlreadyExists,
                "El plato ya está incluido en el borrador."
            );
        }

        if (!BistroBuilderMenuPolicyEvaluator.CanAddDish(
                draftItems.Count,
                commercialPolicy,
                out string policyError
            ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.PolicyViolation,
                policyError
            );
        }

        if (!catalogService.TryGetDefinition(
                normalized,
                out BistroBuilderDishDefinition definition
            ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.DishDefinitionNotFound,
                "No existe una definición canónica para " + normalized + "."
            );
        }

        if (!categoryCatalogService.TryGetDefinition(
                definition.CategoryId,
                out _
            ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.InvalidCategory,
                "La categoría de " + normalized + " no está registrada."
            );
        }

        BistroBuilderMenuItemRuntimeState item =
            BistroBuilderMenuItemRuntimeState.FromDefinition(
                definition,
                draftItems.Count,
                true,
                true
            );

        draftItems.Add(item);
        draftByDishId.Add(item.DishId, item);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.DishAdded,
            item.DishId
        );

        return BistroBuilderMenuMutationResult.Success(
            "Plato añadido al borrador."
        );
    }

    public BistroBuilderMenuMutationResult TryRemoveDish(string dishId)
    {
        if (!TryResolveDraftItem(
                dishId,
                out BistroBuilderMenuItemRuntimeState item,
                out BistroBuilderMenuMutationResult failure
            ))
        {
            return failure;
        }

        draftItems.Remove(item);
        draftByDishId.Remove(item.DishId);
        NormalizeDisplayOrder(draftItems);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.DishRemoved,
            item.DishId
        );

        return BistroBuilderMenuMutationResult.Success(
            "Plato retirado del borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetEnabled(
        string dishId,
        bool value
    )
    {
        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.Enabled == value)
        {
            return NoChange("El estado activo ya tenía ese valor.");
        }

        if (!BistroBuilderMenuPolicyEvaluator
                .CanApplySignatureDependentState(
                    item,
                    value,
                    item.Unlocked,
                    item.AvailableServices,
                    commercialPolicy,
                    out string policyError
                ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.PolicyViolation,
                policyError
            );
        }

        item.SetEnabled(value);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.EnabledChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Estado activo actualizado en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetUnlocked(
        string dishId,
        bool value
    )
    {
        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.Unlocked == value)
        {
            return NoChange("El desbloqueo ya tenía ese valor.");
        }

        if (!BistroBuilderMenuPolicyEvaluator
                .CanApplySignatureDependentState(
                    item,
                    item.Enabled,
                    value,
                    item.AvailableServices,
                    commercialPolicy,
                    out string policyError
                ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.PolicyViolation,
                policyError
            );
        }

        item.SetUnlocked(value);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.UnlockChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Desbloqueo actualizado en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetPriceCents(
        string dishId,
        int priceCents
    )
    {
        if (!BistroBuilderMenuPolicyEvaluator.TryValidatePrice(
                priceCents,
                commercialPolicy,
                out string priceError
            ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.InvalidPrice,
                priceError
            );
        }

        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.CurrentPriceCents == priceCents)
        {
            return NoChange("El plato ya tenía ese precio.");
        }

        item.SetPriceCents(priceCents);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.PriceChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Precio actualizado en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetAvailability(
        string dishId,
        BistroBuilderMealServiceAvailability availability
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                availability,
                true
            ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.InvalidAvailability,
                "La máscara de servicios contiene valores desconocidos."
            );
        }

        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.AvailableServices == availability)
        {
            return NoChange("El plato ya tenía esa disponibilidad.");
        }

        if (!BistroBuilderMenuPolicyEvaluator
                .CanApplySignatureDependentState(
                    item,
                    item.Enabled,
                    item.Unlocked,
                    availability,
                    commercialPolicy,
                    out string policyError
                ))
        {
            return Fail(
                BistroBuilderMenuMutationFailureReason.PolicyViolation,
                policyError
            );
        }

        item.SetAvailableServices(availability);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.AvailabilityChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Servicios actualizados en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetManuallySoldOut(
        string dishId,
        bool value
    )
    {
        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.ManuallySoldOut == value)
        {
            return NoChange("El agotado manual ya tenía ese valor.");
        }

        item.SetManuallySoldOut(value);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.SoldOutChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Agotado manual actualizado en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TrySetSignatureDish(
        string dishId,
        bool value
    )
    {
        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.SignatureDish == value)
        {
            return NoChange("El estado de plato firma ya tenía ese valor.");
        }

        if (!BistroBuilderMenuPolicyEvaluator.CanSetSignatureDish(
                draftItems,
                item,
                value,
                commercialPolicy,
                out BistroBuilderMenuMutationFailureReason failureReason,
                out string policyError
            ))
        {
            return Fail(
                failureReason,
                policyError
            );
        }

        item.SetSignatureDish(value);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.SignatureChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Plato firma actualizado en el borrador."
        );
    }

    public BistroBuilderMenuMutationResult TryMoveDish(
        string dishId,
        int targetIndex
    )
    {
        if (!TryResolveDraftItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (draftItems.Count <= 1)
        {
            return NoChange("La carta solo contiene un plato.");
        }

        int currentIndex = draftItems.IndexOf(item);
        int clampedTarget = Mathf.Clamp(
            targetIndex,
            0,
            draftItems.Count - 1
        );

        if (currentIndex == clampedTarget)
        {
            return NoChange("El plato ya ocupa esa posición.");
        }

        draftItems.RemoveAt(currentIndex);
        draftItems.Insert(clampedTarget, item);
        ReindexDisplayOrderPreservingCurrentOrder(draftItems);
        CompleteDraftChange(
            BistroBuilderMenuDraftChangeType.OrderChanged,
            item.DishId
        );
        return BistroBuilderMenuMutationResult.Success(
            "Orden actualizado en el borrador."
        );
    }

    /// <summary>
    /// Aplica el borrador de forma atómica. Un cambio externo en la carta o
    /// en el restaurante activo convierte la sesión en conflicto y no altera
    /// el estado operativo.
    /// </summary>
    public bool TryCommit(
        out BistroBuilderMenuEditCommitResult result,
        out string error
    )
    {
        result = default(BistroBuilderMenuEditCommitResult);

        if (!sessionOpen)
        {
            error = "No existe una sesión de edición abierta.";
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!string.Equals(
                collectionService.ActiveRestaurantId,
                sessionRestaurantId,
                StringComparison.Ordinal
            ))
        {
            return FailConflict(
                "El restaurante activo cambió mientras se editaba la carta.",
                out result,
                out error
            );
        }

        if (!collectionService.TryGetRestaurantSnapshot(
                sessionRestaurantId,
                out BistroBuilderRestaurantMenuRuntimeState current,
                out error
            ))
        {
            return false;
        }

        if (current.Revision != baseRestaurantRevision ||
            menuService.Revision != baseMenuRevision)
        {
            return FailConflict(
                "La carta cambió fuera de la sesión y el borrador está obsoleto.",
                out result,
                out error
            );
        }

        if (!TryValidateDraft(out error))
        {
            return false;
        }

        int previousRevision = current.Revision;
        int appliedChanges = draftChangeCount;

        if (!dirty)
        {
            result = new BistroBuilderMenuEditCommitResult(
                true,
                false,
                sessionRestaurantId,
                previousRevision,
                previousRevision,
                0,
                "La sesión no contenía cambios."
            );
            CloseSession(
                BistroBuilderMenuEditSessionState.Committed,
                BistroBuilderMenuDraftChangeType.SessionCommitted
            );
            error = string.Empty;
            return true;
        }

        if (!collectionService.TryReplaceActiveRestaurantItems(
                draftItems,
                baseRestaurantRevision,
                baseMenuRevision,
                true,
                out int replacedPreviousRevision,
                out int appliedRestaurantRevision,
                out error
            ))
        {
            return false;
        }

        result = new BistroBuilderMenuEditCommitResult(
            true,
            true,
            sessionRestaurantId,
            replacedPreviousRevision,
            appliedRestaurantRevision,
            appliedChanges,
            "La carta se aplicó de forma atómica."
        );

        CloseSession(
            BistroBuilderMenuEditSessionState.Committed,
            BistroBuilderMenuDraftChangeType.SessionCommitted
        );
        error = string.Empty;
        return true;
    }

    public bool TryDiscard(out string error)
    {
        if (!sessionOpen)
        {
            error = "No existe una sesión de edición abierta.";
            return false;
        }

        CloseSession(
            BistroBuilderMenuEditSessionState.Discarded,
            BistroBuilderMenuDraftChangeType.SessionDiscarded
        );
        error = string.Empty;
        return true;
    }

    private bool TryValidateDraft(out string error)
    {
        if (!BistroBuilderMenuPolicyEvaluator.TryValidateMenu(
                draftItems,
                commercialPolicy,
                out error
            ))
        {
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < draftItems.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = draftItems[index];

            if (!item.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "El borrador contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (!catalogService.TryGetDefinition(
                    item.DishId,
                    out BistroBuilderDishDefinition definition
                ) ||
                definition == null)
            {
                error = "El borrador referencia un plato inexistente: " +
                        item.DishId + ".";
                return false;
            }

            if (!categoryCatalogService.TryGetDefinition(
                    definition.CategoryId,
                    out _
                ))
            {
                error = "El plato " + item.DishId +
                        " referencia una categoría no registrada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool EnsureSession(
        out BistroBuilderMenuMutationResult failure
    )
    {
        if (sessionOpen)
        {
            failure = default(BistroBuilderMenuMutationResult);
            return true;
        }

        failure = Fail(
            BistroBuilderMenuMutationFailureReason.NoActiveEditSession,
            "No existe una sesión de edición abierta."
        );
        return false;
    }

    private bool TryResolveDraftItem(
        string dishId,
        out BistroBuilderMenuItemRuntimeState item,
        out BistroBuilderMenuMutationResult failure
    )
    {
        item = null;

        if (!EnsureSession(out failure))
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            failure = Fail(
                BistroBuilderMenuMutationFailureReason.InvalidDishId,
                "El DishId indicado no es válido."
            );
            return false;
        }

        if (!draftByDishId.TryGetValue(normalized, out item))
        {
            failure = Fail(
                BistroBuilderMenuMutationFailureReason.DishNotInMenu,
                "El plato no está incluido en el borrador."
            );
            return false;
        }

        failure = default(BistroBuilderMenuMutationResult);
        return true;
    }

    private void CompleteDraftChange(
        BistroBuilderMenuDraftChangeType changeType,
        string dishId
    )
    {
        dirty = true;
        draftChangeCount++;
        state = BistroBuilderMenuEditSessionState.OpenDirty;
        Publish(changeType, dishId);
    }

    private bool FailConflict(
        string message,
        out BistroBuilderMenuEditCommitResult result,
        out string error
    )
    {
        state = BistroBuilderMenuEditSessionState.Conflict;
        Publish(
            BistroBuilderMenuDraftChangeType.SessionConflict,
            string.Empty
        );
        result = new BistroBuilderMenuEditCommitResult(
            false,
            dirty,
            sessionRestaurantId,
            baseRestaurantRevision,
            baseRestaurantRevision,
            0,
            message
        );
        error = message;
        return false;
    }

    private void CloseSession(
        BistroBuilderMenuEditSessionState terminalState,
        BistroBuilderMenuDraftChangeType changeType
    )
    {
        state = terminalState;
        sessionOpen = false;
        dirty = false;
        Publish(changeType, string.Empty);
        draftItems.Clear();
        draftByDishId.Clear();
        sessionRestaurantId = string.Empty;
        baseRestaurantRevision = 0;
        baseMenuRevision = 0;
        draftChangeCount = 0;
    }

    private void ResetSessionStorage()
    {
        sessionOpen = false;
        dirty = false;
        draftItems.Clear();
        draftByDishId.Clear();
        sessionRestaurantId = string.Empty;
        baseRestaurantRevision = 0;
        baseMenuRevision = 0;
        draftChangeCount = 0;
        state = BistroBuilderMenuEditSessionState.Closed;
    }

    private void Publish(
        BistroBuilderMenuDraftChangeType changeType,
        string dishId
    )
    {
        BistroBuilderMenuEditSessionChangedEvent change =
            new BistroBuilderMenuEditSessionChangedEvent(
                sessionRestaurantId,
                changeType,
                dishId,
                baseRestaurantRevision,
                draftChangeCount,
                state
            );

        SessionChanged?.Invoke(change);

        if (logChanges)
        {
            Debug.Log(
                "Edición de carta " + changeType +
                " para " +
                (string.IsNullOrWhiteSpace(sessionRestaurantId)
                    ? "<sin restaurante>"
                    : sessionRestaurantId) +
                ". Cambios: " + draftChangeCount + ".",
                this
            );
        }
    }

    private void ResolveDependencies()
    {
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
    }

    private static void NormalizeDisplayOrder(
        List<BistroBuilderMenuItemRuntimeState> target
    )
    {
        target.Sort(CompareItems);
        ReindexDisplayOrderPreservingCurrentOrder(target);
    }

    private static void ReindexDisplayOrderPreservingCurrentOrder(
        List<BistroBuilderMenuItemRuntimeState> target
    )
    {
        for (int index = 0; index < target.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = target[index];

            if (item != null)
            {
                item.SetDisplayOrder(index);
            }
        }
    }

    private static int CompareItems(
        BistroBuilderMenuItemRuntimeState first,
        BistroBuilderMenuItemRuntimeState second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int order = first.DisplayOrder.CompareTo(second.DisplayOrder);
        return order != 0
            ? order
            : string.Compare(
                first.DishId,
                second.DishId,
                StringComparison.Ordinal
            );
    }

    private static BistroBuilderMenuMutationResult Fail(
        BistroBuilderMenuMutationFailureReason reason,
        string message
    )
    {
        return BistroBuilderMenuMutationResult.Failure(reason, message);
    }

    private static BistroBuilderMenuMutationResult NoChange(string message)
    {
        return Fail(
            BistroBuilderMenuMutationFailureReason.NoChange,
            message
        );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
