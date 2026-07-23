using UnityEngine;

/// <summary>
/// Perfil reutilizable de uso de una silla.
///
/// Las medidas están en metros mundiales y permiten perfiles futuros
/// para sillones, taburetes, bancos o asientos fijos.
/// </summary>
[CreateAssetMenu(
    fileName = "SeatUseProfile",
    menuName = "Bistro Builder/Restaurant/Seat Use Profile"
)]
public sealed class RestaurantSeatUseProfileDefinition :
    ScriptableObject
{
    [Header("Identidad")]

    [SerializeField]
    private string profileId =
        "standard_dining_chair";

    [SerializeField]
    private string displayName =
        "Silla de comedor estándar";

    [Header("Asiento")]

    [SerializeField]
    [Min(0.1f)]
    private float seatHeight = 0.46f;

    [SerializeField]
    [Min(0f)]
    private float pullOutDistance = 0.35f;

    [SerializeField]
    [Min(0f)]
    private float occupiedPullOutDistance = 0.12f;

    [SerializeField]
    [Min(0f)]
    private float customerApproachDistance = 0.35f;

    [SerializeField]
    [Min(0.05f)]
    private float customerApproachRadius = 0.25f;

    [Header("Tolerancias de colocación")]

    [SerializeField]
    [Min(0.01f)]
    private float slotPositionTolerance = 0.06f;

    [SerializeField]
    [Range(0f, 90f)]
    private float maximumFacingAngle = 10f;

    [SerializeField]
    [Min(0f)]
    private float maximumVerticalDifference = 0.12f;

    [Header("Movimiento")]

    [SerializeField]
    [Min(0.01f)]
    private float pullOutDuration = 0.45f;

    [SerializeField]
    [Min(0.01f)]
    private float occupiedTransitionDuration = 0.25f;

    [SerializeField]
    [Min(0.01f)]
    private float returnDuration = 0.40f;

    public string ProfileId =>
        string.IsNullOrWhiteSpace(profileId)
            ? string.Empty
            : profileId.Trim().ToLowerInvariant();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName.Trim();

    public float SeatHeight =>
        Mathf.Max(0.1f, seatHeight);

    public float PullOutDistance =>
        Mathf.Max(0f, pullOutDistance);

    public float OccupiedPullOutDistance =>
        Mathf.Clamp(
            occupiedPullOutDistance,
            0f,
            PullOutDistance
        );

    public float CustomerApproachDistance =>
        Mathf.Max(0f, customerApproachDistance);

    public float CustomerApproachRadius =>
        Mathf.Max(0.05f, customerApproachRadius);

    public float SlotPositionTolerance =>
        Mathf.Max(0.01f, slotPositionTolerance);

    public float MaximumFacingAngle =>
        Mathf.Clamp(maximumFacingAngle, 0f, 90f);

    public float MaximumVerticalDifference =>
        Mathf.Max(0f, maximumVerticalDifference);

    public float PullOutDuration =>
        Mathf.Max(0.01f, pullOutDuration);

    public float OccupiedTransitionDuration =>
        Mathf.Max(0.01f, occupiedTransitionDuration);

    public float ReturnDuration =>
        Mathf.Max(0.01f, returnDuration);

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ProfileId))
        {
            error = name + " necesita un ProfileId.";
            return false;
        }

        if (OccupiedPullOutDistance > PullOutDistance)
        {
            error =
                name +
                " tiene una posición ocupada mayor que la " +
                "distancia de extracción.";

            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        profileId =
            string.IsNullOrWhiteSpace(profileId)
                ? "seat_profile"
                : profileId.Trim().ToLowerInvariant();

        displayName =
            string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName.Trim();

        seatHeight = Mathf.Max(0.1f, seatHeight);
        pullOutDistance = Mathf.Max(0f, pullOutDistance);

        occupiedPullOutDistance =
            Mathf.Clamp(
                occupiedPullOutDistance,
                0f,
                pullOutDistance
            );

        customerApproachDistance =
            Mathf.Max(0f, customerApproachDistance);

        customerApproachRadius =
            Mathf.Max(0.05f, customerApproachRadius);

        slotPositionTolerance =
            Mathf.Max(0.01f, slotPositionTolerance);

        maximumVerticalDifference =
            Mathf.Max(0f, maximumVerticalDifference);

        pullOutDuration = Mathf.Max(0.01f, pullOutDuration);

        occupiedTransitionDuration =
            Mathf.Max(0.01f, occupiedTransitionDuration);

        returnDuration = Mathf.Max(0.01f, returnDuration);
    }
#endif
}
