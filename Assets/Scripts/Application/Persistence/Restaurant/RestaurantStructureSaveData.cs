using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado estructural inicial del restaurante.
///
/// Solo contiene datos persistentes. No almacena GameObject, prefabs,
/// ScriptableObject, caches ni referencias de escena.
/// </summary>
[Serializable]
public sealed class RestaurantStructureSaveData
{
    public string sceneName = string.Empty;

    public List<RestaurantPlaceableSaveRecord> placeables =
        new List<RestaurantPlaceableSaveRecord>();

    public List<RestaurantSeatLinkSaveRecord> seatLinks =
        new List<RestaurantSeatLinkSaveRecord>();
}

[Serializable]
public sealed class RestaurantPlaceableSaveRecord
{
    public string instanceId = string.Empty;
    public string itemId = string.Empty;
    public BistroBuilderSaveVector3 worldPosition;
    public BistroBuilderSaveQuaternion worldRotation;
    public BistroBuilderSaveVector3 localScale;
}

[Serializable]
public sealed class RestaurantSeatLinkSaveRecord
{
    public string seatInstanceId = string.Empty;
    public string tableInstanceId = string.Empty;
    public int slotIndex = -1;
}

[Serializable]
public struct BistroBuilderSaveVector3
{
    public float x;
    public float y;
    public float z;

    public BistroBuilderSaveVector3(Vector3 value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }

    public bool IsFinite()
    {
        return IsFiniteValue(x) &&
               IsFiniteValue(y) &&
               IsFiniteValue(z);
    }

    private static bool IsFiniteValue(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}

[Serializable]
public struct BistroBuilderSaveQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public BistroBuilderSaveQuaternion(Quaternion value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
        w = value.w;
    }

    public Quaternion ToQuaternion()
    {
        // Unity no expone sqrMagnitude en Quaternion. Validamos y normalizamos
        // directamente con los cuatro componentes persistidos para mantener
        // compatibilidad entre versiones de Unity.
        if (!HasUsableMagnitude())
        {
            return Quaternion.identity;
        }

        float squaredMagnitude =
            x * x + y * y + z * z + w * w;
        float inverseMagnitude = 1f / Mathf.Sqrt(squaredMagnitude);

        return new Quaternion(
            x * inverseMagnitude,
            y * inverseMagnitude,
            z * inverseMagnitude,
            w * inverseMagnitude);
    }

    public bool IsFinite()
    {
        return IsFiniteValue(x) &&
               IsFiniteValue(y) &&
               IsFiniteValue(z) &&
               IsFiniteValue(w);
    }

    public bool HasUsableMagnitude()
    {
        if (!IsFinite())
        {
            return false;
        }

        float squaredMagnitude =
            x * x + y * y + z * z + w * w;

        return squaredMagnitude > 0.000001f;
    }

    private static bool IsFiniteValue(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}
