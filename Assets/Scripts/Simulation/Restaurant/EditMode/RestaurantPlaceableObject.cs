using System;
using UnityEngine;

/// <summary>
/// Identidad genérica de una instancia colocada en el restaurante.
///
/// Este componente pertenece a cualquier artículo del modo edición,
/// independientemente de su función concreta.
///
/// Ejemplos:
/// - Una mesa tendrá además RestaurantTable.
/// - Una lámpara tendrá componentes de iluminación.
/// - Una planta podrá tener componentes de decoración.
/// - Un horno tendrá componentes de producción.
///
/// El identificador de instancia es distinto del identificador
/// funcional de una mesa, un horno o cualquier otro sistema.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantEditableObject))]
[RequireComponent(typeof(RestaurantAreaMember))]
[RequireComponent(typeof(RestaurantPlacementFootprint))]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Object"
)]
public sealed class RestaurantPlaceableObject :
    MonoBehaviour
{
    [Header("Definición")]

    [Tooltip(
        "Artículo de catálogo del que procede esta instancia."
    )]
    [SerializeField]
    private RestaurantPlaceableItemDefinition itemDefinition;

    [Header("Identidad de instancia")]

    [Tooltip(
        "Identificador único de esta copia concreta. Los prefabs " +
        "deben dejarlo vacío; el registro lo asignará al crear o " +
        "descubrir la instancia."
    )]
    [SerializeField]
    private string instanceId;

    [Header("Sincronización")]

    [Tooltip(
        "Mantiene RestaurantEditableObject sincronizado con la " +
        "definición editable indicada por el artículo."
    )]
    [SerializeField]
    private bool synchronizeEditableDefinition = true;

    private RestaurantEditableObject editableObject;

    /// <summary>
    /// Se ejecuta cuando el registro asigna o cambia la identidad.
    /// </summary>
    public event Action<
        RestaurantPlaceableObject,
        string,
        string
    > InstanceIdChanged;

    /// <summary>
    /// Se ejecuta cuando cambia la definición del artículo.
    /// </summary>
    public event Action<
        RestaurantPlaceableObject,
        RestaurantPlaceableItemDefinition,
        RestaurantPlaceableItemDefinition
    > ItemDefinitionChanged;

    public RestaurantPlaceableItemDefinition ItemDefinition
    {
        get
        {
            return itemDefinition;
        }
    }

    public string InstanceId
    {
        get
        {
            return string.IsNullOrWhiteSpace(instanceId)
                ? string.Empty
                : instanceId.Trim();
        }
    }

    public bool HasInstanceId
    {
        get
        {
            return !string.IsNullOrWhiteSpace(
                InstanceId
            );
        }
    }

    public bool HasValidDefinition
    {
        get
        {
            return itemDefinition != null;
        }
    }

    public string DisplayName
    {
        get
        {
            if (itemDefinition != null)
            {
                return itemDefinition.DisplayName;
            }

            return gameObject.name;
        }
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        SynchronizeDefinitionsIfNeeded();
    }

    /// <summary>
    /// Cambia el artículo asociado a esta instancia.
    ///
    /// Se utilizará durante la creación, la carga de partidas
    /// y las herramientas de preparación de prefabs.
    /// </summary>
    public bool SetItemDefinition(
        RestaurantPlaceableItemDefinition newDefinition
    )
    {
        if (ReferenceEquals(
                itemDefinition,
                newDefinition
            ))
        {
            SynchronizeDefinitionsIfNeeded();
            return false;
        }

        RestaurantPlaceableItemDefinition previousDefinition =
            itemDefinition;

        itemDefinition =
            newDefinition;

        SynchronizeDefinitionsIfNeeded();

        ItemDefinitionChanged?.Invoke(
            this,
            previousDefinition,
            itemDefinition
        );

        return true;
    }

    /// <summary>
    /// Asigna la identidad estable de esta copia.
    ///
    /// Debe llamarlo RestaurantPlaceableRegistry o el sistema
    /// de carga, nunca basarse en el nombre del GameObject.
    /// </summary>
    public bool AssignInstanceId(
        string newInstanceId
    )
    {
        string normalizedId =
            NormalizeInstanceId(
                newInstanceId
            );

        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return false;
        }

        string previousId =
            InstanceId;

        if (string.Equals(
                previousId,
                normalizedId,
                StringComparison.Ordinal
            ))
        {
            return false;
        }

        instanceId =
            normalizedId;

        InstanceIdChanged?.Invoke(
            this,
            previousId,
            instanceId
        );

        return true;
    }

    /// <summary>
    /// Comprueba que el objeto contiene la configuración mínima
    /// necesaria para participar en el sistema.
    /// </summary>
    public bool ValidateConfiguration(
        out string errorMessage
    )
    {
        if (itemDefinition == null)
        {
            errorMessage =
                gameObject.name +
                " no tiene una definición de artículo colocable.";

            return false;
        }

        if (!TryGetComponent(
                out RestaurantEditableObject currentEditableObject
            ))
        {
            errorMessage =
                gameObject.name +
                " no tiene RestaurantEditableObject.";

            return false;
        }

        if (!TryGetComponent(
                out RestaurantAreaMember _
            ))
        {
            errorMessage =
                gameObject.name +
                " no tiene RestaurantAreaMember.";

            return false;
        }

        if (!TryGetComponent(
                out RestaurantPlacementFootprint _
            ))
        {
            errorMessage =
                gameObject.name +
                " no tiene RestaurantPlacementFootprint.";

            return false;
        }

        RestaurantEditableObjectDefinition expectedDefinition =
            itemDefinition.EditableDefinition;

        if (expectedDefinition != null &&
            !ReferenceEquals(
                currentEditableObject.Definition,
                expectedDefinition
            ))
        {
            errorMessage =
                gameObject.name +
                " no utiliza la definición editable indicada por " +
                itemDefinition.DisplayName +
                ".";

            return false;
        }

        errorMessage =
            string.Empty;

        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editableObject == null)
        {
            TryGetComponent(
                out editableObject
            );
        }
    }

    private void SynchronizeDefinitionsIfNeeded()
    {
        if (!synchronizeEditableDefinition ||
            itemDefinition == null)
        {
            return;
        }

        CacheDependenciesIfNeeded();

        if (editableObject == null)
        {
            return;
        }

        RestaurantEditableObjectDefinition targetDefinition =
            itemDefinition.EditableDefinition;

        if (targetDefinition == null ||
            ReferenceEquals(
                editableObject.Definition,
                targetDefinition
            ))
        {
            return;
        }

        editableObject.SetDefinition(
            targetDefinition
        );
    }

    private static string NormalizeInstanceId(
        string rawInstanceId
    )
    {
        if (string.IsNullOrWhiteSpace(rawInstanceId))
        {
            return string.Empty;
        }

        return rawInstanceId
            .Trim()
            .ToLowerInvariant();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
        SynchronizeDefinitionsIfNeeded();
    }

    private void OnValidate()
    {
        instanceId =
            NormalizeInstanceId(
                instanceId
            );

        CacheDependenciesIfNeeded();
        SynchronizeDefinitionsIfNeeded();
    }
#endif
}
