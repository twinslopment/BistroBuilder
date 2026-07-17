using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Panel visual de salud técnica para Bistro Builder.
///
/// Permite validar, filtrar, seleccionar contextos, copiar o exportar
/// el informe y entrar en Play únicamente cuando no hay errores.
/// </summary>
public sealed class BistroBuilderProjectHealthWindow :
    EditorWindow
{
    private const string WindowTitle =
        "Bistro Builder Health";

    private static BistroBuilderValidationReport lastReport;

    private Vector2 scrollPosition;

    private bool showBlockers = true;
    private bool showErrors = true;
    private bool showWarnings = true;
    private bool showInformation;

    private GUIStyle severityStyle;
    private GUIStyle messageStyle;
    private GUIStyle detailsStyle;
    private GUIStyle summaryNumberStyle;

    public static BistroBuilderValidationReport LastReport
    {
        get
        {
            return lastReport;
        }
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Open Project Health",
        false,
        300
    )]
    public static void OpenWindow()
    {
        BistroBuilderProjectHealthWindow window =
            GetWindow<
                BistroBuilderProjectHealthWindow
            >();

        window.titleContent =
            new GUIContent(
                WindowTitle
            );

        window.minSize =
            new Vector2(720f, 420f);

        window.Show();
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Run Full Validation",
        false,
        301
    )]
    private static void RunFullValidationMenu()
    {
        lastReport =
            BistroBuilderProjectValidator
                .RunFullValidation(true);

        OpenWindow();
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Run Scene Preflight",
        false,
        302
    )]
    private static void RunScenePreflightMenu()
    {
        lastReport =
            BistroBuilderProjectValidator
                .RunScenePreflight(true);

        OpenWindow();
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Validate and Enter Play",
        false,
        303
    )]
    private static void ValidateAndEnterPlayMenu()
    {
        ValidateAndEnterPlay();
    }

    public static void SetReport(
        BistroBuilderValidationReport report
    )
    {
        lastReport =
            report;

        OpenWindow();
    }

    public static void ValidateAndEnterPlay()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Unity está compilando. Espera a que termine antes " +
                "de iniciar Play.",
                "Aceptar"
            );

            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Unity ya está entrando o se encuentra en Play.",
                "Aceptar"
            );

            return;
        }

        lastReport =
            BistroBuilderProjectValidator
                .RunScenePreflight(true);

        if (lastReport.HasBlockingProblems)
        {
            OpenWindow();

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Play cancelado.\n\n" +
                "Bloqueantes: " +
                lastReport.BlockerCount +
                "\nErrores: " +
                lastReport.ErrorCount +
                "\n\nCorrige los problemas mostrados en " +
                "Project Health.",
                "Aceptar"
            );

            return;
        }

        if (lastReport.WarningCount > 0)
        {
            bool continueWithWarnings =
                EditorUtility.DisplayDialog(
                    "Bistro Builder",
                    "La validación no contiene errores, pero ha " +
                    "detectado " +
                    lastReport.WarningCount +
                    " advertencia(s).\n\n" +
                    "¿Entrar en Play de todas formas?",
                    "Entrar en Play",
                    "Cancelar"
                );

            if (!continueWithWarnings)
            {
                OpenWindow();
                return;
            }
        }

        EditorApplication.isPlaying =
            true;
    }

    private void OnEnable()
    {
        titleContent =
            new GUIContent(
                WindowTitle
            );
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawToolbar();

        EditorGUILayout.Space(6f);

        if (lastReport == null)
        {
            DrawEmptyState();
            return;
        }

        DrawSummary(
            lastReport
        );

        EditorGUILayout.Space(8f);

        DrawFilters();

        EditorGUILayout.Space(6f);

        DrawIssues(
            lastReport
        );
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(
                   EditorStyles.toolbar
               ))
        {
            if (GUILayout.Button(
                    "Validar todo",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(95f)
                ))
            {
                lastReport =
                    BistroBuilderProjectValidator
                        .RunFullValidation(true);
            }

            if (GUILayout.Button(
                    "Preflight escena",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(115f)
                ))
            {
                lastReport =
                    BistroBuilderProjectValidator
                        .RunScenePreflight(true);
            }

            if (GUILayout.Button(
                    "Validar y Play",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(105f)
                ))
            {
                ValidateAndEnterPlay();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(
                       lastReport == null
                   ))
            {
                if (GUILayout.Button(
                        "Copiar",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(65f)
                    ))
                {
                    CopyReport();
                }

                if (GUILayout.Button(
                        "Exportar",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(70f)
                    ))
                {
                    ExportReport();
                }
            }
        }
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.HelpBox(
            "Todavía no se ha ejecutado ninguna validación.\n\n" +
            "Pulsa «Validar todo» para revisar escena, servicios, " +
            "áreas, artículos, prefabs, catálogo, UI y configuración.",
            MessageType.Info
        );

        GUILayout.Space(10f);

        if (GUILayout.Button(
                "Ejecutar validación completa",
                GUILayout.Height(34f)
            ))
        {
            lastReport =
                BistroBuilderProjectValidator
                    .RunFullValidation(true);
        }
    }

    private void DrawSummary(
        BistroBuilderValidationReport report
    )
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox
               ))
        {
            EditorGUILayout.LabelField(
                report.ValidationScope,
                EditorStyles.boldLabel
            );

            if (!string.IsNullOrWhiteSpace(
                    report.ActiveScenePath
                ))
            {
                EditorGUILayout.LabelField(
                    "Escena",
                    report.ActiveScenePath
                );
            }

            EditorGUILayout.LabelField(
                "Generado",
                report.GeneratedAtUtc
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss")
            );

            EditorGUILayout.Space(5f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryCounter(
                    "Bloqueantes",
                    report.BlockerCount,
                    new Color(0.72f, 0.16f, 0.16f)
                );

                DrawSummaryCounter(
                    "Errores",
                    report.ErrorCount,
                    new Color(0.86f, 0.32f, 0.18f)
                );

                DrawSummaryCounter(
                    "Advertencias",
                    report.WarningCount,
                    new Color(0.88f, 0.65f, 0.12f)
                );

                DrawSummaryCounter(
                    "Información",
                    report.InfoCount,
                    new Color(0.28f, 0.56f, 0.78f)
                );
            }

            EditorGUILayout.Space(5f);

            MessageType summaryType;

            string summaryMessage;

            if (report.HasBlockingProblems)
            {
                summaryType =
                    MessageType.Error;

                summaryMessage =
                    "El proyecto no supera el preflight. " +
                    "«Validar y Play» bloqueará la ejecución.";
            }
            else if (report.WarningCount > 0)
            {
                summaryType =
                    MessageType.Warning;

                summaryMessage =
                    "No hay errores bloqueantes. Revisa las " +
                    "advertencias antes de consolidar el hito.";
            }
            else
            {
                summaryType =
                    MessageType.Info;

                summaryMessage =
                    "La validación no ha detectado problemas que " +
                    "impidan entrar en Play.";
            }

            EditorGUILayout.HelpBox(
                summaryMessage,
                summaryType
            );
        }
    }

    private void DrawSummaryCounter(
        string label,
        int value,
        Color accentColor
    )
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox,
                   GUILayout.MinWidth(120f)
               ))
        {
            Color previousColor =
                GUI.color;

            GUI.color =
                accentColor;

            EditorGUILayout.LabelField(
                value.ToString(),
                summaryNumberStyle,
                GUILayout.Height(24f)
            );

            GUI.color =
                previousColor;

            EditorGUILayout.LabelField(
                label,
                EditorStyles.centeredGreyMiniLabel
            );
        }
    }

    private void DrawFilters()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                "Mostrar",
                GUILayout.Width(55f)
            );

            showBlockers =
                GUILayout.Toggle(
                    showBlockers,
                    "Bloqueantes",
                    EditorStyles.miniButtonLeft
                );

            showErrors =
                GUILayout.Toggle(
                    showErrors,
                    "Errores",
                    EditorStyles.miniButtonMid
                );

            showWarnings =
                GUILayout.Toggle(
                    showWarnings,
                    "Advertencias",
                    EditorStyles.miniButtonMid
                );

            showInformation =
                GUILayout.Toggle(
                    showInformation,
                    "Información",
                    EditorStyles.miniButtonRight
                );

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawIssues(
        BistroBuilderValidationReport report
    )
    {
        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition
            );

        int visibleIssueCount =
            0;

        for (int index = 0;
             index < report.Issues.Count;
             index++)
        {
            BistroBuilderValidationIssue issue =
                report.Issues[index];

            if (!ShouldShow(issue.Severity))
            {
                continue;
            }

            visibleIssueCount++;

            DrawIssue(
                issue
            );

            GUILayout.Space(4f);
        }

        if (visibleIssueCount == 0)
        {
            EditorGUILayout.HelpBox(
                "No hay incidencias visibles con los filtros actuales.",
                MessageType.Info
            );
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawIssue(
        BistroBuilderValidationIssue issue
    )
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox
               ))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color previousColor =
                    GUI.color;

                GUI.color =
                    ResolveSeverityColor(
                        issue.Severity
                    );

                EditorGUILayout.LabelField(
                    issue.Severity.ToString(),
                    severityStyle,
                    GUILayout.Width(88f)
                );

                GUI.color =
                    previousColor;

                EditorGUILayout.LabelField(
                    issue.Code,
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(115f)
                );

                EditorGUILayout.LabelField(
                    issue.Category,
                    EditorStyles.miniLabel
                );

                GUILayout.FlexibleSpace();

                if (issue.Context != null ||
                    !string.IsNullOrWhiteSpace(
                        issue.AssetPath
                    ))
                {
                    if (GUILayout.Button(
                            "Localizar",
                            GUILayout.Width(72f)
                        ))
                    {
                        FocusIssue(
                            issue
                        );
                    }
                }
            }

            EditorGUILayout.LabelField(
                issue.Message,
                messageStyle
            );

            if (!string.IsNullOrWhiteSpace(issue.Details))
            {
                EditorGUILayout.LabelField(
                    issue.Details,
                    detailsStyle
                );
            }

            if (!string.IsNullOrWhiteSpace(issue.AssetPath))
            {
                EditorGUILayout.SelectableLabel(
                    issue.AssetPath,
                    EditorStyles.miniLabel,
                    GUILayout.Height(18f)
                );
            }
        }
    }

    private bool ShouldShow(
        BistroBuilderValidationSeverity severity
    )
    {
        switch (severity)
        {
            case BistroBuilderValidationSeverity.Blocker:
                return showBlockers;

            case BistroBuilderValidationSeverity.Error:
                return showErrors;

            case BistroBuilderValidationSeverity.Warning:
                return showWarnings;

            default:
                return showInformation;
        }
    }

    private static Color ResolveSeverityColor(
        BistroBuilderValidationSeverity severity
    )
    {
        switch (severity)
        {
            case BistroBuilderValidationSeverity.Blocker:
                return new Color(0.92f, 0.22f, 0.22f);

            case BistroBuilderValidationSeverity.Error:
                return new Color(1f, 0.45f, 0.24f);

            case BistroBuilderValidationSeverity.Warning:
                return new Color(1f, 0.74f, 0.2f);

            default:
                return new Color(0.35f, 0.7f, 1f);
        }
    }

    private void EnsureStyles()
    {
        if (severityStyle == null)
        {
            severityStyle =
                new GUIStyle(
                    EditorStyles.boldLabel
                );

            severityStyle.alignment =
                TextAnchor.MiddleLeft;
        }

        if (messageStyle == null)
        {
            messageStyle =
                new GUIStyle(
                    EditorStyles.label
                );

            messageStyle.fontStyle =
                FontStyle.Bold;

            messageStyle.wordWrap =
                true;
        }

        if (detailsStyle == null)
        {
            detailsStyle =
                new GUIStyle(
                    EditorStyles.wordWrappedMiniLabel
                );

            detailsStyle.wordWrap =
                true;

            detailsStyle.margin =
                new RectOffset(4, 4, 2, 2);
        }

        if (summaryNumberStyle == null)
        {
            summaryNumberStyle =
                new GUIStyle(
                    EditorStyles.boldLabel
                );

            summaryNumberStyle.alignment =
                TextAnchor.MiddleCenter;

            summaryNumberStyle.fontSize =
                18;
        }
    }

    private static void FocusIssue(
        BistroBuilderValidationIssue issue
    )
    {
        UnityEngine.Object target =
            issue.Context;

        if (target == null &&
            !string.IsNullOrWhiteSpace(
                issue.AssetPath
            ))
        {
            target =
                AssetDatabase.LoadMainAssetAtPath(
                    issue.AssetPath
                );
        }

        if (target == null)
        {
            return;
        }

        Selection.activeObject =
            target;

        EditorGUIUtility.PingObject(
            target
        );
    }

    private static void CopyReport()
    {
        if (lastReport == null)
        {
            return;
        }

        EditorGUIUtility.systemCopyBuffer =
            lastReport.BuildPlainText();

        Debug.Log(
            "Informe de Bistro Builder copiado al portapapeles."
        );
    }

    private static void ExportReport()
    {
        if (lastReport == null)
        {
            return;
        }

        string defaultFileName =
            "BistroBuilder_Validation_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".txt";

        string targetPath =
            EditorUtility.SaveFilePanel(
                "Exportar informe de Bistro Builder",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop
                ),
                defaultFileName,
                "txt"
            );

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        File.WriteAllText(
            targetPath,
            lastReport.BuildPlainText()
        );

        EditorUtility.RevealInFinder(
            targetPath
        );
    }
}
