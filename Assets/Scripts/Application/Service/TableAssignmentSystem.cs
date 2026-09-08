using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Asigna grupos de clientes a las mesas operativas disponibles.
///
/// Las mesas ya no proceden de un array fijo del Inspector.
/// Se sincronizan dinÃ¡micamente mediante RestaurantTableRegistry,
/// por lo que una mesa aÃ±adida desde el modo ediciÃ³n podrÃ¡ participar
/// sin reiniciar la escena.
/// </summary>
public sealed class TableAssignmentSystem :
    MonoBehaviour
{
    [Header("Elementos iniciales")]

    [FormerlySerializedAs("customerGroups")]
    [SerializeField]
    private CustomerGroup[] initialCustomerGroups;

    [Header("Sistemas")]

    [Tooltip(
        "Registro dinÃ¡mico de las mesas operativas."
    )]
    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [Tooltip(
        "Autoridad de barra que debe cerrar una sesiÃ³n WaitingAtBar antes " +
        "de que el grupo pueda caminar a una mesa."
    )]
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Tooltip("Autoridad avanzada de cola, reservas y rotaciÃƒÂ³n del Bloque 14.")]
    [SerializeField]
    private BistroBuilderAdvancedFrontOfHouseService advancedFrontOfHouseService;

    [Tooltip("Autoridad Ãºnica del derecho lÃ³gico grupo â†’ mesa.")]
    [SerializeField]
    private BistroBuilderSeatingReservationCoordinator seatingReservationCoordinator;

    private readonly List<CustomerGroup>
        registeredGroups =
            new List<CustomerGroup>();

    private readonly List<CustomerGroup>
        waitingGroups =
            new List<CustomerGroup>();

    private readonly HashSet<RestaurantTable>
        registeredTables =
            new HashSet<RestaurantTable>();


    /// <summary>Se emite tras registrar el grupo y antes de intentar asignarle mesa.</summary>
    public event Action<CustomerGroup> CustomerGroupRegistered;

<<<<<<< Updated upstream
=======
    public event Action<CustomerGroup, RestaurantTable> TableAssigned;

>>>>>>> Stashed changes
    public IReadOnlyList<CustomerGroup>
        RegisteredGroups
    {
        get
        {
            return registeredGroups;
        }
    }

    public int RegisteredTableCount
    {
        get
        {
            return registeredTables.Count;
        }
    }

    /// <summary>
    /// Consulta la reserva lÃ³gica de transiciÃ³n a mesa de un grupo que sigue
    /// ocupando barra. La reserva no modifica el estado pÃºblico de la mesa.
    /// </summary>
    public bool TryGetPendingBarTransitionTable(
        CustomerGroup group,
        out RestaurantTable table
    )
    {
        table = null;
        return group != null && group.IsOccupyingBar && !group.HasAssignedTable &&
               seatingReservationCoordinator != null &&
               seatingReservationCoordinator.TryGetTableRight(group, out table, out _);
    }

    /// <summary>
    /// Elimina exclusivamente las reservas lÃ³gicas transitorias antes de una
    /// carga. Las asignaciones reales grupo-mesa se limpian por sus propios
    /// agregados.
    /// </summary>
    public void ClearPendingBarTransitionReservationsForRuntimeLoad()
    {
<<<<<<< Updated upstream
        pendingBarTableReservations.Clear();
        reservedForBarTransitions.Clear();
=======
        if (seatingReservationCoordinator == null) return;
        for (int i = 0; i < registeredGroups.Count; i++)
        {
            CustomerGroup group = registeredGroups[i];
            if (group != null && !group.HasAssignedTable)
                seatingReservationCoordinator.ReleaseGroupRight(group);
        }
>>>>>>> Stashed changes
    }

    /// <summary>
    /// Reconstruye una reserva WaitingAtBar desde service.runtime sin ejecutar
    /// una nueva selecciÃ³n de mesa ni publicar eventos prematuros.
    /// </summary>
    public bool TryRestorePendingBarTransitionReservation(
        CustomerGroup group,
        RestaurantTable table,
        out string error
    )
    {
        error = string.Empty;

        if (group == null || table == null)
        {
            error = "No puede restaurarse una reserva de transiciÃ³n nula.";
            return false;
        }

        if (!registeredGroups.Contains(group) ||
            !registeredTables.Contains(table))
        {
            error = "El grupo o la mesa de transiciÃ³n no estÃ¡n registrados.";
            return false;
        }

        if (group.RequestedServiceMode !=
                BistroBuilderServiceMode.WaitingAtBar ||
            group.CurrentServiceMode !=
                BistroBuilderServiceMode.WaitingAtBar ||
            !group.IsOccupyingBar || group.HasAssignedTable)
        {
            error = "La reserva de transiciÃ³n no pertenece a un grupo " +
                    "WaitingAtBar coherente.";
            return false;
        }

        if (table.CurrentState != TableState.Free ||
            table.AssignedCustomerGroup != null ||
            !table.CanSeatGroup(group.GroupSize))
        {
            error = "La mesa persistida ya no puede reservarse para el grupo.";
            return false;
        }

        if (seatingReservationCoordinator == null)
        {
            error = "Interaction & Reservation no estÃ¡ disponible.";
            return false;
        }

        return seatingReservationCoordinator.TryAcquireTableRight(
            group, table, int.MaxValue / 8, out _, out error);
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToTableRegistry();
        SynchronizeTablesFromRegistry();
        RegisterInitialCustomerGroups();
    }

    private void Start()
    {
        ValidateConfiguration();
        SynchronizeTablesFromRegistry();
        TryAssignWaitingGroups();
    }

    private void OnDisable()
    {
        UnsubscribeFromTableRegistry();
        UnsubscribeFromTables();
        UnsubscribeFromCustomerGroups();

        if (seatingReservationCoordinator != null)
            for (int i = 0; i < registeredGroups.Count; i++)
                if (registeredGroups[i] != null)
                    seatingReservationCoordinator.ReleaseGroupRight(registeredGroups[i]);

        registeredTables.Clear();
        registeredGroups.Clear();
        waitingGroups.Clear();
<<<<<<< Updated upstream
        pendingBarTableReservations.Clear();
        reservedForBarTransitions.Clear();
=======
>>>>>>> Stashed changes
    }

    public bool RegisterCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            registeredGroups.Contains(
                customerGroup
            ))
        {
            return false;
        }

        registeredGroups.Add(
            customerGroup
        );

        customerGroup.StateChanged +=
            HandleCustomerGroupStateChanged;

        CustomerGroupRegistered?.Invoke(customerGroup);

        Debug.Log(
            "Grupo " +
            customerGroup.GroupId +
            " registrado en el sistema de asignaciÃ³n de mesas.",
            customerGroup
        );

        if (customerGroup.CurrentState ==
            CustomerGroupState.WaitingForTable)
        {
            AddWaitingGroup(
                customerGroup
            );

            TryAssignWaitingGroups();
        }

        return true;
    }

    public bool UnregisterCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            !registeredGroups.Remove(
                customerGroup
            ))
        {
            return false;
        }

        customerGroup.StateChanged -=
            HandleCustomerGroupStateChanged;

        waitingGroups.Remove(
            customerGroup
        );
<<<<<<< Updated upstream
        ReleasePendingBarReservation(customerGroup, true);
=======
        seatingReservationCoordinator?.ReleaseGroupRight(customerGroup);
>>>>>>> Stashed changes

        Debug.Log(
            "Grupo " +
            customerGroup.GroupId +
            " eliminado del sistema de asignaciÃ³n de mesas.",
            customerGroup
        );

        return true;
    }

    private void RegisterInitialCustomerGroups()
    {
        if (initialCustomerGroups == null)
        {
            return;
        }

        for (int index = 0;
             index < initialCustomerGroups.Length;
             index++)
        {
            RegisterCustomerGroup(
                initialCustomerGroups[index]
            );
        }
    }

    private void UnsubscribeFromCustomerGroups()
    {
        for (int index = 0;
             index < registeredGroups.Count;
             index++)
        {
            CustomerGroup customerGroup =
                registeredGroups[index];

            if (customerGroup != null)
            {
                customerGroup.StateChanged -=
                    HandleCustomerGroupStateChanged;
            }
        }
    }

    private void SubscribeToTableRegistry()
    {
        if (tableRegistry == null)
        {
            return;
        }

        tableRegistry.TableRegistered -=
            HandleTableRegistered;

        tableRegistry.TableUnregistered -=
            HandleTableUnregistered;

        tableRegistry.TableRegistered +=
            HandleTableRegistered;

        tableRegistry.TableUnregistered +=
            HandleTableUnregistered;
    }

    private void UnsubscribeFromTableRegistry()
    {
        if (tableRegistry == null)
        {
            return;
        }

        tableRegistry.TableRegistered -=
            HandleTableRegistered;

        tableRegistry.TableUnregistered -=
            HandleTableUnregistered;
    }

    private void SynchronizeTablesFromRegistry()
    {
        if (tableRegistry == null)
        {
            return;
        }

        foreach (RestaurantTable table
                 in tableRegistry.RegisteredTables)
        {
            RegisterTable(
                table
            );
        }
    }

    private bool RegisterTable(
        RestaurantTable table
    )
    {
        if (table == null ||
            !registeredTables.Add(table))
        {
            return false;
        }

        table.StateChanged -=
            HandleTableStateChanged;

        table.StateChanged +=
            HandleTableStateChanged;

        TryAssignWaitingGroups();

        return true;
    }

    private bool UnregisterTable(
        RestaurantTable table
    )
    {
        if (table == null ||
            !registeredTables.Remove(table))
        {
            return false;
        }

        table.StateChanged -=
            HandleTableStateChanged;
<<<<<<< Updated upstream
        ReleaseReservationsForTable(table);
=======
        seatingReservationCoordinator?.ReleaseTableRight(table);
>>>>>>> Stashed changes

        return true;
    }

    private void UnsubscribeFromTables()
    {
        foreach (RestaurantTable table
                 in registeredTables)
        {
            if (table != null)
            {
                table.StateChanged -=
                    HandleTableStateChanged;
            }
        }
    }

    private void HandleTableRegistered(
        RestaurantTable table
    )
    {
        RegisterTable(
            table
        );
    }

    private void HandleTableUnregistered(
        RestaurantTable table
    )
    {
        UnregisterTable(
            table
        );
    }

    private void HandleCustomerGroupStateChanged(
        CustomerGroup customerGroup,
        CustomerGroupState newState
    )
    {
        if (newState ==
            CustomerGroupState.WaitingForTable)
        {
            AddWaitingGroup(
                customerGroup
            );

            TryAssignWaitingGroups();

            return;
        }

        waitingGroups.Remove(
            customerGroup
        );
<<<<<<< Updated upstream
        ReleasePendingBarReservation(customerGroup, false);
=======
>>>>>>> Stashed changes

        if (newState ==
            CustomerGroupState.Finished)
        {
            UnregisterCustomerGroup(
                customerGroup
            );
        }
    }

    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        if (table != null && newState != TableState.Free &&
            table.AssignedCustomerGroup == null)
        {
            seatingReservationCoordinator?.ReleaseTableRight(table);
        }

        if (newState ==
            TableState.Free)
        {
            TryAssignWaitingGroups();
        }
    }

    private void AddWaitingGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            waitingGroups.Contains(
                customerGroup
            ))
        {
            return;
        }

        waitingGroups.Add(
            customerGroup
        );
    }

    /// <summary>
    /// Solicita una nueva evaluaciÃ³n desde sistemas externos, por ejemplo
    /// cuando una sesiÃ³n WaitingAtBar termina y libera al grupo.
    /// </summary>
    public void RequestReevaluation()
    {
        TryAssignWaitingGroups();
    }

<<<<<<< Updated upstream
=======
    public bool TryReleasePreferredTableReservation(CustomerGroup customerGroup)
    {
        if (customerGroup == null || customerGroup.HasAssignedTable ||
            seatingReservationCoordinator == null ||
            !seatingReservationCoordinator.TryGetTableRight(customerGroup, out _, out _))
            return false;
        bool released = seatingReservationCoordinator.ReleaseGroupRight(customerGroup);
        if (released) TryAssignWaitingGroups();
        return released;
    }

    /// <summary>
    /// Reserva lÃ³gicamente una mesa concreta para un grupo ya registrado.
    /// La mesa sigue Free hasta que el flujo normal lleve al grupo a
    /// WaitingForTable; entonces la asignaciÃ³n canÃ³nica utilizarÃ¡ esta mesa.
    /// </summary>
    public bool TryReservePreferredTable(
        CustomerGroup customerGroup,
        RestaurantTable table,
        out string error)
    {
        error = string.Empty;

        if (customerGroup == null || table == null)
        {
            error = "El grupo y la mesa preferente deben existir.";
            return false;
        }

        if (!registeredGroups.Contains(customerGroup) ||
            !registeredTables.Contains(table))
        {
            error = "El grupo o la mesa preferente no estÃ¡n registrados.";
            return false;
        }

        if (customerGroup.HasAssignedTable ||
            customerGroup.RequestedServiceMode == BistroBuilderServiceMode.BarService)
        {
            error = "El grupo no admite una reserva de mesa preferente.";
            return false;
        }

        if (table.Capacity < customerGroup.GroupSize)
        {
            error = "La mesa preferente no tiene capacidad suficiente.";
            return false;
        }

        if (seatingReservationCoordinator == null)
        {
            error = "Interaction & Reservation no estÃ¡ disponible.";
            return false;
        }
        if (seatingReservationCoordinator.TryGetTableRight(
                customerGroup, out RestaurantTable current, out _))
        {
            if (ReferenceEquals(current, table)) return true;
            error = "El grupo ya tiene otra mesa reservada lÃ³gicamente.";
            return false;
        }
        return seatingReservationCoordinator.TryAcquireTableRight(
            customerGroup, table, int.MaxValue / 8, out _, out error);
    }

    public bool TryGetPreferredTable(
        CustomerGroup customerGroup,
        out RestaurantTable table)
    {
        table = null;
        return customerGroup != null && !customerGroup.HasAssignedTable &&
               seatingReservationCoordinator != null &&
               seatingReservationCoordinator.TryGetTableRight(
                   customerGroup, out table, out _);
    }

>>>>>>> Stashed changes
    private void TryAssignWaitingGroups()
    {
        if (advancedFrontOfHouseService != null && waitingGroups.Count > 1)
            waitingGroups.Sort(advancedFrontOfHouseService.CompareWaitingGroups);

        int groupIndex = 0;

        while (groupIndex <
               waitingGroups.Count)
        {
            CustomerGroup customerGroup =
                waitingGroups[groupIndex];

            if (customerGroup == null ||
                customerGroup.CurrentState !=
                    CustomerGroupState.WaitingForTable ||
                customerGroup.HasAssignedTable)
            {
                waitingGroups.RemoveAt(
                    groupIndex
                );

                continue;
            }

            // Un cliente de barra exclusiva nunca entra en la asignaciÃ³n de
            // mesas. WaitingAtBar sÃ­ conserva su posiciÃ³n normal en la cola.
            if (customerGroup.RequestedServiceMode ==
                BistroBuilderServiceMode.BarService)
            {
                groupIndex++;
                continue;
            }

            RestaurantTable bestTable =
                ResolveReservedOrBestTable(
                    customerGroup
                );

            if (bestTable == null)
            {
                Debug.Log(
                    "No hay una mesa adecuada disponible para " +
                    "el grupo " +
                    customerGroup.GroupId +
                    ".",
                    this
                );

                groupIndex++;
                continue;
            }

            if (customerGroup.IsOccupyingBar)
            {
                if (!ReserveTableForBarTransition(customerGroup, bestTable))
                {
                    groupIndex++;
                    continue;
                }

                string barTransitionReason = barServiceSystem == null
                    ? "No existe una autoridad de barra conectada."
                    : string.Empty;
                bool barIsReady = barServiceSystem != null &&
                    barServiceSystem.TryPrepareGroupForTable(
                        customerGroup,
                        out barTransitionReason
                    );

                if (!barIsReady)
                {
                    if (!string.IsNullOrWhiteSpace(barTransitionReason))
                    {
                        Debug.Log(
                            "La mesa " + bestTable.TableId +
                            " queda reservada para el grupo " +
                            customerGroup.GroupId +
                            ", que primero debe cerrar barra: " +
                            barTransitionReason,
                            this
                        );
                    }

                    groupIndex++;
                    continue;
                }
            }

            string logicalError = "Interaction & Reservation no está disponible.";
            if (seatingReservationCoordinator == null ||
                !seatingReservationCoordinator.TryAcquireTableRight(
                    customerGroup, bestTable, 0, out _, out logicalError))
            {
                Debug.LogWarning("No pudo adquirirse Assignment grupo->mesa: " + logicalError, this);
                groupIndex++;
                continue;
            }

            bool assigned = customerGroup.AssignTable(bestTable);

            if (!assigned)
            {
                seatingReservationCoordinator?.ReleaseGroupRight(customerGroup);
                groupIndex++;
                continue;
            }

            waitingGroups.RemoveAt(
                groupIndex
            );

            customerGroup.ResetWaitingTime();

            bestTable.SetState(
                TableState.WaitingForWaiter
            );

            customerGroup.SetState(
                CustomerGroupState.WalkingToTable
            );

            TableAssigned?.Invoke(customerGroup, bestTable);

            Debug.Log(
                "TableAssignmentSystem asignÃ³ la mesa " +
                bestTable.TableId +
                " al grupo " +
                customerGroup.GroupId +
                ".",
                this
            );
        }
    }

    private RestaurantTable ResolveReservedOrBestTable(
        CustomerGroup customerGroup
    )
    {
<<<<<<< Updated upstream
        if (customerGroup != null &&
            pendingBarTableReservations.TryGetValue(
                customerGroup,
                out RestaurantTable reserved
            ))
        {
            if (reserved != null &&
                reserved.CanSeatGroup(customerGroup.GroupSize))
            {
                return reserved;
            }

            ReleasePendingBarReservation(customerGroup, true);
        }

=======
        if (customerGroup != null && seatingReservationCoordinator != null &&
            seatingReservationCoordinator.TryGetTableRight(
                customerGroup, out RestaurantTable reserved, out _))
        {
            if (reserved == null || !registeredTables.Contains(reserved) ||
                reserved.Capacity < customerGroup.GroupSize)
            {
                seatingReservationCoordinator.ReleaseGroupRight(customerGroup);
                return null;
            }
            return reserved.CanSeatGroup(customerGroup.GroupSize) ? reserved : null;
        }
>>>>>>> Stashed changes
        return FindBestTableForGroup(customerGroup);
    }

    private bool ReserveTableForBarTransition(
        CustomerGroup group,
        RestaurantTable table
    )
    {
        if (group == null || table == null || seatingReservationCoordinator == null)
            return false;
        if (!seatingReservationCoordinator.TryAcquireTableRight(
                group, table, 0, out _, out string error))
        {
            Debug.LogWarning("No pudo reservarse lÃ³gicamente mesa de transiciÃ³n: " + error, this);
            return false;
        }
        Debug.Log("Mesa " + table.TableId + " reservada por Interaction para grupo " +
                  group.GroupId + " mientras finaliza WaitingAtBar.", this);
        return true;
    }

<<<<<<< Updated upstream
=======
    private void ReleasePreferredTableReservation(CustomerGroup group)
    {
        if (group == null || group.HasAssignedTable) return;
        seatingReservationCoordinator?.ReleaseGroupRight(group);
    }

    private void ReleasePreferredReservationsForTable(RestaurantTable table)
    {
        seatingReservationCoordinator?.ReleaseTableRight(table);
    }

>>>>>>> Stashed changes
    private void ReleasePendingBarReservation(
        CustomerGroup group,
        bool logRelease
    )
    {
        if (group == null || group.HasAssignedTable) return;
        bool released = seatingReservationCoordinator != null &&
            seatingReservationCoordinator.ReleaseGroupRight(group);
        if (released && logRelease)
            Debug.Log("Assignment temporal de mesa liberado para grupo " + group.GroupId + ".", this);
    }

    private void ReleaseReservationsForTable(RestaurantTable table)
    {
        seatingReservationCoordinator?.ReleaseTableRight(table);
    }

    private RestaurantTable FindBestTableForGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null || registeredTables.Count == 0)
            return null;

        RestaurantTable bestTable = null;
        float bestAdvancedScore = float.MinValue;
        int lowestUnusedCapacity = int.MaxValue;
        float shortestDistanceSquared = float.MaxValue;

        foreach (RestaurantTable table in registeredTables)
        {
<<<<<<< Updated upstream
            if (table == null ||
                reservedForBarTransitions.Contains(table) ||
                !table.CanSeatGroup(
                    customerGroup.GroupSize
                ))
=======
            if (table == null || !table.CanSeatGroup(customerGroup.GroupSize) ||
                (seatingReservationCoordinator != null &&
                 seatingReservationCoordinator.IsTableClaimedByOther(table, customerGroup)))
                continue;

            if (advancedFrontOfHouseService != null)
>>>>>>> Stashed changes
            {
                float score = advancedFrontOfHouseService.EvaluateTableScore(customerGroup, table);
                if (score == float.MinValue) continue;
                if (bestTable == null || score > bestAdvancedScore ||
                    (Mathf.Approximately(score, bestAdvancedScore) && table.TableId < bestTable.TableId))
                {
                    bestTable = table;
                    bestAdvancedScore = score;
                }
                continue;
            }

            int unusedCapacity = table.Capacity - customerGroup.GroupSize;
            Vector3 destinationPosition = table.CustomerApproachPoint != null
                ? table.CustomerApproachPoint.position
                : table.transform.position;
            float distanceSquared = (customerGroup.transform.position - destinationPosition).sqrMagnitude;
            bool better = bestTable == null || unusedCapacity < lowestUnusedCapacity ||
                (unusedCapacity == lowestUnusedCapacity && distanceSquared < shortestDistanceSquared) ||
                (unusedCapacity == lowestUnusedCapacity && Mathf.Approximately(distanceSquared, shortestDistanceSquared) &&
                 table.TableId < bestTable.TableId);
            if (!better) continue;
            bestTable = table;
            lowestUnusedCapacity = unusedCapacity;
            shortestDistanceSquared = distanceSquared;
        }

        return bestTable;
    }
    private void CacheDependenciesIfNeeded()
    {
        if (tableRegistry == null)
        {
            TryGetComponent(out tableRegistry);
        }

        if (barServiceSystem == null)
        {
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem
            >();
        }

        if (advancedFrontOfHouseService == null)
            advancedFrontOfHouseService = FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();

        if (seatingReservationCoordinator == null)
            seatingReservationCoordinator = FindFirstObjectByType<BistroBuilderSeatingReservationCoordinator>();
    }

    private void ValidateConfiguration()
    {
        string seatingError = "Interaction & Reservation no está disponible.";
        if (seatingReservationCoordinator == null ||
            !seatingReservationCoordinator.ValidateConfiguration(out seatingError))
        {
            Debug.LogError(nameof(TableAssignmentSystem) +
                " necesita Interaction & Reservation para seating: " + seatingError, this);
            return;
        }

        if (tableRegistry == null)
        {
            Debug.LogError(
                nameof(TableAssignmentSystem) +
                " necesita un " +
                nameof(RestaurantTableRegistry) +
                ".",
                this
            );

            return;
        }

        if (registeredTables.Count == 0)
        {
            Debug.LogError(
                nameof(TableAssignmentSystem) +
                " no tiene mesas registradas.",
                this
            );
        }

        if (barServiceSystem == null)
        {
            Debug.LogWarning(
                nameof(TableAssignmentSystem) +
                " no tiene autoridad de barra; WaitingAtBar no podrÃ¡ " +
                "cerrarse de forma transaccional.",
                this
            );
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
