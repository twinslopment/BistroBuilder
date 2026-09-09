using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BistroBuilderFinanceChartMetric
{
    Revenue = 0,
    OperatingResult = 1,
    NetCash = 2
}

/// <summary>
/// Gráfico uGUI ligero para 3J. Dibuja barras de una única métrica financiera
/// sin Textures, LineRenderer ni dependencias externas. Para históricos largos
/// agrega como máximo 180 buckets, evitando miles de vértices en la UI.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Finance History Chart 3J")]
public sealed class BistroBuilderFinanceHistoryChartGraphic : MaskableGraphic
{
    private const int MaximumRenderedBuckets = 180;

    private readonly List<long> values = new List<long>(MaximumRenderedBuckets);

    [SerializeField] private BistroBuilderFinanceChartMetric metric =
        BistroBuilderFinanceChartMetric.Revenue;

    public BistroBuilderFinanceChartMetric Metric => metric;
    public int SourcePointCount { get; private set; }
    public int RenderedPointCount => values.Count;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Bind(
        IReadOnlyList<BistroBuilderDayFinancialResult> days,
        BistroBuilderFinanceChartMetric selectedMetric)
    {
        metric = selectedMetric;
        SourcePointCount = days != null ? days.Count : 0;
        values.Clear();

        if (days == null || days.Count == 0)
        {
            SetVerticesDirty();
            return;
        }

        int bucketCount = Mathf.Min(days.Count, MaximumRenderedBuckets);
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int start = (int)((long)bucket * days.Count / bucketCount);
            int endExclusive = (int)((long)(bucket + 1) * days.Count / bucketCount);
            if (endExclusive <= start)
            {
                endExclusive = start + 1;
            }

            // La UI nunca debe lanzar una excepción por sumar un histórico
            // extremo. Se acumula en decimal y se satura únicamente para el
            // gráfico; los valores contables canónicos no se modifican.
            decimal total = 0m;
            for (int index = start;
                 index < endExclusive && index < days.Count;
                 index++)
            {
                BistroBuilderDayFinancialResult day = days[index];
                if (day == null)
                {
                    continue;
                }
                total += ResolveValue(day, selectedMetric);
            }
            values.Add(ClampDecimalToLong(total));
        }

        SetVerticesDirty();
    }

    public void Clear()
    {
        SourcePointCount = 0;
        values.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (values.Count == 0 || rect.width < 4f || rect.height < 4f)
        {
            return;
        }

        long minimum = 0L;
        long maximum = 0L;
        for (int index = 0; index < values.Count; index++)
        {
            minimum = Math.Min(minimum, values[index]);
            maximum = Math.Max(maximum, values[index]);
        }

        if (minimum == 0L && maximum == 0L)
        {
            AddQuad(
                vertexHelper,
                new Rect(rect.xMin, rect.center.y - 0.5f, rect.width, 1f),
                new Color32(105, 112, 107, 90));
            return;
        }

        decimal range = (decimal)maximum - minimum;
        if (range <= 0m)
        {
            range = 1m;
        }

        float zeroNormalized = (float)((0m - minimum) / range);
        zeroNormalized = Mathf.Clamp01(zeroNormalized);
        float baselineY = Mathf.Lerp(rect.yMin + 5f, rect.yMax - 5f, zeroNormalized);

        Color32 grid = new Color32(110, 118, 112, 55);
        AddQuad(
            vertexHelper,
            new Rect(rect.xMin, baselineY - 0.5f, rect.width, 1f),
            grid);

        float step = rect.width / values.Count;
        float barWidth = Mathf.Clamp(step * 0.64f, 1.2f, 28f);

        for (int index = 0; index < values.Count; index++)
        {
            long value = values[index];
            float normalized = (float)(((decimal)value - minimum) / range);
            float valueY = Mathf.Lerp(
                rect.yMin + 5f,
                rect.yMax - 5f,
                Mathf.Clamp01(normalized));

            float xCenter = rect.xMin + step * (index + 0.5f);
            float yMin = Mathf.Min(baselineY, valueY);
            float yMax = Mathf.Max(baselineY, valueY);
            if (Mathf.Abs(yMax - yMin) < 1.2f)
            {
                yMax = yMin + 1.2f;
            }

            AddQuad(
                vertexHelper,
                new Rect(
                    xCenter - barWidth * 0.5f,
                    yMin,
                    barWidth,
                    yMax - yMin),
                ResolveColor(value));
        }
    }

    private Color32 ResolveColor(long value)
    {
        if (metric == BistroBuilderFinanceChartMetric.Revenue)
        {
            return new Color32(190, 149, 64, 230);
        }

        if (value < 0L)
        {
            return new Color32(193, 79, 75, 230);
        }

        if (metric == BistroBuilderFinanceChartMetric.NetCash)
        {
            return new Color32(86, 132, 166, 230);
        }

        return new Color32(78, 146, 100, 230);
    }

    private static long ResolveValue(
        BistroBuilderDayFinancialResult day,
        BistroBuilderFinanceChartMetric metric)
    {
        switch (metric)
        {
            case BistroBuilderFinanceChartMetric.OperatingResult:
                return day.operatingResultCents;
            case BistroBuilderFinanceChartMetric.NetCash:
                return day.netCashChangeCents;
            default:
                return day.revenueCents;
        }
    }

    private static long ClampDecimalToLong(decimal value)
    {
        if (value >= long.MaxValue)
        {
            return long.MaxValue;
        }
        if (value <= long.MinValue)
        {
            return long.MinValue;
        }
        return (long)value;
    }

    private static void AddQuad(
        VertexHelper vertexHelper,
        Rect rect,
        Color32 color)
    {
        int start = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
        vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
    }
}
