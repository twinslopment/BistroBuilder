using UnityEngine;

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
    private Color takingOrderColor = new(1f, 0.5f, 0f);

    [SerializeField]
    private Color waitingForFoodColor = Color.red;

    [SerializeField]
    private Color eatingColor = Color.cyan;

    [SerializeField]
    private Color waitingForBillColor = Color.magenta;

    [SerializeField]
    private Color dirtyColor = Color.gray;

    private static readonly int BaseColorProperty =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorProperty =
        Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        if (restaurantTable == null)
            restaurantTable = GetComponent<RestaurantTable>();

        if (tableRenderer == null)
            tableRenderer = GetComponent<Renderer>();

        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (restaurantTable == null)
        {
            Debug.LogError(
                "TableStateView necesita una referencia a RestaurantTable.",
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

        restaurantTable.StateChanged += HandleStateChanged;
        UpdateVisualState(restaurantTable.CurrentState);
    }

    private void OnDisable()
    {
        if (restaurantTable != null)
            restaurantTable.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        UpdateVisualState(newState);
    }

    private void UpdateVisualState(TableState state)
    {
        Color targetColor = state switch
        {
            TableState.Free => freeColor,
            TableState.WaitingForWaiter => waitingForWaiterColor,
            TableState.TakingOrder => takingOrderColor,
            TableState.WaitingForFood => waitingForFoodColor,
            TableState.Eating => eatingColor,
            TableState.WaitingForBill => waitingForBillColor,
            TableState.Dirty => dirtyColor,
            _ => Color.white
        };

        tableRenderer.GetPropertyBlock(propertyBlock);

        Material material = tableRenderer.sharedMaterial;

        if (material != null && material.HasProperty(BaseColorProperty))
        {
            propertyBlock.SetColor(BaseColorProperty, targetColor);
        }
        else
        {
            propertyBlock.SetColor(ColorProperty, targetColor);
        }

        tableRenderer.SetPropertyBlock(propertyBlock);
    }
}