using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de las mesas operativas del restaurante.
///
/// Responsabilidades:
/// - Descubrir las mesas iniciales.
/// - Registrar y retirar mesas creadas dinámicamente.
/// - Garantizar TableId únicos.
/// - Publicar altas y bajas para los sistemas de sala.
///
/// Este registro es específico de la función "mesa".
/// La identidad genérica del mueble pertenece a
/// RestaurantPlaceableRegistry.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Table Registry"
)]
public sealed class RestaurantTableRegistry :
    MonoBehaviour
{
    [Header("Descubrimiento inicial")]

    [Tooltip(
        "Busca una sola vez las mesas existentes en la escena."
    )]
    [SerializeField]
    private bool discoverSceneTablesOnStart = true;

    [Header("Numeración")]

    [Tooltip(
        "Primer identificador que puede asignarse automáticamente."
    )]
    [SerializeField]
    [Min(1)]
    private int firstAutomaticTableId = 1;

    [Tooltip(
        "Corrige automáticamente identificadores duplicados."
    )]
    [SerializeField]
    private bool repairDuplicateIds = true;

    [Header("Depuración")]

    [SerializeField]
    private bool logStartupSummary = true;

    private readonly HashSet<RestaurantTable>
        registeredTables =
            new HashSet<RestaurantTable>();

    private readonly Dictionary<
        int,
        RestaurantTable
    > tableById =
        new Dictionary<
            int,
            RestaurantTable
        >();

    private int nextAutomaticTableId = 1;

    public event Action<RestaurantTable>
        TableRegistered;

    public event Action<RestaurantTable>
        TableUnregistered;

    public event Action<
        RestaurantTable,
        int
    > TableIdAssigned;

    public int RegisteredTableCount
    {
        get
        {
            return registeredTables.Count;
        }
    }

    public IReadOnlyCollection<RestaurantTable>
        RegisteredTables
    {
        get
        {
            return registeredTables;
        }
    }

    private void Awake()
    {
        nextAutomaticTableId =
            Mathf.Max(
                1,
                firstAutomaticTableId
            );

        /*
         * Se descubre en Awake para que los consumidores puedan
         * sincronizarse en OnEnable o Start sin depender del orden
         * de ejecución de Start entre componentes de GameSystems.
         */
        if (discoverSceneTablesOnStart)
        {
            DiscoverExistingSceneTables();
        }
    }

    private void Start()
    {
        if (logStartupSummary)
        {
            Debug.Log(
                nameof(RestaurantTableRegistry) +
                " ha registrado " +
                registeredTables.Count +
                " mesa(s).",
                this
            );
        }
    }

    private void OnDestroy()
    {
        registeredTables.Clear();
        tableById.Clear();
    }

    public bool RegisterTable(
        RestaurantTable table
    )
    {
        if (table == null ||
            registeredTables.Contains(table))
        {
            return false;
        }

        int requestedId =
            table.TableId;

        bool requestedIdIsAvailable =
            requestedId >= 1 &&
            !tableById.ContainsKey(
                requestedId
            );

        if (!requestedIdIsAvailable)
        {
            if (!repairDuplicateIds)
            {
                Debug.LogError(
                    table.name +
                    " no puede registrarse porque TableId " +
                    requestedId +
                    " está duplicado o no es válido.",
                    table
                );

                return false;
            }

            requestedId =
                GetNextAvailableTableId();

            table.AssignTableId(
                requestedId
            );

            Debug.Log(
                table.name +
                " ha recibido el TableId " +
                requestedId +
                " para evitar una identidad duplicada.",
                table
            );

            TableIdAssigned?.Invoke(
                table,
                requestedId
            );
        }

        registeredTables.Add(
            table
        );

        tableById.Add(
            table.TableId,
            table
        );

        nextAutomaticTableId =
            Mathf.Max(
                nextAutomaticTableId,
                table.TableId + 1
            );

        TableRegistered?.Invoke(
            table
        );

        return true;
    }

    public bool UnregisterTable(
        RestaurantTable table
    )
    {
        if (table == null ||
            !registeredTables.Remove(table))
        {
            return false;
        }

        if (tableById.TryGetValue(
                table.TableId,
                out RestaurantTable indexedTable
            ) &&
            ReferenceEquals(
                indexedTable,
                table
            ))
        {
            tableById.Remove(
                table.TableId
            );
        }

        TableUnregistered?.Invoke(
            table
        );

        return true;
    }

    public bool ContainsTable(
        RestaurantTable table
    )
    {
        return table != null &&
               registeredTables.Contains(
                   table
               );
    }

    public bool TryGetTableById(
        int tableId,
        out RestaurantTable table
    )
    {
        return tableById.TryGetValue(
            tableId,
            out table
        );
    }

    private void DiscoverExistingSceneTables()
    {
        RestaurantTable[] sceneTables =
            FindObjectsByType<RestaurantTable>(
                FindObjectsSortMode.InstanceID
            );

        for (int index = 0;
             index < sceneTables.Length;
             index++)
        {
            RegisterTable(
                sceneTables[index]
            );
        }
    }

    private int GetNextAvailableTableId()
    {
        int candidate =
            Mathf.Max(
                1,
                nextAutomaticTableId
            );

        while (tableById.ContainsKey(
            candidate
        ))
        {
            candidate++;
        }

        nextAutomaticTableId =
            candidate + 1;

        return candidate;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        firstAutomaticTableId =
            Mathf.Max(
                1,
                firstAutomaticTableId
            );
    }
#endif
}
