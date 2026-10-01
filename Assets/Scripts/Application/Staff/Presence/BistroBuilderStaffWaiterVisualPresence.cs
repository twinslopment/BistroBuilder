using UnityEngine;

/// <summary>
/// Reversible, presentation-only visibility of a real operational Waiter slot.
/// The GameObject and Waiter remain active for session identity/Save. Off-duty
/// actors cannot occupy invisible space in Navigation or physics.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderStaffWaiterVisualPresence : MonoBehaviour
{
    private Renderer[] renderers;
    private bool[] initialRenderers;
    private Collider[] colliders;
    private bool[] initialColliders;
    private WaiterMovementView movement;
    private bool initialMovement;
    private bool initialized;
    private bool visible = true;

    private void EnsureBaseline()
    {
        if (initialized) return;
        initialized = true;

        renderers = GetComponentsInChildren<Renderer>(true);
        initialRenderers = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            initialRenderers[i] = renderers[i] != null && renderers[i].enabled;

        colliders = GetComponentsInChildren<Collider>(true);
        initialColliders = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            initialColliders[i] = colliders[i] != null && colliders[i].enabled;

        movement = GetComponent<WaiterMovementView>();
        initialMovement = movement != null && movement.enabled;
    }

    /// <summary>
    /// Cloning a currently off-duty template also clones disabled renderers,
    /// colliders and movement. Reuse the original archetype's baseline instead.
    /// The clone must have exactly the same operational component layout.
    /// </summary>
    public bool TryAdoptBaseline(BistroBuilderStaffWaiterVisualPresence template)
    {
        if (template == null) return false;
        template.EnsureBaseline();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        movement = GetComponent<WaiterMovementView>();
        if (renderers.Length != template.initialRenderers.Length ||
            colliders.Length != template.initialColliders.Length ||
            (movement != null) != (template.movement != null))
            return false;

        initialRenderers = (bool[])template.initialRenderers.Clone();
        initialColliders = (bool[])template.initialColliders.Clone();
        initialMovement = template.initialMovement;
        initialized = true;
        visible = true;
        SetPresent(false);
        return true;
    }

    public void SetPresent(bool present)
    {
        EnsureBaseline();
        if (visible == present) return;
        visible = present;

        // OnDisable explicitly releases Navigation ownership before colliders
        // are hidden, avoiding invisible obstacles outside the employee shift.
        if (!present && movement != null) movement.enabled = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = present && initialRenderers[i];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = present && initialColliders[i];
        if (present && movement != null) movement.enabled = initialMovement;
    }
}
