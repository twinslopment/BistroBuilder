using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de las cartas pertenecientes a todos los restaurantes de la
/// partida. BistroBuilderRestaurantMenuService continúa siendo la autoridad
/// operativa de la carta activa; este servicio conserva, cambia y restaura
/// sus estados por RestaurantId sin duplicar la lógica de comandas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Restaurant Menu Collection Service")]
public sealed class BistroBuilderRestaurantMenuCollectionService :
    MonoBehaviour
{
    public const string DefaultRestaurantId = "restaurant_primary";
    public const string RuntimeRevision = "MENU-2.1A";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [Header("Restaurante activo")]

    [SerializeField]
    private string initialRestaurantId = DefaultRestaurantId;

    [SerializeField]
    private string activeRestaurantId = DefaultRestaurantId;

    [Header("Cartas por restaurante")]

    [SerializeField]
    private List<BistroBuilderRestaurantMenuRuntimeState> restaurantStates =
        new List<BistroBuilderRestaurantMenuRuntimeState>();

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges = true;

    private readonly Dictionary<string, BistroBuilderRestaurantMenuRuntimeState>
        byRestaurantId =
            new Dictionary<string, BistroBuilderRestaurantMenuRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderMenuItemRuntimeState> menuBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<BistroBuilderMenuItemRuntimeState> replacementBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private readonly List<BistroBuilderDishDefinition> definitionBuffer =
        new List<BistroBuilderDishDefinition>(32);

    private bool initialized;
    private bool subscribed;
    private bool suppressMenuEvents;

    public event Action<string, string> ActiveRestaurantChanged;

    public string ActiveRestaurantId => activeRestaurantId;

    public int RestaurantCount => restaurantStates != null
        ? restaurantStates.Count
        : 0;

    public int UnresolvedItemCount
    {
        get
        {
            int count = 0;

            if (restaurantStates == null)
            {
                return count;
            }

            for (int index = 0; index < restaurantStates.Count; index++)
            {
                BistroBuilderRestaurantMenuRuntimeState state =
                    restaurantStates[index];

                if (state != null)
                {
                    count += state.UnresolvedItemCount;
                }
            }

            return count;
        }
    }

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    private void Awake()
    {
        if (!RebuildRuntimeIndexAndEnsurePrimaryRestaurant(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Valida la colección completa sin cambiar la carta activa.
    /// </summary>
    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
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

        if (!BistroBuilderMenuIdUtility.IsValidStableId(activeRestaurantId))
        {
            error = "El RestaurantId activo no es válido.";
            return false;
        }

        if (restaurantStates == null || restaurantStates.Count == 0)
        {
            error = "No existe ninguna carta por restaurante.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        bool activeFound = false;

        for (int index = 0; index < restaurantStates.Count; index++)
        {
            BistroBuilderRestaurantMenuRuntimeState state =
                restaurantStates[index];

            if (state == null)
            {
                error = "La colección contiene una carta nula.";
                return false;
            }

            if (!state.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (!ids.Add(state.RestaurantId))
            {
                error = "El RestaurantId " + state.RestaurantId +
                        " está duplicado.";
                return false;
            }

            activeFound |= string.Equals(
                state.RestaurantId,
                activeRestaurantId,
                StringComparison.Ordinal
            );
        }

        if (!activeFound)
        {
            error = "La carta del restaurante activo no existe.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reconstruye el índice, migra la carta global antigua al restaurante
    /// principal y reconcilia entradas conocidas/no resueltas.
    /// </summary>
    public bool RebuildRuntimeIndexAndEnsurePrimaryRestaurant(
        out string error
    )
    {
        CacheDependenciesIfNeeded();

        if (menuService == null || catalogService == null)
        {
            error = "Faltan dependencias de la colección de cartas.";
            initialized = false;
            return false;
        }

        if (!menuService.RebuildRuntimeIndexAndEnsureDefaults(out error) ||
            !catalogService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        initialRestaurantId = NormalizeRestaurantId(initialRestaurantId);
        activeRestaurantId = NormalizeRestaurantId(activeRestaurantId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(initialRestaurantId))
        {
            initialRestaurantId = DefaultRestaurantId;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(activeRestaurantId))
        {
            activeRestaurantId = initialRestaurantId;
        }

        if (restaurantStates == null)
        {
            restaurantStates =
                new List<BistroBuilderRestaurantMenuRuntimeState>();
        }

        if (restaurantStates.Count == 0)
        {
            if (!menuService.TryGetSnapshot(menuBuffer, out error))
            {
                initialized = false;
                return false;
            }

            restaurantStates.Add(
                new BistroBuilderRestaurantMenuRuntimeState(
                    activeRestaurantId,
                    menuService.Revision,
                    menuBuffer,
                    Array.Empty<BistroBuilderMenuItemRuntimeState>()
                )
            );
        }

        if (!TryReconcileAndBuildIndex(restaurantStates, out error))
        {
            initialized = false;
            return false;
        }

        if (!byRestaurantId.ContainsKey(activeRestaurantId))
        {
            activeRestaurantId = byRestaurantId.ContainsKey(initialRestaurantId)
                ? initialRestaurantId
                : restaurantStates[0].RestaurantId;
        }

        // La lista serializada del servicio operativo es la autoridad de la
        // escena inicial. Se copia al registro activo para migrar 367A sin
        // reescribir ni perder los datos ya validados.
        if (!CaptureActiveRestaurant(false, out error))
        {
            initialized = false;
            return false;
        }

        initialized = true;
        Subscribe();
        error = string.Empty;
        return true;
    }

    public bool TryGetRestaurantSnapshot(
        string restaurantId,
        out BistroBuilderRestaurantMenuRuntimeState snapshot,
        out string error
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out error) ||
            !CaptureActiveRestaurant(false, out error))
        {
            return false;
        }

        string normalized = NormalizeRestaurantId(restaurantId);

        if (!byRestaurantId.TryGetValue(
                normalized,
                out BistroBuilderRestaurantMenuRuntimeState state
            ))
        {
            error = "No existe una carta para el restaurante " +
                    normalized + ".";
            return false;
        }

        snapshot = state.Clone();
        error = string.Empty;
        return true;
    }

    public bool TryGetAllRestaurantSnapshots(
        List<BistroBuilderRestaurantMenuRuntimeState> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino de las cartas es nulo.";
            return false;
        }

        if (!EnsureInitialized(out error) ||
            !CaptureActiveRestaurant(false, out error))
        {
            return false;
        }

        destination.Clear();

        for (int index = 0; index < restaurantStates.Count; index++)
        {
            destination.Add(restaurantStates[index].Clone());
        }

        destination.Sort(CompareRestaurantStates);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Crea una carta nueva desde los valores canónicos del catálogo.
    /// No cambia el restaurante activo salvo que se solicite expresamente.
    /// </summary>
    public bool TryCreateRestaurantFromCatalogDefaults(
        string restaurantId,
        bool activate,
        out string error
    )
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalized = NormalizeRestaurantId(restaurantId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            error = "El RestaurantId indicado no es válido.";
            return false;
        }

        if (byRestaurantId.ContainsKey(normalized))
        {
            error = "El restaurante " + normalized + " ya tiene carta.";
            return false;
        }

        catalogService.CopyDefinitionsTo(definitionBuffer);
        definitionBuffer.Sort(CompareDefinitions);
        replacementBuffer.Clear();

        for (int index = 0; index < definitionBuffer.Count; index++)
        {
            replacementBuffer.Add(
                BistroBuilderMenuItemRuntimeState.FromDefinition(
                    definitionBuffer[index],
                    index,
                    true,
                    true
                )
            );
        }

        BistroBuilderRestaurantMenuRuntimeState state =
            new BistroBuilderRestaurantMenuRuntimeState(
                normalized,
                0,
                replacementBuffer,
                Array.Empty<BistroBuilderMenuItemRuntimeState>()
            );

        if (!state.TryValidate(catalogService, out error))
        {
            return false;
        }

        restaurantStates.Add(state);
        byRestaurantId.Add(normalized, state);

        if (activate && !TryActivateRestaurant(normalized, out error))
        {
            // La creación y activación se consideran una sola operación.
            // Si no puede activarse, no queda una carta parcial registrada.
            restaurantStates.Remove(state);
            byRestaurantId.Remove(normalized);
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Cambia de restaurante de forma atómica: primero conserva la carta
    /// actual, valida la de destino y solo entonces sustituye el estado activo.
    /// </summary>
    public bool TryActivateRestaurant(
        string restaurantId,
        out string error
    )
    {
        if (!EnsureInitialized(out error) ||
            !CaptureActiveRestaurant(false, out error))
        {
            return false;
        }

        string normalized = NormalizeRestaurantId(restaurantId);

        if (!byRestaurantId.TryGetValue(
                normalized,
                out BistroBuilderRestaurantMenuRuntimeState target
            ))
        {
            error = "No existe una carta para el restaurante " +
                    normalized + ".";
            return false;
        }

        if (string.Equals(
                normalized,
                activeRestaurantId,
                StringComparison.Ordinal
            ))
        {
            error = string.Empty;
            return true;
        }

        if (!target.TryValidate(catalogService, out error))
        {
            return false;
        }

        CopyItems(target.Items, replacementBuffer);
        string previousRestaurantId = activeRestaurantId;
        suppressMenuEvents = true;

        bool replaced;
        try
        {
            replaced = menuService.TryReplaceAll(
                replacementBuffer,
                true,
                out error
            );
        }
        finally
        {
            suppressMenuEvents = false;
        }

        if (!replaced)
        {
            return false;
        }

        activeRestaurantId = normalized;

        if (!CaptureActiveRestaurant(false, out error))
        {
            return false;
        }

        ActiveRestaurantChanged?.Invoke(
            previousRestaurantId,
            activeRestaurantId
        );

        if (logChanges)
        {
            Debug.Log(
                "Carta activa cambiada de " + previousRestaurantId +
                " a " + activeRestaurantId + ".",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Reemplaza todas las cartas desde persistencia después de validar una
    /// copia completa. Las entradas se reclasifican según el catálogo actual.
    /// </summary>
    public bool TryReplaceAllRestaurantStates(
        IList<BistroBuilderRestaurantMenuRuntimeState> replacement,
        string nextActiveRestaurantId,
        bool notify,
        out string error
    )
    {
        if (replacement == null || replacement.Count == 0)
        {
            error = "El reemplazo de cartas está vacío.";
            return false;
        }

        CacheDependenciesIfNeeded();

        if (menuService == null || catalogService == null)
        {
            error = "Faltan dependencias para restaurar las cartas.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        string normalizedActive =
            NormalizeRestaurantId(nextActiveRestaurantId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedActive))
        {
            error = "El RestaurantId activo del reemplazo es inválido.";
            return false;
        }

        List<BistroBuilderRestaurantMenuRuntimeState> candidate =
            new List<BistroBuilderRestaurantMenuRuntimeState>(
                replacement.Count
            );

        for (int index = 0; index < replacement.Count; index++)
        {
            BistroBuilderRestaurantMenuRuntimeState source =
                replacement[index];

            if (source == null)
            {
                error = "El reemplazo contiene una carta nula.";
                return false;
            }

            candidate.Add(source.Clone());
        }

        Dictionary<string, BistroBuilderRestaurantMenuRuntimeState>
            candidateIndex =
                new Dictionary<string, BistroBuilderRestaurantMenuRuntimeState>(
                    StringComparer.Ordinal
                );

        if (!TryReconcileAndBuildIndex(
                candidate,
                candidateIndex,
                out error
            ) ||
            !candidateIndex.TryGetValue(
                normalizedActive,
                out BistroBuilderRestaurantMenuRuntimeState activeCandidate
            ))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "El reemplazo no contiene la carta activa.";
            }

            return false;
        }

        CopyItems(activeCandidate.Items, replacementBuffer);
        suppressMenuEvents = true;

        bool applied;
        try
        {
            applied = menuService.TryReplaceAll(
                replacementBuffer,
                notify,
                out error
            );
        }
        finally
        {
            suppressMenuEvents = false;
        }

        if (!applied)
        {
            return false;
        }

        restaurantStates.Clear();
        restaurantStates.AddRange(candidate);
        byRestaurantId.Clear();

        foreach (KeyValuePair<string, BistroBuilderRestaurantMenuRuntimeState>
                 pair in candidateIndex)
        {
            byRestaurantId.Add(pair.Key, pair.Value);
        }

        string previousRestaurantId = activeRestaurantId;
        activeRestaurantId = normalizedActive;
        initialized = true;

        if (!CaptureActiveRestaurant(false, out error))
        {
            return false;
        }

        if (notify && !string.Equals(
                previousRestaurantId,
                activeRestaurantId,
                StringComparison.Ordinal
            ))
        {
            ActiveRestaurantChanged?.Invoke(
                previousRestaurantId,
                activeRestaurantId
            );
        }

        return true;
    }

    private bool CaptureActiveRestaurant(
        bool incrementRevision,
        out string error
    )
    {
        error = string.Empty;

        if (menuService == null ||
            !menuService.TryGetSnapshot(menuBuffer, out error))
        {
            return false;
        }

        if (!byRestaurantId.TryGetValue(
                activeRestaurantId,
                out BistroBuilderRestaurantMenuRuntimeState state
            ))
        {
            state = new BistroBuilderRestaurantMenuRuntimeState(
                activeRestaurantId,
                menuService.Revision,
                menuBuffer,
                Array.Empty<BistroBuilderMenuItemRuntimeState>()
            );
            restaurantStates.Add(state);
            byRestaurantId.Add(activeRestaurantId, state);
            return true;
        }

        int nextRevision = incrementRevision
            ? state.Revision + 1
            : state.Revision;
        state.ReplaceResolvedItems(menuBuffer, nextRevision);
        return true;
    }

    private bool TryReconcileAndBuildIndex(
        List<BistroBuilderRestaurantMenuRuntimeState> source,
        out string error
    )
    {
        return TryReconcileAndBuildIndex(source, byRestaurantId, out error);
    }

    private bool TryReconcileAndBuildIndex(
        List<BistroBuilderRestaurantMenuRuntimeState> source,
        Dictionary<string, BistroBuilderRestaurantMenuRuntimeState> destination,
        out string error
    )
    {
        destination.Clear();

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderRestaurantMenuRuntimeState original = source[index];

            if (original == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(
                    original.RestaurantId
                ))
            {
                error = "La colección contiene una carta inválida.";
                return false;
            }

            if (destination.ContainsKey(original.RestaurantId))
            {
                error = "El RestaurantId " + original.RestaurantId +
                        " está duplicado.";
                return false;
            }

            List<BistroBuilderMenuItemRuntimeState> resolved =
                new List<BistroBuilderMenuItemRuntimeState>();
            List<BistroBuilderMenuItemRuntimeState> unresolved =
                new List<BistroBuilderMenuItemRuntimeState>();
            HashSet<string> ids =
                new HashSet<string>(StringComparer.Ordinal);

            if (!ClassifyItems(
                    original.Items,
                    ids,
                    resolved,
                    unresolved,
                    out error
                ) ||
                !ClassifyItems(
                    original.UnresolvedItems,
                    ids,
                    resolved,
                    unresolved,
                    out error
                ))
            {
                return false;
            }

            // No normalizamos aquí: las cartas inactivas y las entradas no
            // resueltas deben conservar exactamente su orden persistido. El
            // servicio operativo normaliza una copia al activar la carta.
            BistroBuilderRestaurantMenuRuntimeState reconciled =
                new BistroBuilderRestaurantMenuRuntimeState(
                    original.RestaurantId,
                    original.Revision,
                    resolved,
                    unresolved
                );

            if (!reconciled.TryValidate(catalogService, out error))
            {
                return false;
            }

            source[index] = reconciled;
            destination.Add(reconciled.RestaurantId, reconciled);
        }

        source.Sort(CompareRestaurantStates);
        error = string.Empty;
        return true;
    }

    private bool ClassifyItems(
        IReadOnlyList<BistroBuilderMenuItemRuntimeState> source,
        HashSet<string> ids,
        List<BistroBuilderMenuItemRuntimeState> resolved,
        List<BistroBuilderMenuItemRuntimeState> unresolved,
        out string error
    )
    {
        if (source == null)
        {
            error = "La colección de platos de un restaurante es nula.";
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];

            if (item == null)
            {
                error = "La colección contiene una entrada nula.";
                return false;
            }

            if (!item.TryValidateStructure(out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La colección contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (catalogService.TryGetDefinition(item.DishId, out _))
            {
                resolved.Add(item.Clone());
            }
            else
            {
                unresolved.Add(item.Clone());
            }
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

        return RebuildRuntimeIndexAndEnsurePrimaryRestaurant(out error);
    }

    private void HandleMenuChanged(BistroBuilderMenuChangedEvent change)
    {
        if (suppressMenuEvents || !initialized)
        {
            return;
        }

        if (!CaptureActiveRestaurant(true, out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        CacheDependenciesIfNeeded();

        if (menuService != null)
        {
            menuService.MenuChanged += HandleMenuChanged;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (menuService != null)
        {
            menuService.MenuChanged -= HandleMenuChanged;
        }

        subscribed = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (catalogService == null)
        {
            TryGetComponent(out catalogService);
        }
    }

    private static string NormalizeRestaurantId(string value)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? DefaultRestaurantId
            : normalized;
    }

    private static void CopyItems(
        IReadOnlyList<BistroBuilderMenuItemRuntimeState> source,
        List<BistroBuilderMenuItemRuntimeState> destination
    )
    {
        destination.Clear();

        if (source == null)
        {
            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            destination.Add(item != null ? item.Clone() : null);
        }
    }

    private static void NormalizeDisplayOrder(
        List<BistroBuilderMenuItemRuntimeState> target
    )
    {
        target.Sort(CompareMenuItems);

        for (int index = 0; index < target.Count; index++)
        {
            target[index].SetDisplayOrder(index);
        }
    }

    private static int CompareMenuItems(
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

    private static int CompareRestaurantStates(
        BistroBuilderRestaurantMenuRuntimeState first,
        BistroBuilderRestaurantMenuRuntimeState second
    )
    {
        return string.Compare(
            first != null ? first.RestaurantId : string.Empty,
            second != null ? second.RestaurantId : string.Empty,
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

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
        initialRestaurantId = DefaultRestaurantId;
        activeRestaurantId = DefaultRestaurantId;
    }

    private void OnValidate()
    {
        initialRestaurantId = NormalizeRestaurantId(initialRestaurantId);
        activeRestaurantId = NormalizeRestaurantId(activeRestaurantId);
        CacheDependenciesIfNeeded();
    }
#endif
}
