using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de todos los artículos colocables del restaurante.
///
/// Responsabilidades:
/// - Descubrir las instancias iniciales.
/// - Asignar una identidad única a cada copia.
/// - Registrar y retirar artículos creados dinámicamente.
/// - Consultar artículos por identidad o definición.
/// - Publicar eventos para guardado, catálogo e integraciones.
///
/// No conoce funciones específicas de mesas, luces o equipamiento.
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Registry"
)]
public sealed class RestaurantPlaceableRegistry :
    MonoBehaviour
{
    [Header("Descubrimiento inicial")]

    [Tooltip(
        "Busca una sola vez los artículos que ya existen en la escena."
    )]
    [SerializeField]
    private bool discoverScenePlaceablesOnStart = true;

    [Header("Identidad")]

    [Tooltip(
        "Corrige automáticamente identidades vacías o duplicadas."
    )]
    [SerializeField]
    private bool repairMissingOrDuplicateIds = true;

    [Header("Depuración")]

    [Tooltip(
        "Escribe un resumen al finalizar la inicialización."
    )]
    [SerializeField]
    private bool logStartupSummary = true;

    private readonly HashSet<RestaurantPlaceableObject>
        registeredPlaceables =
            new HashSet<RestaurantPlaceableObject>();

    private readonly Dictionary<
        string,
        RestaurantPlaceableObject
    > placeableByInstanceId =
        new Dictionary<
            string,
            RestaurantPlaceableObject
        >(StringComparer.Ordinal);

    private readonly Dictionary<
        RestaurantPlaceableItemDefinition,
        HashSet<RestaurantPlaceableObject>
    > placeablesByDefinition =
        new Dictionary<
            RestaurantPlaceableItemDefinition,
            HashSet<RestaurantPlaceableObject>
        >();

    public event Action<RestaurantPlaceableObject>
        PlaceableRegistered;

    public event Action<RestaurantPlaceableObject>
        PlaceableUnregistered;

    public event Action<
        RestaurantPlaceableObject,
        string
    > PlaceableIdentityAssigned;

    public int RegisteredPlaceableCount
    {
        get
        {
            return registeredPlaceables.Count;
        }
    }

    public IReadOnlyCollection<RestaurantPlaceableObject>
        RegisteredPlaceables
    {
        get
        {
            return registeredPlaceables;
        }
    }

    private void Start()
    {
        if (discoverScenePlaceablesOnStart)
        {
            DiscoverExistingScenePlaceables();
        }

        if (logStartupSummary)
        {
            Debug.Log(
                nameof(RestaurantPlaceableRegistry) +
                " ha registrado " +
                registeredPlaceables.Count +
                " artículo(s) colocable(s).",
                this
            );
        }
    }

    private void OnDestroy()
    {
        registeredPlaceables.Clear();
        placeableByInstanceId.Clear();
        placeablesByDefinition.Clear();
    }

    /// <summary>
    /// Registra una instancia y garantiza que tenga identidad única.
    /// </summary>
    public bool RegisterPlaceable(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null)
        {
            return false;
        }

        if (registeredPlaceables.Contains(placeable))
        {
            return false;
        }

        if (!placeable.ValidateConfiguration(
                out string configurationError
            ))
        {
            Debug.LogError(
                configurationError,
                placeable
            );

            return false;
        }

        string requestedId =
            placeable.InstanceId;

        bool identityIsUsable =
            IsIdentityAvailableFor(
                requestedId,
                placeable
            );

        if (!identityIsUsable)
        {
            if (!repairMissingOrDuplicateIds)
            {
                Debug.LogError(
                    placeable.name +
                    " no puede registrarse porque su identidad " +
                    "está vacía o duplicada.",
                    placeable
                );

                return false;
            }

            requestedId =
                GenerateUniqueInstanceId();

            placeable.AssignInstanceId(
                requestedId
            );

            PlaceableIdentityAssigned?.Invoke(
                placeable,
                requestedId
            );
        }

        registeredPlaceables.Add(
            placeable
        );

        placeableByInstanceId.Add(
            placeable.InstanceId,
            placeable
        );

        AddToDefinitionIndex(
            placeable,
            placeable.ItemDefinition
        );

        PlaceableRegistered?.Invoke(
            placeable
        );

        return true;
    }

    /// <summary>
    /// Retira una instancia de todos los índices.
    /// </summary>
    public bool UnregisterPlaceable(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null ||
            !registeredPlaceables.Remove(placeable))
        {
            return false;
        }

        string instanceId =
            placeable.InstanceId;

        if (!string.IsNullOrWhiteSpace(instanceId) &&
            placeableByInstanceId.TryGetValue(
                instanceId,
                out RestaurantPlaceableObject indexedPlaceable
            ) &&
            ReferenceEquals(
                indexedPlaceable,
                placeable
            ))
        {
            placeableByInstanceId.Remove(
                instanceId
            );
        }

        RemoveFromDefinitionIndex(
            placeable,
            placeable.ItemDefinition
        );

        PlaceableUnregistered?.Invoke(
            placeable
        );

        return true;
    }

    public bool ContainsPlaceable(
        RestaurantPlaceableObject placeable
    )
    {
        return placeable != null &&
               registeredPlaceables.Contains(
                   placeable
               );
    }

    public bool TryGetByInstanceId(
        string instanceId,
        out RestaurantPlaceableObject placeable
    )
    {
        placeable = null;

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        return placeableByInstanceId.TryGetValue(
            instanceId.Trim().ToLowerInvariant(),
            out placeable
        );
    }

    /// <summary>
    /// Copia las instancias de una definición en una lista reutilizable.
    /// </summary>
    public int CopyPlaceablesByDefinition(
        RestaurantPlaceableItemDefinition definition,
        List<RestaurantPlaceableObject> results
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        results.Clear();

        if (definition == null ||
            !placeablesByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantPlaceableObject> placeables
            ))
        {
            return 0;
        }

        foreach (RestaurantPlaceableObject placeable
                 in placeables)
        {
            if (placeable != null)
            {
                results.Add(
                    placeable
                );
            }
        }

        return results.Count;
    }

    /// <summary>
    /// Descubre una sola vez los artículos existentes.
    /// </summary>
    private void DiscoverExistingScenePlaceables()
    {
        RestaurantPlaceableObject[] scenePlaceables =
            FindObjectsByType<RestaurantPlaceableObject>(
                FindObjectsSortMode.InstanceID
            );

        for (int index = 0;
             index < scenePlaceables.Length;
             index++)
        {
            RegisterPlaceable(
                scenePlaceables[index]
            );
        }
    }

    private bool IsIdentityAvailableFor(
        string instanceId,
        RestaurantPlaceableObject placeable
    )
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        if (!placeableByInstanceId.TryGetValue(
                instanceId,
                out RestaurantPlaceableObject existingPlaceable
            ))
        {
            return true;
        }

        return ReferenceEquals(
            existingPlaceable,
            placeable
        );
    }

    private string GenerateUniqueInstanceId()
    {
        string generatedId;

        do
        {
            generatedId =
                Guid.NewGuid()
                    .ToString("N")
                    .ToLowerInvariant();
        }
        while (placeableByInstanceId.ContainsKey(
            generatedId
        ));

        return generatedId;
    }

    private void AddToDefinitionIndex(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableItemDefinition definition
    )
    {
        if (placeable == null ||
            definition == null)
        {
            return;
        }

        if (!placeablesByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantPlaceableObject> placeables
            ))
        {
            placeables =
                new HashSet<RestaurantPlaceableObject>();

            placeablesByDefinition.Add(
                definition,
                placeables
            );
        }

        placeables.Add(
            placeable
        );
    }

    private void RemoveFromDefinitionIndex(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableItemDefinition definition
    )
    {
        if (placeable == null ||
            definition == null ||
            !placeablesByDefinition.TryGetValue(
                definition,
                out HashSet<RestaurantPlaceableObject> placeables
            ))
        {
            return;
        }

        placeables.Remove(
            placeable
        );

        if (placeables.Count == 0)
        {
            placeablesByDefinition.Remove(
                definition
            );
        }
    }
}
