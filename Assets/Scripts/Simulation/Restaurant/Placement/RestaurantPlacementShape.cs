using UnityEngine;

/// <summary>
/// Representa una huella rectangular orientada en el plano XZ.
///
/// Es una estructura de datos independiente de MonoBehaviour.
/// Puede representar tanto la posición actual de un objeto como
/// una posición candidata todavía no confirmada.
///
/// Se utilizará para:
/// - Detectar solapamientos entre muebles.
/// - Aplicar separación mínima.
/// - Validar objetos rotados.
/// - Consultar posiciones candidatas del modo edición.
/// </summary>
public readonly struct RestaurantPlacementShape
{
    /// <summary>
    /// Centro mundial de la huella.
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// Eje horizontal local del objeto proyectado en XZ.
    /// </summary>
    public Vector3 RightAxis { get; }

    /// <summary>
    /// Eje de profundidad local del objeto proyectado en XZ.
    /// </summary>
    public Vector3 ForwardAxis { get; }

    /// <summary>
    /// Semianchura y semiprofundidad de la huella.
    /// X representa semianchura.
    /// Y representa semiprofundidad.
    /// </summary>
    public Vector2 HalfExtents { get; }

    /// <summary>
    /// Separación mínima solicitada alrededor del objeto.
    /// No forma parte del tamaño físico real.
    /// </summary>
    public float MinimumClearance { get; }

    public float HalfWidth =>
        HalfExtents.x;

    public float HalfDepth =>
        HalfExtents.y;

    public RestaurantPlacementShape(
        Vector3 center,
        Vector3 rightAxis,
        Vector3 forwardAxis,
        Vector2 halfExtents,
        float minimumClearance
    )
    {
        Center = center;

        RightAxis =
            NormalizeHorizontalAxis(
                rightAxis,
                Vector3.right
            );

        ForwardAxis =
            NormalizeHorizontalAxis(
                forwardAxis,
                Vector3.forward
            );

        HalfExtents =
            new Vector2(
                Mathf.Max(0.001f, halfExtents.x),
                Mathf.Max(0.001f, halfExtents.y)
            );

        MinimumClearance =
            Mathf.Max(0f, minimumClearance);
    }

    /// <summary>
    /// Devuelve una esquina mundial de la huella.
    ///
    /// horizontalSign:
    /// -1 para izquierda.
    ///  1 para derecha.
    ///
    /// depthSign:
    /// -1 para atrás.
    ///  1 para delante.
    /// </summary>
    public Vector3 GetCorner(
        float horizontalSign,
        float depthSign
    )
    {
        return Center +
               RightAxis *
               HalfWidth *
               horizontalSign +
               ForwardAxis *
               HalfDepth *
               depthSign;
    }

    /// <summary>
    /// Proyecta la distancia entre dos centros sobre un eje.
    /// Se utiliza en las futuras pruebas de separación.
    /// </summary>
    public static float ProjectCenterDistance(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second,
        Vector3 axis
    )
    {
        Vector3 horizontalAxis =
            NormalizeHorizontalAxis(
                axis,
                Vector3.right
            );

        Vector3 centerDifference =
            second.Center - first.Center;

        centerDifference.y = 0f;

        return Mathf.Abs(
            Vector3.Dot(
                centerDifference,
                horizontalAxis
            )
        );
    }

    /// <summary>
    /// Calcula el radio de proyección de la huella sobre un eje.
    ///
    /// Este cálculo forma parte del teorema de ejes separadores,
    /// que permitirá detectar colisiones entre rectángulos
    /// orientados sin utilizar colliders físicos.
    /// </summary>
    public float CalculateProjectionRadius(
        Vector3 axis,
        float additionalClearance = 0f
    )
    {
        Vector3 horizontalAxis =
            NormalizeHorizontalAxis(
                axis,
                Vector3.right
            );

        float clearance =
            Mathf.Max(
                0f,
                additionalClearance
            );

        float expandedHalfWidth =
            HalfWidth + clearance;

        float expandedHalfDepth =
            HalfDepth + clearance;

        float rightProjection =
            Mathf.Abs(
                Vector3.Dot(
                    RightAxis,
                    horizontalAxis
                )
            );

        float forwardProjection =
            Mathf.Abs(
                Vector3.Dot(
                    ForwardAxis,
                    horizontalAxis
                )
            );

        return expandedHalfWidth *
               rightProjection +
               expandedHalfDepth *
               forwardProjection;
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
}