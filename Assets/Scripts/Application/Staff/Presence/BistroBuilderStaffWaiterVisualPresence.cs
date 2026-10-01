using UnityEngine;

/// <summary>
/// Hides an unbound operational slot without deactivating it: 4D, routing and
/// service.runtime still require the same unique WaiterId in the active scene.
/// Caches the original visibility BEFORE any presentation filtering, and
/// transfers that baseline to clones of a currently hidden archetype.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderStaffWaiterVisualPresence : MonoBehaviour
{
    private Renderer[] renderers;
    private bool[] initialStates;
    private bool visible = true;

    private void EnsureBaseline()
    {
        if (renderers != null) return;
        renderers = GetComponentsInChildren<Renderer>(true);
        initialStates = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            initialStates[i] = renderers[i] != null && renderers[i].enabled;
    }

    /// <summary>
    /// Unity clones the current enabled state of renderers. If the template
    /// is off duty its renderers are hidden, but a new employee must retain
    /// the ORIGINAL art visibility instead of inheriting permanent invisibility.
    /// </summary>
    public bool TryAdoptBaseline(BistroBuilderStaffWaiterVisualPresence template)
    {
        if (template == null) return false;
        template.EnsureBaseline();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length != template.initialStates.Length) return false;
        initialStates = (bool[])template.initialStates.Clone();
        visible = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;
        return true;
    }

    public void SetPresent(bool present)
    {
        EnsureBaseline();
        if (visible == present) return;
        visible = present;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = present && initialStates[i];
    }
}
