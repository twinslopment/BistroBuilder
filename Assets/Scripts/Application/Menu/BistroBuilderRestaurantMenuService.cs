using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad única de la carta activa del restaurante.
///
/// Mantiene únicamente estado de partida: precio actual, activación,
/// disponibilidad, agotado, desbloqueo, plato firma y orden visual.
/// Las definiciones inmutables se resuelven mediante DishId en el catálogo.
///
/// No utiliza Update ni búsquedas continuas. Todas las modificaciones pasan
/// por operaciones validadas y publican un único evento de dominio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Restaurant Menu Service")]
public sealed class BistroBuilderRestaurantMenuService : MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [Header("Inicialización")]

    [Tooltip(
        "Si la carta está vacía, añade todas las definiciones del catálogo " +
        "usando sus precios y servicios predeterminados."
    )]
    [SerializeField]
    private bool initializeCatalogDishesWhenEmpty = true;

    [SerializeField]
    private bool defaultDishEnabled = true;

    [SerializeField]
    private bool defaultDishUnlocked = true;

    [Header("Estado runtime")]

    [SerializeField]
    private List<BistroBuilderMenuItemRuntimeState> items =
        new List<BistroBuilderMenuItemRuntimeState>();

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges = true;

    private readonly Dictionary<string, BistroBuilderMenuItemRuntimeState>
        byDishId =
            new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderDishDefinition> definitionBuffer =
        new List<BistroBuilderDishDefinition>(32);

    private bool initialized;

    public event Action<BistroBuilderMenuChangedEvent> MenuChanged;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public int ItemCount => items != null ? items.Count : 0;

    public int Revision { get; private set; }

    private void Awake()
    {
        if (!RebuildRuntimeIndexAndEnsureDefaults(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    /// <summary>
    /// Comprueba referencias, catálogo y estado actual sin modificarlo.
    /// </summary>
    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (items == null)
        {
            error = "La lista runtime de la carta es nula.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = items[index];

            if (item == null)
            {
                error = "La carta contiene una entrada nula en la posición " +
                        index + ".";
                return false;
            }

            if (!item.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La carta contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reconstruye el índice O(1) y crea la carta inicial solo cuando no
    /// existe estado previo. Es idempotente.
    /// </summary>
    public bool RebuildRuntimeIndexAndEnsureDefaults(out string error)
    {
        CacheDependenciesIfNeeded();

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            initialized = false;
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (items == null)
        {
            items = new List<BistroBuilderMenuItemRuntimeState>();
        }

        if (items.Count == 0 && initializeCatalogDishesWhenEmpty)
        {
            BuildDefaultsFromCatalog();
        }

        if (!TryBuildIndex(items, byDishId, out error))
        {
            initialized = false;
            return false;
        }

        NormalizeDisplayOrder(items);
        initialized = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetItemSnapshot(
        string dishId,
        out BistroBuilderMenuItemRuntimeState snapshot
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out _))
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!byDishId.TryGetValue(
                normalized,
                out BistroBuilderMenuItemRuntimeState item
            ))
        {
            return false;
        }

        snapshot = item.Clone();
        return true;
    }

    /// <summary>
    /// Copia una fotografía ordenada e independiente del estado interno.
    /// </summary>
    public bool TryGetSnapshot(
        List<BistroBuilderMenuItemRuntimeState> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino de snapshot es nulo.";
            return false;
        }

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        destination.Clear();

        for (int index = 0; index < items.Count; index++)
        {
            destination.Add(items[index].Clone());
        }

        destination.Sort(CompareItems);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reemplaza toda la carta de forma atómica. Primero valida una copia;
    /// si existe cualquier error, el estado actual permanece intacto.
    /// </summary>
    public bool TryReplaceAll(
        IList<BistroBuilderMenuItemRuntimeState> replacement,
        bool notify,
        out string error
    )
    {
        if (replacement == null)
        {
            error = "El estado de carta de reemplazo es nulo.";
            return false;
        }

        CacheDependenciesIfNeeded();

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        List<BistroBuilderMenuItemRuntimeState> candidate =
            new List<BistroBuilderMenuItemRuntimeState>(replacement.Count);

        for (int index = 0; index < replacement.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = replacement[index];

            if (item == null)
            {
                error = "El estado de carta contiene una entrada nula.";
                return false;
            }

            candidate.Add(item.Clone());
        }

        Dictionary<string, BistroBuilderMenuItemRuntimeState>
            candidateIndex =
                new Dictionary<string, BistroBuilderMenuItemRuntimeState>(
                    StringComparer.Ordinal
                );

        if (!TryBuildIndex(candidate, candidateIndex, out error))
        {
            return false;
        }

        NormalizeDisplayOrder(candidate);

        items.Clear();
        items.AddRange(candidate);

        byDishId.Clear();

        foreach (KeyValuePair<string, BistroBuilderMenuItemRuntimeState> pair
                 in candidateIndex)
        {
            byDishId.Add(pair.Key, pair.Value);
        }

        initialized = true;
        Revision++;

        if (notify)
        {
            PublishChange(
                BistroBuilderMenuChangeType.StateReplaced,
                string.Empty,
                "La carta activa se ha restaurado de forma atómica."
            );
        }

        error = string.Empty;
        return true;
    }

    public BistroBuilderMenuMutationResult TryAddDish(string dishId)
    {
        if (!EnsureInitialized(out string error))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidConfiguration,
                error
            );
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidDishId,
                "El DishId indicado no es válido."
            );
        }

        if (byDishId.ContainsKey(normalized))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.DishAlreadyExists,
                "El plato ya está incluido en la carta."
            );
        }

        if (!catalogService.TryGetDefinition(
                normalized,
                out BistroBuilderDishDefinition definition
            ))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.DishDefinitionNotFound,
                "No existe una definición canónica para " + normalized + "."
            );
        }

        BistroBuilderMenuItemRuntimeState item =
            BistroBuilderMenuItemRuntimeState.FromDefinition(
                definition,
                items.Count,
                defaultDishEnabled,
                defaultDishUnlocked
            );

        items.Add(item);
        byDishId.Add(item.DishId, item);
        Revision++;

        PublishChange(
            BistroBuilderMenuChangeType.DishAdded,
            item.DishId,
            "Plato añadido a la carta."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Plato añadido correctamente."
        );
    }

    public BistroBuilderMenuMutationResult TryRemoveDish(string dishId)
    {
        if (!TryResolveItem(
                dishId,
                out BistroBuilderMenuItemRuntimeState item,
                out BistroBuilderMenuMutationResult failure
            ))
        {
            return failure;
        }

        items.Remove(item);
        byDishId.Remove(item.DishId);
        NormalizeDisplayOrder(items);
        Revision++;

        PublishChange(
            BistroBuilderMenuChangeType.DishRemoved,
            item.DishId,
            "Plato retirado de la carta."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Plato retirado correctamente."
        );
    }

    public BistroBuilderMenuMutationResult TrySetEnabled(
        string dishId,
        bool value
    )
    {
        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.Enabled == value)
        {
            return NoChange("El estado activo ya tenía ese valor.");
        }

        item.SetEnabled(value);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.EnabledChanged,
            "Disponibilidad comercial actualizada."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Estado activo actualizado."
        );
    }

    public BistroBuilderMenuMutationResult TrySetUnlocked(
        string dishId,
        bool value
    )
    {
        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.Unlocked == value)
        {
            return NoChange("El desbloqueo ya tenía ese valor.");
        }

        item.SetUnlocked(value);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.UnlockChanged,
            "Desbloqueo de plato actualizado."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Desbloqueo actualizado."
        );
    }

    public BistroBuilderMenuMutationResult TrySetPriceCents(
        string dishId,
        int priceCents
    )
    {
        if (priceCents < 0 ||
            priceCents > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidPrice,
                "El precio queda fuera del rango permitido."
            );
        }

        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.CurrentPriceCents == priceCents)
        {
            return NoChange("El plato ya tenía ese precio.");
        }

        item.SetPriceCents(priceCents);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.PriceChanged,
            "Precio actual actualizado."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Precio actualizado."
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
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidAvailability,
                "La máscara de servicios contiene valores desconocidos."
            );
        }

        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.AvailableServices == availability)
        {
            return NoChange("El plato ya tenía esa disponibilidad.");
        }

        item.SetAvailableServices(availability);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.AvailabilityChanged,
            "Servicios disponibles actualizados."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Disponibilidad actualizada."
        );
    }

    public BistroBuilderMenuMutationResult TrySetManuallySoldOut(
        string dishId,
        bool value
    )
    {
        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.ManuallySoldOut == value)
        {
            return NoChange("El agotado manual ya tenía ese valor.");
        }

        item.SetManuallySoldOut(value);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.SoldOutChanged,
            "Agotado manual actualizado."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Agotado manual actualizado."
        );
    }

    public BistroBuilderMenuMutationResult TrySetSignatureDish(
        string dishId,
        bool value
    )
    {
        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (item.SignatureDish == value)
        {
            return NoChange("El estado de plato firma ya tenía ese valor.");
        }

        item.SetSignatureDish(value);
        CommitItemChange(
            item,
            BistroBuilderMenuChangeType.SignatureChanged,
            "Estado de plato firma actualizado."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Plato firma actualizado."
        );
    }

    public BistroBuilderMenuMutationResult TryMoveDish(
        string dishId,
        int targetIndex
    )
    {
        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            return failure;
        }

        if (items.Count <= 1)
        {
            return NoChange("La carta solo contiene un plato.");
        }

        int currentIndex = items.IndexOf(item);
        int clampedTarget = Mathf.Clamp(targetIndex, 0, items.Count - 1);

        if (currentIndex == clampedTarget)
        {
            return NoChange("El plato ya ocupa esa posición.");
        }

        items.RemoveAt(currentIndex);
        items.Insert(clampedTarget, item);

        // La lista ya representa el nuevo orden solicitado por el jugador.
        // No debemos ordenarla de nuevo usando los valores DisplayOrder
        // anteriores, porque eso desharía inmediatamente el movimiento.
        ReindexDisplayOrderPreservingCurrentOrder(items);
        Revision++;

        PublishChange(
            BistroBuilderMenuChangeType.OrderChanged,
            item.DishId,
            "Orden de presentación actualizado."
        );

        return BistroBuilderMenuMutationResult.Success(
            "Orden de presentación actualizado."
        );
    }

    /// <summary>
    /// Comprueba si un plato puede pedirse en un servicio concreto.
    /// Inventario podrá añadir después una razón adicional sin cambiar la
    /// autoridad de carta.
    /// </summary>
    public bool IsDishOrderable(
        string dishId,
        BistroBuilderMealServiceAvailability service,
        out string rejectionReason
    )
    {
        rejectionReason = string.Empty;

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(service, false) ||
            service == BistroBuilderMealServiceAvailability.All)
        {
            rejectionReason = "Debe indicarse un servicio concreto.";
            return false;
        }

        if (!TryResolveItem(dishId, out var item, out var failure))
        {
            rejectionReason = failure.Message;
            return false;
        }

        if (!item.Unlocked)
        {
            rejectionReason = "El plato todavía no está desbloqueado.";
            return false;
        }

        if (!item.Enabled)
        {
            rejectionReason = "El plato está desactivado en la carta.";
            return false;
        }

        if (item.ManuallySoldOut)
        {
            rejectionReason = "El plato está marcado como agotado.";
            return false;
        }

        if ((item.AvailableServices & service) == 0)
        {
            rejectionReason = "El plato no está disponible en este servicio.";
            return false;
        }

        return true;
    }

    public BistroBuilderMenuMutationResult ResetToCatalogDefaults()
    {
        CacheDependenciesIfNeeded();

        if (catalogService == null)
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidConfiguration,
                "Falta BistroBuilderDishCatalogService."
            );
        }

        if (!catalogService.ValidateConfiguration(out string error))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidConfiguration,
                error
            );
        }

        List<BistroBuilderMenuItemRuntimeState> defaults =
            new List<BistroBuilderMenuItemRuntimeState>();

        catalogService.CopyDefinitionsTo(definitionBuffer);
        definitionBuffer.Sort(CompareDefinitions);

        for (int index = 0; index < definitionBuffer.Count; index++)
        {
            defaults.Add(
                BistroBuilderMenuItemRuntimeState.FromDefinition(
                    definitionBuffer[index],
                    index,
                    defaultDishEnabled,
                    defaultDishUnlocked
                )
            );
        }

        if (!TryReplaceAll(defaults, true, out error))
        {
            return Failure(
                BistroBuilderMenuMutationFailureReason.InvalidState,
                error
            );
        }

        return BistroBuilderMenuMutationResult.Success(
            "Carta restaurada desde el catálogo."
        );
    }

    private void BuildDefaultsFromCatalog()
    {
        items.Clear();
        catalogService.CopyDefinitionsTo(definitionBuffer);
        definitionBuffer.Sort(CompareDefinitions);

        for (int index = 0; index < definitionBuffer.Count; index++)
        {
            items.Add(
                BistroBuilderMenuItemRuntimeState.FromDefinition(
                    definitionBuffer[index],
                    index,
                    defaultDishEnabled,
                    defaultDishUnlocked
                )
            );
        }
    }

    private bool TryBuildIndex(
        IList<BistroBuilderMenuItemRuntimeState> source,
        Dictionary<string, BistroBuilderMenuItemRuntimeState> destination,
        out string error
    )
    {
        destination.Clear();

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];

            if (item == null)
            {
                error = "La carta contiene una entrada nula.";
                return false;
            }

            if (!item.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (destination.ContainsKey(item.DishId))
            {
                error = "La carta contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            destination.Add(item.DishId, item);
        }

        error = string.Empty;
        return true;
    }

    private bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        return RebuildRuntimeIndexAndEnsureDefaults(out error);
    }

    private bool TryResolveItem(
        string dishId,
        out BistroBuilderMenuItemRuntimeState item,
        out BistroBuilderMenuMutationResult failure
    )
    {
        item = null;
        failure = default(BistroBuilderMenuMutationResult);

        if (!EnsureInitialized(out string error))
        {
            failure = Failure(
                BistroBuilderMenuMutationFailureReason.InvalidConfiguration,
                error
            );
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            failure = Failure(
                BistroBuilderMenuMutationFailureReason.InvalidDishId,
                "El DishId indicado no es válido."
            );
            return false;
        }

        if (!byDishId.TryGetValue(normalized, out item))
        {
            failure = Failure(
                BistroBuilderMenuMutationFailureReason.DishNotInMenu,
                "El plato no está incluido en la carta activa."
            );
            return false;
        }

        return true;
    }

    private void CommitItemChange(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMenuChangeType changeType,
        string description
    )
    {
        Revision++;
        PublishChange(changeType, item.DishId, description);
    }

    private void PublishChange(
        BistroBuilderMenuChangeType changeType,
        string dishId,
        string description
    )
    {
        BistroBuilderMenuChangedEvent change =
            new BistroBuilderMenuChangedEvent(
                changeType,
                dishId,
                Revision,
                description
            );

        MenuChanged?.Invoke(change);

        if (logChanges)
        {
            string target = string.IsNullOrEmpty(dishId)
                ? "carta completa"
                : dishId;

            Debug.Log(
                "Carta runtime: " + changeType + " sobre " + target +
                ". Revisión: " + Revision + ".",
                this
            );
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (catalogService == null)
        {
            TryGetComponent(out catalogService);
        }
    }


    /// <summary>
    /// Reasigna índices consecutivos respetando exactamente el orden actual
    /// de la lista. Se usa después de una operación de movimiento explícita.
    /// </summary>
    private static void ReindexDisplayOrderPreservingCurrentOrder(
        List<BistroBuilderMenuItemRuntimeState> target
    )
    {
        if (target == null)
        {
            return;
        }

        for (int index = 0; index < target.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = target[index];

            if (item != null)
            {
                item.SetDisplayOrder(index);
            }
        }
    }

    private static void NormalizeDisplayOrder(
        List<BistroBuilderMenuItemRuntimeState> target
    )
    {
        target.Sort(CompareItems);

        for (int index = 0; index < target.Count; index++)
        {
            target[index].SetDisplayOrder(index);
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

        int orderComparison = first.DisplayOrder.CompareTo(second.DisplayOrder);

        return orderComparison != 0
            ? orderComparison
            : string.Compare(
                first.DishId,
                second.DishId,
                StringComparison.Ordinal
            );
    }

    private static int CompareDefinitions(
        BistroBuilderDishDefinition first,
        BistroBuilderDishDefinition second
    )
    {
        return string.Compare(
            first != null ? first.DishId : string.Empty,
            second != null ? second.DishId : string.Empty,
            StringComparison.Ordinal
        );
    }

    private static BistroBuilderMenuMutationResult Failure(
        BistroBuilderMenuMutationFailureReason reason,
        string message
    )
    {
        return BistroBuilderMenuMutationResult.Failure(reason, message);
    }

    private static BistroBuilderMenuMutationResult NoChange(string message)
    {
        return Failure(
            BistroBuilderMenuMutationFailureReason.NoChange,
            message
        );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
