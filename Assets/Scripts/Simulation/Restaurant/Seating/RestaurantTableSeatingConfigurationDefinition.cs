using UnityEngine;

/// <summary>
/// Configuración inmutable de plazas de un tipo concreto de mesa.
///
/// Una mesa de cuatro no se convierte en una de cinco o seis al
/// añadir sillas. La capacidad y la distribución forman parte de la
/// definición del artículo.
/// </summary>
[CreateAssetMenu(
    fileName = "TableSeatingConfiguration",
    menuName = "Bistro Builder/Restaurant/Table Seating Configuration"
)]
public sealed class RestaurantTableSeatingConfigurationDefinition :
    ScriptableObject
{
    [Header("Identidad")]

    [SerializeField]
    private string configurationId =
        "table_2_rectangular";

    [SerializeField]
    private string displayName =
        "Mesa rectangular de 2 clientes";

    [Header("Capacidad fija")]

    [SerializeField]
    [Min(1)]
    private int maximumCustomers = 2;

    [SerializeField]
    private RestaurantTableSeatingShape shape =
        RestaurantTableSeatingShape.Rectangular;

    [Header("Dimensiones")]

    [Tooltip(
        "Usa la huella real del prefab para anchura y profundidad."
    )]
    [SerializeField]
    private bool usePlacementFootprintDimensions = true;

    [SerializeField]
    [Min(0.1f)]
    private float manualWidth = 1f;

    [SerializeField]
    [Min(0.1f)]
    private float manualDepth = 1f;

    [Tooltip(
        "Diámetro funcional para mesas redondas. Puede sustituirse " +
        "por la dimensión mayor de la huella."
    )]
    [SerializeField]
    [Min(0.1f)]
    private float manualRoundDiameter = 1f;

    [Header("Distribución rectangular")]

    [SerializeField]
    [Min(0)]
    private int positiveZSeats = 1;

    [SerializeField]
    [Min(0)]
    private int negativeZSeats = 1;

    [SerializeField]
    [Min(0)]
    private int positiveXSeats;

    [SerializeField]
    [Min(0)]
    private int negativeXSeats;

    [Tooltip(
        "Margen sin comensales en cada extremo de un lado."
    )]
    [SerializeField]
    [Min(0f)]
    private float sideEndInset = 0.10f;

    [Header("Separación por comensal")]

    [Tooltip(
        "Longitud o arco mínimo disponible por cliente."
    )]
    [SerializeField]
    [Min(0.1f)]
    private float minimumSpacePerCustomer = 0.55f;

    [Tooltip(
        "Separación entre el borde de la mesa y el frente de la silla."
    )]
    [SerializeField]
    [Min(0f)]
    private float parkedGapFromTableEdge = 0.10f;

    [Header("Mesas redondas")]

    [SerializeField]
    [Range(-180f, 180f)]
    private float firstRoundSeatAngleDegrees;

    public string ConfigurationId =>
        string.IsNullOrWhiteSpace(configurationId)
            ? string.Empty
            : configurationId.Trim().ToLowerInvariant();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName.Trim();

    public int MaximumCustomers =>
        Mathf.Max(1, maximumCustomers);

    public RestaurantTableSeatingShape Shape =>
        shape;

    public bool UsePlacementFootprintDimensions =>
        usePlacementFootprintDimensions;

    public float ManualWidth =>
        Mathf.Max(0.1f, manualWidth);

    public float ManualDepth =>
        Mathf.Max(0.1f, manualDepth);

    public float ManualRoundDiameter =>
        Mathf.Max(0.1f, manualRoundDiameter);

    public int PositiveZSeats =>
        Mathf.Max(0, positiveZSeats);

    public int NegativeZSeats =>
        Mathf.Max(0, negativeZSeats);

    public int PositiveXSeats =>
        Mathf.Max(0, positiveXSeats);

    public int NegativeXSeats =>
        Mathf.Max(0, negativeXSeats);

    public float SideEndInset =>
        Mathf.Max(0f, sideEndInset);

    public float MinimumSpacePerCustomer =>
        Mathf.Max(0.1f, minimumSpacePerCustomer);

    public float ParkedGapFromTableEdge =>
        Mathf.Max(0f, parkedGapFromTableEdge);

    public float FirstRoundSeatAngleDegrees =>
        firstRoundSeatAngleDegrees;

    public int RectangularSeatCount =>
        PositiveZSeats +
        NegativeZSeats +
        PositiveXSeats +
        NegativeXSeats;

    public bool ValidateConfiguration(
        float resolvedWidth,
        float resolvedDepth,
        out string error
    )
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ConfigurationId))
        {
            error = name + " necesita un ConfigurationId.";
            return false;
        }

        float width = Mathf.Max(0.1f, resolvedWidth);
        float depth = Mathf.Max(0.1f, resolvedDepth);

        if (shape == RestaurantTableSeatingShape.Round)
        {
            float diameter =
                usePlacementFootprintDimensions
                    ? Mathf.Max(width, depth)
                    : ManualRoundDiameter;

            float circumference =
                Mathf.PI * diameter;

            float spacePerCustomer =
                circumference /
                MaximumCustomers;

            if (spacePerCustomer + 0.0001f <
                MinimumSpacePerCustomer)
            {
                error =
                    DisplayName +
                    " solo ofrece " +
                    spacePerCustomer.ToString("0.###") +
                    " m de arco por cliente; necesita " +
                    MinimumSpacePerCustomer.ToString("0.###") +
                    " m.";

                return false;
            }

            return true;
        }

        if (shape != RestaurantTableSeatingShape.Rectangular)
        {
            error =
                DisplayName +
                " utiliza una forma todavía no implementada.";

            return false;
        }

        if (RectangularSeatCount != MaximumCustomers)
        {
            error =
                DisplayName +
                " declara " +
                MaximumCustomers +
                " clientes, pero distribuye " +
                RectangularSeatCount +
                " plazas.";

            return false;
        }

        if (!ValidateSide(
                width,
                PositiveZSeats,
                "+Z",
                out error
            ) ||
            !ValidateSide(
                width,
                NegativeZSeats,
                "-Z",
                out error
            ) ||
            !ValidateSide(
                depth,
                PositiveXSeats,
                "+X",
                out error
            ) ||
            !ValidateSide(
                depth,
                NegativeXSeats,
                "-X",
                out error
            ))
        {
            return false;
        }

        return true;
    }

    private bool ValidateSide(
        float sideLength,
        int seatCount,
        string sideName,
        out string error
    )
    {
        error = string.Empty;

        if (seatCount <= 0)
        {
            return true;
        }

        float usableLength =
            Mathf.Max(
                0f,
                sideLength -
                SideEndInset * 2f
            );

        float spacePerCustomer =
            usableLength /
            seatCount;

        if (spacePerCustomer + 0.0001f >=
            MinimumSpacePerCustomer)
        {
            return true;
        }

        error =
            DisplayName +
            " solo ofrece " +
            spacePerCustomer.ToString("0.###") +
            " m por cliente en el lado " +
            sideName +
            "; necesita " +
            MinimumSpacePerCustomer.ToString("0.###") +
            " m.";

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        configurationId =
            string.IsNullOrWhiteSpace(configurationId)
                ? "table_seating_configuration"
                : configurationId.Trim().ToLowerInvariant();

        displayName =
            string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName.Trim();

        maximumCustomers = Mathf.Max(1, maximumCustomers);
        manualWidth = Mathf.Max(0.1f, manualWidth);
        manualDepth = Mathf.Max(0.1f, manualDepth);
        manualRoundDiameter = Mathf.Max(0.1f, manualRoundDiameter);
        positiveZSeats = Mathf.Max(0, positiveZSeats);
        negativeZSeats = Mathf.Max(0, negativeZSeats);
        positiveXSeats = Mathf.Max(0, positiveXSeats);
        negativeXSeats = Mathf.Max(0, negativeXSeats);
        sideEndInset = Mathf.Max(0f, sideEndInset);
        minimumSpacePerCustomer =
            Mathf.Max(0.1f, minimumSpacePerCustomer);
        parkedGapFromTableEdge =
            Mathf.Max(0f, parkedGapFromTableEdge);
    }
#endif
}
