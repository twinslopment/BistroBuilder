using UnityEngine;

/// <summary>
/// Lado visual de aproximación a un portal. La elección del lado pertenece
/// al flujo que ya resolvió navegación/espacio; BB18 sólo expone sus anclajes.
/// </summary>
public enum BistroBuilderPortalPresentationSide
{
    SideA = 0,
    SideB = 1
}

/// <summary>
/// Describe los anclajes de presentación de una puerta sin asumir autoridad
/// de navegación, gameplay ni BBSIS.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderPortalAnimationDescriptor : MonoBehaviour
{
    [SerializeField] private BistroBuilderNavigableDoor door;
    [SerializeField] private Transform sideAApproach;
    [SerializeField] private Transform sideBApproach;
    [SerializeField] private Transform sideAThrough;
    [SerializeField] private Transform sideBThrough;
    [SerializeField] private Transform sideAHandleTarget;
    [SerializeField] private Transform sideBHandleTarget;
    [SerializeField] private Transform lookTarget;

    public BistroBuilderNavigableDoor Door => door;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (door == null)
        {
            error = name + ": falta BistroBuilderNavigableDoor.";
            return false;
        }
        if (sideAApproach == null || sideBApproach == null ||
            sideAThrough == null || sideBThrough == null)
        {
            error = name + ": faltan frames Approach/Through de ambos lados.";
            return false;
        }
        if (sideAHandleTarget == null || sideBHandleTarget == null)
        {
            error = name + ": faltan targets de manilla de ambos lados.";
            return false;
        }
        return true;
    }

    public bool TryResolveSide(
        BistroBuilderPortalPresentationSide side,
        out Transform approach,
        out Transform through,
        out Transform handleTarget,
        out Transform resolvedLookTarget)
    {
        approach = side == BistroBuilderPortalPresentationSide.SideA
            ? sideAApproach
            : sideBApproach;
        through = side == BistroBuilderPortalPresentationSide.SideA
            ? sideBThrough
            : sideAThrough;
        handleTarget = side == BistroBuilderPortalPresentationSide.SideA
            ? sideAHandleTarget
            : sideBHandleTarget;
        resolvedLookTarget = lookTarget != null ? lookTarget : handleTarget;
        return door != null && approach != null && through != null &&
               handleTarget != null;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderNavigableDoor configuredDoor,
        Transform configuredSideAApproach,
        Transform configuredSideBApproach,
        Transform configuredSideAThrough,
        Transform configuredSideBThrough,
        Transform configuredSideAHandle,
        Transform configuredSideBHandle,
        Transform configuredLookTarget)
    {
        door = configuredDoor;
        sideAApproach = configuredSideAApproach;
        sideBApproach = configuredSideBApproach;
        sideAThrough = configuredSideAThrough;
        sideBThrough = configuredSideBThrough;
        sideAHandleTarget = configuredSideAHandle;
        sideBHandleTarget = configuredSideBHandle;
        lookTarget = configuredLookTarget;
    }
#endif
}
