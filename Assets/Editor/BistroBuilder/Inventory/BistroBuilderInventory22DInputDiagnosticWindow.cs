using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Diagnóstico específico para el InputField "Stock mínimo" de 2.2D.
/// No modifica datos canónicos: únicamente inspecciona configuración, raycasts,
/// foco y conservación del borrador frente a un refresco de la vista.
/// </summary>
public sealed class BistroBuilderInventory22DInputDiagnosticWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/2.2D Input Diagnostic";

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode, abre Inventario y pulsa Ejecutar diagnóstico completo.";

    [MenuItem(MenuPath, false, 395)]
    private static void Open()
    {
        GetWindow<BistroBuilderInventory22DInputDiagnosticWindow>(
            "2.2D Input Diagnostic"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.2D — Diagnóstico de Stock mínimo",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Este diagnóstico NO cambia stock ni mínimos canónicos. Inspecciona " +
            "el InputField, compara con Ajuste manual, analiza los raycasts reales " +
            "y prueba foco/texto temporal en la propia UI.",
            MessageType.Info
        );

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
        if (GUILayout.Button("Ejecutar diagnóstico completo", GUILayout.Height(36f)))
        {
            RunDiagnostic();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8f);
        if (GUILayout.Button("Copiar informe al portapapeles"))
        {
            EditorGUIUtility.systemCopyBuffer = report ?? string.Empty;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report ?? string.Empty, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunDiagnostic()
    {
        var sb = new StringBuilder(8192);
        sb.AppendLine("BISTRO BUILDER — DIAGNÓSTICO 2.2D STOCK MÍNIMO");
        sb.AppendLine("Fecha editor: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        if (!EditorApplication.isPlaying)
        {
            sb.AppendLine("FALLO: requiere Play Mode.");
            report = sb.ToString();
            return;
        }

        BistroBuilderInventoryWarehouseRuntimeView view =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>();
        if (view == null)
        {
            sb.AppendLine("FALLO: no existe BistroBuilderInventoryWarehouseRuntimeView.");
            report = sb.ToString();
            return;
        }

        sb.AppendLine("Vista: " + GetPath(view.transform));
        sb.AppendLine("IsOpen antes: " + view.IsOpen);
        if (!view.IsOpen)
        {
            if (!view.TryOpenFromInterface(out string openError))
            {
                sb.AppendLine("FALLO al abrir Inventario: " + openError);
                report = sb.ToString();
                return;
            }
            Canvas.ForceUpdateCanvases();
            sb.AppendLine("Inventario abierto automáticamente para diagnóstico.");
        }

        InputField minimum = GetPrivateField<InputField>(view, "minimumInput");
        InputField adjustment = GetPrivateField<InputField>(view, "adjustmentInput");

        sb.AppendLine();
        AppendInputFieldReport(sb, "STOCK MÍNIMO", minimum);
        sb.AppendLine();
        AppendInputFieldReport(sb, "AJUSTE MANUAL (CONTROL)", adjustment);

        EventSystem eventSystem = EventSystem.current;
        sb.AppendLine();
        sb.AppendLine("=== EVENT SYSTEM ===");
        sb.AppendLine("Existe: " + (eventSystem != null));
        if (eventSystem != null)
        {
            sb.AppendLine("Objeto: " + GetPath(eventSystem.transform));
            sb.AppendLine("Seleccionado inicial: " +
                GetPath(eventSystem.currentSelectedGameObject != null
                    ? eventSystem.currentSelectedGameObject.transform
                    : null));
        }

        sb.AppendLine();
        AppendRaycastReport(sb, "STOCK MÍNIMO", minimum, eventSystem);
        sb.AppendLine();
        AppendRaycastReport(sb, "AJUSTE MANUAL", adjustment, eventSystem);

        sb.AppendLine();
        sb.AppendLine("=== PRUEBA DE FOCO PROGRAMÁTICO ===");
        ProbeFocus(sb, "Stock mínimo", minimum, eventSystem);
        ProbeFocus(sb, "Ajuste manual", adjustment, eventSystem);

        sb.AppendLine();
        sb.AppendLine("=== PRUEBA DE CLICK POR RAYCAST REAL ===");
        ProbePointerDispatch(sb, "Stock mínimo", minimum, eventSystem);
        ProbePointerDispatch(sb, "Ajuste manual", adjustment, eventSystem);

        sb.AppendLine();
        sb.AppendLine("=== PRUEBA DE TEXTO TEMPORAL + REFRESCO ===");
        if (minimum == null)
        {
            sb.AppendLine("No se puede probar: minimumInput es null.");
        }
        else
        {
            string originalText = minimum.text;
            string selectedId = view.SelectedIngredientId;
            sb.AppendLine("Ingrediente seleccionado: " + selectedId);
            sb.AppendLine("Texto original: [" + originalText + "]");

            minimum.text = "12";
            sb.AppendLine("Tras minimum.text = 12: [" + minimum.text + "]");

            bool refreshOk = view.TrySelectIngredientForTest(
                selectedId,
                out string refreshError
            );
            Canvas.ForceUpdateCanvases();
            sb.AppendLine("Refresco mismo ingrediente: " + refreshOk +
                (string.IsNullOrEmpty(refreshError) ? string.Empty : " · " + refreshError));
            sb.AppendLine("Texto tras refresco: [" + minimum.text + "]");

            // Restauración visual únicamente. No se llama a Aplicar, por lo que
            // no existe modificación de inventory.policy.
            minimum.text = originalText;
            view.TrySelectIngredientForTest(selectedId, out _);
            Canvas.ForceUpdateCanvases();
            sb.AppendLine("Texto visual restaurado: [" + minimum.text + "]");
        }

        sb.AppendLine();
        sb.AppendLine("=== INTERPRETACIÓN AUTOMÁTICA ===");
        AppendInterpretation(sb, minimum, adjustment, eventSystem);

        report = sb.ToString();
        Repaint();
        Debug.Log(report);
    }

    private static void AppendInputFieldReport(
        StringBuilder sb,
        string label,
        InputField input
    )
    {
        sb.AppendLine("=== " + label + " ===");
        if (input == null)
        {
            sb.AppendLine("NULL");
            return;
        }

        RectTransform rect = input.transform as RectTransform;
        sb.AppendLine("Path: " + GetPath(input.transform));
        sb.AppendLine("activeSelf/activeInHierarchy: " + input.gameObject.activeSelf +
            " / " + input.gameObject.activeInHierarchy);
        sb.AppendLine("enabled: " + input.enabled);
        sb.AppendLine("interactable: " + input.interactable);
        sb.AppendLine("readOnly: " + input.readOnly);
        sb.AppendLine("isFocused: " + input.isFocused);
        sb.AppendLine("text: [" + input.text + "]");
        sb.AppendLine("contentType: " + input.contentType);
        sb.AppendLine("lineType: " + input.lineType);
        sb.AppendLine("characterValidation: " + input.characterValidation);
        sb.AppendLine("navigation: " + input.navigation.mode);

        if (rect != null)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            sb.AppendLine("rect local: " + rect.rect);
            sb.AppendLine("world corners: " + corners[0] + " | " + corners[1] +
                " | " + corners[2] + " | " + corners[3]);
            sb.AppendLine("screen center: " + GetScreenCenter(rect));
        }

        Graphic target = input.targetGraphic;
        sb.AppendLine("targetGraphic: " + DescribeGraphic(target));
        sb.AppendLine("textComponent: " + DescribeGraphic(input.textComponent));
        sb.AppendLine("placeholder: " + DescribeGraphic(input.placeholder as Graphic));

        Image rootImage = input.GetComponent<Image>();
        sb.AppendLine("root Image: " + DescribeGraphic(rootImage));

        sb.AppendLine("CanvasGroup ancestors:");
        Transform current = input.transform;
        bool foundGroup = false;
        while (current != null)
        {
            CanvasGroup[] groups = current.GetComponents<CanvasGroup>();
            for (int index = 0; index < groups.Length; index++)
            {
                foundGroup = true;
                CanvasGroup group = groups[index];
                sb.AppendLine("  - " + GetPath(current) +
                    " interactable=" + group.interactable +
                    " blocksRaycasts=" + group.blocksRaycasts +
                    " alpha=" + group.alpha +
                    " ignoreParentGroups=" + group.ignoreParentGroups);
            }
            current = current.parent;
        }
        if (!foundGroup)
        {
            sb.AppendLine("  (ninguno)");
        }
    }

    private static void AppendRaycastReport(
        StringBuilder sb,
        string label,
        InputField input,
        EventSystem eventSystem
    )
    {
        sb.AppendLine("=== RAYCAST " + label + " ===");
        if (input == null || eventSystem == null)
        {
            sb.AppendLine("No disponible.");
            return;
        }

        RectTransform rect = input.transform as RectTransform;
        Vector2 screen = GetScreenCenter(rect);
        sb.AppendLine("Punto pantalla: " + screen);

        var pointer = new PointerEventData(eventSystem)
        {
            position = screen,
            button = PointerEventData.InputButton.Left
        };
        var results = new List<RaycastResult>(32);
        eventSystem.RaycastAll(pointer, results);
        sb.AppendLine("Resultados: " + results.Count);

        int shown = Math.Min(12, results.Count);
        for (int index = 0; index < shown; index++)
        {
            RaycastResult hit = results[index];
            sb.AppendLine(
                "  [" + index + "] " + GetPath(hit.gameObject != null ? hit.gameObject.transform : null) +
                " | module=" + (hit.module != null ? hit.module.GetType().Name : "null") +
                " | depth=" + hit.depth +
                " | sortingLayer=" + hit.sortingLayer +
                " | sortingOrder=" + hit.sortingOrder
            );
        }

        GameObject first = results.Count > 0 ? results[0].gameObject : null;
        sb.AppendLine("Top hit pertenece al InputField: " + IsWithin(first, input.gameObject));
    }

    private static void ProbeFocus(
        StringBuilder sb,
        string label,
        InputField input,
        EventSystem eventSystem
    )
    {
        if (input == null || eventSystem == null)
        {
            sb.AppendLine(label + ": no disponible.");
            return;
        }

        eventSystem.SetSelectedGameObject(null);
        input.Select();
        input.ActivateInputField();
        Canvas.ForceUpdateCanvases();

        sb.AppendLine(label + ": selected=" +
            GetPath(eventSystem.currentSelectedGameObject != null
                ? eventSystem.currentSelectedGameObject.transform
                : null) +
            " · isFocused=" + input.isFocused);

        input.DeactivateInputField();
        eventSystem.SetSelectedGameObject(null);
    }

    private static void ProbePointerDispatch(
        StringBuilder sb,
        string label,
        InputField input,
        EventSystem eventSystem
    )
    {
        if (input == null || eventSystem == null)
        {
            sb.AppendLine(label + ": no disponible.");
            return;
        }

        Vector2 screen = GetScreenCenter(input.transform as RectTransform);
        var pointer = new PointerEventData(eventSystem)
        {
            position = screen,
            button = PointerEventData.InputButton.Left
        };
        var results = new List<RaycastResult>(32);
        eventSystem.RaycastAll(pointer, results);
        GameObject top = results.Count > 0 ? results[0].gameObject : null;

        eventSystem.SetSelectedGameObject(null);
        if (top != null)
        {
            ExecuteEvents.ExecuteHierarchy(
                top,
                pointer,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.ExecuteHierarchy(
                top,
                pointer,
                ExecuteEvents.pointerClickHandler
            );
        }
        Canvas.ForceUpdateCanvases();

        sb.AppendLine(label + ": topHit=" + GetPath(top != null ? top.transform : null) +
            " · selected=" +
            GetPath(eventSystem.currentSelectedGameObject != null
                ? eventSystem.currentSelectedGameObject.transform
                : null) +
            " · isFocused=" + input.isFocused);

        input.DeactivateInputField();
        eventSystem.SetSelectedGameObject(null);
    }

    private static void AppendInterpretation(
        StringBuilder sb,
        InputField minimum,
        InputField adjustment,
        EventSystem eventSystem
    )
    {
        if (minimum == null)
        {
            sb.AppendLine("DIAGNÓSTICO: minimumInput no existe o no está enlazado.");
            return;
        }

        if (!minimum.gameObject.activeInHierarchy || !minimum.enabled || !minimum.interactable)
        {
            sb.AppendLine("DIAGNÓSTICO: el campo Stock mínimo está deshabilitado/inactivo.");
            return;
        }

        if (minimum.readOnly)
        {
            sb.AppendLine("DIAGNÓSTICO: el campo Stock mínimo está en readOnly.");
            return;
        }

        if (minimum.targetGraphic == null || !minimum.targetGraphic.raycastTarget)
        {
            sb.AppendLine("DIAGNÓSTICO: targetGraphic de Stock mínimo no acepta raycast.");
            return;
        }

        if (eventSystem != null)
        {
            Vector2 screen = GetScreenCenter(minimum.transform as RectTransform);
            var pointer = new PointerEventData(eventSystem) { position = screen };
            var results = new List<RaycastResult>(32);
            eventSystem.RaycastAll(pointer, results);
            GameObject top = results.Count > 0 ? results[0].gameObject : null;
            if (!IsWithin(top, minimum.gameObject))
            {
                sb.AppendLine("DIAGNÓSTICO: otro Graphic intercepta el click de Stock mínimo. " +
                    "Top hit=" + GetPath(top != null ? top.transform : null));
                return;
            }
        }

        sb.AppendLine(
            "DIAGNÓSTICO: la configuración básica y el raycast de Stock mínimo parecen correctos. " +
            "Si la prueba de foco programático funciona pero el teclado físico no, la siguiente " +
            "revisión debe centrarse en pérdida de selección/foco por eventos posteriores."
        );

        if (adjustment != null)
        {
            sb.AppendLine("Referencia Ajuste manual: interactable=" + adjustment.interactable +
                " readOnly=" + adjustment.readOnly +
                " targetRaycast=" +
                (adjustment.targetGraphic != null && adjustment.targetGraphic.raycastTarget));
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        return field != null ? field.GetValue(target) as T : null;
    }

    private static string DescribeGraphic(Graphic graphic)
    {
        if (graphic == null)
        {
            return "null";
        }
        return graphic.GetType().Name + " @ " + GetPath(graphic.transform) +
            " enabled=" + graphic.enabled +
            " raycastTarget=" + graphic.raycastTarget +
            " color=" + graphic.color;
    }

    private static Vector2 GetScreenCenter(RectTransform rect)
    {
        if (rect == null)
        {
            return Vector2.zero;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
    }

    private static bool IsWithin(GameObject candidate, GameObject root)
    {
        if (candidate == null || root == null)
        {
            return false;
        }
        Transform current = candidate.transform;
        while (current != null)
        {
            if (current.gameObject == root)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }
        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }
}
