using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Nivel de gravedad de un hallazgo del validador técnico.
/// </summary>
public enum BistroBuilderValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Blocker = 3
}

/// <summary>
/// Hallazgo individual producido por el validador.
/// </summary>
public sealed class BistroBuilderValidationIssue
{
    public BistroBuilderValidationSeverity Severity
    {
        get;
    }

    public string Code
    {
        get;
    }

    public string Category
    {
        get;
    }

    public string Message
    {
        get;
    }

    public string Details
    {
        get;
    }

    public UnityEngine.Object Context
    {
        get;
    }

    public string AssetPath
    {
        get;
    }

    public bool BlocksValidatedPlay
    {
        get
        {
            return Severity == BistroBuilderValidationSeverity.Error ||
                   Severity == BistroBuilderValidationSeverity.Blocker;
        }
    }

    public BistroBuilderValidationIssue(
        BistroBuilderValidationSeverity severity,
        string code,
        string category,
        string message,
        string details,
        UnityEngine.Object context,
        string assetPath
    )
    {
        Severity =
            severity;

        Code =
            string.IsNullOrWhiteSpace(code)
                ? "BB-UNSPECIFIED"
                : code.Trim();

        Category =
            string.IsNullOrWhiteSpace(category)
                ? "General"
                : category.Trim();

        Message =
            string.IsNullOrWhiteSpace(message)
                ? "Hallazgo sin descripción."
                : message.Trim();

        Details =
            string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : details.Trim();

        Context =
            context;

        AssetPath =
            string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Trim();
    }
}

/// <summary>
/// Resultado agregado de una ejecución del validador.
/// </summary>
public sealed class BistroBuilderValidationReport
{
    private readonly List<BistroBuilderValidationIssue> issues =
        new List<BistroBuilderValidationIssue>(128);

    public DateTime GeneratedAtUtc
    {
        get;
    }

    public string ValidationScope
    {
        get;
    }

    public string ActiveScenePath
    {
        get;
    }

    public IReadOnlyList<BistroBuilderValidationIssue> Issues
    {
        get
        {
            return issues;
        }
    }

    public int IssueCount
    {
        get
        {
            return issues.Count;
        }
    }

    public int InfoCount
    {
        get;
        private set;
    }

    public int WarningCount
    {
        get;
        private set;
    }

    public int ErrorCount
    {
        get;
        private set;
    }

    public int BlockerCount
    {
        get;
        private set;
    }

    public bool HasBlockingProblems
    {
        get
        {
            return ErrorCount > 0 ||
                   BlockerCount > 0;
        }
    }

    public bool IsClean
    {
        get
        {
            return BlockerCount == 0 &&
                   ErrorCount == 0 &&
                   WarningCount == 0;
        }
    }

    public BistroBuilderValidationReport(
        string validationScope,
        string activeScenePath
    )
    {
        GeneratedAtUtc =
            DateTime.UtcNow;

        ValidationScope =
            string.IsNullOrWhiteSpace(validationScope)
                ? "Validación"
                : validationScope.Trim();

        ActiveScenePath =
            string.IsNullOrWhiteSpace(activeScenePath)
                ? string.Empty
                : activeScenePath.Trim();
    }

    public void Add(
        BistroBuilderValidationIssue issue
    )
    {
        if (issue == null)
        {
            return;
        }

        issues.Add(issue);

        switch (issue.Severity)
        {
            case BistroBuilderValidationSeverity.Info:
                InfoCount++;
                break;

            case BistroBuilderValidationSeverity.Warning:
                WarningCount++;
                break;

            case BistroBuilderValidationSeverity.Error:
                ErrorCount++;
                break;

            case BistroBuilderValidationSeverity.Blocker:
                BlockerCount++;
                break;
        }
    }

    public void Add(
        BistroBuilderValidationSeverity severity,
        string code,
        string category,
        string message,
        string details = "",
        UnityEngine.Object context = null,
        string assetPath = ""
    )
    {
        Add(
            new BistroBuilderValidationIssue(
                severity,
                code,
                category,
                message,
                details,
                context,
                assetPath
            )
        );
    }

    public void Sort()
    {
        issues.Sort(
            CompareIssues
        );
    }

    public string BuildPlainText()
    {
        StringBuilder builder =
            new StringBuilder(8192);

        builder.AppendLine(
            "BISTRO BUILDER — INFORME DE VALIDACIÓN"
        );

        builder.AppendLine(
            "======================================"
        );

        builder.AppendLine(
            "Ámbito: " + ValidationScope
        );

        builder.AppendLine(
            "Generado: " +
            GeneratedAtUtc.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss")
        );

        if (!string.IsNullOrWhiteSpace(ActiveScenePath))
        {
            builder.AppendLine(
                "Escena: " + ActiveScenePath
            );
        }

        builder.AppendLine(
            "Unity: " + Application.unityVersion
        );

        builder.AppendLine();

        builder.AppendLine(
            "Bloqueantes: " + BlockerCount
        );

        builder.AppendLine(
            "Errores: " + ErrorCount
        );

        builder.AppendLine(
            "Advertencias: " + WarningCount
        );

        builder.AppendLine(
            "Información: " + InfoCount
        );

        builder.AppendLine();

        if (issues.Count == 0)
        {
            builder.AppendLine(
                "No se han detectado incidencias."
            );

            return builder.ToString();
        }

        for (int index = 0;
             index < issues.Count;
             index++)
        {
            BistroBuilderValidationIssue issue =
                issues[index];

            builder.Append('[');
            builder.Append(issue.Severity);
            builder.Append("] ");
            builder.Append(issue.Code);
            builder.Append(" — ");
            builder.AppendLine(issue.Message);

            builder.AppendLine(
                "  Categoría: " + issue.Category
            );

            if (!string.IsNullOrWhiteSpace(issue.AssetPath))
            {
                builder.AppendLine(
                    "  Ruta: " + issue.AssetPath
                );
            }

            if (issue.Context != null)
            {
                builder.AppendLine(
                    "  Contexto: " + issue.Context.name
                );
            }

            if (!string.IsNullOrWhiteSpace(issue.Details))
            {
                builder.AppendLine(
                    "  Detalle: " + issue.Details
                );
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static int CompareIssues(
        BistroBuilderValidationIssue first,
        BistroBuilderValidationIssue second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int severityComparison =
            second.Severity.CompareTo(
                first.Severity
            );

        if (severityComparison != 0)
        {
            return severityComparison;
        }

        int categoryComparison =
            string.Compare(
                first.Category,
                second.Category,
                StringComparison.CurrentCultureIgnoreCase
            );

        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        return string.Compare(
            first.Code,
            second.Code,
            StringComparison.Ordinal
        );
    }
}
