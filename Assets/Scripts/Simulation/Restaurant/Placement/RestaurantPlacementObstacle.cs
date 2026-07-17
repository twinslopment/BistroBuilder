using System;
using UnityEngine;

/// <summary>
/// Representa un elemento fijo que bloquea la colocación de
/// mobiliario o equipamiento.
///
/// Ejemplos:
/// - Paredes.
/// - Columnas.
/// - Barras construidas.
/// - Escaleras.
/// - Maquinaria anclada.
/// - Zonas estructurales no ocupables.
///
/// El obstáculo no es editable ni pertenece al registro de
/// objetos colocables.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Obstacle"
)]
public sealed class RestaurantPlacementObstacle :
    MonoBehaviour
{
    [Header("Identidad")]

    [Tooltip(
        "Identificador técnico estable del obstáculo."
    )]
    [SerializeField]
    private string obstacleId =
        "placement_obstacle";

    [Header("Geometría local")]

    [Tooltip(
        "Centro local de la superficie bloqueada."
    )]
    [SerializeField]
    private Vector3 localCenter =
        Vector3.zero;

    [Tooltip(
        "Anchura y profundidad locales del obstáculo."
    )]
    [SerializeField]
    private Vector2 localSize =
        Vector2.one;

    [Header("Separación")]

    [Tooltip(
        "Separación adicional exigida alrededor del obstáculo."
    )]
    [SerializeField]
    [Min(0f)]
    private float minimumClearance;

    [Header("Estado")]

    [Tooltip(
        "Determina si este elemento bloquea objetos colocables."
    )]
    [SerializeField]
    private bool blocksPlacement = true;

    [Tooltip(
        "Permite desactivar temporalmente el obstáculo."
    )]
    [SerializeField]
    private bool operational = true;

    [Header("Visualización")]

    [Tooltip(
        "Muestra la superficie bloqueada en la vista Scene."
    )]
    [SerializeField]
    private bool showGizmos = true;

    /// <summary>
    /// Se ejecuta cuando cambia el estado lógico del obstáculo.
    /// </summary>
    public event Action<RestaurantPlacementObstacle>
        ObstacleChanged;

    public string ObstacleId
    {
        get
        {
            return NormalizeIdentifier(
                obstacleId
            );
        }
    }

    public Vector3 LocalCenter
    {
        get
        {
            return localCenter;
        }
    }

    public Vector2 LocalSize
    {
        get
        {
            return new Vector2(
                Mathf.Max(
                    0.01f,
                    localSize.x
                ),
                Mathf.Max(
                    0.01f,
                    localSize.y
                )
            );
        }
    }

    public float MinimumClearance
    {
        get
        {
            return Mathf.Max(
                0f,
                minimumClearance
            );
        }
    }

    public bool BlocksPlacement
    {
        get
        {
            return blocksPlacement;
        }
    }

    public bool Operational
    {
        get
        {
            return operational;
        }
    }

    public bool IsBlocking
    {
        get
        {
            return isActiveAndEnabled &&
                   gameObject.activeInHierarchy &&
                   operational &&
                   blocksPlacement;
        }
    }

    /// <summary>
    /// Centro del obstáculo en coordenadas mundiales.
    /// </summary>
    public Vector3 WorldCenter
    {
        get
        {
            return transform.TransformPoint(
                localCenter
            );
        }
    }

    /// <summary>
    /// Eje horizontal derecho del obstáculo.
    /// </summary>
    public Vector3 WorldRightAxis
    {
        get
        {
            return NormalizeHorizontalAxis(
                transform.right,
                Vector3.right
            );
        }
    }

    /// <summary>
    /// Eje horizontal frontal del obstáculo.
    /// </summary>
    public Vector3 WorldForwardAxis
    {
        get
        {
            return NormalizeHorizontalAxis(
                transform.forward,
                Vector3.forward
            );
        }
    }

    /// <summary>
    /// Tamaño del obstáculo teniendo en cuenta la escala mundial.
    /// </summary>
    public Vector2 WorldSize
    {
        get
        {
            Vector3 worldScale =
                transform.lossyScale;

            return new Vector2(
                LocalSize.x *
                Mathf.Abs(worldScale.x),

                LocalSize.y *
                Mathf.Abs(worldScale.z)
            );
        }
    }

    /// <summary>
    /// Activa o desactiva el bloqueo de colocación.
    /// </summary>
    public void SetBlocksPlacement(
        bool shouldBlock
    )
    {
        if (blocksPlacement == shouldBlock)
        {
            return;
        }

        blocksPlacement =
            shouldBlock;

        NotifyObstacleChanged();
    }

    /// <summary>
    /// Activa o desactiva operacionalmente el obstáculo.
    /// </summary>
    public void SetOperational(
        bool isOperational
    )
    {
        if (operational == isOperational)
        {
            return;
        }

        operational =
            isOperational;

        NotifyObstacleChanged();
    }

    /// <summary>
    /// Permite avisar al registro cuando la geometría cambia
    /// durante la ejecución.
    /// </summary>
    public void NotifyGeometryChanged()
    {
        NotifyObstacleChanged();
    }

    private void NotifyObstacleChanged()
    {
        ObstacleChanged?.Invoke(
            this
        );
    }

    private static Vector3 NormalizeHorizontalAxis(
        Vector3 axis,
        Vector3 fallback
    )
    {
        axis.y = 0f;

        if (axis.sqrMagnitude <= 0.000001f)
        {
            return fallback;
        }

        return axis.normalized;
    }

    private static string NormalizeIdentifier(
        string rawIdentifier
    )
    {
        if (string.IsNullOrWhiteSpace(rawIdentifier))
        {
            return string.Empty;
        }

        return rawIdentifier
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        obstacleId =
            NormalizeIdentifier(
                obstacleId
            );

        if (string.IsNullOrWhiteSpace(obstacleId))
        {
            obstacleId =
                "placement_obstacle";
        }

        localSize.x =
            Mathf.Max(
                0.01f,
                localSize.x
            );

        localSize.y =
            Mathf.Max(
                0.01f,
                localSize.y
            );

        minimumClearance =
            Mathf.Max(
                0f,
                minimumClearance
            );

        if (Application.isPlaying)
        {
            NotifyObstacleChanged();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector2 size =
            LocalSize;

        Matrix4x4 previousMatrix =
            Gizmos.matrix;

        Gizmos.matrix =
            transform.localToWorldMatrix;

        Gizmos.DrawWireCube(
            localCenter,
            new Vector3(
                size.x,
                0.1f,
                size.y
            )
        );

        if (minimumClearance > 0f)
        {
            Vector3 worldScale =
                transform.lossyScale;

            float localClearanceX =
                minimumClearance /
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(worldScale.x)
                );

            float localClearanceZ =
                minimumClearance /
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(worldScale.z)
                );

            Gizmos.DrawWireCube(
                localCenter,
                new Vector3(
                    size.x +
                    localClearanceX * 2f,
                    0.12f,
                    size.y +
                    localClearanceZ * 2f
                )
            );
        }

        Gizmos.matrix =
            previousMatrix;
    }
#endif
}