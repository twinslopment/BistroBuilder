using UnityEngine;


/// <summary>
/// Convierte el movimiento real de RestaurantSeat en una reserva anticipada
/// del espacio ocupado por silla y cliente durante sentarse o levantarse.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantSeat))]
public sealed class BistroBuilderSeatCirculationEnvelope :
    BistroBuilderDynamicCirculationEnvelope
{
    private RestaurantSeat seat;

    protected override void Awake()
    {
        seat = GetComponent<RestaurantSeat>();
        base.Awake();
        ConfigureFromSeat();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (seat == null) seat = GetComponent<RestaurantSeat>();
        if (seat != null) seat.OperationalStateChanged += HandleState;
    }

    protected override void OnDisable()
    {
        if (seat != null) seat.OperationalStateChanged -= HandleState;
        base.OnDisable();
    }

    private void HandleState(
        RestaurantSeat changedSeat,
        RestaurantSeatOperationalState state)
    {
        if (changedSeat == null || changedSeat.UseProfile == null) return;
        float duration = state switch
        {
            RestaurantSeatOperationalState.PullingOut =>
                changedSeat.UseProfile.PullOutDuration,
            RestaurantSeatOperationalState.CustomerEntering =>
                changedSeat.UseProfile.OccupiedTransitionDuration,
            RestaurantSeatOperationalState.CustomerLeaving =>
                changedSeat.UseProfile.OccupiedTransitionDuration,
            RestaurantSeatOperationalState.Returning =>
                changedSeat.UseProfile.ReturnDuration,
            _ => 0f
        };

        if (duration > 0f)
            BeginWindow("seat:" + changedSeat.GetInstanceID(), duration + 0.12f);
    }

    private void ConfigureFromSeat()
    {
        if (seat == null || seat.UseProfile == null) return;
        float travel = Mathf.Max(
            seat.UseProfile.PullOutDistance,
            seat.UseProfile.OccupiedPullOutDistance);
        float width = Mathf.Max(
            0.55f,
            seat.UseProfile.CustomerApproachRadius * 2f);
        Vector3 center = Vector3.back * (travel * 0.5f);
        ConfigureEnvelope(
            center,
            new Vector2(width, Mathf.Max(0.65f, travel + 0.55f)),
            BistroBuilderNavigationAgentMask.All,
            BistroBuilderDynamicSpaceKind.SeatMovement);
    }
}
