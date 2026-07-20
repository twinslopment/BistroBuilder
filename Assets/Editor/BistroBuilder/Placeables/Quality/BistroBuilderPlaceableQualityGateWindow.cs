using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Informe unificado de la puerta de calidad de artículos.
/// </summary>
public sealed class BistroBuilderPlaceableQualityGateReport
{
    public DateTime GeneratedAtUtc =
        DateTime.UtcNow;

    public BistroBuilderValidationReport ProjectHealth;

    public BistroBuilderPlaceableMaintenanceReport Maintenance;

    public BistroBuilderThumbnailQualityBatchReport Thumbnails;

    public BistroBuilderPlaceablePipelineSelfTestResult SelfTest;

    public bool HasFailures
    {
        get
        {
            return
                ProjectHealth == null ||
                ProjectHealth.HasBlockingProblems ||
                Maintenance == null ||
                Maintenance.HasBlockingIssues ||
                Thumbnails == null ||
                Thumbnails.HasErrors ||
                SelfTest == null ||
                !SelfTest.Succeeded;
        }
    }

    public int WarningCount
    {
        get
        {
            int count = 0;

            if (ProjectHealth != null)
            {
                count +=
                    ProjectHealth.WarningCount;
            }

            if (Maintenance != null)
            {
                count +=
                    Maintenance.WarningCount;
            }

            if (Thumbnails != null)
            {
                count +=
                    Thumbnails.WarningCount;
            }

            return count;
        }
    }

    public string BuildPlainText()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "BISTRO BUILDER — PUERTA DE CALIDAD DE ARTÍCULOS"
        );

        builder.AppendLine(
            "=============================================="
        );

        builder.AppendLine(
            "Generado: " +
            GeneratedAtUtc.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss")
        );

        builder.AppendLine(
            "Resultado: " +
            (
                HasFailures
                    ? "NO SUPERADA"
                    : WarningCount > 0
                        ? "SUPERADA CON ADVERTENCIAS"
                        : "SUPERADA"
            )
        );

        builder.AppendLine();

        if (ProjectHealth != null)
        {
            builder.AppendLine(
                "Project Health: " +
                ProjectHealth.BlockerCount +
                " bloqueantes, " +
                ProjectHealth.ErrorCount +
                " errores, " +
                ProjectHealth.WarningCount +
                " advertencias."
            );
        }
        else
        {
            builder.AppendLine(
                "Project Health: no ejecutado."
            );
        }

        if (Maintenance != null)
        {
            builder.AppendLine(
                "Mantenimiento: " +
                Maintenance.BlockerCount +
                " bloqueantes, " +
                Maintenance.ErrorCount +
                " errores, " +
                Maintenance.WarningCount +
                " advertencias, " +
                Maintenance.AutoRepairableCount +
                " autorreparables."
            );
        }
        else
        {
            builder.AppendLine(
                "Mantenimiento: no ejecutado."
            );
        }

        if (Thumbnails != null)
        {
            builder.AppendLine(
                "Miniaturas: " +
                Thumbnails.GoodCount +
                " correctas, " +
                Thumbnails.WarningCount +
                " mejorables, " +
                Thumbnails.ErrorCount +
                " defectuosas, " +
                Thumbnails.MissingCount +
                " ausentes."
            );
        }
        else
        {
            builder.AppendLine(
                "Miniaturas: no auditadas."
            );
        }

        if (SelfTest != null)
        {
            builder.AppendLine(
                "Autotest: " +
                (
                    SelfTest.Succeeded
                        ? "correcto"
                        : "fallido"
                ) +
                ", limpieza " +
                (
                    SelfTest.CleanupSucceeded
                        ? "correcta"
                        : "fallida"
                ) +
                "."
            );
        }
        else
        {
            builder.AppendLine(
                "Autotest: no ejecutado."
            );
        }

        builder.AppendLine();

        if (Thumbnails != null &&
            Thumbnails.Messages.Count > 0)
        {
            builder.AppendLine(
                "Auditoría de miniaturas:"
            );

            for (int index = 0;
                 index < Thumbnails.Messages.Count;
                 index++)
            {
                builder.AppendLine(
                    "- " +
                    Thumbnails.Messages[index]
                );
            }

            builder.AppendLine();
        }

        if (SelfTest != null)
        {
            builder.AppendLine(
                SelfTest.BuildDetailedReport()
            );
        }

        return builder.ToString();
    }
}

/// <summary>
/// Ventana de una sola acción para validar toda la canalización de
/// artículos colocables.
/// </summary>
public sealed class
    BistroBuilderPlaceableQualityGateWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Placeables/" +
        "Run Full Placeable Quality Gate";

    private BistroBuilderPlaceableQualityGateReport report;

    private Vector2 scrollPosition;

    [MenuItem(MenuPath, false, 105)]
    public static void OpenAndRun()
    {
        BistroBuilderPlaceableQualityGateWindow window =
            GetWindow<
                BistroBuilderPlaceableQualityGateWindow
            >(
                "Bistro Builder Quality Gate"
            );

        window.minSize =
            new Vector2(
                680f,
                520f
            );

        window.Show();
        window.RunGate();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Puerta de calidad de artículos colocables",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Ejecuta Project Health, mantenimiento de solo lectura, " +
            "auditoría de miniaturas y una creación real dentro de un " +
            "sandbox temporal que se elimina al terminar.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isCompiling ||
                   EditorApplication.isUpdating ||
                   EditorApplication.isPlayingOrWillChangePlaymode
               ))
        {
            if (GUILayout.Button(
                    "Ejecutar puerta completa",
                    GUILayout.Height(32f)
                ))
            {
                RunGate();
            }

            if (GUILayout.Button(
                    "Mejorar miniaturas administradas",
                    GUILayout.Height(32f)
                ))
            {
                ImproveThumbnails();
            }
        }

        if (GUILayout.Button(
                "Copiar informe",
                GUILayout.Height(32f)
            ))
        {
            EditorGUIUtility.systemCopyBuffer =
                report != null
                    ? report.BuildPlainText()
                    : string.Empty;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);

        if (report == null)
        {
            EditorGUILayout.LabelField(
                "Todavía no hay informe."
            );

            return;
        }

        DrawSummary();

        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition
            );

        EditorGUILayout.TextArea(
            report.BuildPlainText(),
            GUILayout.ExpandHeight(true)
        );

        EditorGUILayout.EndScrollView();
    }

    private void RunGate()
    {
        if (!CanRun())
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Bistro Builder",
                "Ejecutando Project Health...",
                0.1f
            );

            BistroBuilderValidationReport projectHealth =
                BistroBuilderProjectValidator
                    .RunFullValidation(false);

            EditorUtility.DisplayProgressBar(
                "Bistro Builder",
                "Analizando artículos...",
                0.3f
            );

            BistroBuilderPlaceableMaintenanceReport maintenance =
                BistroBuilderPlaceableMaintenanceService
                    .AnalyzeProject();

            EditorUtility.DisplayProgressBar(
                "Bistro Builder",
                "Auditando miniaturas...",
                0.5f
            );

            BistroBuilderThumbnailQualityBatchReport thumbnails =
                BistroBuilderCatalogThumbnailQualityService
                    .AuditAllDefinitions();

            EditorUtility.DisplayProgressBar(
                "Bistro Builder",
                "Ejecutando autotest en sandbox...",
                0.7f
            );

            BistroBuilderPlaceablePipelineSelfTestResult selfTest =
                BistroBuilderPlaceablePipelineSelfTest
                    .Run();

            report =
                new BistroBuilderPlaceableQualityGateReport
                {
                    ProjectHealth = projectHealth,
                    Maintenance = maintenance,
                    Thumbnails = thumbnails,
                    SelfTest = selfTest
                };

            string plainText =
                report.BuildPlainText();

            if (report.HasFailures)
            {
                Debug.LogError(plainText);
            }
            else if (report.WarningCount > 0)
            {
                Debug.LogWarning(plainText);
            }
            else
            {
                Debug.Log(plainText);
            }

            Repaint();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La puerta de calidad no pudo completarse.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void ImproveThumbnails()
    {
        if (!CanRun())
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Bistro Builder",
                "Mejorando miniaturas...",
                0.4f
            );

            BistroBuilderCatalogThumbnailService
                .ThumbnailBatchResult result =
                    BistroBuilderCatalogThumbnailQualityService
                        .ImproveLowQualityManagedThumbnails();

            Debug.Log(
                "BISTRO BUILDER - MEJORA DE MINIATURAS\n" +
                result.BuildSummary() +
                "\n\n" +
                string.Join(
                    "\n",
                    result.Messages
                )
            );

            EditorUtility.ClearProgressBar();

            RunGate();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se pudieron mejorar las miniaturas.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private bool CanRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de ejecutar la puerta de calidad.",
                "Aceptar"
            );

            return false;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Unity está compilando o importando assets. " +
                "Espera a que termine.",
                "Aceptar"
            );

            return false;
        }

        return true;
    }

    private void DrawSummary()
    {
        MessageType messageType =
            report.HasFailures
                ? MessageType.Error
                : report.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;

        string summary =
            report.HasFailures
                ? "La puerta de calidad no se ha superado."
                : report.WarningCount > 0
                    ? "La puerta se supera con advertencias."
                    : "La puerta de calidad se supera sin incidencias.";

        EditorGUILayout.HelpBox(
            summary,
            messageType
        );

        EditorGUILayout.BeginHorizontal();

        DrawMetric(
            "Health",
            report.ProjectHealth != null
                ? report.ProjectHealth.BlockerCount +
                  report.ProjectHealth.ErrorCount
                : -1
        );

        DrawMetric(
            "Mantenimiento",
            report.Maintenance != null
                ? report.Maintenance.BlockerCount +
                  report.Maintenance.ErrorCount
                : -1
        );

        DrawMetric(
            "Miniaturas",
            report.Thumbnails != null
                ? report.Thumbnails.ErrorCount +
                  report.Thumbnails.MissingCount
                : -1
        );

        DrawMetric(
            "Autotest",
            report.SelfTest != null &&
            report.SelfTest.Succeeded
                ? 0
                : 1
        );

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawMetric(
        string label,
        int failures
    )
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox,
            GUILayout.MinWidth(120f)
        );

        EditorGUILayout.LabelField(
            label,
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            failures < 0
                ? "No ejecutado"
                : failures == 0
                    ? "Correcto"
                    : failures +
                      " incidencia(s)"
        );

        EditorGUILayout.EndVertical();
    }
}
