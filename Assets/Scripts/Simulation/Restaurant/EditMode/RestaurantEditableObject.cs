using System;
using UnityEngine;

/// <summary>
/// Marca una instancia concreta del restaurante como editable.
///
/// Vincula el GameObject de la escena con una definición de datos.
/// No contiene reglas espaciales ni lógica de colisión.
///
/// Las reglas espaciales continúan perteneciendo a:
/// - RestaurantPlacementValidationService.
/// - RestaurantPlacementTransactionService.
/// - RestaurantAreaAssignmentService.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantAreaMember))]
[RequireComponent(typeof(RestaurantPlacementFootprint))]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Editable Object"
)]
public sealed class RestaurantEditableObject :
    MonoBehaviour
{
    [Header("Definición")]

    [Tooltip(
        "Definición compartida que establece las operaciones " +
        "permitidas para este tipo de objeto."
    )]
    [SerializeField]
    private RestaurantEditableObjectDefinition definition;

    [Header("Estado de instancia")]

    [Tooltip(
        "Permite desactivar temporalmente la edición de esta " +
        "instancia sin eliminar su definición."
    )]
    [SerializeField]
    private bool editingEnabled = true;

    /// <summary>
    /// Se ejecuta cuando cambia la disponibilidad de edición.
    /// </summary>
    public event Action<
        RestaurantEditableObject,
        bool
    > EditingAvailabilityChanged;

    public RestaurantEditableObjectDefinition Definition
    {
        get
        {
            return definition;
        }
    }

    public bool EditingEnabled
    {
        get
        {
            return editingEnabled;
        }
    }

    public bool HasValidDefinition
    {
        get
        {
            return definition != null;
        }
    }

    public bool CanMove
    {
        get
        {
            return editingEnabled &&
                   definition != null &&
                   definition.CanMove;
        }
    }

    public bool CanRotate
    {
        get
        {
            return editingEnabled &&
                   definition != null &&
                   definition.CanRotate;
        }
    }

    public string DisplayName
    {
        get
        {
            if (definition != null)
            {
                return definition.DisplayName;
            }

            return gameObject.name;
        }
    }

    /// <summary>
    /// Comprueba si la interacción de edición puede comenzar.
    /// </summary>
    public bool CanBeginEditing(
        out string rejectionReason
    )
    {
        if (!isActiveAndEnabled ||
            !gameObject.activeInHierarchy)
        {
            rejectionReason =
                "El objeto no está activo.";

            return false;
        }

        if (!editingEnabled)
        {
            rejectionReason =
                "La edición de esta instancia está desactivada.";

            return false;
        }

        if (definition == null)
        {
            rejectionReason =
                "El objeto no tiene una definición editable.";

            return false;
        }

        if (!definition.CanMove)
        {
            rejectionReason =
                "Este tipo de objeto no permite movimiento.";

            return false;
        }

        RestaurantAreaMember areaMember;

        if (!TryGetComponent(
                out areaMember
            ))
        {
            rejectionReason =
                "El objeto no tiene RestaurantAreaMember.";

            return false;
        }

        RestaurantPlacementFootprint footprint;

        if (!TryGetComponent(
                out footprint
            ))
        {
            rejectionReason =
                "El objeto no tiene RestaurantPlacementFootprint.";

            return false;
        }

        rejectionReason =
            string.Empty;

        return true;
    }

    /// <summary>
    /// Obtiene el tamaño de cuadrícula aplicable a esta instancia.
    /// </summary>
    public float ResolveGridSize(
        float defaultGridSize
    )
    {
        if (definition == null)
        {
            return Mathf.Max(
                0.01f,
                defaultGridSize
            );
        }

        return definition.ResolveGridSize(
            defaultGridSize
        );
    }

    /// <summary>
    /// Obtiene el incremento de rotación aplicable.
    /// </summary>
    public float ResolveRotationStepDegrees(
        float defaultRotationStepDegrees
    )
    {
        if (definition == null)
        {
            return Mathf.Clamp(
                defaultRotationStepDegrees,
                1f,
                180f
            );
        }

        return definition.ResolveRotationStepDegrees(
            defaultRotationStepDegrees
        );
    }

    /// <summary>
    /// Cambia la definición durante la preparación del restaurante
    /// o la carga de una partida.
    /// </summary>
    public void SetDefinition(
        RestaurantEditableObjectDefinition newDefinition
    )
    {
        definition =
            newDefinition;
    }

    /// <summary>
    /// Activa o desactiva la edición de esta instancia.
    /// </summary>
    public void SetEditingEnabled(
        bool enabled
    )
    {
        if (editingEnabled == enabled)
        {
            return;
        }

        editingEnabled =
            enabled;

        EditingAvailabilityChanged?.Invoke(
            this,
            editingEnabled
        );
    }
}