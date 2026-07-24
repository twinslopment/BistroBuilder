using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Implementación inicial legible y depurable basada en JsonUtility.
///
/// Se mantiene detrás de un contrato de bytes intercambiable. Una sección
/// futura puede usar binario sin cambiar los sistemas ni el storage.
/// </summary>
public sealed class BistroBuilderJsonSaveSerializer :
    IBistroBuilderSaveSerializer
{
    public const string StableSerializerId = "unity-json-v1";

    public string SerializerId => StableSerializerId;

    public string FileExtension => ".json";

    public byte[] Serialize(
        object value,
        bool prettyPrint
    )
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string json = JsonUtility.ToJson(value, prettyPrint);
        return Encoding.UTF8.GetBytes(json);
    }

    public object Deserialize(
        byte[] serializedValue,
        Type targetType
    )
    {
        if (serializedValue == null || serializedValue.Length == 0)
        {
            throw new ArgumentException(
                "El contenido serializado está vacío.",
                nameof(serializedValue)
            );
        }

        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        string json = Encoding.UTF8.GetString(serializedValue);
        object result = JsonUtility.FromJson(json, targetType);

        if (result == null)
        {
            throw new InvalidOperationException(
                "JsonUtility no pudo reconstruir " +
                targetType.Name + "."
            );
        }

        return result;
    }
}
