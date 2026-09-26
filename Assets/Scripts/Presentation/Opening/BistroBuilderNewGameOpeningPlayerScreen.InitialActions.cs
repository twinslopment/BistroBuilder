using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    // The construction workspace already owns the canonical Initial Design
    // actions. Keeping a second overlay here caused duplicated controls.
    private Canvas initialActionsCanvas;
    private TMP_Text initialStatus;
    private Button initialSave, initialContinue;

    private void RefreshInitialActions()
    {
        if (initialActionsCanvas != null)
            initialActionsCanvas.gameObject.SetActive(false);
    }
}
