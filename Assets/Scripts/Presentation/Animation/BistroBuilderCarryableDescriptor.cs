using UnityEngine;

public enum BistroBuilderCarryMode
{
    GenericOneHand = 0,
    Plate = 1,
    Tray = 2,
    BoxTwoHand = 3
}

/// <summary>
/// Describe cómo debe presentarse un objeto durante pickup/carry/place.
/// No contiene ownership ni inventario.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCarryableDescriptor : MonoBehaviour
{
    [SerializeField] private BistroBuilderCarryMode carryMode = BistroBuilderCarryMode.GenericOneHand;
    [SerializeField] private BistroBuilderCarrySocketKind preferredOneHand = BistroBuilderCarrySocketKind.RightHand;
    [SerializeField] private Transform rightGrip;
    [SerializeField] private Transform leftGrip;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Vector3 carriedLocalPosition;
    [SerializeField] private Vector3 carriedLocalEuler;

    public BistroBuilderCarryMode CarryMode => carryMode;
    public Transform RightGrip => rightGrip != null ? rightGrip : transform;
    public Transform LeftGrip => leftGrip != null ? leftGrip : transform;
    public Transform LookTarget => lookTarget != null ? lookTarget : transform;
    public Vector3 CarriedLocalPosition => carriedLocalPosition;
    public Quaternion CarriedLocalRotation => Quaternion.Euler(carriedLocalEuler);
    public BistroBuilderCarrySocketKind ResolveSocketKind()
    {
        return carryMode switch
        {
            BistroBuilderCarryMode.Plate => BistroBuilderCarrySocketKind.RightHand,
            BistroBuilderCarryMode.Tray => BistroBuilderCarrySocketKind.Tray,
            BistroBuilderCarryMode.BoxTwoHand => BistroBuilderCarrySocketKind.TwoHandCenter,
            _ => preferredOneHand
        };
    }

    public bool UsesRightHandIK =>
        carryMode == BistroBuilderCarryMode.Plate ||
        carryMode == BistroBuilderCarryMode.Tray ||
        carryMode == BistroBuilderCarryMode.BoxTwoHand;

    public bool UsesLeftHandIK =>
        carryMode == BistroBuilderCarryMode.Tray ||
        carryMode == BistroBuilderCarryMode.BoxTwoHand;

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderCarryMode mode,
        Transform configuredRightGrip,
        Transform configuredLeftGrip,
        Vector3 localPosition,
        Vector3 localEuler)
    {
        carryMode = mode;
        rightGrip = configuredRightGrip;
        leftGrip = configuredLeftGrip;
        carriedLocalPosition = localPosition;
        carriedLocalEuler = localEuler;
    }
#endif
}
