#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validación estructural de 2.3A.
/// Distingue errores del modelo de datos de advertencias de contenido visual
/// todavía pendiente (logos e imágenes).
/// </summary>
public sealed class BistroBuilderSuppliers23A2ValidationWindow : EditorWindow
{
    private BistroBuilderAuthoringValidationReport report;
    private Vector2 scroll;
    private int runtimeSupplierAuthorityCount;
    private bool runtimeAuthorityTypePresent;
    private int discoveredCanonicalIngredients;
    private string discoveredIngredientSource;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3A2 - Validar base de autoría", priority = 40)]
    public static void OpenAndValidate()
    {
        BistroBuilderSuppliers23A2ValidationWindow window =
            GetWindow<BistroBuilderSuppliers23A2ValidationWindow>();
        window.titleContent = new GUIContent("Validación 2.3A");
        window.minSize = new Vector2(720f, 480f);
        window.RunValidation();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            "2.3A — Modelo maestro y herramientas de autoría",
            EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Validar de nuevo", GUILayout.Width(120f)))
        {
            RunValidation();
        }
        EditorGUILayout.EndHorizontal();

        if (report == null)
        {
            EditorGUILayout.HelpBox("Pulsa Validar de nuevo.", MessageType.Info);
            return;
        }

        MessageType type = report.ErrorCount > 0
            ? MessageType.Error
            : report.WarningCount > 0
                ? MessageType.Warning
                : MessageType.Info;

        EditorGUILayout.HelpBox(
            "Errores estructurales: " + report.ErrorCount +
            "   |   Advertencias de contenido: " + report.WarningCount +
            "   |   Información: " + report.InfoCount,
            type);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Integración con autoridades existentes", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Tipo BistroBuilderSupplierCatalogService presente",
            runtimeAuthorityTypePresent ? "sí" : "no localizado en assemblies cargados");
        EditorGUILayout.LabelField(
            "Autoridades runtime activas detectadas",
            runtimeSupplierAuthorityCount.ToString());
        EditorGUILayout.LabelField(
            "Ingredientes canónicos descubiertos ahora",
            discoveredCanonicalIngredients.ToString());
        EditorGUILayout.LabelField(
            "Fuente de ingredientes",
            string.IsNullOrWhiteSpace(discoveredIngredientSource)
                ? "sin fuente compatible cargada"
                : discoveredIngredientSource);

        EditorGUILayout.HelpBox(
            "2.3A de este paquete es una capa de autoría. No crea un segundo SupplierCatalogService y no escribe en Inventario ni Recepciones.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        IReadOnlyList<BistroBuilderAuthoringValidationIssue> issues = report.Issues;
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("Sin incidencias de autoría.", MessageType.Info);
        }
        else
        {
            for (int index = 0; index < issues.Count; index++)
            {
                BistroBuilderAuthoringValidationIssue issue = issues[index];
                MessageType issueType = issue.severity == BistroBuilderAuthoringValidationSeverity.Error
                    ? MessageType.Error
                    : issue.severity == BistroBuilderAuthoringValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;

                string prefix = string.IsNullOrWhiteSpace(issue.recordId)
                    ? issue.code
                    : issue.code + " · " + issue.recordId;

                EditorGUILayout.HelpBox(prefix + "\n" + issue.message, issueType);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        report = BistroBuilderSupplierAuthoringValidator.Validate(
            suppliers,
            ingredients);

        EvaluateIntegration();
        Repaint();

        Debug.Log(
            "VALIDACIÓN 2.3A — Errores: " + report.ErrorCount +
            ", advertencias: " + report.WarningCount +
            ", información: " + report.InfoCount +
            ". Autoridades SupplierCatalogService activas: " + runtimeSupplierAuthorityCount +
            ". Ingredientes canónicos descubiertos: " + discoveredCanonicalIngredients + ".");
    }

    private void EvaluateIntegration()
    {
        runtimeSupplierAuthorityCount = 0;
        runtimeAuthorityTypePresent = false;

        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            if (!string.Equals(
                    type.Name,
                    "BistroBuilderSupplierCatalogService",
                    StringComparison.Ordinal))
            {
                continue;
            }

            runtimeAuthorityTypePresent = true;

            GameObject owner = behaviour.gameObject;
            if (owner != null &&
                owner.scene.IsValid() &&
                owner.activeInHierarchy &&
                behaviour.enabled)
            {
                runtimeSupplierAuthorityCount++;
            }
        }

        // Puede no existir una instancia cargada, pero sí el tipo compilado.
        if (!runtimeAuthorityTypePresent)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null)
                {
                    continue;
                }

                for (int index = 0; index < types.Length; index++)
                {
                    Type type = types[index];
                    if (type != null &&
                        string.Equals(
                            type.Name,
                            "BistroBuilderSupplierCatalogService",
                            StringComparison.Ordinal))
                    {
                        runtimeAuthorityTypePresent = true;
                        break;
                    }
                }

                if (runtimeAuthorityTypePresent)
                {
                    break;
                }
            }
        }

        List<BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.DiscoveredIngredient> buffer =
            new List<BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.DiscoveredIngredient>();
        discoveredCanonicalIngredients =
            BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.Discover(
                buffer,
                out discoveredIngredientSource);
    }
}
#endif
