using System;
using UnityEngine;

/// <summary>
/// Define la superficie horizontal ocupada por un objeto
/// colocable del restaurante.
///
/// Permite representar:
/// - El tamaño físico del objeto.
/// - Su centro local.
/// - Su separación mínima respecto a otros objetos.
/// - Si debe bloquear la colocación de otros elementos.
/// - Su huella actual o una pose candidata.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlacementFootprint :
    MonoBehaviour
{
    private const int SamplePointCount = 5;

    [Header("Huella física")]

    [Tooltip(
        "Desplazamiento local del centro de la huella respecto " +
        "al origen del objeto."
    )]
    [SerializeField]
    private Vector3 localCenter = Vector3.zero;

    [Tooltip(
        "Anchura y profundidad locales ocupadas por el objeto. " +
        "X representa anchura y Y representa profundidad."
    )]
    [SerializeField]
    private Vector2 size = Vector2.one;

    [Tooltip(
        "Pequeño margen interior aplicado a las esquinas para " +
        "evitar rechazos causados por límites matemáticos exactos."
    )]
    [SerializeField]
    [Min(0f)]
    private float boundaryInset = 0.02f;

    [Header("Separación y bloqueo")]

    [Tooltip(
        "Distancia mínima que debe mantenerse respecto a otros " +
        "objetos colocables. No aumenta el tamaño visual."
    )]
    [SerializeField]
    [Min(0f)]
    private float minimumClearance;

    [Tooltip(
        "Indica si esta huella impide que otros objetos ocupen " +
        "su mismo espacio."
    )]
    [SerializeField]
    private bool blocksOtherPlacements = true;

    public Vector3 LocalCenter =>
        localCenter;

    public Vector2 Size =>
        size;

    public float BoundaryInset =>
        boundaryInset;

    public float MinimumClearance =>
        minimumClearance;

    public bool BlocksOtherPlacements =>
        blocksOtherPlacements;

    public int PlacementSamplePointCount =>
        SamplePointCount;

    /// <summary>
    /// Construye la representación geométrica de la huella para
    /// una posición y rotación candidatas.
    ///
    /// No modifica el Transform real del objeto.
    /// </summary>
    public RestaurantPlacementShape BuildShapeAtPose(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation
    )
    {
        Vector3 currentScale =
            transform.lossyScale;

        Vector3 scaledLocalCenter =
            new Vector3(
                localCenter.x * currentScale.x,
                localCenter.y * currentScale.y,
                localCenter.z * currentScale.z
            );

        Vector3 worldCenter =
            candidateRootPosition +
            candidateRootRotation *
            scaledLocalCenter;

        float halfWidth =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    size.x * currentScale.x
                ) * 0.5f
            );

        float halfDepth =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    size.y * currentScale.z
                ) * 0.5f
            );

        Vector3 rightAxis =
            candidateRootRotation *
            Vector3.right;

        Vector3 forwardAxis =
            candidateRootRotation *
            Vector3.forward;

        return new RestaurantPlacementShape(
            worldCenter,
            rightAxis,
            forwardAxis,
            new Vector2(
                halfWidth,
                halfDepth
            ),
            minimumClearance
        );
    }

    /// <summary>
    /// Construye la huella correspondiente a la pose actual.
    /// </summary>
    public RestaurantPlacementShape BuildCurrentShape()
    {
        return BuildShapeAtPose(
            transform.position,
            transform.rotation
        );
    }

    /// <summary>
    /// Calcula el centro mundial para una pose candidata.
    /// </summary>
    public Vector3 GetWorldCenter(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation
    )
    {
        return BuildShapeAtPose(
            candidateRootPosition,
            candidateRootRotation
        ).Center;
    }

    /// <summary>
    /// Escribe el centro y las cuatro esquinas mundiales en un
    /// array reutilizable.
    ///
    /// El array debe contener al menos cinco posiciones.
    /// </summary>
    public int WriteWorldSamplePoints(
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        Vector3[] results
    )
    {
        if (results == null)
        {
            throw new ArgumentNullException(
                nameof(results)
            );
        }

        if (results.Length < SamplePointCount)
        {
            throw new ArgumentException(
                $"El array debe contener al menos " +
                $"{SamplePointCount} posiciones.",
                nameof(results)
            );
        }

        RestaurantPlacementShape shape =
            BuildShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        float inset =
            Mathf.Max(
                0f,
                boundaryInset
            );

        float insetHalfWidth =
            Mathf.Max(
                0.001f,
                shape.HalfWidth - inset
            );

        float insetHalfDepth =
            Mathf.Max(
                0.001f,
                shape.HalfDepth - inset
            );

        Vector3 rightOffset =
            shape.RightAxis *
            insetHalfWidth;

        Vector3 forwardOffset =
            shape.ForwardAxis *
            insetHalfDepth;

        results[0] =
            shape.Center;

        results[1] =
            shape.Center +
            rightOffset +
            forwardOffset;

        results[2] =
            shape.Center -
            rightOffset +
            forwardOffset;

        results[3] =
            shape.Center -
            rightOffset -
            forwardOffset;

        results[4] =
            shape.Center +
            rightOffset -
            forwardOffset;

        return SamplePointCount;
    }

    public int WriteCurrentWorldSamplePoints(
        Vector3[] results
    )
    {
        return WriteWorldSamplePoints(
            transform.position,
            transform.rotation,
            results
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        size.x =
            Mathf.Max(
                0.05f,
                size.x
            );

        size.y =
            Mathf.Max(
                0.05f,
                size.y
            );

        boundaryInset =
            Mathf.Max(
                0f,
                boundaryInset
            );

        minimumClearance =
            Mathf.Max(
                0f,
                minimumClearance
            );
    }

    /// <summary>
    /// Dibuja en blanco la huella física y, cuando existe,
    /// una segunda caja que representa la separación mínima.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        RestaurantPlacementShape shape =
            BuildCurrentShape();

        Matrix4x4 previousMatrix =
            Gizmos.matrix;

        Gizmos.matrix =
            Matrix4x4.TRS(
                shape.Center,
                transform.rotation,
                Vector3.one
            );

        Vector3 physicalSize =
            new Vector3(
                shape.HalfWidth * 2f,
                0.05f,
                shape.HalfDepth * 2f
            );

        Gizmos.DrawWireCube(
            Vector3.zero,
            physicalSize
        );

        if (minimumClearance > 0f)
        {
            Vector3 clearanceSize =
                new Vector3(
                    physicalSize.x +
                    minimumClearance * 2f,
                    0.07f,
                    physicalSize.z +
                    minimumClearance * 2f
                );

            Gizmos.DrawWireCube(
                Vector3.zero,
                clearanceSize
            );
        }

        Gizmos.matrix =
            previousMatrix;
    }
#endif
}