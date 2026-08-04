using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada canónica de oferta comercial 2.1C.
///
/// Es el único punto de lectura que combina:
/// - carta activa por restaurante;
/// - precio, orden y plato firma;
/// - desayuno, comida o cena;
/// - mesa, barra completa o espera en barra;
/// - receta e inventario derivados por 368EF.
///
/// No persiste disponibilidad ni utiliza Update. Los consumidores solicitan
/// snapshots inmutables y reciben un único evento consolidado al cambiar una
/// de las autoridades de origen.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Offer Service")]
public sealed class BistroBuilderMenuOfferService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1C";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderRestaurantMenuCollectionService collectionService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderDishAvailabilityService availabilityService;

    [SerializeField]
    private BistroBuilderCanonicalOrderIntegrationService orderIntegration;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges;

    private readonly List<BistroBuilderMenuItemRuntimeState> menuBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private bool subscribed;
    private bool initialized;
    private bool hasPublishedSourceState;
    private int lastPublishedMenuRevision = -1;
    private int lastPublishedAvailabilityRevision = -1;
    private string lastPublishedRestaurantId = string.Empty;
    private BistroBuilderMealServiceAvailability lastPublishedMealService =
        BistroBuilderMealServiceAvailability.None;

    public event Action<BistroBuilderMenuOfferChangedEvent> OfferChanged;

    public int Revision { get; private set; }

    public string ActiveRestaurantId => collectionService != null
        ? collectionService.ActiveRestaurantId
        : BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public BistroBuilderMealServiceAvailability CurrentMealService =>
        orderIntegration != null
            ? orderIntegration.CurrentMealService
            : BistroBuilderMealServiceAvailability.Lunch;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderRestaurantMenuCollectionService CollectionService =>
        collectionService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public BistroBuilderDishAvailabilityService AvailabilityService =>
        availabilityService;

    public BistroBuilderCanonicalOrderIntegrationService OrderIntegration =>
        orderIntegration;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            initialized = false;
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (collectionService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuCollectionService.";
            initialized = false;
            return false;
        }

        if (!collectionService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (!ReferenceEquals(collectionService.MenuService, menuService))
        {
            error = "La oferta y la colección no comparten la carta activa.";
            initialized = false;
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            initialized = false;
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error) ||
            !ReferenceEquals(menuService.CatalogService, catalogService))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "La oferta no comparte el catálogo canónico.";
            }

            initialized = false;
            return false;
        }

        if (availabilityService == null)
        {
            error = "Falta BistroBuilderDishAvailabilityService.";
            initialized = false;
            return false;
        }

        if (!availabilityService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (orderIntegration == null)
        {
            error = "Falta BistroBuilderCanonicalOrderIntegrationService.";
            initialized = false;
            return false;
        }

        if (!BistroBuilderMenuOfferContext.IsConcreteMealService(
                orderIntegration.CurrentMealService
            ))
        {
            error = "La oferta no recibe un servicio del día concreto.";
            initialized = false;
            return false;
        }

        initialized = true;
        error = string.Empty;
        return true;
    }

    public bool TryGetCurrentOffer(
        BistroBuilderServiceMode serviceMode,
        bool includeUnavailable,
        List<BistroBuilderMenuOfferItemSnapshot> destination,
        out string error
    )
    {
        return TryGetOffer(
            CurrentMealService,
            serviceMode,
            includeUnavailable,
            destination,
            out error
        );
    }

    public bool TryGetOffer(
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        bool includeUnavailable,
        List<BistroBuilderMenuOfferItemSnapshot> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino de la oferta es nulo.";
            return false;
        }

        destination.Clear();

        BistroBuilderMenuOfferContext context =
            new BistroBuilderMenuOfferContext(mealService, serviceMode);

        if (!context.TryValidate(out error) || !EnsureReady(out error))
        {
            return false;
        }

        if (!menuService.TryGetSnapshot(menuBuffer, out error))
        {
            return false;
        }

        for (int index = 0; index < menuBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuBuffer[index];

            if (!TryEvaluateItem(
                    item,
                    context,
                    out BistroBuilderMenuOfferItemSnapshot snapshot,
                    out error
                ))
            {
                destination.Clear();
                return false;
            }

            if (includeUnavailable || snapshot.IsOrderable)
            {
                destination.Add(snapshot);
            }
        }

        destination.Sort(CompareOfferItems);
        error = string.Empty;
        return true;
    }

    public bool TryEvaluateDish(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        out BistroBuilderMenuOfferItemSnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderMenuOfferItemSnapshot);

        BistroBuilderMenuOfferContext context =
            new BistroBuilderMenuOfferContext(mealService, serviceMode);

        if (!context.TryValidate(out error) || !EnsureReady(out error))
        {
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            error = "El DishId indicado no es válido.";
            return false;
        }

        if (!menuService.TryGetItemSnapshot(
                normalized,
                out BistroBuilderMenuItemRuntimeState item
            ) || item == null)
        {
            error = "El plato no forma parte de la carta activa.";
            return false;
        }

        return TryEvaluateItem(item, context, out snapshot, out error);
    }

    public bool IsDishOrderable(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        out BistroBuilderMenuOfferRejectionReason rejectionReason,
        out string rejectionMessage
    )
    {
        rejectionReason = BistroBuilderMenuOfferRejectionReason.None;
        rejectionMessage = string.Empty;

        BistroBuilderMenuOfferContext context =
            new BistroBuilderMenuOfferContext(mealService, serviceMode);

        if (!context.TryValidate(out rejectionMessage) ||
            !EnsureReady(out rejectionMessage))
        {
            rejectionReason =
                BistroBuilderMenuOfferRejectionReason.InvalidContext;
            return false;
        }

        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            rejectionReason =
                BistroBuilderMenuOfferRejectionReason.InvalidContext;
            rejectionMessage = "El DishId indicado no es válido.";
            return false;
        }

        if (!menuService.TryGetItemSnapshot(
                normalized,
                out BistroBuilderMenuItemRuntimeState item
            ) || item == null)
        {
            rejectionReason =
                BistroBuilderMenuOfferRejectionReason.DishNotInMenu;
            rejectionMessage =
                "El plato no forma parte de la carta activa.";
            return false;
        }

        if (!TryEvaluateItem(
                item,
                context,
                out BistroBuilderMenuOfferItemSnapshot snapshot,
                out rejectionMessage
            ))
        {
            rejectionReason =
                BistroBuilderMenuOfferRejectionReason.InvalidContext;
            return false;
        }

        if (snapshot.IsOrderable)
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionReason = snapshot.PrimaryRejectionReason;
        rejectionMessage = string.IsNullOrWhiteSpace(snapshot.RejectionMessage)
            ? "El plato no está disponible."
            : snapshot.RejectionMessage;
        return false;
    }

    public bool IsDishOrderable(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        out string rejectionMessage
    )
    {
        return IsDishOrderable(
            dishId,
            mealService,
            serviceMode,
            out _,
            out rejectionMessage
        );
    }

    private bool TryEvaluateItem(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMenuOfferContext context,
        out BistroBuilderMenuOfferItemSnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderMenuOfferItemSnapshot);

        if (item == null)
        {
            error = "La carta activa contiene una entrada nula.";
            return false;
        }

        catalogService.TryGetDefinition(
            item.DishId,
            out BistroBuilderDishDefinition definition
        );

        if (!availabilityService.TryEvaluateForService(
                item.DishId,
                context.MealService,
                out BistroBuilderDishAvailabilitySnapshot availability,
                out error
            ))
        {
            return false;
        }

        return BistroBuilderMenuOfferEvaluator.TryEvaluate(
            ActiveRestaurantId,
            item,
            definition,
            availability,
            menuService.CommercialPolicy,
            context,
            Revision,
            out snapshot,
            out error
        );
    }

    private bool EnsureReady(out string error)
    {
        if (!initialized && !ValidateConfiguration(out error))
        {
            return false;
        }

        if (availabilityService == null)
        {
            error = "Falta BistroBuilderDishAvailabilityService.";
            initialized = false;
            return false;
        }

        return availabilityService.ValidateRuntimeReadiness(out error);
    }

    private void HandleMenuChanged(BistroBuilderMenuChangedEvent change)
    {
        Publish(
            BistroBuilderMenuOfferChangeType.MenuChanged,
            change != null ? change.DishId : string.Empty,
            "La carta activa cambió."
        );
    }

    private void HandleAvailabilityChanged(
        BistroBuilderDishAvailabilityChangedEvent change
    )
    {
        Publish(
            BistroBuilderMenuOfferChangeType.AvailabilityChanged,
            !string.IsNullOrWhiteSpace(change.Current.DishId)
                ? change.Current.DishId
                : change.Previous.DishId,
            "La disponibilidad derivada cambió."
        );
    }

    private void HandleActiveRestaurantChanged(
        string previousRestaurantId,
        string currentRestaurantId
    )
    {
        Publish(
            BistroBuilderMenuOfferChangeType.ActiveRestaurantChanged,
            string.Empty,
            "Cambió el restaurante activo."
        );
    }

    private void HandleMealServiceChanged(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        Publish(
            BistroBuilderMenuOfferChangeType.MealServiceChanged,
            string.Empty,
            "Cambió el servicio del día."
        );
    }

    private void Publish(
        BistroBuilderMenuOfferChangeType changeType,
        string dishId,
        string description
    )
    {
        int menuRevision = menuService != null ? menuService.Revision : -1;
        int availabilityRevision = availabilityService != null
            ? availabilityService.Revision
            : -1;
        string restaurantId = ActiveRestaurantId;
        BistroBuilderMealServiceAvailability mealService =
            CurrentMealService;

        // Una mutación de carta puede recalcular disponibilidad de forma
        // síncrona y producir dos callbacks encadenados. La tupla de
        // autoridades evita dos refrescos idénticos sin ocultar cambios de
        // precio, inventario, restaurante o franja.
        if (hasPublishedSourceState &&
            lastPublishedMenuRevision == menuRevision &&
            lastPublishedAvailabilityRevision == availabilityRevision &&
            string.Equals(
                lastPublishedRestaurantId,
                restaurantId,
                StringComparison.Ordinal
            ) &&
            lastPublishedMealService == mealService)
        {
            return;
        }

        hasPublishedSourceState = true;
        lastPublishedMenuRevision = menuRevision;
        lastPublishedAvailabilityRevision = availabilityRevision;
        lastPublishedRestaurantId = restaurantId;
        lastPublishedMealService = mealService;
        Revision++;

        BistroBuilderMenuOfferChangedEvent change =
            new BistroBuilderMenuOfferChangedEvent(
                changeType,
                restaurantId,
                dishId,
                mealService,
                Revision,
                description
            );

        OfferChanged?.Invoke(change);

        if (logChanges)
        {
            Debug.Log(
                "Oferta de carta: " + changeType +
                ". Restaurante: " + restaurantId +
                ". Servicio: " + mealService +
                ". Revisión: " + Revision + ".",
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

        CacheDependenciesIfNeeded();

        if (menuService != null)
        {
            menuService.MenuChanged += HandleMenuChanged;
        }

        if (availabilityService != null)
        {
            availabilityService.AvailabilityChanged +=
                HandleAvailabilityChanged;
        }

        if (collectionService != null)
        {
            collectionService.ActiveRestaurantChanged +=
                HandleActiveRestaurantChanged;
        }

        if (orderIntegration != null)
        {
            orderIntegration.CurrentMealServiceChanged +=
                HandleMealServiceChanged;
        }

        subscribed = true;
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

        if (availabilityService != null)
        {
            availabilityService.AvailabilityChanged -=
                HandleAvailabilityChanged;
        }

        if (collectionService != null)
        {
            collectionService.ActiveRestaurantChanged -=
                HandleActiveRestaurantChanged;
        }

        if (orderIntegration != null)
        {
            orderIntegration.CurrentMealServiceChanged -=
                HandleMealServiceChanged;
        }

        subscribed = false;
    }

    private void CacheDependenciesIfNeeded()
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

        if (availabilityService == null)
        {
            TryGetComponent(out availabilityService);
        }

        if (orderIntegration == null)
        {
            TryGetComponent(out orderIntegration);
        }
    }

    private static int CompareOfferItems(
        BistroBuilderMenuOfferItemSnapshot first,
        BistroBuilderMenuOfferItemSnapshot second
    )
    {
        int orderComparison =
            first.DisplayOrder.CompareTo(second.DisplayOrder);

        return orderComparison != 0
            ? orderComparison
            : string.Compare(
                first.DishId,
                second.DishId,
                StringComparison.Ordinal
            );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
