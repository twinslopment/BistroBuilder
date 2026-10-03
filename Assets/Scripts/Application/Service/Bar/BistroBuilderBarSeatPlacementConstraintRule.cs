using UnityEngine;

/// <summary>Checks the proposed seat/bar relationship without altering either scene pose.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderBarSeatPlacementConstraintRule : MonoBehaviour,
    IRestaurantPlacementConstraintRule
{
    public int Priority => 15;
    public bool IsConstraintEnabled => isActiveAndEnabled;

    public RestaurantPlacementConstraintEvaluation Evaluate(RestaurantPlacementConstraintContext context)
    {
        if (context.Member == null) return RestaurantPlacementConstraintEvaluation.Valid();
        var seat = context.Member.GetComponent<BistroBuilderBarSeatBinding>();
        if (seat != null && !seat.TryResolveSpotAtPose(context.CandidateRootPosition,
                context.CandidateRootRotation, out _, out string error))
            return RestaurantPlacementConstraintEvaluation.Invalid("bar.seat.incompatible_place",
                "Acerca el taburete a una plaza libre de barra y oriéntalo hacia el mostrador.", error, seat, true);
        var bar = context.Member.GetComponent<BistroBuilderBarPlaceableBinding>();
        if (bar != null && (Vector3.Distance(context.Member.transform.position, context.CandidateRootPosition) > 0.0001f ||
            Quaternion.Angle(context.Member.transform.rotation, context.CandidateRootRotation) > 0.01f))
            foreach (var spot in bar.Spots)
                if (spot != null && spot.AttachedSeat != null)
                    return RestaurantPlacementConstraintEvaluation.Invalid("bar.seat.attached_dependency",
                        "Retira primero los taburetes asociados antes de mover esta barra.",
                        "Moving the bar would orphan its confirmed native stool association.", spot, true);
        return RestaurantPlacementConstraintEvaluation.Valid();
    }
}
