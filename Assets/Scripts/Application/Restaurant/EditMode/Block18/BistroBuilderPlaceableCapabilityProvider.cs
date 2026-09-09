using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Placeable Capability Provider")]
public sealed class BistroBuilderPlaceableCapabilityProvider : MonoBehaviour, IBistroBuilderEditCapabilityProvider
{
    [SerializeField] private RestaurantPlaceableRegistry registry;

    private void Awake()
    {
        if (registry == null) registry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
    }

    public bool TryGetCapabilities(BistroBuilderEditId entityId, out BistroBuilderEditCapability capabilities)
    {
        capabilities = BistroBuilderEditCapability.None;
        if (!entityId.IsValid) return false;
        if (registry == null) registry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (registry == null || !registry.TryGetByInstanceId(entityId.Value, out RestaurantPlaceableObject placeable) || placeable == null)
            return false;

        capabilities = BistroBuilderEditCapability.Remove |
            BistroBuilderEditCapability.Duplicate |
            BistroBuilderEditCapability.Snap;

        if (placeable.TryGetComponent(out RestaurantEditableObject editable))
        {
            if (editable.CanMove) capabilities |= BistroBuilderEditCapability.Move;
            if (editable.CanRotate) capabilities |= BistroBuilderEditCapability.Rotate;
        }
        return capabilities != BistroBuilderEditCapability.None;
    }

    public bool TryResolve(BistroBuilderEditId entityId, out RestaurantPlaceableObject placeable)
    {
        placeable = null;
        if (!entityId.IsValid) return false;
        if (registry == null) registry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        return registry != null && registry.TryGetByInstanceId(entityId.Value, out placeable) && placeable != null;
    }
}
