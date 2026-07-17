using UnityEngine;

/// <summary>
/// Tipo de conflicto existente entre dos huellas colocables.
/// </summary>
public enum RestaurantPlacementConflictType
{
    None = 0,
    PhysicalOverlap = 1,
    MinimumClearanceViolation = 2
}

/// <summary>
/// Utilidades geométricas para comparar huellas rectangulares
/// orientadas en el plano XZ.
///
/// Utiliza el teorema de ejes separadores —SAT— y funciona con
/// objetos rotados sin depender de colliders ni de Physics.
///
/// No crea colecciones, no usa LINQ y no necesita Update.
/// </summary>
public static class RestaurantPlacementCollisionUtility
{
    private const float SeparationEpsilon = 0.0001f;

    /// <summary>
    /// Determina el conflicto más importante entre dos huellas.
    ///
    /// Primero comprueba el solapamiento físico. Cuando no existe,
    /// comprueba la separación mínima solicitada por los objetos.
    /// </summary>
    public static RestaurantPlacementConflictType
        EvaluateConflict(
            RestaurantPlacementShape first,
            RestaurantPlacementShape second
        )
    {
        if (AreShapesOverlapping(
                first,
                second,
                0f
            ))
        {
            return
                RestaurantPlacementConflictType
                    .PhysicalOverlap;
        }

        float requiredClearance =
            Mathf.Max(
                first.MinimumClearance,
                second.MinimumClearance
            );

        if (requiredClearance <= 0f)
        {
            return
                RestaurantPlacementConflictType.None;
        }

        if (AreShapesOverlapping(
                first,
                second,
                requiredClearance
            ))
        {
            return
                RestaurantPlacementConflictType
                    .MinimumClearanceViolation;
        }

        return RestaurantPlacementConflictType.None;
    }

    /// <summary>
    /// Comprueba exclusivamente el solapamiento físico.
    ///
    /// Dos objetos que únicamente se tocan por el borde no se
    /// consideran solapados.
    /// </summary>
    public static bool HasPhysicalOverlap(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second
    )
    {
        return AreShapesOverlapping(
            first,
            second,
            0f
        );
    }

    /// <summary>
    /// Comprueba si se incumple la separación mínima.
    ///
    /// También devuelve true cuando existe solapamiento físico.
    /// Para distinguir ambos casos debe usarse EvaluateConflict.
    /// </summary>
    public static bool ViolatesMinimumClearance(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second
    )
    {
        float requiredClearance =
            Mathf.Max(
                first.MinimumClearance,
                second.MinimumClearance
            );

        return AreShapesOverlapping(
            first,
            second,
            requiredClearance
        );
    }

    /// <summary>
    /// Aplica SAT sobre los cuatro ejes relevantes:
    /// - Derecha del primer objeto.
    /// - Delante del primer objeto.
    /// - Derecha del segundo objeto.
    /// - Delante del segundo objeto.
    ///
    /// Encontrar un solo eje separador demuestra que las huellas
    /// no se solapan.
    /// </summary>
    private static bool AreShapesOverlapping(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second,
        float requiredClearance
    )
    {
        float clearance =
            Mathf.Max(
                0f,
                requiredClearance
            );

        if (HasSeparatingAxis(
                first,
                second,
                first.RightAxis,
                clearance
            ))
        {
            return false;
        }

        if (HasSeparatingAxis(
                first,
                second,
                first.ForwardAxis,
                clearance
            ))
        {
            return false;
        }

        if (HasSeparatingAxis(
                first,
                second,
                second.RightAxis,
                clearance
            ))
        {
            return false;
        }

        if (HasSeparatingAxis(
                first,
                second,
                second.ForwardAxis,
                clearance
            ))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Comprueba si las proyecciones de ambas huellas están
    /// separadas sobre un eje concreto.
    /// </summary>
    private static bool HasSeparatingAxis(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second,
        Vector3 axis,
        float requiredClearance
    )
    {
        float centerDistance =
            RestaurantPlacementShape
                .ProjectCenterDistance(
                    first,
                    second,
                    axis
                );

        float firstRadius =
            first.CalculateProjectionRadius(axis);

        float secondRadius =
            second.CalculateProjectionRadius(axis);

        float requiredDistance =
            firstRadius +
            secondRadius +
            requiredClearance;

        return centerDistance >=
               requiredDistance -
               SeparationEpsilon;
    }
}