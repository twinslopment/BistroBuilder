using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado visual genérico de un destino de ajuste.
/// </summary>
public enum RestaurantPlacementSnapHintState
{
    Available = 0,
    Occupied = 1,
    Blocked = 2
}

/// <summary>
/// Identidad estable de un destino de snapping durante una sesión.
///
/// No utiliza cadenas ni asignaciones por frame. La combinación del
/// proveedor, el objeto relacionado y el identificador local hace que
/// dos destinos sean deterministas incluso cuando están muy próximos.
/// </summary>
public readonly struct RestaurantPlacementSnapTargetKey :
    IEquatable<RestaurantPlacementSnapTargetKey>
{
    public int ProviderInstanceId { get; }

    public int RelatedObjectInstanceId { get; }

    public int LocalTargetId { get; }

    public bool IsValid => ProviderInstanceId != 0;

    public RestaurantPlacementSnapTargetKey(
        int providerInstanceId,
        int relatedObjectInstanceId,
        int localTargetId
    )
    {
        ProviderInstanceId = providerInstanceId;
        RelatedObjectInstanceId = relatedObjectInstanceId;
        LocalTargetId = localTargetId;
    }

    public bool Equals(RestaurantPlacementSnapTargetKey other)
    {
        return ProviderInstanceId == other.ProviderInstanceId &&
               RelatedObjectInstanceId == other.RelatedObjectInstanceId &&
               LocalTargetId == other.LocalTargetId;
    }

    public override bool Equals(object obj)
    {
        return obj is RestaurantPlacementSnapTargetKey other &&
               Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ProviderInstanceId;
            hash = hash * 397 ^ RelatedObjectInstanceId;
            hash = hash * 397 ^ LocalTargetId;
            return hash;
        }
    }

    public static bool operator ==(
        RestaurantPlacementSnapTargetKey first,
        RestaurantPlacementSnapTargetKey second
    )
    {
        return first.Equals(second);
    }

    public static bool operator !=(
        RestaurantPlacementSnapTargetKey first,
        RestaurantPlacementSnapTargetKey second
    )
    {
        return !first.Equals(second);
    }
}

/// <summary>
/// Contexto inmutable que reciben los proveedores de snapping.
/// </summary>
public readonly struct RestaurantPlacementSnapContext
{
    public RestaurantAreaMember Member { get; }

    public Vector3 RawRootPosition { get; }

    public Quaternion RawRootRotation { get; }

    public bool HasCapturedTarget { get; }

    public RestaurantPlacementSnapTargetKey CapturedTarget { get; }

    public RestaurantPlacementSnapContext(
        RestaurantAreaMember member,
        Vector3 rawRootPosition,
        Quaternion rawRootRotation,
        bool hasCapturedTarget,
        RestaurantPlacementSnapTargetKey capturedTarget
    )
    {
        Member = member;
        RawRootPosition = rawRootPosition;
        RawRootRotation = rawRootRotation;
        HasCapturedTarget = hasCapturedTarget;
        CapturedTarget = capturedTarget;
    }
}

/// <summary>
/// Pose propuesta por un proveedor especializado.
/// </summary>
public readonly struct RestaurantPlacementSnapCandidate
{
    public IRestaurantPlacementSnapProvider Provider { get; }

    public RestaurantPlacementSnapTargetKey TargetKey { get; }

    public Vector3 RootPosition { get; }

    public Quaternion RootRotation { get; }

    public float Distance { get; }

    public float CaptureRadius { get; }

    public float ReleaseRadius { get; }

    public float PreferenceScore { get; }

    public UnityEngine.Object RelatedObject { get; }

    public RestaurantPlacementSnapHintState HintState { get; }

    public RestaurantPlacementSnapCandidate(
        IRestaurantPlacementSnapProvider provider,
        RestaurantPlacementSnapTargetKey targetKey,
        Vector3 rootPosition,
        Quaternion rootRotation,
        float distance,
        float captureRadius,
        float releaseRadius,
        float preferenceScore,
        UnityEngine.Object relatedObject,
        RestaurantPlacementSnapHintState hintState
    )
    {
        Provider = provider;
        TargetKey = targetKey;
        RootPosition = rootPosition;
        RootRotation = rootRotation;
        Distance = Mathf.Max(0f, distance);
        CaptureRadius = Mathf.Max(0.01f, captureRadius);
        ReleaseRadius = Mathf.Max(CaptureRadius, releaseRadius);
        PreferenceScore = preferenceScore;
        RelatedObject = relatedObject;
        HintState = hintState;
    }
}

/// <summary>
/// Indicador visual genérico de un destino de snapping.
/// </summary>
public readonly struct RestaurantPlacementSnapHint
{
    public RestaurantPlacementSnapTargetKey TargetKey { get; }

    public Vector3 WorldPosition { get; }

    public Vector3 FacingDirection { get; }

    public float Radius { get; }

    public RestaurantPlacementSnapHintState State { get; }

    public UnityEngine.Object RelatedObject { get; }

    public RestaurantPlacementSnapHint(
        RestaurantPlacementSnapTargetKey targetKey,
        Vector3 worldPosition,
        Vector3 facingDirection,
        float radius,
        RestaurantPlacementSnapHintState state,
        UnityEngine.Object relatedObject
    )
    {
        TargetKey = targetKey;
        WorldPosition = worldPosition;

        facingDirection.y = 0f;

        FacingDirection =
            facingDirection.sqrMagnitude > 0.000001f
                ? facingDirection.normalized
                : Vector3.forward;

        Radius = Mathf.Max(0.05f, radius);
        State = state;
        RelatedObject = relatedObject;
    }
}

/// <summary>
/// Resultado público del servicio universal de snapping.
/// </summary>
public readonly struct RestaurantPlacementSnapResult
{
    public bool IsSnapped { get; }

    public Vector3 RootPosition { get; }

    public Quaternion RootRotation { get; }

    public RestaurantPlacementSnapTargetKey TargetKey { get; }

    public UnityEngine.Object RelatedObject { get; }

    public RestaurantPlacementSnapHintState HintState { get; }

    public float Distance { get; }

    public RestaurantPlacementSnapResult(
        bool isSnapped,
        Vector3 rootPosition,
        Quaternion rootRotation,
        RestaurantPlacementSnapTargetKey targetKey,
        UnityEngine.Object relatedObject,
        RestaurantPlacementSnapHintState hintState,
        float distance
    )
    {
        IsSnapped = isSnapped;
        RootPosition = rootPosition;
        RootRotation = rootRotation;
        TargetKey = targetKey;
        RelatedObject = relatedObject;
        HintState = hintState;
        Distance = Mathf.Max(0f, distance);
    }

    public static RestaurantPlacementSnapResult Unsnapped(
        Vector3 rootPosition,
        Quaternion rootRotation
    )
    {
        return new RestaurantPlacementSnapResult(
            false,
            rootPosition,
            rootRotation,
            default,
            null,
            RestaurantPlacementSnapHintState.Available,
            0f
        );
    }
}

/// <summary>
/// Contrato universal para familias que ofrecen posiciones asistidas.
///
/// Ejemplos futuros: sillas-mesa, módulos de barra, muebles-pared,
/// decoración-superficie o equipamiento encastrado.
/// </summary>
public interface IRestaurantPlacementSnapProvider
{
    int Priority { get; }

    bool IsSnapEnabled { get; }

    void CollectCandidates(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapCandidate> results
    );

    void CollectVisualHints(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapHint> results
    );
}
