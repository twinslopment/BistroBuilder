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
    ///
    /// Ejemplo:
    /// una definición "Comedor" puede tener:
    /// - Comedor principal.
    /// - Comedor privado.
    /// - Comedor superior.
    /// </summary>
    private readonly Dictionary<
        RestaurantAreaDefinition,
        HashSet<RestaurantArea>
    > areasByDefinition =
        new Dictionary<
            RestaurantAreaDefinition,
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
            $"{registeredAreas.Count} área(s).",
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
    }

    /// <summary>
    /// Registra una nueva zona en el sistema espacial.
    ///
    /// El futuro modo construcción deberá llamar a este método
    /// cuando cree una zona durante la partida.
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

        if (isActiveAndEnabled)
        {
            SubscribeToArea(area);
        }

        AreaRegistered?.Invoke(area);

        return true;
    }

    /// <summary>
    /// Retira una zona del registro.
    ///
    /// El futuro modo construcción deberá llamar a este método
    /// antes de eliminar o desactivar definitivamente una zona.
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

        AreaUnregistered?.Invoke(area);

        return true;
    }

    /// <summary>
    /// Devuelve un área concreta mediante su identificador único.
    /// La consulta es O(1).
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
    /// Comprueba si una zona está actualmente registrada.
    /// </summary>
    public bool ContainsArea(
        RestaurantArea area
    )
    {
        return area != null &&
               registeredAreas.Contains(area);
    }

    /// <summary>
    /// Localiza la primera zona que contiene una posición.
    ///
    /// Esta consulta solo se ejecuta cuando otro sistema la solicita.
    /// No existe comprobación espacial por frame.
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
    /// Copia en una lista reutilizable todas las áreas que utilizan
    /// una determinada definición.
    ///
    /// El método evita crear listas nuevas y generar basura para
    /// el recolector de memoria.
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

        foreach (RestaurantArea area in areas)
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

        return results.Count;
    }

    /// <summary>
    /// Descubre una sola vez las áreas existentes en la escena.
    ///
    /// No se repite durante la partida.
    /// </summary>
    private void DiscoverExistingSceneAreas()
    {
        RestaurantArea[] sceneAreas =
            FindObjectsByType<RestaurantArea>(
                FindObjectsSortMode.None
            );

        foreach (RestaurantArea area
                 in sceneAreas)
        {
            RegisterArea(area);
        }
    }

    private void AddAreaToDefinitionGroup(
        RestaurantArea area
    )
    {
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
        RestaurantAreaDefinition definition =
            area.Definition;

        if (definition == null)
        {
            return;
        }

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