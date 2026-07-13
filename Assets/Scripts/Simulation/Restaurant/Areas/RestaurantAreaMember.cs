using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa un elemento que pertenece a un área del restaurante.
///
/// Puede utilizarse en mesas, puntos de servicio, equipamiento,
/// decoración funcional, empleados u otros elementos espaciales.
///
/// También declara qué capacidades debe ofrecer el área donde
/// se coloque el elemento.
///
/// El componente no busca áreas continuamente ni utiliza Update.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantAreaMember : MonoBehaviour
{
    [Header("Área asignada")]

    [Tooltip(
        "Área principal a la que pertenece actualmente este elemento."
    )]
    [SerializeField]
    private RestaurantArea assignedArea;

    [Header("Referencia espacial")]

    [Tooltip(
        "Transform cuya posición representa al elemento para resolver " +
        "su área. Si no se configura, se utiliza este mismo GameObject."
    )]
    [SerializeField]
    private Transform positionReference;

    [Header("Requisitos funcionales")]

    [Tooltip(
        "Capacidades que debe ofrecer el área donde se coloque " +
        "este elemento."
    )]
    [SerializeField]
    private RestaurantAreaCapabilityDefinition[] requiredCapabilities =
        Array.Empty<RestaurantAreaCapabilityDefinition>();

    /// <summary>
    /// Se emite cuando cambia el área asignada.
    ///
    /// Parámetros:
    /// - Elemento que ha cambiado.
    /// - Área anterior.
    /// - Área nueva.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantArea,
        RestaurantArea
    > AreaChanged;

    public RestaurantArea AssignedArea =>
        assignedArea;

    public bool HasAssignedArea =>
        assignedArea != null;

    public Transform PositionReference =>
        positionReference != null
            ? positionReference
            : transform;

    public Vector3 ReferencePosition =>
        PositionReference.position;

    public IReadOnlyList<
        RestaurantAreaCapabilityDefinition
    > RequiredCapabilities =>
        requiredCapabilities;

    public int RequiredCapabilityCount =>
        requiredCapabilities != null
            ? requiredCapabilities.Length
            : 0;

    private void Awake()
    {
        CachePositionReferenceIfNeeded();
        EnsureRequirementsArray();
    }

    /// <summary>
    /// Asigna el elemento a una nueva área.
    ///
    /// No genera eventos cuando el área ya era la misma.
    /// Devuelve true únicamente cuando se produce un cambio real.
    /// </summary>
    public bool SetArea(
        RestaurantArea newArea
    )
    {
        if (ReferenceEquals(
                assignedArea,
                newArea
            ))
        {
            return false;
        }

        RestaurantArea previousArea =
            assignedArea;

        assignedArea =
            newArea;

        AreaChanged?.Invoke(
            this,
            previousArea,
            assignedArea
        );

        return true;
    }

    /// <summary>
    /// Elimina la asignación de área actual.
    /// </summary>
    public bool ClearArea()
    {
        return SetArea(null);
    }

    /// <summary>
    /// Comprueba si el elemento pertenece al área indicada.
    /// </summary>
    public bool IsAssignedTo(
        RestaurantArea area
    )
    {
        return area != null &&
               ReferenceEquals(
                   assignedArea,
                   area
               );
    }

    /// <summary>
    /// Comprueba si este elemento requiere una capacidad concreta.
    ///
    /// Los arrays de requisitos son deliberadamente pequeños,
    /// por lo que una búsqueda lineal evita memoria adicional
    /// y resulta más eficiente que mantener otro índice por objeto.
    /// </summary>
    public bool RequiresCapability(
        RestaurantAreaCapabilityDefinition capability
    )
    {
        if (capability == null ||
            requiredCapabilities == null)
        {
            return false;
        }

        for (int index = 0;
             index < requiredCapabilities.Length;
             index++)
        {
            if (ReferenceEquals(
                    requiredCapabilities[index],
                    capability
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Comprueba si un área ofrece todas las capacidades
    /// requeridas por este elemento.
    ///
    /// Devuelve en missingCapability el primer requisito
    /// que no se cumple.
    /// </summary>
    public bool AreRequirementsSatisfiedBy(
        RestaurantArea area,
        out RestaurantAreaCapabilityDefinition missingCapability
    )
    {
        missingCapability = null;

        if (area == null ||
            area.Definition == null)
        {
            return false;
        }

        if (requiredCapabilities == null ||
            requiredCapabilities.Length == 0)
        {
            return true;
        }

        for (int index = 0;
             index < requiredCapabilities.Length;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                requiredCapabilities[index];

            if (capability == null)
            {
                continue;
            }

            if (area.Definition.SupportsCapability(
                    capability
                ))
            {
                continue;
            }

            missingCapability = capability;
            return false;
        }

        return true;
    }

    private void CachePositionReferenceIfNeeded()
    {
        if (positionReference == null)
        {
            positionReference =
                transform;
        }
    }

    private void EnsureRequirementsArray()
    {
        if (requiredCapabilities == null)
        {
            requiredCapabilities =
                Array.Empty<
                    RestaurantAreaCapabilityDefinition
                >();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        positionReference =
            transform;

        EnsureRequirementsArray();
    }

    private void OnValidate()
    {
        CachePositionReferenceIfNeeded();
        EnsureRequirementsArray();
    }
#endif
}