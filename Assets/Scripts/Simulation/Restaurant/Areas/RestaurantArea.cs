using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa una zona física concreta dentro de un restaurante.
///
/// Una misma definición puede utilizarse varias veces:
/// - Comedor principal.
/// - Comedor privado.
/// - Terraza norte.
/// - Terraza cubierta.
///
/// El área no utiliza Update. Sus límites se consultan únicamente
/// cuando otro sistema necesita localizar o validar una posición.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantArea : MonoBehaviour
{
    [Header("Identificación")]

    [Tooltip(
        "Identificador único de esta zona concreta. " +
        "Ejemplos: dining_main, kitchen_01."
    )]
    [SerializeField]
    private string areaId;

    [Tooltip(
        "Definición que indica qué tipo de zona es."
    )]
    [SerializeField]
    private RestaurantAreaDefinition definition;

    [Header("Límites espaciales")]

    [Tooltip(
        "Colliders que delimitan físicamente esta zona. " +
        "Pueden utilizarse varios para formar áreas complejas."
    )]
    [SerializeField]
    private Collider[] boundaryColliders;

    [Header("Estado operativo")]

    [SerializeField]
    private bool isOperational = true;

    /// <summary>
    /// Se emite únicamente cuando cambia el estado operativo.
    /// </summary>
    public event Action<RestaurantArea, bool>
        OperationalStateChanged;

    public string AreaId => areaId;

    public RestaurantAreaDefinition Definition =>
        definition;

    public bool IsOperational =>
        isOperational;

    public IReadOnlyList<Collider> BoundaryColliders =>
        boundaryColliders;

    private void Awake()
    {
        CacheBoundaryCollidersIfNeeded();
        ValidateRuntimeConfiguration();
    }

    /// <summary>
    /// Activa o desactiva operativamente la zona.
    ///
    /// Una zona puede seguir existiendo físicamente aunque esté
    /// cerrada, bloqueada, en obras o fuera de servicio.
    /// </summary>
    public void SetOperational(
        bool operational
    )
    {
        if (isOperational == operational)
        {
            return;
        }

        isOperational = operational;

        OperationalStateChanged?.Invoke(
            this,
            isOperational
        );
    }

    /// <summary>
    /// Comprueba si una posición del mundo pertenece a esta zona.
    ///
    /// No se ejecuta automáticamente cada frame.
    /// Los sistemas externos deciden cuándo necesitan consultarlo.
    /// </summary>
    public bool ContainsPosition(
        Vector3 worldPosition
    )
    {
        if (boundaryColliders == null)
        {
            return false;
        }

        const float positionToleranceSquared =
            0.000001f;

        for (int index = 0;
             index < boundaryColliders.Length;
             index++)
        {
            Collider boundary =
                boundaryColliders[index];

            if (boundary == null ||
                !boundary.enabled)
            {
                continue;
            }

            // Prueba rápida antes de utilizar ClosestPoint.
            if (!boundary.bounds.Contains(worldPosition))
            {
                continue;
            }

            Vector3 closestPoint =
                boundary.ClosestPoint(worldPosition);

            float distanceSquared =
                (
                    closestPoint -
                    worldPosition
                ).sqrMagnitude;

            if (distanceSquared <=
                positionToleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recupera los colliders una sola vez cuando todavía
    /// no han sido configurados desde el Inspector.
    /// </summary>
    private void CacheBoundaryCollidersIfNeeded()
    {
        if (boundaryColliders != null &&
            boundaryColliders.Length > 0)
        {
            return;
        }

        boundaryColliders =
            GetComponents<Collider>();
    }

    private void ValidateRuntimeConfiguration()
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            Debug.LogError(
                $"{name} no tiene Area Id configurado.",
                this
            );
        }

        if (definition == null)
        {
            Debug.LogError(
                $"{name} no tiene una definición de área.",
                this
            );
        }

        if (boundaryColliders == null ||
            boundaryColliders.Length == 0)
        {
            Debug.LogError(
                $"{name} no tiene ningún Collider que " +
                "delimite su superficie.",
                this
            );
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        boundaryColliders =
            GetComponents<Collider>();

        if (string.IsNullOrWhiteSpace(areaId))
        {
            areaId =
                NormalizeIdentifier(gameObject.name);
        }
    }

    private void OnValidate()
    {
        areaId =
            NormalizeIdentifier(areaId);

        if (boundaryColliders == null ||
            boundaryColliders.Length == 0)
        {
            boundaryColliders =
                GetComponents<Collider>();
        }
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