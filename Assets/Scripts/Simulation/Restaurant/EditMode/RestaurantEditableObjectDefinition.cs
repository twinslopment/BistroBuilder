using UnityEngine;

/// <summary>
/// Define las reglas de edición de un tipo de objeto del restaurante.
///
/// Esta información se guarda como ScriptableObject para que:
/// - Varias instancias compartan la misma configuración.
/// - Las reglas no dependan del nombre del GameObject.
/// - Los futuros muebles y equipamientos puedan configurarse
///   sin modificar código.
/// - La información pueda reutilizarse en catálogo, guardado,
///   desbloqueos, economía y modo reforma.
/// </summary>
[CreateAssetMenu(
    fileName = "EditableObjectDefinition_",
    menuName =
        "Bistro Builder/Restaurant/Edit Mode/" +
        "Editable Object Definition"
)]
public sealed class RestaurantEditableObjectDefinition :
    ScriptableObject
{
    [Header("Identidad")]

    [Tooltip(
        "Identificador técnico estable. " +
        "Se utilizará posteriormente en guardado y catálogo."
    )]
    [SerializeField]
    private string definitionId =
        "editable_object";

    [Tooltip(
        "Nombre legible mostrado al jugador."
    )]
    [SerializeField]
    private string displayName =
        "Objeto editable";

    [Tooltip(
        "Descripción interna o visible del tipo de objeto."
    )]
    [SerializeField]
    [TextArea(2, 5)]
    private string description;

    [Header("Operaciones permitidas")]

    [Tooltip(
        "Permite cambiar la posición del objeto."
    )]
    [SerializeField]
    private bool canMove = true;

    [Tooltip(
        "Permite rotar el objeto durante la colocación."
    )]
    [SerializeField]
    private bool canRotate = true;

    [Header("Cuadrícula")]

    [Tooltip(
        "Utiliza un tamaño de cuadrícula específico para este " +
        "tipo de objeto."
    )]
    [SerializeField]
    private bool useCustomGridSize;

    [Tooltip(
        "Tamaño de cuadrícula utilizado cuando está activada " +
        "la configuración personalizada."
    )]
    [SerializeField]
    [Min(0.01f)]
    private float customGridSize = 0.25f;

    [Header("Rotación")]

    [Tooltip(
        "Utiliza un incremento de rotación específico para este " +
        "tipo de objeto."
    )]
    [SerializeField]
    private bool useCustomRotationStep = true;

    [Tooltip(
        "Grados aplicados en cada rotación."
    )]
    [SerializeField]
    [Range(1f, 180f)]
    private float customRotationStepDegrees = 90f;

    public string DefinitionId
    {
        get
        {
            return NormalizeIdentifier(
                definitionId
            );
        }
    }

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return name;
            }

            return displayName.Trim();
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public bool CanMove
    {
        get
        {
            return canMove;
        }
    }

    public bool CanRotate
    {
        get
        {
            return canRotate;
        }
    }

    public bool UsesCustomGridSize
    {
        get
        {
            return useCustomGridSize;
        }
    }

    public float CustomGridSize
    {
        get
        {
            return Mathf.Max(
                0.01f,
                customGridSize
            );
        }
    }

    public bool UsesCustomRotationStep
    {
        get
        {
            return useCustomRotationStep;
        }
    }

    public float CustomRotationStepDegrees
    {
        get
        {
            return Mathf.Clamp(
                customRotationStepDegrees,
                1f,
                180f
            );
        }
    }

    /// <summary>
    /// Devuelve el tamaño de cuadrícula efectivo.
    /// </summary>
    public float ResolveGridSize(
        float defaultGridSize
    )
    {
        if (useCustomGridSize)
        {
            return CustomGridSize;
        }

        return Mathf.Max(
            0.01f,
            defaultGridSize
        );
    }

    /// <summary>
    /// Devuelve el incremento de rotación efectivo.
    /// </summary>
    public float ResolveRotationStepDegrees(
        float defaultRotationStepDegrees
    )
    {
        if (useCustomRotationStep)
        {
            return CustomRotationStepDegrees;
        }

        return Mathf.Clamp(
            defaultRotationStepDegrees,
            1f,
            180f
        );
    }

    private void OnValidate()
    {
        definitionId =
            NormalizeIdentifier(
                definitionId
            );

        if (string.IsNullOrWhiteSpace(definitionId))
        {
            definitionId =
                "editable_object";
        }

        customGridSize =
            Mathf.Max(
                0.01f,
                customGridSize
            );

        customRotationStepDegrees =
            Mathf.Clamp(
                customRotationStepDegrees,
                1f,
                180f
            );
    }

    /// <summary>
    /// Normaliza identificadores para mantener un formato estable.
    /// </summary>
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
}