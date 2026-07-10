using UnityEngine;

/// <summary>
/// Representa visualmente el estado operativo de una mesa.
///
/// Cada estado de RestaurantTable se muestra mediante un color provisional.
/// Esta clase solo se ocupa de la presentación visual y no modifica
/// la lógica interna de la mesa.
/// </summary>
public sealed class TableStateView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private RestaurantTable restaurantTable;

    [SerializeField]
    private Renderer tableRenderer;

    [Header("Colores provisionales")]
    [SerializeField]
    private Color freeColor = Color.green;

    [SerializeField]
    private Color waitingForWaiterColor = Color.yellow;

    [SerializeField]
    private Color takingOrderColor =
        new Color(1f, 0.5f, 0f);

    [SerializeField]
    private Color waitingForFoodColor = Color.red;

    [SerializeField]
    private Color eatingColor = Color.cyan;

    [SerializeField]
    private Color waitingForBillColor = Color.magenta;

    [SerializeField]
    private Color payingColor =
        new Color(0.6f, 0.2f, 0.8f);

    [SerializeField]
    private Color dirtyColor = Color.gray;

    // Identificadores de las propiedades de color utilizadas
    // por los shaders habituales de Unity.
    private static readonly int BaseColorProperty =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorProperty =
        Shader.PropertyToID("_Color");

    // Permite modificar el color del Renderer sin crear
    // una copia independiente del material para cada mesa.
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        FindRequiredComponents();
        EnsurePropertyBlockExists();
    }

    private void OnEnable()
    {
        // Unity puede ejecutar OnEnable después de recompilar scripts
        // en el editor sin conservar los campos no serializados.
        // Por eso garantizamos aquí que propertyBlock vuelva a existir.
        FindRequiredComponents();
        EnsurePropertyBlockExists();

        if (restaurantTable == null)
        {
            Debug.LogError(
                "TableStateView necesita una referencia " +
                "a RestaurantTable.",
                this
            );

            enabled = false;
            return;
        }

        if (tableRenderer == null)
        {
            Debug.LogError(
                "TableStateView necesita una referencia a Renderer.",
                this
            );

            enabled = false;
            return;
        }

        restaurantTable.StateChanged +=
            HandleStateChanged;

        // Sincronizamos inmediatamente el color con el estado
        // actual de la mesa.
        UpdateVisualState(
            restaurantTable.CurrentState
        );
    }

    private void OnDisable()
    {
        if (restaurantTable != null)
        {
            restaurantTable.StateChanged -=
                HandleStateChanged;
        }
    }

    /// <summary>
    /// Localiza automáticamente los componentes cuando no han sido
    /// asignados manualmente desde el Inspector.
    /// </summary>
    private void FindRequiredComponents()
    {
        if (restaurantTable == null)
        {
            restaurantTable =
                GetComponent<RestaurantTable>();
        }

        if (tableRenderer == null)
        {
            tableRenderer =
                GetComponent<Renderer>();
        }
    }

    /// <summary>
    /// Crea el bloque de propiedades si todavía no existe.
    ///
    /// Esta comprobación es necesaria porque los campos no serializados
    /// pueden perderse durante una recompilación en el editor.
    /// </summary>
    private void EnsurePropertyBlockExists()
    {
        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }
    }

    /// <summary>
    /// Recibe los cambios de estado enviados por RestaurantTable.
    /// </summary>
    private void HandleStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        UpdateVisualState(newState);
    }

    /// <summary>
    /// Selecciona y aplica el color correspondiente al estado actual
    /// de la mesa.
    /// </summary>
    private void UpdateVisualState(TableState state)
    {
        if (tableRenderer == null)
        {
            Debug.LogError(
                "No se puede actualizar la mesa porque falta Renderer.",
                this
            );

            return;
        }

        EnsurePropertyBlockExists();

        Color targetColor = state switch
        {
            TableState.Free =>
                freeColor,

            TableState.WaitingForWaiter =>
                waitingForWaiterColor,

            TableState.TakingOrder =>
                takingOrderColor,

            TableState.WaitingForFood =>
                waitingForFoodColor,

            TableState.Eating =>
                eatingColor,

            TableState.WaitingForBill =>
                waitingForBillColor,

            TableState.Paying =>
                payingColor,

            TableState.Dirty =>
                dirtyColor,

            _ =>
                Color.white
        };

        // Recuperamos primero las propiedades ya aplicadas al Renderer
        // para no sobrescribir otros valores visuales.
        tableRenderer.GetPropertyBlock(
            propertyBlock
        );

        Material material =
            tableRenderer.sharedMaterial;

        // URP utiliza habitualmente _BaseColor.
        // Otros shaders pueden utilizar la propiedad clásica _Color.
        if (material != null &&
            material.HasProperty(BaseColorProperty))
        {
            propertyBlock.SetColor(
                BaseColorProperty,
                targetColor
            );
        }
        else
        {
            propertyBlock.SetColor(
                ColorProperty,
                targetColor
            );
        }

        tableRenderer.SetPropertyBlock(
            propertyBlock
        );
    }
}