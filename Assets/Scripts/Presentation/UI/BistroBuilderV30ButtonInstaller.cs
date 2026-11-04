using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Installs V30 once per uGUI Button, including buttons created at runtime on new screens.
/// This is an opt-in project-wide standard, not a change to any gameplay callback.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class BistroBuilderV30ButtonInstaller : MonoBehaviour
{
    private const float ScanIntervalSeconds = 0.6f;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;
        if (Object.FindFirstObjectByType<BistroBuilderV30ButtonInstaller>(FindObjectsInactive.Include) != null)
            return;
        var root = new GameObject("BB_V30_ButtonFeedback_Global");
        Object.DontDestroyOnLoad(root);
        root.AddComponent<BistroBuilderV30ButtonInstaller>();
    }

    private void OnEnable()
    {
        nextScan = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        InstallOnAllCanvases();
        nextScan = Time.unscaledTime + ScanIntervalSeconds;
    }

    public static int InstallOnAllCanvases()
    {
        int installed = 0;
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid() ||
                button.GetComponentInParent<Canvas>(true) == null ||
                IsEditorChrome(button.transform))
                continue;

            var existing = button.GetComponent<BistroBuilderV30ButtonMotion>();
            if (existing == null)
            {
                button.gameObject.AddComponent<BistroBuilderV30ButtonMotion>();
                installed++;
            }

            // Prevent the former press-scale animation from running concurrently.
            var previous = button.GetComponent<BistroBuilderUiSelectableMotion>();
            if (previous != null && previous.enabled) previous.enabled = false;
        }
        return installed;
    }

    private static bool IsEditorChrome(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.name == BistroBuilderUiShell.EditModeTopBarName ||
                current.name == BistroBuilderUiShell.EditModeBottomBarName)
                return true;
        }
        return false;
    }
}
