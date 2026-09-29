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

    [Header("Acentos operativos")]
    [SerializeField, Range(0f, 0.35f)]
    private float stateTintStrength = 0.12f;

    [SerializeField]
    private Color waitingForWaiterColor =
        new Color(0.82f, 0.69f, 0.30f, 1f);

    [SerializeField]
    private Color takingOrderColor =
        new Color(0.84f, 0.55f, 0.28f, 1f);

    [SerializeField]
    private Color waitingForFoodColor =
        new Color(0.72f, 0.36f, 0.31f, 1f);

    [SerializeField]
    private Color eatingColor =
        new Color(0.32f, 0.58f, 0.52f, 1f);

    [SerializeField]
    private Color waitingForBillColor =
        new Color(0.50f, 0.48f, 0.68f, 1f);

    [SerializeField]
    private Color payingColor =
        new Color(0.55f, 0.42f, 0.66f, 1f);

    [SerializeField]
    private Color dirtyColor =
        new Color(0.42f, 0.42f, 0.40f, 1f);

    // Identificadores de las propiedades de color utilizadas
    // por los shaders habituales de Unity.
    private static readonly int BaseColorProperty =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorProperty =
        Shader.PropertyToID("_Color");

    // Permite modificar el color del Renderer sin crear
    // una copia independiente del material para cada mesa.
    private MaterialPropertyBlock propertyBlock;
    private Color originalBaseColor = Color.white;
    private bool originalColorCaptured;

    private void Awake()
    {
        FindRequiredComponents();
        EnsurePropertyBlockExists();
        CaptureOriginalColor();
    }

    private void OnEnable()
    {
        // Unity puede ejecutar OnEnable después de recompilar scripts
        // en el editor sin conservar los campos no serializados.
        // Por eso garantizamos aquí que propertyBlock vuelva a existir.
        FindRequiredComponents();
        EnsurePropertyBlockExists();
        CaptureOriginalColor();

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

        RestoreOriginalColor();
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

    private void CaptureOriginalColor()
    {
        if (originalColorCaptured ||
            tableRenderer == null)
        {
            return;
        }

        Material material =
            tableRenderer.sharedMaterial;

        if (material == null)
        {
            originalBaseColor =
                Color.white;

            originalColorCaptured =
                true;

            return;
        }

        if (material.HasProperty(
                BaseColorProperty))
        {
            originalBaseColor =
                material.GetColor(
                    BaseColorProperty);
        }
        else if (material.HasProperty(
                     ColorProperty))
        {
            originalBaseColor =
                material.GetColor(
                    ColorProperty);
        }
        else
        {
            originalBaseColor =
                Color.white;
        }

        originalColorCaptured =
            true;
    }

    private void RestoreOriginalColor()
    {
        if (!originalColorCaptured ||
            tableRenderer == null)
        {
            return;
        }

        EnsurePropertyBlockExists();

        tableRenderer.GetPropertyBlock(
            propertyBlock);

        Material material =
            tableRenderer.sharedMaterial;

        if (material != null &&
            material.HasProperty(
                BaseColorProperty))
        {
            propertyBlock.SetColor(
                BaseColorProperty,
                originalBaseColor);
        }
        else
        {
            propertyBlock.SetColor(
                ColorProperty,
                originalBaseColor);
        }

        tableRenderer.SetPropertyBlock(
            propertyBlock);
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

        CaptureOriginalColor();

        Color accentColor = state switch
        {
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
                originalBaseColor
        };

        float blend =
            state == TableState.Free
                ? 0f
                : Mathf.Clamp01(
                    stateTintStrength);

        Color targetColor =
            Color.Lerp(
                originalBaseColor,
                accentColor,
                blend);

        targetColor.a =
            originalBaseColor.a;

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