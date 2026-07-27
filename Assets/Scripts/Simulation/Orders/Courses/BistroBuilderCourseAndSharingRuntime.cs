using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proyección persistible del avance de pases de una comanda activa.
///
/// Los estados de línea canónicos siguen siendo la autoridad. Esta proyección
/// conserva decisiones de coordinación y facilita depuración/restauración.
/// </summary>
[Serializable]
public sealed class BistroBuilderCourseOrderRuntime
{
    [SerializeField]
    private string orderId;

    [SerializeField]
    private int legacyOrderId;

    [SerializeField]
    private BistroBuilderCourseCoordinationPolicy coordinationPolicy;

    [SerializeField]
    private int initialCourseIndex;

    [SerializeField]
    private List<int> releasedCourseIndices = new List<int>();

    [SerializeField]
    private List<string> releasedLineIds = new List<string>();

    [SerializeField]
    private int revision;

    public string OrderId => orderId ?? string.Empty;
    public int LegacyOrderId => legacyOrderId;
    public BistroBuilderCourseCoordinationPolicy CoordinationPolicy =>
        coordinationPolicy;
    public int InitialCourseIndex => initialCourseIndex;
    public IReadOnlyList<int> ReleasedCourseIndices => releasedCourseIndices;
    public IReadOnlyList<string> ReleasedLineIds => releasedLineIds;
    public int Revision => revision;

    internal BistroBuilderCourseOrderRuntime(
        string orderId,
        int legacyOrderId,
        BistroBuilderCourseCoordinationPolicy coordinationPolicy,
        int initialCourseIndex
    )
    {
        this.orderId = BistroBuilderOrderIdUtility.Normalize(orderId);
        this.legacyOrderId = legacyOrderId;
        this.coordinationPolicy = coordinationPolicy;
        this.initialCourseIndex = initialCourseIndex;
        releasedCourseIndices = new List<int>();
        releasedLineIds = new List<string>();
        revision = 0;
    }

    private BistroBuilderCourseOrderRuntime()
    {
    }

    public bool IsCourseReleased(int courseIndex)
    {
        return BistroBuilderCourseAndSharingPolicy.ContainsCourse(
            releasedCourseIndices,
            courseIndex
        );
    }

    public bool IsLineReleased(string lineId)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(lineId);

        for (int index = 0; index < releasedLineIds.Count; index++)
        {
            if (string.Equals(
                    releasedLineIds[index],
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }

    internal bool MarkLineReleased(string lineId, int courseIndex)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(lineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalized) ||
            !BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                courseIndex
            ))
        {
            return false;
        }

        bool changed = false;

        if (!IsLineReleased(normalized))
        {
            releasedLineIds.Add(normalized);
            releasedLineIds.Sort(StringComparer.Ordinal);
            changed = true;
        }

        if (!IsCourseReleased(courseIndex))
        {
            releasedCourseIndices.Add(courseIndex);
            releasedCourseIndices.Sort();
            changed = true;
        }

        if (changed)
        {
            revision++;
        }

        return changed;
    }

    internal void IncrementRevision()
    {
        revision++;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(OrderId))
        {
            error = "El runtime de pases contiene un OrderId inválido.";
            return false;
        }

        if (legacyOrderId < 1)
        {
            error = "El runtime de pases contiene un LegacyOrderId inválido.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                coordinationPolicy
            ))
        {
            error = "El runtime contiene una política de pases inválida.";
            return false;
        }

        if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                initialCourseIndex
            ))
        {
            error = "El runtime contiene un pase inicial inválido.";
            return false;
        }

        if (releasedCourseIndices == null || releasedLineIds == null)
        {
            error = "Las colecciones del runtime de pases son nulas.";
            return false;
        }

        HashSet<int> courses = new HashSet<int>();

        for (int index = 0; index < releasedCourseIndices.Count; index++)
        {
            int courseIndex = releasedCourseIndices[index];

            if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                    courseIndex
                ) ||
                !courses.Add(courseIndex))
            {
                error = "El runtime contiene un pase liberado inválido o duplicado.";
                return false;
            }
        }

        HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < releasedLineIds.Count; index++)
        {
            string lineId = BistroBuilderOrderIdUtility.Normalize(
                releasedLineIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(lineId) ||
                !lines.Add(lineId))
            {
                error = "El runtime contiene un LineId liberado inválido o duplicado.";
                return false;
            }

            releasedLineIds[index] = lineId;
        }

        if (revision < 0)
        {
            error = "La revisión del runtime de pases no es válida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal BistroBuilderCourseOrderRuntime Clone()
    {
        return new BistroBuilderCourseOrderRuntime
        {
            orderId = OrderId,
            legacyOrderId = legacyOrderId,
            coordinationPolicy = coordinationPolicy,
            initialCourseIndex = initialCourseIndex,
            releasedCourseIndices = new List<int>(releasedCourseIndices),
            releasedLineIds = new List<string>(releasedLineIds),
            revision = revision
        };
    }
}

/// <summary>
/// Snapshot versionado del runtime 367F.
/// </summary>
[Serializable]
public sealed class BistroBuilderCourseAndSharingRuntimeSnapshot
{
    public int schemaVersion = 1;
    public int revision;
    public List<BistroBuilderCourseOrderRuntime> orders =
        new List<BistroBuilderCourseOrderRuntime>();

    public bool TryValidate(out string error)
    {
        if (schemaVersion != 1)
        {
            error = "El snapshot 367F utiliza un esquema no soportado.";
            return false;
        }

        if (revision < 0 || orders == null)
        {
            error = "El snapshot 367F contiene datos básicos inválidos.";
            return false;
        }

        HashSet<string> orderIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < orders.Count; index++)
        {
            BistroBuilderCourseOrderRuntime runtime = orders[index];

            if (runtime == null)
            {
                error = "El snapshot 367F contiene una entrada runtime nula.";
                return false;
            }

            if (!runtime.TryValidate(out error))
            {
                return false;
            }

            if (!orderIds.Add(runtime.OrderId))
            {
                error = "El snapshot 367F contiene un OrderId duplicado.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
