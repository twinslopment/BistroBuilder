using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Placeable Manipulator Bridge")]
public sealed class BistroBuilderPlaceableManipulatorBridge : MonoBehaviour
{
    [SerializeField] private BistroBuilderPlaceableCapabilityProvider capabilityProvider;
    [SerializeField] private RestaurantEditInteractionController interactionController;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool TrySelect(BistroBuilderEditId entityId, out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (capabilityProvider == null || interactionController == null)
        {
            error = "El manipulador de colocables no está disponible.";
            return false;
        }
        if (!capabilityProvider.TryResolve(entityId, out RestaurantPlaceableObject placeable))
        {
            error = "No existe un colocable con esa identidad estable.";
            return false;
        }
        if (!interactionController.TrySelectPlaceable(placeable))
        {
            error = "El colocable no puede seleccionarse en el estado actual.";
            return false;
        }
        return true;
    }
    public bool TryBeginMove(BistroBuilderEditId entityId, out string error)
    {
        if (!TrySelect(entityId, out error)) return false;
        if (!capabilityProvider.TryGetCapabilities(entityId, out BistroBuilderEditCapability capabilities) ||
            (capabilities & BistroBuilderEditCapability.Move) == 0)
        {
            error = "El colocable no admite movimiento.";
            return false;
        }
        if (!interactionController.TryBeginMoveSelected())
        {
            error = "No se pudo iniciar la transacción de movimiento.";
            return false;
        }
        return true;
    }

    private void CacheDependencies()
    {
        if (capabilityProvider == null)
            capabilityProvider = FindFirstObjectByType<BistroBuilderPlaceableCapabilityProvider>();
        if (interactionController == null)
            interactionController = FindFirstObjectByType<RestaurantEditInteractionController>();
    }
}
