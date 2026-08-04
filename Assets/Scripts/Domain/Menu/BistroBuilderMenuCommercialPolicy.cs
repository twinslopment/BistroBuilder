using UnityEngine;

/// <summary>
/// Política comercial canónica de la carta.
///
/// No contiene estado de partida. Define límites de autoría y edición que
/// deben aplicarse de la misma forma desde UI, herramientas y APIs runtime.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderMenuCommercialPolicy",
    menuName = "Bistro Builder/Menu/Commercial Policy",
    order = 92
)]
public sealed class BistroBuilderMenuCommercialPolicy : ScriptableObject
{
    public const int DefaultMinimumPriceCents = 0;
    public const int DefaultMaximumMenuItems = 256;
    public const int DefaultMaximumSignatureDishes = 3;
    public const int DefaultSignatureSelectionWeightBasisPoints = 15000;
    public const int BasisPointsPerUnit = 10000;

    [Header("Precios")]

    [SerializeField]
    [Min(0)]
    private int minimumPriceCents = DefaultMinimumPriceCents;

    [SerializeField]
    [Min(0)]
    private int maximumPriceCents =
        BistroBuilderDishDefinition.MaximumPriceCents;

    [Header("Capacidad de carta")]

    [SerializeField]
    [Min(1)]
    private int maximumMenuItems = DefaultMaximumMenuItems;

    [SerializeField]
    [Min(1)]
    private int maximumSignatureDishes =
        DefaultMaximumSignatureDishes;

    [Header("Integridad de platos firma")]

    [SerializeField]
    private bool requireSignatureDishEnabled = true;

    [SerializeField]
    private bool requireSignatureDishUnlocked = true;

    [SerializeField]
    private bool requireSignatureDishServiceAvailability = true;

    [Header("Selección de clientes")]

    [Tooltip(
        "Peso de elección de un plato firma expresado en puntos básicos. " +
        "10000 equivale a x1 y 15000 a x1,5."
    )]
    [SerializeField]
    [Min(BasisPointsPerUnit)]
    private int signatureSelectionWeightBasisPoints =
        DefaultSignatureSelectionWeightBasisPoints;

    public int MinimumPriceCents => minimumPriceCents;

    public int MaximumPriceCents => maximumPriceCents;

    public int MaximumMenuItems => maximumMenuItems;

    public int MaximumSignatureDishes => maximumSignatureDishes;

    public bool RequireSignatureDishEnabled =>
        requireSignatureDishEnabled;

    public bool RequireSignatureDishUnlocked =>
        requireSignatureDishUnlocked;

    public bool RequireSignatureDishServiceAvailability =>
        requireSignatureDishServiceAvailability;

    public int SignatureSelectionWeightBasisPoints =>
        signatureSelectionWeightBasisPoints;

    public bool TryValidate(out string error)
    {
        if (minimumPriceCents < 0)
        {
            error = "El precio mínimo no puede ser negativo.";
            return false;
        }

        if (maximumPriceCents < minimumPriceCents ||
            maximumPriceCents >
                BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "El rango de precios de la política no es válido.";
            return false;
        }

        if (maximumMenuItems < 1)
        {
            error = "La carta debe admitir al menos un plato.";
            return false;
        }

        if (maximumSignatureDishes < 1 ||
            maximumSignatureDishes > maximumMenuItems)
        {
            error = "El límite de platos firma no es válido.";
            return false;
        }

        if (signatureSelectionWeightBasisPoints < BasisPointsPerUnit)
        {
            error = "El peso de plato firma no puede ser inferior a x1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumPriceCents = Mathf.Max(0, minimumPriceCents);
        maximumPriceCents = Mathf.Clamp(
            maximumPriceCents,
            minimumPriceCents,
            BistroBuilderDishDefinition.MaximumPriceCents
        );
        maximumMenuItems = Mathf.Max(1, maximumMenuItems);
        maximumSignatureDishes = Mathf.Clamp(
            maximumSignatureDishes,
            1,
            maximumMenuItems
        );
        signatureSelectionWeightBasisPoints = Mathf.Max(
            BasisPointsPerUnit,
            signatureSelectionWeightBasisPoints
        );
    }
#endif
}
