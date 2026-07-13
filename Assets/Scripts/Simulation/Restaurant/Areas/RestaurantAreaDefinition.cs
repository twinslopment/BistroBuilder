using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define una categoría reutilizable de área del restaurante.
///
/// Cada definición puede contener una o varias capacidades.
/// De esta forma, los sistemas consultan qué permite una zona,
/// en lugar de depender de nombres rígidos como cocina,
/// comedor, terraza o barra.
/// </summary>
[CreateAssetMenu(
    fileName = "AreaDefinition_",
    menuName =
        "Bistro Builder/Restaurant/Area Definition"
)]
public sealed class RestaurantAreaDefinition :
    ScriptableObject
{
    [Header("Identificación")]

    [Tooltip(
        "Identificador estable del tipo de área. " +
        "Ejemplos: dining, kitchen, entrance."
    )]
    [SerializeField]
    private string areaTypeId;

    [Tooltip("Nombre visible del tipo de área.")]
    [SerializeField]
    private string displayName;

    [TextArea(2, 5)]
    [SerializeField]
    private string description;

    [Header("Capacidades funcionales")]

    [Tooltip(
        "Funciones que pueden realizarse dentro de esta " +
        "categoría de área."
    )]
    [SerializeField]
    private RestaurantAreaCapabilityDefinition[] capabilities =
        Array.Empty<RestaurantAreaCapabilityDefinition>();

    /// <summary>
    /// Índice en memoria para comprobar capacidades en O(1).
    /// No se serializa porque se reconstruye al cargar el asset.
    /// </summary>
    private HashSet<RestaurantAreaCapabilityDefinition>
        capabilityLookup;

    public string AreaTypeId => areaTypeId;
    public string DisplayName => displayName;
    public string Description => description;

    public IReadOnlyList<
        RestaurantAreaCapabilityDefinition
    > Capabilities =>
        capabilities;

    private void OnEnable()
    {
        RebuildCapabilityLookup();
    }

    /// <summary>
    /// Comprueba si este tipo de área ofrece una capacidad.
    ///
    /// La consulta utiliza un HashSet y no recorre el array.
    /// </summary>
    public bool SupportsCapability(
        RestaurantAreaCapabilityDefinition capability
    )
    {
        if (capability == null)
        {
            return false;
        }

        EnsureCapabilityLookup();

        return capabilityLookup.Contains(capability);
    }

    private void EnsureCapabilityLookup()
    {
        if (capabilityLookup == null)
        {
            RebuildCapabilityLookup();
        }
    }

    private void RebuildCapabilityLookup()
    {
        if (capabilityLookup == null)
        {
            capabilityLookup =
                new HashSet<
                    RestaurantAreaCapabilityDefinition
                >();
        }
        else
        {
            capabilityLookup.Clear();
        }

        if (capabilities == null)
        {
            capabilities =
                Array.Empty<
                    RestaurantAreaCapabilityDefinition
                >();

            return;
        }

        for (int index = 0;
             index < capabilities.Length;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                capabilities[index];

            if (capability != null)
            {
                capabilityLookup.Add(capability);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        areaTypeId =
            NormalizeIdentifier(areaTypeId);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }

        RebuildCapabilityLookup();
    }

    private static string NormalizeIdentifier(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_");
    }
#endif
}