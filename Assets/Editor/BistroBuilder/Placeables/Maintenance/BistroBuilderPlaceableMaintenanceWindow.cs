using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Centro de mantenimiento de artículos colocables.
///
/// Reúne:
/// - análisis de solo lectura;
/// - reparación segura por lotes;
/// - generación y regeneración de miniaturas;
/// - edición controlada de un artículo existente;
/// - previsualización de cambios;
/// - ejecución de Project Health tras escribir assets.
/// </summary>
public sealed class BistroBuilderPlaceableMaintenanceWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Placeables/" +
        "Open Placeable Maintenance";

    private enum WindowTab
    {
        ProjectHealth = 0,
        EditItem = 1
    }

    [SerializeField]
    private WindowTab selectedTab;

    [SerializeField]
    private RestaurantPlaceableItemDefinition selectedDefinition;

    private BistroBuilderPlaceableMaintenanceReport report;

    private BistroBuilderPlaceableEditDraft draft;

    private readonly List<string> draftPreview =
        new List<string>();

    private Vector2 projectScroll;
    private Vector2 editScroll;

    [MenuItem(MenuPath, false, 140)]
    public static void OpenWindow()
    {
        BistroBuilderPlaceableMaintenanceWindow window =
            GetWindow<
                BistroBuilderPlaceableMaintenanceWindow
            >(
                "Bistro Builder Placeables"
            );

        window.minSize =
            new Vector2(760f, 620f);

        window.Show();
        window.SynchronizeSelection();
    }

    private void OnEnable()
    {
        Selection.selectionChanged +=
            HandleSelectionChanged;

        SynchronizeSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -=
            HandleSelectionChanged;
    }

    private void HandleSelectionChanged()
    {
        RestaurantPlaceableItemDefinition resolved =
            BistroBuilderPlaceableMaintenanceService
                .ResolveDefinitionFromSelection();

        if (resolved != null &&
            !ReferenceEquals(
                resolved,
                selectedDefinition
            ))
        {
            LoadDefinition(resolved);
        }

        Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();

        selectedTab =
            (WindowTab)GUILayout.Toolbar(
                (int)selectedTab,
                new[]
                {
                    "Proyecto y lotes",
                    "Editar artículo"
                }
            );

        EditorGUILayout.Space(6f);

        switch (selectedTab)
        {
            case WindowTab.EditItem:
                DrawEditItemTab();
                break;

            default:
                DrawProjectTab();
                break;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField(
            "Mantenimiento de artículos colocables",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Analiza antes de modificar. Las reparaciones por lotes " +
            "solo corrigen referencias seguras, preservan ItemId, " +
            "no sustituyen iconos manuales y disponen de rollback.",
            MessageType.Info
        );
    }

    private void DrawProjectTab()
    {
        projectScroll =
            EditorGUILayout.BeginScrollView(
                projectScroll
            );

        DrawProjectActions();
        EditorGUILayout.Space(10f);
        DrawProjectReport();

        EditorGUILayout.EndScrollView();
    }

    private void DrawProjectActions()
    {
        EditorGUILayout.LabelField(
            "Acciones automatizadas",
            EditorStyles.boldLabel
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Analizar proyecto",
                GUILayout.Height(34f)
            ))
        {
            AnalyzeProject();
        }

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling ||
                   EditorApplication.isUpdating
               ))
        {
            if (GUILayout.Button(
                    "Reparar seguro + miniaturas faltantes",
                    GUILayout.Height(34f)
                ))
            {
                RepairProject();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling ||
                   EditorApplication.isUpdating
               ))
        {
            if (GUILayout.Button(
                    "Generar miniaturas faltantes"
                ))
            {
                GenerateMissingThumbnails();
            }

            if (GUILayout.Button(
                    "Regenerar miniaturas administradas"
                ))
            {
                RegenerateManagedThumbnails();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Los iconos asignados manualmente se conservan. " +
            "Regenerar solo afecta a miniaturas dentro de " +
            BistroBuilderCatalogThumbnailService.GeneratedIconFolder +
            ".",
            MessageType.None
        );
    }

    private void DrawProjectReport()
    {
        EditorGUILayout.LabelField(
            "Informe de mantenimiento",
            EditorStyles.boldLabel
        );

        if (report == null)
        {
            EditorGUILayout.LabelField(
                "Todavía no se ha ejecutado el análisis."
            );

            return;
        }

        EditorGUILayout.BeginHorizontal();

        DrawCountBox(
            "Bloqueantes",
            report.BlockerCount
        );

        DrawCountBox(
            "Errores",
            report.ErrorCount
        );

        DrawCountBox(
            "Advertencias",
            report.WarningCount
        );

        DrawCountBox(
            "Información",
            report.InfoCount
        );

        DrawCountBox(
            "Autorreparables",
            report.AutoRepairableCount
        );

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);

        if (report.Findings.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No se han detectado incidencias de mantenimiento.",
                MessageType.Info
            );

            return;
        }

        for (int index = 0;
             index < report.Findings.Count;
             index++)
        {
            DrawFinding(
                report.Findings[index]
            );
        }
    }

    private void DrawCountBox(
        string label,
        int value
    )
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox,
            GUILayout.MinWidth(105f)
        );

        EditorGUILayout.LabelField(
            value.ToString(),
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            }
        );

        EditorGUILayout.LabelField(
            label,
            new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            }
        );

        EditorGUILayout.EndVertical();
    }

    private void DrawFinding(
        BistroBuilderPlaceableMaintenanceFinding finding
    )
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            finding.Severity +
            "  " +
            finding.Code,
            EditorStyles.boldLabel
        );

        GUILayout.FlexibleSpace();

        if (finding.IsAutoRepairable)
        {
            GUILayout.Label(
                "Autorreparable",
                EditorStyles.miniBoldLabel
            );
        }

        using (new EditorGUI.DisabledScope(
                   finding.Context == null
               ))
        {
            if (GUILayout.Button(
                    "Localizar",
                    GUILayout.Width(72f)
                ))
            {
                Selection.activeObject =
                    finding.Context;

                EditorGUIUtility.PingObject(
                    finding.Context
                );
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            finding.Message,
            ResolveMessageType(finding.Severity)
        );

        if (!string.IsNullOrWhiteSpace(
                finding.Recommendation
            ))
        {
            EditorGUILayout.LabelField(
                finding.Recommendation,
                EditorStyles.wordWrappedMiniLabel
            );
        }

        if (!string.IsNullOrWhiteSpace(
                finding.AssetPath
            ))
        {
            EditorGUILayout.LabelField(
                finding.AssetPath,
                EditorStyles.miniLabel
            );
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEditItemTab()
    {
        editScroll =
            EditorGUILayout.BeginScrollView(
                editScroll
            );

        RestaurantPlaceableItemDefinition newSelection =
            (RestaurantPlaceableItemDefinition)
            EditorGUILayout.ObjectField(
                "Artículo",
                selectedDefinition,
                typeof(
                    RestaurantPlaceableItemDefinition
                ),
                false
            );

        if (!ReferenceEquals(
                newSelection,
                selectedDefinition
            ))
        {
            LoadDefinition(newSelection);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Usar selección de Project"
            ))
        {
            SynchronizeSelection();
        }

        using (new EditorGUI.DisabledScope(
                   selectedDefinition == null
               ))
        {
            if (GUILayout.Button(
                    "Recargar valores"
                ))
            {
                LoadDefinition(
                    selectedDefinition
                );
            }
        }

        EditorGUILayout.EndHorizontal();

        if (draft == null ||
            draft.Definition == null)
        {
            EditorGUILayout.HelpBox(
                "Selecciona un PlaceableItemDefinition o un prefab " +
                "colocable en Project.",
                MessageType.Info
            );

            EditorGUILayout.EndScrollView();
            return;
        }

        DrawCurrentIcon();
        EditorGUILayout.Space(8f);
        DrawStableIdentity();
        EditorGUILayout.Space(8f);
        DrawItemMetadata();
        EditorGUILayout.Space(8f);
        DrawEditRules();
        EditorGUILayout.Space(8f);
        DrawSpatialRules();
        EditorGUILayout.Space(8f);
        DrawDraftPreview();
        EditorGUILayout.Space(10f);
        DrawDraftActions();

        EditorGUILayout.EndScrollView();
    }

    private void DrawCurrentIcon()
    {
        EditorGUILayout.LabelField(
            "Miniatura actual",
            EditorStyles.boldLabel
        );

        Sprite icon =
            draft.Definition.CatalogIcon;

        if (icon == null)
        {
            EditorGUILayout.HelpBox(
                "El artículo todavía no tiene miniatura.",
                MessageType.None
            );

            return;
        }

        Rect previewRect =
            GUILayoutUtility.GetRect(
                128f,
                128f,
                GUILayout.ExpandWidth(false)
            );

        EditorGUI.DrawPreviewTexture(
            previewRect,
            icon.texture,
            null,
            ScaleMode.ScaleToFit
        );

        EditorGUILayout.LabelField(
            BistroBuilderCatalogThumbnailService
                .IsGeneratedIcon(icon)
                ? "Miniatura administrada automáticamente."
                : "Icono manual: las acciones automáticas lo preservan.",
            EditorStyles.miniLabel
        );
    }

    private void DrawStableIdentity()
    {
        EditorGUILayout.LabelField(
            "Identidad estable",
            EditorStyles.boldLabel
        );

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(
                "ItemId",
                draft.Definition.ItemId
            );

            EditorGUILayout.ObjectField(
                "Prefab",
                draft.Definition.Prefab,
                typeof(RestaurantPlaceableObject),
                false
            );

            EditorGUILayout.ObjectField(
                "Definición editable",
                draft.Definition.EditableDefinition,
                typeof(
                    RestaurantEditableObjectDefinition
                ),
                false
            );
        }

        EditorGUILayout.HelpBox(
            "ItemId no se edita aquí porque participa en guardado, " +
            "catálogo y economía. Cambiarlo requiere una migración " +
            "versionada.",
            MessageType.None
        );
    }

    private void DrawItemMetadata()
    {
        EditorGUILayout.LabelField(
            "Datos de catálogo",
            EditorStyles.boldLabel
        );

        draft.DisplayName =
            EditorGUILayout.TextField(
                "Nombre visible",
                draft.DisplayName
            );

        draft.Category =
            (RestaurantPlaceableItemCategory)
            EditorGUILayout.EnumPopup(
                "Categoría",
                draft.Category
            );

        draft.PurchasePrice =
            Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    "Precio de compra",
                    draft.PurchasePrice
                )
            );

        EditorGUILayout.LabelField(
            "Descripción"
        );

        draft.Description =
            EditorGUILayout.TextArea(
                draft.Description ?? string.Empty,
                GUILayout.MinHeight(60f)
            );

        draft.RegenerateThumbnail =
            EditorGUILayout.Toggle(
                "Regenerar miniatura",
                draft.RegenerateThumbnail
            );
    }

    private void DrawEditRules()
    {
        EditorGUILayout.LabelField(
            "Reglas de edición",
            EditorStyles.boldLabel
        );

        draft.CanMove =
            EditorGUILayout.Toggle(
                "Se puede mover",
                draft.CanMove
            );

        draft.CanRotate =
            EditorGUILayout.Toggle(
                "Se puede rotar",
                draft.CanRotate
            );

        draft.UseCustomGridSize =
            EditorGUILayout.Toggle(
                "Cuadrícula personalizada",
                draft.UseCustomGridSize
            );

        using (new EditorGUI.DisabledScope(
                   !draft.UseCustomGridSize
               ))
        {
            draft.CustomGridSize =
                Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField(
                        "Tamaño de cuadrícula",
                        draft.CustomGridSize
                    )
                );
        }

        draft.UseCustomRotationStep =
            EditorGUILayout.Toggle(
                "Rotación personalizada",
                draft.UseCustomRotationStep
            );

        using (new EditorGUI.DisabledScope(
                   !draft.UseCustomRotationStep
               ))
        {
            draft.RotationStepDegrees =
                Mathf.Clamp(
                    EditorGUILayout.FloatField(
                        "Paso de rotación",
                        draft.RotationStepDegrees
                    ),
                    1f,
                    180f
                );
        }
    }

    private void DrawSpatialRules()
    {
        EditorGUILayout.LabelField(
            "Reglas espaciales",
            EditorStyles.boldLabel
        );

        draft.MinimumClearance =
            Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "Separación mínima",
                    draft.MinimumClearance
                )
            );

        EditorGUILayout.LabelField(
            "Capacidades requeridas"
        );

        for (int index = 0;
             index < draft.RequiredCapabilities.Count;
             index++)
        {
            EditorGUILayout.BeginHorizontal();

            draft.RequiredCapabilities[index] =
                (RestaurantAreaCapabilityDefinition)
                EditorGUILayout.ObjectField(
                    "Capacidad " +
                    (index + 1),
                    draft.RequiredCapabilities[index],
                    typeof(
                        RestaurantAreaCapabilityDefinition
                    ),
                    false
                );

            if (GUILayout.Button(
                    "Quitar",
                    GUILayout.Width(60f)
                ))
            {
                draft.RequiredCapabilities.RemoveAt(
                    index
                );

                EditorGUILayout.EndHorizontal();
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button(
                "Añadir capacidad"
            ))
        {
            draft.RequiredCapabilities.Add(null);
        }
    }

    private void DrawDraftPreview()
    {
        EditorGUILayout.LabelField(
            "Simulación previa",
            EditorStyles.boldLabel
        );

        if (draftPreview.Count == 0)
        {
            EditorGUILayout.LabelField(
                "Pulsa Previsualizar cambios antes de aplicar."
            );

            return;
        }

        for (int index = 0;
             index < draftPreview.Count;
             index++)
        {
            EditorGUILayout.LabelField(
                "• " +
                draftPreview[index],
                EditorStyles.wordWrappedLabel
            );
        }
    }

    private void DrawDraftActions()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Previsualizar cambios",
                GUILayout.Height(34f)
            ))
        {
            RefreshDraftPreview();
        }

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling ||
                   EditorApplication.isUpdating
               ))
        {
            if (GUILayout.Button(
                    "Aplicar con rollback",
                    GUILayout.Height(34f)
                ))
            {
                ApplyDraft();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void AnalyzeProject()
    {
        report =
            BistroBuilderPlaceableMaintenanceService
                .AnalyzeProject();

        Repaint();
    }

    private void RepairProject()
    {
        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se repararán únicamente incidencias seguras y se " +
                "generarán miniaturas faltantes.\n\n" +
                "No se cambiará ningún ItemId ni icono manual.",
                "Reparar",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        BistroBuilderPlaceableMaintenanceResult result =
            BistroBuilderPlaceableMaintenanceService
                .RepairSafeIssuesAndGenerateMissingThumbnails();

        ShowOperationResult(
            "Mantenimiento por lotes",
            result.BuildSummary(),
            result.Messages
        );

        AnalyzeProject();
    }

    private void GenerateMissingThumbnails()
    {
        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        int missingCount =
            definitions.Count(
                definition =>
                    definition != null &&
                    definition.CatalogIcon == null
            );

        if (missingCount == 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Todos los artículos tienen miniatura.",
                "Aceptar"
            );

            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se generarán " +
                missingCount +
                " miniatura(s) faltante(s).",
                "Generar",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        BistroBuilderCatalogThumbnailService
            .ThumbnailBatchResult result =
                BistroBuilderCatalogThumbnailService
                    .GenerateBatch(
                        definitions,
                        true,
                        false
                    );

        ShowOperationResult(
            "Miniaturas faltantes",
            result.BuildSummary(),
            result.Messages
        );

        AnalyzeProject();
        ScheduleProjectHealth();
    }

    private void RegenerateManagedThumbnails()
    {
        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        List<RestaurantPlaceableItemDefinition> managed =
            definitions
                .Where(
                    definition =>
                        definition != null &&
                        BistroBuilderCatalogThumbnailService
                            .IsGeneratedIcon(
                                definition.CatalogIcon
                            )
                )
                .ToList();

        if (managed.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No hay miniaturas administradas para regenerar.",
                "Aceptar"
            );

            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se regenerarán " +
                managed.Count +
                " miniatura(s) administrada(s).\n\n" +
                "Los iconos manuales no se modificarán.",
                "Regenerar",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        BistroBuilderCatalogThumbnailService
            .ThumbnailBatchResult result =
                BistroBuilderCatalogThumbnailService
                    .GenerateBatch(
                        managed,
                        false,
                        false
                    );

        ShowOperationResult(
            "Regenerar miniaturas",
            result.BuildSummary(),
            result.Messages
        );

        AnalyzeProject();
        ScheduleProjectHealth();
    }

    private void ApplyDraft()
    {
        RefreshDraftPreview();

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se aplicarán los cambios previsualizados a:\n\n" +
                draft.Definition.DisplayName +
                "\n\nLos archivos se restaurarán si falla una fase.",
                "Aplicar",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        bool succeeded =
            BistroBuilderPlaceableMaintenanceService
                .TryApplyDraft(
                    draft,
                    out string message
                );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            message,
            "Aceptar"
        );

        if (succeeded)
        {
            LoadDefinition(
                draft.Definition
            );

            AnalyzeProject();
        }
    }

    private void RefreshDraftPreview()
    {
        draftPreview.Clear();

        draftPreview.AddRange(
            BistroBuilderPlaceableMaintenanceService
                .PreviewDraftChanges(draft)
        );
    }

    private void LoadDefinition(
        RestaurantPlaceableItemDefinition definition
    )
    {
        selectedDefinition = definition;

        draft =
            BistroBuilderPlaceableMaintenanceService
                .CreateDraft(definition);

        draftPreview.Clear();

        Repaint();
    }

    private void SynchronizeSelection()
    {
        RestaurantPlaceableItemDefinition resolved =
            BistroBuilderPlaceableMaintenanceService
                .ResolveDefinitionFromSelection();

        if (resolved != null)
        {
            LoadDefinition(resolved);
        }
        else if (selectedDefinition != null &&
                 draft == null)
        {
            LoadDefinition(
                selectedDefinition
            );
        }
    }

    private static void ShowOperationResult(
        string title,
        string summary,
        IReadOnlyList<string> messages
    )
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(summary);

        for (int index = 0;
             index < messages.Count;
             index++)
        {
            builder.AppendLine();
            builder.AppendLine(messages[index]);
        }

        Debug.Log(
            "BISTRO BUILDER - " +
            title.ToUpperInvariant() +
            "\n" +
            builder
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            summary +
            "\n\nConsulta Console para ver el detalle.",
            "Aceptar"
        );
    }

    private static MessageType ResolveMessageType(
        BistroBuilderPlaceableMaintenanceSeverity severity
    )
    {
        switch (severity)
        {
            case BistroBuilderPlaceableMaintenanceSeverity.Blocker:
            case BistroBuilderPlaceableMaintenanceSeverity.Error:
                return MessageType.Error;

            case BistroBuilderPlaceableMaintenanceSeverity.Warning:
                return MessageType.Warning;

            case BistroBuilderPlaceableMaintenanceSeverity.Info:
                return MessageType.Info;

            default:
                return MessageType.None;
        }
    }

    private static void ScheduleProjectHealth()
    {
        EditorApplication.delayCall +=
            RunProjectHealthWhenReady;
    }

    private static void RunProjectHealthWhenReady()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall +=
                RunProjectHealthWhenReady;

            return;
        }

        BistroBuilderValidationReport report =
            BistroBuilderProjectValidator
                .RunFullValidation(true);

        BistroBuilderProjectHealthWindow.SetReport(
            report
        );
    }
}
