using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Motor derivado de disponibilidad dinámica 368EF.
///
/// Calcula raciones vendibles por ingrediente limitante usando el stock
/// disponible real (OnHand - Reserved). Reacciona a eventos de inventario y
/// carta; no utiliza Update ni persiste estados derivados.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Dish Availability Service")]
public sealed class BistroBuilderDishAvailabilityService : MonoBehaviour
{
    public const string RuntimeRevision = "368EF";

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderInventoryService inventoryService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderCanonicalOrderIntegrationService orderIntegration;

    [Header("Umbral de últimas raciones")]

    [SerializeField, Min(1)]
    private int lowStockPortionThreshold = 3;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges;

    private readonly Dictionary<string, BistroBuilderDishAvailabilitySnapshot>
        snapshotsByDishId =
            new Dictionary<string, BistroBuilderDishAvailabilitySnapshot>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderMenuItemRuntimeState> menuBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private const int EnableRecalculationMaximumWaitFrames = 60;

    private bool initialized;
    private bool hasStarted;
    private Coroutine enableRecalculationRoutine;

    public event Action<BistroBuilderDishAvailabilityChangedEvent>
        AvailabilityChanged;

    public int Revision { get; private set; }

    public int DishCount => snapshotsByDishId.Count;

    public int LowStockPortionThreshold =>
        Mathf.Max(1, lowStockPortionThreshold);

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();

        // Durante el arranque Unity no garantiza el orden de OnEnable entre
        // componentes. Start realiza el primer cálculo cuando todos los Awake
        // han finalizado. Las reactivaciones posteriores se difieren porque
        // guardado/carga y la salida de Play Mode pueden reactivar componentes
        // mientras el inventario está sustituyendo temporalmente su estado.
        if (hasStarted && Application.isPlaying)
        {
            RequestRecalculationAfterEnable();
        }
    }

    private void Start()
    {
        hasStarted = true;
        CancelEnableRecalculation();

        if (!RecalculateAll(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnDisable()
    {
        CancelEnableRecalculation();
        Unsubscribe();
    }

    private void RequestRecalculationAfterEnable()
    {
        CancelEnableRecalculation();

        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        enableRecalculationRoutine =
            StartCoroutine(RecalculateAfterEnableRoutine());
    }

    private void CancelEnableRecalculation()
    {
        if (enableRecalculationRoutine == null)
        {
            return;
        }

        StopCoroutine(enableRecalculationRoutine);
        enableRecalculationRoutine = null;
    }

    private IEnumerator RecalculateAfterEnableRoutine()
    {
        for (int frame = 0;
             frame < EnableRecalculationMaximumWaitFrames;
             frame++)
        {
            // Esperar al menos un frame permite que el proveedor de guardado
            // termine de sustituir inventario y carta antes de validar.
            yield return null;

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                enableRecalculationRoutine = null;
                yield break;
            }

            CacheDependenciesIfNeeded();

            if (!AreRuntimeDependenciesReady())
            {
                continue;
            }

            enableRecalculationRoutine = null;

            if (!RecalculateAll(out string error))
            {
                Debug.LogError(error, this);
            }

            yield break;
        }

        enableRecalculationRoutine = null;

        // Si la reactivación no era transitoria, mantenemos un diagnóstico
        // real tras dar tiempo suficiente a la reconstrucción. Durante la
        // salida de Play Mode esta rama no se ejecuta.
        if (Application.isPlaying && isActiveAndEnabled &&
            !RecalculateAll(out string finalError))
        {
            Debug.LogError(finalError, this);
        }
    }

    private bool AreRuntimeDependenciesReady()
    {
        CacheDependenciesIfNeeded();

        if (recipeCatalogService == null ||
            inventoryService == null ||
            menuService == null ||
            orderIntegration == null)
        {
            return false;
        }

        if (!inventoryService.IsInitialized)
        {
            return false;
        }

        int expectedIngredientCount = recipeCatalogService.IngredientCount;

        return expectedIngredientCount >= 0 &&
               inventoryService.StockEntryCount == expectedIngredientCount &&
               IsConcreteMealService(orderIntegration.CurrentMealService);
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (!recipeCatalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (inventoryService == null)
        {
            error = "Falta BistroBuilderInventoryService.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error))
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

        if (orderIntegration == null)
        {
            error = "Falta BistroBuilderCanonicalOrderIntegrationService.";
            return false;
        }

        if (!IsConcreteMealService(orderIntegration.CurrentMealService))
        {
            error = "La integración de comandas no expone un servicio " +
                    "concreto (desayuno, comida o cena).";
            return false;
        }

        if (lowStockPortionThreshold < 1)
        {
            error = "El umbral de últimas raciones debe ser positivo.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Comprueba que las dependencias runtime están preparadas para calcular
    /// disponibilidad. A diferencia de ValidateConfiguration, esta validación
    /// exige que el inventario ya haya construido todos sus balances.
    /// </summary>
    public bool ValidateRuntimeReadiness(out string error)
    {
        error = string.Empty;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!inventoryService.IsInitialized)
        {
            error = "El inventario canónico no está inicializado.";
            return false;
        }

        if (!inventoryService.ValidateRuntimeState(out error))
        {
            return false;
        }

        return true;
    }

    public bool RecalculateAll(out string error)
    {
        error = string.Empty;

        if (!ValidateRuntimeReadiness(out error))
        {
            initialized = false;
            return false;
        }

        if (!menuService.TryGetSnapshot(menuBuffer, out error))
        {
            initialized = false;
            return false;
        }

        var next =
            new Dictionary<string, BistroBuilderDishAvailabilitySnapshot>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < menuBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuBuffer[index];

            if (!TryCalculateSnapshot(item, out var snapshot, out error))
            {
                initialized = false;
                return false;
            }

            next.Add(snapshot.DishId, snapshot);
        }

        List<BistroBuilderDishAvailabilityChangedEvent> changes =
            PrepareDifferences(next);

        snapshotsByDishId.Clear();
        foreach (KeyValuePair<string, BistroBuilderDishAvailabilitySnapshot>
                 pair in next)
        {
            snapshotsByDishId.Add(pair.Key, pair.Value);
        }

        initialized = true;
        PublishChanges(changes);
        return true;
    }

    public bool TryGetSnapshot(
        string dishId,
        out BistroBuilderDishAvailabilitySnapshot snapshot
    )
    {
        snapshot = default;
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!initialized && !RecalculateAll(out _))
        {
            return false;
        }

        return snapshotsByDishId.TryGetValue(normalized, out snapshot);
    }

    public bool TryEvaluateForService(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderDishAvailabilitySnapshot snapshot,
        out string error
    )
    {
        snapshot = default;
        error = string.Empty;

        if (!IsConcreteMealService(mealService))
        {
            error = "Debe indicarse un servicio concreto.";
            return false;
        }

        if (!ValidateRuntimeReadiness(out error))
        {
            return false;
        }

        if (!menuService.TryGetItemSnapshot(
                dishId,
                out BistroBuilderMenuItemRuntimeState item
            ) ||
            item == null)
        {
            error = "El plato no está incluido en la carta.";
            return false;
        }

        return TryCalculateSnapshot(
            item,
            mealService,
            out snapshot,
            out error
        );
    }

    /// <summary>
    /// Evalúa una entrada arbitraria de carta usando la misma autoridad de
    /// receta e inventario que 368EF. Está pensado para borradores de UI:
    /// no consulta ni modifica la carta operativa y nunca persiste el
    /// resultado derivado.
    /// </summary>
    public bool TryEvaluateMenuItem(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderDishAvailabilitySnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderDishAvailabilitySnapshot);

        if (!IsConcreteMealService(mealService))
        {
            error = "Debe indicarse un servicio concreto.";
            return false;
        }

        if (!ValidateRuntimeReadiness(out error))
        {
            return false;
        }

        if (item == null)
        {
            error = "No puede evaluarse una entrada de carta nula.";
            return false;
        }

        return TryCalculateSnapshot(
            item,
            mealService,
            out snapshot,
            out error
        );
    }

    public bool IsDishOrderable(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        out string rejectionReason
    )
    {
        rejectionReason = string.Empty;

        if (!TryEvaluateForService(
                dishId,
                mealService,
                out BistroBuilderDishAvailabilitySnapshot snapshot,
                out string error
            ))
        {
            rejectionReason = error;
            return false;
        }

        if (snapshot.IsOrderable)
        {
            return true;
        }

        rejectionReason = string.IsNullOrWhiteSpace(snapshot.Reason)
            ? "El plato no está disponible."
            : snapshot.Reason;
        return false;
    }

    private bool TryCalculateSnapshot(
        BistroBuilderMenuItemRuntimeState item,
        out BistroBuilderDishAvailabilitySnapshot snapshot,
        out string error
    )
    {
        BistroBuilderMealServiceAvailability currentMealService =
            orderIntegration != null
                ? orderIntegration.CurrentMealService
                : BistroBuilderMealServiceAvailability.Lunch;

        return TryCalculateSnapshot(
            item,
            currentMealService,
            out snapshot,
            out error
        );
    }

    private bool TryCalculateSnapshot(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderDishAvailabilitySnapshot snapshot,
        out string error
    )
    {
        snapshot = default;
        error = string.Empty;

        if (item == null)
        {
            error = "No puede evaluarse una entrada de carta nula.";
            return false;
        }

        string dishId = item.DishId;

        if (!item.Unlocked)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.Locked,
                "El plato todavía no está desbloqueado."
            );
            return true;
        }

        if (!item.Enabled)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.Disabled,
                "El plato está desactivado en la carta."
            );
            return true;
        }

        if (item.ManuallySoldOut)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.ManuallyPaused,
                "El plato está marcado manualmente como agotado."
            );
            return true;
        }

        if ((item.AvailableServices & mealService) == 0)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.UnavailableForService,
                "El plato no está disponible en este servicio."
            );
            return true;
        }

        string recipeError = string.Empty;
        if (!recipeCatalogService.TryGetRecipeByDishId(
                dishId,
                out BistroBuilderRecipeDefinition recipe
            ) ||
            recipe == null)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.InvalidRecipe,
                "El plato no tiene una receta válida."
            );
            return true;
        }

        if (!recipe.TryValidate(out recipeError))
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.InvalidRecipe,
                string.IsNullOrWhiteSpace(recipeError)
                    ? "El plato no tiene una receta válida."
                    : recipeError
            );
            return true;
        }

        long possiblePortions = long.MaxValue;
        string limitingIngredientId = string.Empty;
        long limitingAvailable = 0L;
        long limitingRequired = 0L;

        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount amount =
                recipe.Ingredients[index];

            if (amount == null || amount.Ingredient == null ||
                !amount.TryGetCanonicalMilliUnits(
                    out long batchQuantity,
                    out error
                ))
            {
                return false;
            }

            long perPortion = DivideCeiling(
                batchQuantity,
                recipe.YieldPortions
            );

            if (perPortion <= 0L ||
                !inventoryService.TryGetStockSnapshot(
                    amount.Ingredient.IngredientId,
                    out BistroBuilderInventoryStockSnapshot stock
                ))
            {
                snapshot = BuildTerminalSnapshot(
                    dishId,
                    BistroBuilderDishAvailabilityState.InvalidRecipe,
                    "La receta referencia un ingrediente sin balance válido."
                );
                return true;
            }

            long ingredientPortions =
                stock.AvailableCanonicalMilliUnits / perPortion;

            if (ingredientPortions < possiblePortions ||
                ingredientPortions == possiblePortions &&
                string.CompareOrdinal(
                    stock.IngredientId,
                    limitingIngredientId
                ) < 0)
            {
                possiblePortions = ingredientPortions;
                limitingIngredientId = stock.IngredientId;
                limitingAvailable = stock.AvailableCanonicalMilliUnits;
                limitingRequired = perPortion;
            }
        }

        if (possiblePortions == long.MaxValue)
        {
            snapshot = BuildTerminalSnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.InvalidRecipe,
                "La receta no contiene cantidades utilizables."
            );
            return true;
        }

        BistroBuilderDishAvailabilityState state = possiblePortions <= 0L
            ? BistroBuilderDishAvailabilityState.OutOfStock
            : possiblePortions <= LowStockPortionThreshold
                ? BistroBuilderDishAvailabilityState.LowStock
                : BistroBuilderDishAvailabilityState.Available;

        string reason = state switch
        {
            BistroBuilderDishAvailabilityState.OutOfStock =>
                "Sin stock. Ingrediente limitante: " +
                limitingIngredientId + ".",
            BistroBuilderDishAvailabilityState.LowStock =>
                "Últimas " + possiblePortions +
                " ración(es). Ingrediente limitante: " +
                limitingIngredientId + ".",
            _ => string.Empty
        };

        snapshot = new BistroBuilderDishAvailabilitySnapshot(
            dishId,
            state,
            possiblePortions,
            limitingIngredientId,
            limitingAvailable,
            limitingRequired,
            Revision,
            reason
        );
        return true;
    }

    private BistroBuilderDishAvailabilitySnapshot BuildTerminalSnapshot(
        string dishId,
        BistroBuilderDishAvailabilityState state,
        string reason
    )
    {
        return new BistroBuilderDishAvailabilitySnapshot(
            dishId,
            state,
            0L,
            string.Empty,
            0L,
            0L,
            Revision,
            reason
        );
    }

    private List<BistroBuilderDishAvailabilityChangedEvent>
        PrepareDifferences(
            Dictionary<string, BistroBuilderDishAvailabilitySnapshot> next
        )
    {
        var changes = new List<BistroBuilderDishAvailabilityChangedEvent>();
        bool changed = snapshotsByDishId.Count != next.Count;

        if (!changed)
        {
            foreach (KeyValuePair<string, BistroBuilderDishAvailabilitySnapshot>
                     pair in next)
            {
                if (!snapshotsByDishId.TryGetValue(
                        pair.Key,
                        out BistroBuilderDishAvailabilitySnapshot previous
                    ) ||
                    !AreEquivalent(previous, pair.Value))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return changes;
        }

        Revision++;
        var dishIds = new List<string>(next.Keys);
        dishIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < dishIds.Count; index++)
        {
            string dishId = dishIds[index];
            BistroBuilderDishAvailabilitySnapshot current =
                CopyWithRevision(next[dishId], Revision);
            next[dishId] = current;

            snapshotsByDishId.TryGetValue(
                dishId,
                out BistroBuilderDishAvailabilitySnapshot previous
            );

            if (!AreEquivalent(previous, current))
            {
                changes.Add(
                    new BistroBuilderDishAvailabilityChangedEvent(
                        previous,
                        current
                    )
                );
            }
        }

        // Un plato retirado de la carta también publica una transición a un
        // estado vacío, para que futuras UIs puedan eliminarlo sin sondeo.
        foreach (KeyValuePair<string, BistroBuilderDishAvailabilitySnapshot>
                 pair in snapshotsByDishId)
        {
            if (!next.ContainsKey(pair.Key))
            {
                changes.Add(
                    new BistroBuilderDishAvailabilityChangedEvent(
                        pair.Value,
                        default
                    )
                );
            }
        }

        return changes;
    }

    private void PublishChanges(
        List<BistroBuilderDishAvailabilityChangedEvent> changes
    )
    {
        if (changes == null)
        {
            return;
        }

        for (int index = 0; index < changes.Count; index++)
        {
            BistroBuilderDishAvailabilityChangedEvent change = changes[index];
            AvailabilityChanged?.Invoke(change);

            if (logChanges &&
                !string.IsNullOrWhiteSpace(change.Current.DishId))
            {
                Debug.Log(
                    "Disponibilidad " + change.Current.DishId + ": " +
                    change.Current.State + " (" +
                    change.Current.AvailablePortions + " raciones).",
                    this
                );
            }
        }
    }

    private static BistroBuilderDishAvailabilitySnapshot CopyWithRevision(
        BistroBuilderDishAvailabilitySnapshot source,
        int revision
    )
    {
        return new BistroBuilderDishAvailabilitySnapshot(
            source.DishId,
            source.State,
            source.AvailablePortions,
            source.LimitingIngredientId,
            source.LimitingIngredientAvailableCanonicalMilliUnits,
            source.LimitingIngredientRequiredCanonicalMilliUnits,
            revision,
            source.Reason
        );
    }

    private static bool AreEquivalent(
        BistroBuilderDishAvailabilitySnapshot left,
        BistroBuilderDishAvailabilitySnapshot right
    )
    {
        return string.Equals(left.DishId, right.DishId, StringComparison.Ordinal) &&
               left.State == right.State &&
               left.AvailablePortions == right.AvailablePortions &&
               string.Equals(
                   left.LimitingIngredientId,
                   right.LimitingIngredientId,
                   StringComparison.Ordinal
               ) &&
               left.LimitingIngredientAvailableCanonicalMilliUnits ==
                   right.LimitingIngredientAvailableCanonicalMilliUnits &&
               left.LimitingIngredientRequiredCanonicalMilliUnits ==
                   right.LimitingIngredientRequiredCanonicalMilliUnits;
    }

    private void HandleInventoryChanged(
        BistroBuilderInventoryStockSnapshot changedStock
    )
    {
        RecalculateAll(out string ignoredError);
    }

    private void HandleReservationChanged(
        BistroBuilderInventoryReservationSnapshot changedReservation
    )
    {
        RecalculateAll(out string ignoredError);
    }

    private void HandleMenuChanged(
        BistroBuilderMenuChangedEvent menuChangedEvent
    )
    {
        RecalculateAll(out string ignoredError);
    }

    private void HandleMealServiceChanged(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        RecalculateAll(out string ignoredError);
    }

    private void Subscribe()
    {
        Unsubscribe();

        if (inventoryService != null)
        {
            inventoryService.StockChanged += HandleInventoryChanged;
            inventoryService.ReservationChanged += HandleReservationChanged;
        }

        if (menuService != null)
        {
            menuService.MenuChanged += HandleMenuChanged;
        }

        if (orderIntegration != null)
        {
            orderIntegration.CurrentMealServiceChanged +=
                HandleMealServiceChanged;
        }
    }

    private void Unsubscribe()
    {
        if (inventoryService != null)
        {
            inventoryService.StockChanged -= HandleInventoryChanged;
            inventoryService.ReservationChanged -= HandleReservationChanged;
        }

        if (menuService != null)
        {
            menuService.MenuChanged -= HandleMenuChanged;
        }

        if (orderIntegration != null)
        {
            orderIntegration.CurrentMealServiceChanged -=
                HandleMealServiceChanged;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }

        if (inventoryService == null)
        {
            TryGetComponent(out inventoryService);
        }

        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (orderIntegration == null)
        {
            TryGetComponent(out orderIntegration);
        }
    }

    private static long DivideCeiling(long numerator, int denominator)
    {
        if (numerator <= 0L || denominator <= 0)
        {
            return 0L;
        }

        long quotient = numerator / denominator;
        return numerator % denominator == 0L ? quotient : quotient + 1L;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        lowStockPortionThreshold = Mathf.Max(1, lowStockPortionThreshold);
        CacheDependenciesIfNeeded();
    }
#endif
    private static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        return mealService == BistroBuilderMealServiceAvailability.Breakfast ||
               mealService == BistroBuilderMealServiceAvailability.Lunch ||
               mealService == BistroBuilderMealServiceAvailability.Dinner;
    }

}
