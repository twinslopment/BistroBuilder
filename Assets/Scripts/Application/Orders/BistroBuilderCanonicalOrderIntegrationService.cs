using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente transitorio entre el servicio legacy y las comandas canónicas.
///
/// Responsabilidades:
/// - Crear una comanda canónica antes de crear RestaurantOrder.
/// - Generar una línea por miembro de CustomerGroup.
/// - Conservar un enlace único legacy OrderId -> Canonical OrderId.
/// - Aprobar cada transición legacy solo después de aplicarla de forma
///   atómica en la autoridad canónica.
/// - Impedir divergencias si falla la carta, una referencia o una transición.
///
/// No ejecuta cocina, movimiento ni entrega. Esas responsabilidades continúan
/// en sus sistemas actuales hasta los siguientes bloques de integración.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Orders/Canonical Order Integration Service"
)]
public sealed class BistroBuilderCanonicalOrderIntegrationService :
    MonoBehaviour,
    IRestaurantOrderTransitionGate
{
    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [Header("Servicio provisional")]

    [Tooltip(
        "Servicio utilizado hasta que el sistema definitivo de horarios " +
        "publique la franja activa."
    )]
    [SerializeField]
    private BistroBuilderMealServiceAvailability currentMealService =
        BistroBuilderMealServiceAvailability.Lunch;

    [SerializeField, Range(0, 20)]
    private int defaultCourseIndex = 1;

    [Header("Depuración")]

    [SerializeField]
    private bool logSynchronization = true;

    private readonly List<string> customerIds =
        new List<string>(16);

    private readonly Dictionary<int, string> canonicalByLegacyOrderId =
        new Dictionary<int, string>();

    private readonly Dictionary<string, int> legacyByCanonicalOrderId =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private bool initialized;

    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;

    public BistroBuilderMealServiceAvailability CurrentMealService =>
        currentMealService;

    public int DefaultCourseIndex => defaultCourseIndex;

    public int ActiveLinkCount => canonicalByLegacyOrderId.Count;

    private void Awake()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (canonicalOrderService == null)
        {
            initialized = false;
            error = "Falta BistroBuilderCanonicalOrderService.";
            return false;
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        if (!IsConcreteMealService(currentMealService))
        {
            initialized = false;
            error =
                "La integración necesita un servicio concreto válido.";
            return false;
        }

        if (defaultCourseIndex < 0 ||
            defaultCourseIndex > 20)
        {
            initialized = false;
            error = "El pase predeterminado queda fuera de rango.";
            return false;
        }

        initialized = true;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Permite que el futuro sistema de horarios cambie la franja activa sin
    /// modificar OrderSystem ni el dominio de comandas.
    /// </summary>
    public bool TrySetCurrentMealService(
        BistroBuilderMealServiceAvailability mealService,
        out string error
    )
    {
        if (!IsConcreteMealService(mealService))
        {
            error = "Debe indicarse un único servicio válido.";
            return false;
        }

        currentMealService = mealService;
        initialized = false;
        error = string.Empty;
        return true;
    }

    public bool TryCreateCanonicalOrder(
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter waiter,
        int legacyOrderId,
        out string canonicalOrderId,
        out string error
    )
    {
        canonicalOrderId = string.Empty;

        if (!EnsureReady(out error))
        {
            return false;
        }

        if (table == null)
        {
            error = "No se puede crear una comanda sin mesa.";
            return false;
        }

        if (customerGroup == null)
        {
            error = "No se puede crear una comanda sin grupo.";
            return false;
        }

        if (waiter == null)
        {
            error = "No se puede crear una comanda sin camarero.";
            return false;
        }

        if (legacyOrderId < 1)
        {
            error = "La identidad legacy de comanda no es válida.";
            return false;
        }

        if (!ReferenceEquals(
                table.AssignedCustomerGroup,
                customerGroup
            ))
        {
            error =
                "La mesa no está ocupada por el grupo indicado.";
            return false;
        }

        if (!BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    customerGroup.GroupId,
                    customerGroup.GroupSize,
                    customerIds,
                    out error
                ))
        {
            return false;
        }

        string externalReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(legacyOrderId);
        string tableReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildTableReference(table.TableId);
        string groupReferenceId =
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(customerGroup.GroupId);

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryCreateIndividualOrder(
                externalReferenceId,
                tableReferenceId,
                groupReferenceId,
                customerIds,
                currentMealService,
                defaultCourseIndex,
                out BistroBuilderCanonicalOrder snapshot
            );

        if (!result.Succeeded ||
            snapshot == null)
        {
            error = string.IsNullOrWhiteSpace(result.Message)
                ? "La autoridad canónica rechazó la comanda."
                : result.Message;
            return false;
        }

        if (!string.Equals(
                snapshot.ExternalReferenceId,
                externalReferenceId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.TableReferenceId,
                tableReferenceId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.CustomerGroupReferenceId,
                groupReferenceId,
                StringComparison.Ordinal
            ) ||
            snapshot.Lines.Count != customerGroup.GroupSize)
        {
            TryRollbackUnregisteredCanonicalOrder(
                snapshot.OrderId,
                out _
            );

            error =
                "La comanda canónica creada no conserva todas sus " +
                "referencias o líneas.";
            return false;
        }

        canonicalOrderId = snapshot.OrderId;
        error = string.Empty;
        return true;
    }

    public bool TryRegisterLegacyOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error =
                "La comanda legacy no contiene un CanonicalOrderId válido.";
            return false;
        }

        if (canonicalByLegacyOrderId.ContainsKey(order.OrderId))
        {
            error =
                "El OrderId legacy ya está registrado en la integración.";
            return false;
        }

        if (legacyByCanonicalOrderId.ContainsKey(order.CanonicalOrderId))
        {
            error =
                "El CanonicalOrderId ya está enlazado a otra comanda.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            error =
                "La comanda canónica enlazada no existe en el runtime.";
            return false;
        }

        string expectedExternal =
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(order.OrderId);
        string expectedTable =
            BistroBuilderServiceOrderIdentityUtility
                .BuildTableReference(order.Table.TableId);
        string expectedGroup =
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(order.CustomerGroup.GroupId);

        if (!string.Equals(
                snapshot.ExternalReferenceId,
                expectedExternal,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.TableReferenceId,
                expectedTable,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                snapshot.CustomerGroupReferenceId,
                expectedGroup,
                StringComparison.Ordinal
            ) ||
            snapshot.Lines.Count != order.CustomerGroup.GroupSize)
        {
            error =
                "Las referencias del enlace legacy-canónico no coinciden.";
            return false;
        }

        canonicalByLegacyOrderId.Add(
            order.OrderId,
            order.CanonicalOrderId
        );
        legacyByCanonicalOrderId.Add(
            order.CanonicalOrderId,
            order.OrderId
        );

        error = string.Empty;
        return true;
    }

    public bool TryApproveTransition(
        RestaurantOrder order,
        OrderState currentState,
        OrderState targetState,
        out string error
    )
    {
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error =
                "La comanda legacy no está enlazada a una comanda canónica.";
            return false;
        }

        if (!canonicalByLegacyOrderId.TryGetValue(
                order.OrderId,
                out string registeredCanonicalId
            ) ||
            !string.Equals(
                registeredCanonicalId,
                order.CanonicalOrderId,
                StringComparison.Ordinal
            ))
        {
            error =
                "El enlace legacy-canónico no está registrado o ha cambiado.";
            return false;
        }

        if (!BistroBuilderLegacyCanonicalOrderStateMap.TryGetLineTarget(
                targetState,
                out BistroBuilderCanonicalOrderLineState targetLineState,
                out bool cancelOrder
            ))
        {
            error =
                "No existe traducción canónica para el estado " +
                targetState + ".";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result;

        if (cancelOrder)
        {
            result = canonicalOrderService.TryCancelOrder(
                order.CanonicalOrderId,
                BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(order.AssignedWaiter.WaiterId)
            );
        }
        else
        {
            result = canonicalOrderService.TryAdvanceAllLinesToState(
                order.CanonicalOrderId,
                targetLineState,
                BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(order.AssignedWaiter.WaiterId)
            );
        }

        if (!result.Succeeded)
        {
            error =
                "La transición canónica fue rechazada. " +
                result.Message;
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            error =
                "La comanda canónica no pudo verificarse tras la transición.";
            return false;
        }

        if (!BistroBuilderLegacyCanonicalOrderStateMap
                .IsAggregateCompatible(
                    targetState,
                    snapshot.State
                ))
        {
            error =
                "El estado canónico resultante " + snapshot.State +
                " no es compatible con el estado legacy " +
                targetState + ".";
            return false;
        }

        if (logSynchronization)
        {
            Debug.Log(
                "367C sincroniza comanda legacy " +
                order.OrderId + " (" + currentState + " -> " +
                targetState + ") con " +
                order.CanonicalOrderId + " (" +
                snapshot.State + ").",
                this
            );
        }

        error = string.Empty;
        return true;
    }

    public void NotifyLegacyOrderRemoved(RestaurantOrder order)
    {
        if (order == null)
        {
            return;
        }

        if (canonicalByLegacyOrderId.TryGetValue(
                order.OrderId,
                out string canonicalOrderId
            ))
        {
            canonicalByLegacyOrderId.Remove(order.OrderId);
            legacyByCanonicalOrderId.Remove(canonicalOrderId);
        }
    }

    /// <summary>
    /// Revierte una comanda canónica recién creada cuando todavía no existe
    /// RestaurantOrder. Se usa únicamente durante la creación atómica.
    /// </summary>
    public bool TryRollbackUnregisteredCanonicalOrder(
        string canonicalOrderId,
        out string error
    )
    {
        error = string.Empty;

        if (canonicalOrderService == null)
        {
            error = "La autoridad canónica no está disponible.";
            return false;
        }

        string normalized =
            BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (!canonicalOrderService.TryGetOrderSnapshot(
                normalized,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            return true;
        }

        if (!snapshot.IsTerminal)
        {
            BistroBuilderCanonicalOrderOperationResult cancelResult =
                canonicalOrderService.TryCancelOrder(
                    normalized,
                    "integration_rollback"
                );

            if (!cancelResult.Succeeded)
            {
                error = cancelResult.Message;
                return false;
            }
        }

        BistroBuilderCanonicalOrderOperationResult removeResult =
            canonicalOrderService.TryRemoveTerminalOrder(normalized);

        if (!removeResult.Succeeded)
        {
            error = removeResult.Message;
            return false;
        }

        return true;
    }

    public bool TryGetLinkedCanonicalOrderId(
        int legacyOrderId,
        out string canonicalOrderId
    )
    {
        return canonicalByLegacyOrderId.TryGetValue(
            legacyOrderId,
            out canonicalOrderId
        );
    }

    private bool EnsureReady(out string error)
    {
        if (initialized &&
            canonicalOrderService != null)
        {
            error = string.Empty;
            return true;
        }

        return ValidateConfiguration(out error);
    }

    private static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                mealService,
                false
            ))
        {
            return false;
        }

        int value = (int)mealService;

        // Un único bit activo representa Breakfast, Lunch o Dinner.
        return value > 0 &&
               (value & (value - 1)) == 0;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (canonicalOrderService == null)
        {
            TryGetComponent(out canonicalOrderService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        defaultCourseIndex = Mathf.Clamp(
            defaultCourseIndex,
            0,
            20
        );
    }
#endif
}
