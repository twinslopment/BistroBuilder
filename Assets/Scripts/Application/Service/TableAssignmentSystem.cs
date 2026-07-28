using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Asigna grupos de clientes a las mesas operativas disponibles.
///
/// Las mesas ya no proceden de un array fijo del Inspector.
/// Se sincronizan dinámicamente mediante RestaurantTableRegistry,
/// por lo que una mesa añadida desde el modo edición podrá participar
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
        "Registro dinámico de las mesas operativas."
    )]
    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [Tooltip(
        "Autoridad de barra que debe cerrar una sesión WaitingAtBar antes " +
        "de que el grupo pueda caminar a una mesa."
    )]
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    private readonly List<CustomerGroup>
        registeredGroups =
            new List<CustomerGroup>();

    private readonly List<CustomerGroup>
        waitingGroups =
            new List<CustomerGroup>();

    private readonly HashSet<RestaurantTable>
        registeredTables =
            new HashSet<RestaurantTable>();

    // Reserva lógica de una mesa mientras WaitingAtBar termina su consumo.
    // No cambia el estado de RestaurantTable y solo existe durante el
    // servicio activo, cuyo guardado continúa bloqueado globalmente.
    private readonly Dictionary<CustomerGroup, RestaurantTable>
        pendingBarTableReservations =
            new Dictionary<CustomerGroup, RestaurantTable>();

    private readonly HashSet<RestaurantTable> reservedForBarTransitions =
        new HashSet<RestaurantTable>();

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

        registeredTables.Clear();
        registeredGroups.Clear();
        waitingGroups.Clear();
        pendingBarTableReservations.Clear();
        reservedForBarTransitions.Clear();
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

        Debug.Log(
            "Grupo " +
            customerGroup.GroupId +
            " registrado en el sistema de asignación de mesas.",
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
        ReleasePendingBarReservation(customerGroup, true);

        Debug.Log(
            "Grupo " +
            customerGroup.GroupId +
            " eliminado del sistema de asignación de mesas.",
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
        ReleaseReservationsForTable(table);

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
        ReleasePendingBarReservation(customerGroup, false);

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
        if (table != null &&
            reservedForBarTransitions.Contains(table) &&
            newState != TableState.Free)
        {
            ReleaseReservationsForTable(table);
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
    /// Solicita una nueva evaluación desde sistemas externos, por ejemplo
    /// cuando una sesión WaitingAtBar termina y libera al grupo.
    /// </summary>
    public void RequestReevaluation()
    {
        TryAssignWaitingGroups();
    }

    private void TryAssignWaitingGroups()
    {
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

            // Un cliente de barra exclusiva nunca entra en la asignación de
            // mesas. WaitingAtBar sí conserva su posición normal en la cola.
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
                ReserveTableForBarTransition(customerGroup, bestTable);

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

            // La reserva deja de ser necesaria justo antes de la asignación
            // real. Ningún otro grupo ha podido usar la mesa en este intervalo.
            ReleasePendingBarReservation(customerGroup, false);

            bool assigned =
                customerGroup.AssignTable(
                    bestTable
                );

            if (!assigned)
            {
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

            Debug.Log(
                "TableAssignmentSystem asignó la mesa " +
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

        return FindBestTableForGroup(customerGroup);
    }

    private void ReserveTableForBarTransition(
        CustomerGroup group,
        RestaurantTable table
    )
    {
        if (group == null || table == null)
        {
            return;
        }

        if (pendingBarTableReservations.TryGetValue(
                group,
                out RestaurantTable current
            ))
        {
            if (ReferenceEquals(current, table))
            {
                return;
            }

            ReleasePendingBarReservation(group, true);
        }

        pendingBarTableReservations[group] = table;
        reservedForBarTransitions.Add(table);

        Debug.Log(
            "Mesa " + table.TableId +
            " reservada temporalmente para el grupo " + group.GroupId +
            " mientras finaliza su sesión WaitingAtBar.",
            this
        );
    }

    private void ReleasePendingBarReservation(
        CustomerGroup group,
        bool logRelease
    )
    {
        if (group == null ||
            !pendingBarTableReservations.TryGetValue(
                group,
                out RestaurantTable table
            ))
        {
            return;
        }

        pendingBarTableReservations.Remove(group);

        if (table != null)
        {
            reservedForBarTransitions.Remove(table);

            if (logRelease)
            {
                Debug.Log(
                    "Reserva temporal de la mesa " + table.TableId +
                    " liberada para el grupo " + group.GroupId + ".",
                    this
                );
            }
        }
    }

    private void ReleaseReservationsForTable(RestaurantTable table)
    {
        if (table == null || !reservedForBarTransitions.Remove(table))
        {
            return;
        }

        CustomerGroup owner = null;

        foreach (KeyValuePair<CustomerGroup, RestaurantTable> pair
                 in pendingBarTableReservations)
        {
            if (ReferenceEquals(pair.Value, table))
            {
                owner = pair.Key;
                break;
            }
        }

        if (owner != null)
        {
            pendingBarTableReservations.Remove(owner);
        }
    }

    private RestaurantTable FindBestTableForGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null ||
            registeredTables.Count == 0)
        {
            return null;
        }

        RestaurantTable bestTable =
            null;

        int lowestUnusedCapacity =
            int.MaxValue;

        float shortestDistanceSquared =
            float.MaxValue;

        foreach (RestaurantTable table
                 in registeredTables)
        {
            if (table == null ||
                reservedForBarTransitions.Contains(table) ||
                !table.CanSeatGroup(
                    customerGroup.GroupSize
                ))
            {
                continue;
            }

            int unusedCapacity =
                table.Capacity -
                customerGroup.GroupSize;

            Vector3 destinationPosition =
                table.CustomerApproachPoint != null
                    ? table.CustomerApproachPoint.position
                    : table.transform.position;

            float distanceSquared =
                (
                    customerGroup.transform.position -
                    destinationPosition
                ).sqrMagnitude;

            bool hasBetterCapacity =
                unusedCapacity <
                lowestUnusedCapacity;

            bool sameCapacityButCloser =
                unusedCapacity ==
                    lowestUnusedCapacity &&
                distanceSquared <
                    shortestDistanceSquared;

            bool sameScoreButLowerTableId =
                unusedCapacity ==
                    lowestUnusedCapacity &&
                Mathf.Approximately(
                    distanceSquared,
                    shortestDistanceSquared
                ) &&
                bestTable != null &&
                table.TableId <
                    bestTable.TableId;

            if (!hasBetterCapacity &&
                !sameCapacityButCloser &&
                !sameScoreButLowerTableId)
            {
                continue;
            }

            bestTable =
                table;

            lowestUnusedCapacity =
                unusedCapacity;

            shortestDistanceSquared =
                distanceSquared;
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
    }

    private void ValidateConfiguration()
    {
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
                " no tiene autoridad de barra; WaitingAtBar no podrá " +
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
