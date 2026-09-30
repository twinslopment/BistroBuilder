using UnityEngine;

/// <summary>
/// Legacy initial-design ribbon placeholder.
///
/// The definitive edit-mode chrome owns Save / Validate and Continue actions.
/// Keeping a second overlay here caused duplicate controls and visual authority.
/// The field remains only because the opening screen's OnDisable safely hides
/// any legacy canvas that may survive scene/domain reloads.
/// </summary>
public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    private Canvas initialActionsCanvas;

    // Retained as a no-op compatibility hook for any older caller.
    private void RefreshInitialActions()
    {
        if (initialActionsCanvas != null)
            initialActionsCanvas.gameObject.SetActive(false);
    }
}
