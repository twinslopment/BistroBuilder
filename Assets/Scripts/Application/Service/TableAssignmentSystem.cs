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

    private readonly List<CustomerGroup>
        registeredGroups =
            new List<CustomerGroup>();

    private readonly List<CustomerGroup>
        waitingGroups =
            new List<CustomerGroup>();

    private readonly HashSet<RestaurantTable>
        registeredTables =
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

            RestaurantTable bestTable =
                FindBestTableForGroup(
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
            TryGetComponent(
                out tableRegistry
            );
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
