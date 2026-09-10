using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BistroBuilderUi21AValidator
{
    [MenuItem("Tools/Bistro Builder/UI UX/21A - Validar", false, 4101)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — UI/UX 21A", report, "Aceptar");
        if (!ok) Debug.LogError("UI/UX 21A no supera validación.");
    }

    public static bool ValidateCurrentScene(out string report)
    {
        int passed = 0;
        int failed = 0;
        StringBuilder log = new StringBuilder();
        log.AppendLine("BISTRO BUILDER — UI/UX 21A VALIDATION");
        Scene scene = SceneManager.GetActiveScene();

        Canvas canvas = FindCanonicalCanvas(scene);
        Check(canvas != null, "Canvas HUD canónico localizado", ref passed, ref failed, log);
        if (canvas == null)
        {
            report = log.AppendLine("RESULTADO: FAIL").ToString();
            return false;
        }

        BistroBuilderUiDesignSystem design = canvas.GetComponent<BistroBuilderUiDesignSystem>();
        BistroBuilderUiShell shell = canvas.GetComponent<BistroBuilderUiShell>();
        BistroBuilderUnifiedUiInteractionService interaction =
            canvas.GetComponent<BistroBuilderUnifiedUiInteractionService>();

        Check(design != null, "Design System instalado", ref passed, ref failed, log);
        Check(shell != null, "HUD Shell instalado", ref passed, ref failed, log);
        Check(interaction != null, "Interacción transversal instalada", ref passed, ref failed, log);
        Check(canvas.GetComponents<BistroBuilderUiDesignSystem>().Length == 1,
            "Design System idempotente (1 instancia)", ref passed, ref failed, log);
        Check(canvas.GetComponents<BistroBuilderUiShell>().Length == 1,
            "HUD Shell idempotente (1 instancia)", ref passed, ref failed, log);
        Check(canvas.GetComponents<BistroBuilderUnifiedUiInteractionService>().Length == 1,
            "Interacción transversal sin duplicados", ref passed, ref failed, log);
        Check(canvas.GetComponent<GraphicRaycaster>() != null,
            "GraphicRaycaster disponible", ref passed, ref failed, log);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
            "Escalado responsive activo", ref passed, ref failed, log);
        Check(scaler != null && scaler.referenceResolution == BistroBuilderUiTokens.ReferenceResolution,
            "Resolución de referencia 1920x1080", ref passed, ref failed, log);

        Transform root = canvas.transform.Find(BistroBuilderUiShell.RootName);
        Check(root != null, "Root definitivo creado", ref passed, ref failed, log);
        Check(CountNamedChildren(canvas.transform, BistroBuilderUiShell.RootName) == 1,
            "Root 21A idempotente (1 instancia)", ref passed, ref failed, log);
        Check(root != null && root.Find(BistroBuilderUiShell.TopBarName) != null,
            "Navegación horizontal superior creada", ref passed, ref failed, log);
        Check(root != null && root.Find(BistroBuilderUiShell.BottomBarName) != null,
            "Franja operativa inferior creada", ref passed, ref failed, log);
        Check(root != null && root.Find(BistroBuilderUiShell.ActivityPanelName) != null,
            "Panel Actividad lateral creado", ref passed, ref failed, log);

        Transform top = root != null ? root.Find(BistroBuilderUiShell.TopBarName) : null;
        Transform nav = top != null ? top.Find("NavigationContent") : null;
        Check(nav != null && nav.Find("BBNav_Actividad") != null,
            "Actividad forma parte de la navegación superior", ref passed, ref failed, log);
        Check(nav != null && nav.Find("BBNav_Edicion") != null,
            "Edición queda separada como cambio de modo", ref passed, ref failed, log);
        string[] requiredNav =
        {
            "BBNav_Actividad", "BBNav_Personal", "BBNav_Carta", "BBNav_Inventario",
            "BBNav_Proveedores", "BBNav_Reservas", "BBNav_Economia", "BBNav_Marketing",
            "BBNav_Reputacion", "BBNav_Progreso", "BBNav_Edicion"
        };
        for (int i = 0; i < requiredNav.Length; i++)
            Check(nav != null && nav.Find(requiredNav[i]) != null,
                "Acceso superior disponible: " + requiredNav[i], ref passed, ref failed, log);

        Transform bottom = root != null ? root.Find(BistroBuilderUiShell.BottomBarName) : null;
        Transform status = bottom != null ? bottom.Find("StatusContent") : null;
        Check(status != null && status.Find("Cash/Label") != null, "Pill Caja disponible", ref passed, ref failed, log);
        Check(status != null && status.Find("Satisfaction/Label") != null, "Pill Satisfacción disponible", ref passed, ref failed, log);
        Check(status != null && status.Find("Kitchen/Label") != null, "Pill Cocina disponible", ref passed, ref failed, log);
        Check(status != null && status.Find("Waiting/Label") != null, "Pill Espera disponible", ref passed, ref failed, log);
        Check(nav == null || (nav.Find("BBNav_General") == null &&
            nav.Find("BBNav_Isometrica") == null && nav.Find("BBNav_Cenital") == null),
            "No existen vistas predefinidas en la navegación final", ref passed, ref failed, log);

        Transform timeDock = canvas.transform.Find("BB_368B_TimeControlsDock");
        Check(timeDock != null, "Pausa y velocidades permanecen en franja inferior",
            ref passed, ref failed, log);

        if (design != null)
        {
            Check(design.ValidateConfiguration(out _), "Design System valida configuración",
                ref passed, ref failed, log);
        }
        if (shell != null)
        {
            Check(shell.ValidateConfiguration(out _), "HUD Shell valida configuración",
                ref passed, ref failed, log);
        }

        log.AppendLine($"RESULTADO: {passed} OK / {failed} errores");
        report = log.ToString();
        return failed == 0;
    }

    private static int CountNamedChildren(Transform parent, string name)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) count++;
        return count;
    }
    private static Canvas FindCanonicalCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            Transform parent = canvas.transform.parent;
            if (canvas.name == "Canvas" && parent != null && parent.name == "MainHUD") return canvas;
        }
        return null;
    }

    private static void Check(bool condition, string description,
        ref int passed, ref int failed, StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("OK  · " + description);
        }
        else
        {
            failed++;
            log.AppendLine("FAIL· " + description);
        }
    }
}
