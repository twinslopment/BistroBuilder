#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23B12ValidationWindow : EditorWindow
{
    private BistroBuilderSuppliers23B12ValidationReport report;
    private Vector2 scroll;

    [MenuItem(
        "Tools/Bistro Builder/Proveedores/2.3B1+B2 - Validar formatos y catálogo base",
        priority = 3)]
    public static void OpenAndValidate()
    {
        BistroBuilderSuppliers23B12ValidationWindow window =
            GetWindow<BistroBuilderSuppliers23B12ValidationWindow>();
        window.titleContent = new GUIContent("Validación 2.3B1+B2");
        window.minSize = new Vector2(760f, 520f);
        window.RunValidation();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label(
            "2.3B1+B2 — Formatos comerciales, catálogo y ofertas base",
            EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Validar", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            RunValidation();
        }
        EditorGUILayout.EndHorizontal();

        if (report == null)
        {
            EditorGUILayout.HelpBox("Pulsa Validar para generar el informe.", MessageType.Info);
            return;
        }

        MessageType type = report.ErrorCount > 0
            ? MessageType.Error
            : report.WarningCount > 0
                ? MessageType.Warning
                : MessageType.Info;

        EditorGUILayout.HelpBox(
            "Errores estructurales: " + report.ErrorCount +
            " | Advertencias de contenido: " + report.WarningCount +
            " | Información: " + report.InfoCount,
            type);

        EditorGUILayout.HelpBox(
            "Las advertencias por logos e imágenes pendientes son contenido visual, no corrupción estructural. " +
            "B1+B2 no publican todavía en supplier.catalog runtime ni escriben en Inventario/Recepciones.",
            MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        IReadOnlyList<BistroBuilderSuppliers23B12Issue> issues = report.Issues;
        for (int index = 0; index < issues.Count; index++)
        {
            BistroBuilderSuppliers23B12Issue issue = issues[index];
            MessageType issueType = issue.severity == BistroBuilderSuppliers23B12IssueSeverity.Error
                ? MessageType.Error
                : issue.severity == BistroBuilderSuppliers23B12IssueSeverity.Warning
                    ? MessageType.Warning
                    : MessageType.Info;

            string prefix = string.IsNullOrWhiteSpace(issue.recordId)
                ? issue.code
                : issue.code + " · " + issue.recordId;

            EditorGUILayout.HelpBox(prefix + "\n" + issue.message, issueType);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        report = BistroBuilderSuppliers23B12Validator.Validate(suppliers, ingredients);
        Debug.Log(
            "VALIDACIÓN 2.3B1+B2 — Errores: " + report.ErrorCount +
            ", advertencias: " + report.WarningCount +
            ", información: " + report.InfoCount + ".");
        Repaint();
    }
}
#endif
