using UnityEngine;

/// <summary>
/// Hides an unbound operational slot without deactivating it: 4D, routing and
/// service.runtime still require the same unique WaiterId in the active scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderStaffWaiterVisualPresence : MonoBehaviour
{
    private Renderer[] renderers;
    private bool[] initialStates;
    private bool visible = true;

    public void SetPresent(bool present)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            initialStates = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                initialStates[i] = renderers[i] != null && renderers[i].enabled;
        }

        if (visible == present) return;
        visible = present;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = present && initialStates[i];
    }
}
