using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Origen de una actualización de telemetría de platos firma.
/// </summary>
public enum BistroBuilderSignatureDishTelemetryChangeType
{
    SelectionRecorded = 0,
    OrderObserved = 1,
    ConsumptionObserved = 2,
    StateRestored = 3,
    Reset = 4
}

public readonly struct BistroBuilderSignatureDishTelemetryChangedEvent
{
    public BistroBuilderSignatureDishTelemetryChangeType ChangeType { get; }

    /// <summary>
    /// DishId para una selección y LineId para pedido/consumo. Puede quedar
    /// vacío en restauraciones o sincronizaciones consolidadas.
    /// </summary>
    public string SubjectId { get; }

    public int Revision { get; }

    public BistroBuilderSignatureDishTelemetryChangedEvent(
        BistroBuilderSignatureDishTelemetryChangeType changeType,
        string subjectId,
        int revision
    )
    {
        ChangeType = changeType;
        SubjectId = subjectId ?? string.Empty;
        Revision = Math.Max(0, revision);
    }
}

/// <summary>
/// Fotografía neutral y versionada de métricas 2.1D.
///
/// Todavía no constituye una sección independiente de guardado. El contrato
/// queda preparado para que 2.1F lo integre sin cambiar la semántica ni perder
/// idempotencia al restaurar comandas activas.
/// </summary>
[Serializable]
public sealed class BistroBuilderSignatureDishTelemetrySnapshot
{
    public const int CurrentSchemaVersion = 1;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private long totalSelections;

    [SerializeField]
    private long signatureSelections;

    [SerializeField]
    private long totalOrderedLines;

    [SerializeField]
    private long signatureOrderedLines;

    [SerializeField]
    private long totalConsumedLines;

    [SerializeField]
    private long signatureConsumedLines;

    [SerializeField]
    private List<string> observedOrderLineIds = new List<string>();

    [SerializeField]
    private List<string> observedConsumedLineIds = new List<string>();

    public int SchemaVersion => schemaVersion;
    public long TotalSelections => totalSelections;
    public long SignatureSelections => signatureSelections;
    public long TotalOrderedLines => totalOrderedLines;
    public long SignatureOrderedLines => signatureOrderedLines;
    public long TotalConsumedLines => totalConsumedLines;
    public long SignatureConsumedLines => signatureConsumedLines;
    public IReadOnlyList<string> ObservedOrderLineIds => observedOrderLineIds;
    public IReadOnlyList<string> ObservedConsumedLineIds =>
        observedConsumedLineIds;

    public BistroBuilderSignatureDishTelemetrySnapshot(
        long totalSelections,
        long signatureSelections,
        long totalOrderedLines,
        long signatureOrderedLines,
        long totalConsumedLines,
        long signatureConsumedLines,
        IList<string> observedOrderLineIds,
        IList<string> observedConsumedLineIds
    )
    {
        this.totalSelections = totalSelections;
        this.signatureSelections = signatureSelections;
        this.totalOrderedLines = totalOrderedLines;
        this.signatureOrderedLines = signatureOrderedLines;
        this.totalConsumedLines = totalConsumedLines;
        this.signatureConsumedLines = signatureConsumedLines;
        this.observedOrderLineIds = CopyIds(observedOrderLineIds);
        this.observedConsumedLineIds = CopyIds(observedConsumedLineIds);
    }

    private BistroBuilderSignatureDishTelemetrySnapshot()
    {
    }

    public BistroBuilderSignatureDishTelemetrySnapshot Clone()
    {
        return new BistroBuilderSignatureDishTelemetrySnapshot(
            totalSelections,
            signatureSelections,
            totalOrderedLines,
            signatureOrderedLines,
            totalConsumedLines,
            signatureConsumedLines,
            observedOrderLineIds,
            observedConsumedLineIds
        );
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            error = "La versión de telemetría 2.1D no es compatible.";
            return false;
        }

        if (totalSelections < 0L || signatureSelections < 0L ||
            totalOrderedLines < 0L || signatureOrderedLines < 0L ||
            totalConsumedLines < 0L || signatureConsumedLines < 0L)
        {
            error = "La telemetría contiene contadores negativos.";
            return false;
        }

        if (signatureSelections > totalSelections ||
            signatureOrderedLines > totalOrderedLines ||
            signatureConsumedLines > totalConsumedLines ||
            totalConsumedLines > totalOrderedLines)
        {
            error = "Los contadores de telemetría no son coherentes.";
            return false;
        }

        if (observedOrderLineIds == null ||
            observedConsumedLineIds == null)
        {
            error = "La telemetría contiene colecciones nulas.";
            return false;
        }

        HashSet<string> ordered =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < observedOrderLineIds.Count; index++)
        {
            string lineId = BistroBuilderOrderIdUtility.Normalize(
                observedOrderLineIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(lineId) ||
                !ordered.Add(lineId))
            {
                error = "La telemetría contiene un LineId ordenado inválido " +
                        "o duplicado.";
                return false;
            }
        }

        HashSet<string> consumed =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < observedConsumedLineIds.Count; index++)
        {
            string lineId = BistroBuilderOrderIdUtility.Normalize(
                observedConsumedLineIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(lineId) ||
                !consumed.Add(lineId) ||
                !ordered.Contains(lineId))
            {
                error = "La telemetría contiene un LineId consumido inválido, " +
                        "duplicado o no observado como pedido.";
                return false;
            }
        }

        if (totalOrderedLines < observedOrderLineIds.Count ||
            totalConsumedLines < observedConsumedLineIds.Count)
        {
            error = "Los conjuntos idempotentes superan sus contadores.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static List<string> CopyIds(IList<string> source)
    {
        List<string> result = new List<string>(
            source != null ? source.Count : 0
        );

        if (source != null)
        {
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(BistroBuilderOrderIdUtility.Normalize(source[index]));
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
