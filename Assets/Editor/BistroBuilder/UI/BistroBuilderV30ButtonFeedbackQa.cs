using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Deterministic installer smoke test; does not save scenes or assets.</summary>
public static class BistroBuilderV30ButtonFeedbackQa
{
    [MenuItem("Bistro Builder/QA/UI/V30 - Instalación global")]
    public static void Run()
    {
        var root = new GameObject("V30_QA_Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            var regular = new GameObject("V30_QA_Regular", typeof(RectTransform), typeof(Image), typeof(Button));
            regular.transform.SetParent(root.transform, false);
            var legacy = regular.AddComponent<BistroBuilderUiSelectableMotion>();

            var editChrome = new GameObject(BistroBuilderUiShell.EditModeTopBarName, typeof(RectTransform));
            editChrome.transform.SetParent(root.transform, false);
            var edit = new GameObject("V30_QA_Editor", typeof(RectTransform), typeof(Image), typeof(Button));
            edit.transform.SetParent(editChrome.transform, false);

            BistroBuilderV30ButtonInstaller.InstallOnAllCanvases();
            Verify(regular.GetComponent<BistroBuilderV30ButtonMotion>() != null, "V30 not installed on regular button");
            Verify(!legacy.enabled, "Legacy press effect is still active");
            Verify(edit.GetComponent<BistroBuilderV30ButtonMotion>() == null, "Editor V2 must remain excluded");

            BistroBuilderV30ButtonInstaller.InstallOnAllCanvases();
            Verify(regular.GetComponents<BistroBuilderV30ButtonMotion>().Length == 1, "Double installation");
            Verify(Mathf.Approximately(BistroBuilderV30ButtonMotion.DurationSeconds, .28f), "Unexpected duration");
            Verify(Mathf.Approximately(BistroBuilderV30ButtonMotion.PressScale, .94f), "Unexpected press scale");
            Verify(Mathf.Approximately(BistroBuilderV30ButtonMotion.ReboundScale, 1.03f), "Unexpected rebound scale");
            Debug.Log("BB_V30_QA_PASS: installation, idempotence, legacy arbitration, edit chrome exclusion, V30 constants.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Verify(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("BB_V30_QA_FAIL: " + message);
    }
}
