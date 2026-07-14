using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de todas las áreas físicas del restaurante.
///
/// Responsabilidades:
/// - Registrar y retirar áreas.
/// - Impedir identificadores duplicados.
/// - Localizar áreas por ID.
/// - Agrupar áreas por definición.
/// - Agrupar áreas por capacidades funcionales.
/// - Resolver qué área contiene una posición.
/// - Propagar cambios de estado operativo.
///
/// No utiliza Update y no realiza búsquedas continuas.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantAreaRegistry : MonoBehaviour
{
    [Header("Descubrimiento inicial")]

    [Tooltip(
        "Busca una sola vez, al iniciar la escena, las áreas " +
        "que ya existen en el restaurante."
    )]
    [SerializeField]
    private bool discoverSceneAreasOnStart = true;

    /// <summary>
    /// Colección principal de áreas registradas.
    /// HashSet evita registros duplicados por referencia.
    /// </summary>
    private readonly HashSet<RestaurantArea>
        registeredAreas =
            new HashSet<RestaurantArea>();

    /// <summary>
    /// Acceso directo a un área mediante su identificador único.
    /// </summary>
    private readonly Dictionary<
        string,
        RestaurantArea
    > areasById =
        new Dictionary<
            string,
            RestaurantArea
        >(StringComparer.Ordinal);

    /// <summary>
    /// Agrupa las áreas que comparten una misma definición.
    /// </summary>
    private readonly Dictionary<
        RestaurantAreaDefinition,
        HashSet<RestaurantArea>
    > areasByDefinition =
        new Dictionary<
            RestaurantAreaDefinition,
            HashSet<RestaurantArea>
        >();

    /// <summary>
    /// Agrupa las áreas según las capacidades que ofrecen.
    ///
    /// Este índice permite realizar consultas directas sin
    /// recorrer todas las áreas registradas.
    /// </summary>
    private readonly Dictionary<
        RestaurantAreaCapabilityDefinition,
        HashSet<RestaurantArea>
    > areasByCapability =
        new Dictionary<
            RestaurantAreaCapabilityDefinition,
            HashSet<RestaurantArea>
        >();

    public event Action<RestaurantArea>
        AreaRegistered;

    public event Action<RestaurantArea>
        AreaUnregistered;

    public event Action<RestaurantArea, bool>
        AreaOperationalStateChanged;

    public int RegisteredAreaCount =>
        registeredAreas.Count;

    public int IndexedCapabilityCount =>
        areasByCapability.Count;

    public IReadOnlyCollection<RestaurantArea>
        RegisteredAreas =>
            registeredAreas;

    private void OnEnable()
    {
        SubscribeToRegisteredAreas();
    }

    private void Start()
    {
        if (discoverSceneAreasOnStart)
        {
            DiscoverExistingSceneAreas();
        }

        Debug.Log(
            $"RestaurantAreaRegistry ha registrado " +
            $"{registeredAreas.Count} área(s) e indexado " +
            $"{areasByCapability.Count} capacidad(es).",
            this
        );
    }

    private void OnDisable()
    {
        UnsubscribeFromRegisteredAreas();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRegisteredAreas();

        registeredAreas.Clear();
        areasById.Clear();
        areasByDefinition.Clear();
        areasByCapability.Clear();
    }

    /// <summary>
    /// Registra una nueva área en el sistema espacial.
    /// </summary>
    public bool RegisterArea(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return false;
        }

        if (registeredAreas.Contains(area))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(area.AreaId))
        {
            Debug.LogError(
                $"{area.name} no puede registrarse porque " +
                "no tiene Area Id.",
                area
            );

            return false;
        }

        if (area.Definition == null)
        {
            Debug.LogError(
                $"{area.name} no puede registrarse porque " +
                "no tiene una definición de área.",
                area
            );

            return false;
        }

        if (areasById.TryGetValue(
                area.AreaId,
                out RestaurantArea existingArea
            ))
        {
            Debug.LogError(
                $"El Area Id '{area.AreaId}' está duplicado. " +
                $"Ya pertenece a {existingArea.name}.",
                area
            );

            return false;
        }

        registeredAreas.Add(area);

        areasById.Add(
            area.AreaId,
            area
        );

        AddAreaToDefinitionGroup(area);
        AddAreaToCapabilityGroups(area);

        if (isActiveAndEnabled)
        {
            SubscribeToArea(area);
        }

        AreaRegistered?.Invoke(area);

        return true;
    }

    /// <summary>
    /// Retira un área del registro.
    /// </summary>
    public bool UnregisterArea(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return false;
        }

        if (!registeredAreas.Remove(area))
        {
            return false;
        }

        UnsubscribeFromArea(area);

        if (areasById.TryGetValue(
                area.AreaId,
                out RestaurantArea registeredArea
            ) &&
            ReferenceEquals(
                registeredArea,
                area
            ))
        {
            areasById.Remove(area.AreaId);
        }

        RemoveAreaFromDefinitionGroup(area);
        RemoveAreaFromCapabilityGroups(area);

        AreaUnregistered?.Invoke(area);

        return true;
    }

    /// <summary>
    /// Devuelve un área mediante su identificador único.
    /// </summary>
    public bool TryGetAreaById(
        string areaId,
        out RestaurantArea area
    )
    {
        area = null;

        if (string.IsNullOrWhiteSpace(areaId))
        {
            return false;
        }

        return areasById.TryGetValue(
            areaId,
            out area
        );
    }

    /// <summary>
    /// Comprueba si un área está registrada.
    /// </summary>
    public bool ContainsArea(
        RestaurantArea area
    )
    {
        return area != null &&
               registeredAreas.Contains(area);
    }

    /// <summary>
    /// Localiza la primera área que contiene una posición.
    /// </summary>
    public bool TryFindAreaContainingPosition(
        Vector3 worldPosition,
        out RestaurantArea area,
        bool operationalOnly = true
    )
    {
        foreach (RestaurantArea candidate
                 in registeredAreas)
        {
            if (candidate == null)
            {
                continue;
            }

            if (operationalOnly &&
                !candidate.IsOperational)
            {
                continue;
            }

            if (!candidate.ContainsPosition(
                    worldPosition
                ))
            {
                continue;
            }

            area = candidate;
            return true;
        }

        area = null;
        return false;
    }

    /// <summary>
    /// Copia todas las áreas que utilizan una definición.
    /// </summary>
    public int CopyAreasByDefinition(
        RestaurantAreaDefinition definition,
        List<RestaurantArea> results,
        bool operationalOnly = false
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        if (definition == null)
        {
            return 0;
        }

        if (!areasByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantArea> areas
            ))
        {
            return 0;
        }

        CopyValidAreas(
            areas,
            results,
            operationalOnly
        );

        return results.Count;
    }

    /// <summary>
    /// Copia todas las áreas que ofrecen una capacidad concreta.
    /// </summary>
    public int CopyAreasByCapability(
        RestaurantAreaCapabilityDefinition capability,
        List<RestaurantArea> results,
        bool operationalOnly = false
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        if (capability == null)
        {
            return 0;
        }

        if (!areasByCapability.TryGetValue(
                capability,
                out HashSet<RestaurantArea> areas
            ))
        {
            return 0;
        }

        CopyValidAreas(
            areas,
            results,
            operationalOnly
        );

        return results.Count;
    }

    /// <summary>
    /// Devuelve cuántas áreas ofrecen una capacidad.
    /// </summary>
    public int GetAreaCountByCapability(
        RestaurantAreaCapabilityDefinition capability,
        bool operationalOnly = false
    )
    {
        if (capability == null)
        {
            return 0;
        }

        if (!areasByCapability.TryGetValue(
                capability,
                out HashSet<RestaurantArea> areas
            ))
        {
            return 0;
        }

        if (!operationalOnly)
        {
            return areas.Count;
        }

        int operationalCount = 0;

        foreach (RestaurantArea area in areas)
        {
            if (area != null &&
                area.IsOperational)
            {
                operationalCount++;
            }
        }

        return operationalCount;
    }

    /// <summary>
    /// Descubre una sola vez las áreas existentes en la escena.
    /// </summary>
    private void DiscoverExistingSceneAreas()
    {
        RestaurantArea[] sceneAreas =
            FindObjectsByType<RestaurantArea>(
                FindObjectsSortMode.None
            );

        foreach (RestaurantArea area in sceneAreas)
        {
            RegisterArea(area);
        }
    }

    private void AddAreaToDefinitionGroup(
        RestaurantArea area
    )
    {
        if (area == null ||
            area.Definition == null)
        {
            return;
        }

        RestaurantAreaDefinition definition =
            area.Definition;

        if (!areasByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantArea> areas
            ))
        {
            areas =
                new HashSet<RestaurantArea>();

            areasByDefinition.Add(
                definition,
                areas
            );
        }

        areas.Add(area);
    }

    private void RemoveAreaFromDefinitionGroup(
        RestaurantArea area
    )
    {
        if (area == null ||
            area.Definition == null)
        {
            return;
        }

        RestaurantAreaDefinition definition =
            area.Definition;

        if (!areasByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantArea> areas
            ))
        {
            return;
        }

        areas.Remove(area);

        if (areas.Count == 0)
        {
            areasByDefinition.Remove(
                definition
            );
        }
    }

    private void AddAreaToCapabilityGroups(
        RestaurantArea area
    )
    {
        if (area == null ||
            area.Definition == null)
        {
            return;
        }

        IReadOnlyList<
            RestaurantAreaCapabilityDefinition
        > capabilities =
            area.Definition.Capabilities;

        if (capabilities == null)
        {
            return;
        }

        for (int index = 0;
             index < capabilities.Count;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                capabilities[index];

            if (capability == null)
            {
                continue;
            }

            if (!areasByCapability.TryGetValue(
                    capability,
                    out HashSet<RestaurantArea> areas
                ))
            {
                areas =
                    new HashSet<RestaurantArea>();

                areasByCapability.Add(
                    capability,
                    areas
                );
            }

            areas.Add(area);
        }
    }

    private void RemoveAreaFromCapabilityGroups(
        RestaurantArea area
    )
    {
        if (area == null ||
            area.Definition == null)
        {
            return;
        }

        IReadOnlyList<
            RestaurantAreaCapabilityDefinition
        > capabilities =
            area.Definition.Capabilities;

        if (capabilities == null)
        {
            return;
        }

        for (int index = 0;
             index < capabilities.Count;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                capabilities[index];

            if (capability == null)
            {
                continue;
            }

            if (!areasByCapability.TryGetValue(
                    capability,
                    out HashSet<RestaurantArea> areas
                ))
            {
                continue;
            }

            areas.Remove(area);

            if (areas.Count == 0)
            {
                areasByCapability.Remove(
                    capability
                );
            }
        }
    }

    private static void CopyValidAreas(
        HashSet<RestaurantArea> source,
        List<RestaurantArea> results,
        bool operationalOnly
    )
    {
        if (source == null)
        {
            return;
        }

        foreach (RestaurantArea area in source)
        {
            if (area == null)
            {
                continue;
            }

            if (operationalOnly &&
                !area.IsOperational)
            {
                continue;
            }

            results.Add(area);
        }
    }

    private void SubscribeToRegisteredAreas()
    {
        foreach (RestaurantArea area
                 in registeredAreas)
        {
            SubscribeToArea(area);
        }
    }

    private void UnsubscribeFromRegisteredAreas()
    {
        foreach (RestaurantArea area
                 in registeredAreas)
        {
            UnsubscribeFromArea(area);
        }
    }

    private void SubscribeToArea(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return;
        }

        area.OperationalStateChanged -=
            HandleAreaOperationalStateChanged;

        area.OperationalStateChanged +=
            HandleAreaOperationalStateChanged;
    }

    private void UnsubscribeFromArea(
        RestaurantArea area
    )
    {
        if (area == null)
        {
            return;
        }

        area.OperationalStateChanged -=
            HandleAreaOperationalStateChanged;
    }

    private void HandleAreaOperationalStateChanged(
        RestaurantArea area,
        bool isOperational
    )
    {
        AreaOperationalStateChanged?.Invoke(
            area,
            isOperational
        );
    }
}