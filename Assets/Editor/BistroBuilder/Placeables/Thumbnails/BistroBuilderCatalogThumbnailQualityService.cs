using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resultado cualitativo de una miniatura.
/// </summary>
public enum BistroBuilderThumbnailQualityStatus
{
    Good = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Métricas de legibilidad de una miniatura de catálogo.
/// </summary>
public sealed class BistroBuilderThumbnailQualityReport
{
    public string SourceName
    {
        get;
    }

    public BistroBuilderThumbnailQualityStatus Status
    {
        get;
    }

    public float Score
    {
        get;
    }

    public float ForegroundCoverage
    {
        get;
    }

    public float EdgeOccupancy
    {
        get;
    }

    public float AverageLuminance
    {
        get;
    }

    public float Contrast
    {
        get;
    }

    public string Recommendation
    {
        get;
    }

    public BistroBuilderThumbnailQualityReport(
        string sourceName,
        BistroBuilderThumbnailQualityStatus status,
        float score,
        float foregroundCoverage,
        float edgeOccupancy,
        float averageLuminance,
        float contrast,
        string recommendation
    )
    {
        SourceName =
            sourceName ?? string.Empty;

        Status = status;
        Score = score;
        ForegroundCoverage = foregroundCoverage;
        EdgeOccupancy = edgeOccupancy;
        AverageLuminance = averageLuminance;
        Contrast = contrast;
        Recommendation =
            recommendation ?? string.Empty;
    }

    public string BuildCompactSummary()
    {
        return
            "Calidad " +
            Status +
            " " +
            Score.ToString("0.00") +
            " (ocupación " +
            (
                ForegroundCoverage *
                100f
            ).ToString("0.0") +
            "%, luminosidad " +
            AverageLuminance.ToString("0.00") +
            ").";
    }

    public string BuildDetailedSummary()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            string.IsNullOrWhiteSpace(SourceName)
                ? "Miniatura"
                : SourceName
        );

        builder.Append(
            ": " +
            Status +
            ", puntuación " +
            Score.ToString("0.00")
        );

        builder.Append(
            ", ocupación " +
            (
                ForegroundCoverage *
                100f
            ).ToString("0.0") +
            "%"
        );

        builder.Append(
            ", borde " +
            (
                EdgeOccupancy *
                100f
            ).ToString("0.0") +
            "%"
        );

        builder.Append(
            ", luminosidad " +
            AverageLuminance.ToString("0.00")
        );

        builder.Append(
            ", contraste " +
            Contrast.ToString("0.00") +
            "."
        );

        if (!string.IsNullOrWhiteSpace(
                Recommendation
            ))
        {
            builder.Append(
                " " +
                Recommendation
            );
        }

        return builder.ToString();
    }
}

/// <summary>
/// Informe agregado de auditoría de miniaturas administradas.
/// </summary>
public sealed class BistroBuilderThumbnailQualityBatchReport
{
    public int GoodCount;
    public int WarningCount;
    public int ErrorCount;
    public int MissingCount;

    public readonly List<
        BistroBuilderThumbnailQualityReport
    > Reports =
        new List<
            BistroBuilderThumbnailQualityReport
        >();

    public readonly List<string> Messages =
        new List<string>();

    public bool HasErrors =>
        ErrorCount > 0 ||
        MissingCount > 0;

    public string BuildSummary()
    {
        return
            "Correctas: " + GoodCount + "\n" +
            "Mejorables: " + WarningCount + "\n" +
            "Defectuosas: " + ErrorCount + "\n" +
            "Sin miniatura: " + MissingCount;
    }
}

/// <summary>
/// Audita y mejora iconos de catálogo sin modificar prefabs.
/// </summary>
public static class
    BistroBuilderCatalogThumbnailQualityService
{
    private const float VisibleAlphaThreshold =
        12f / 255f;

    private const float BackgroundDistanceThreshold =
        0.055f;

    /// <summary>
    /// Analiza bytes PNG sin crear assets.
    /// </summary>
    public static BistroBuilderThumbnailQualityReport
        AnalyzePngBytes(
            byte[] pngBytes,
            string sourceName = ""
        )
    {
        if (pngBytes == null ||
            pngBytes.Length == 0)
        {
            return CreateFailureReport(
                sourceName,
                "El PNG está vacío."
            );
        }

        Texture2D texture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false,
                false
            );

        try
        {
            if (!ImageConversion.LoadImage(
                    texture,
                    pngBytes,
                    false
                ))
            {
                return CreateFailureReport(
                    sourceName,
                    "Unity no pudo decodificar el PNG."
                );
            }

            return AnalyzeTexture(
                texture,
                sourceName
            );
        }
        catch (Exception exception)
        {
            return CreateFailureReport(
                sourceName,
                exception.Message
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                texture
            );
        }
    }

    /// <summary>
    /// Analiza un Sprite existente leyendo el PNG físico. No requiere
    /// que el TextureImporter sea readable.
    /// </summary>
    public static BistroBuilderThumbnailQualityReport
        AnalyzeSprite(
            Sprite sprite
        )
    {
        if (sprite == null)
        {
            return CreateFailureReport(
                string.Empty,
                "El Sprite es nulo."
            );
        }

        string assetPath =
            NormalizeUnityAssetPath(
                AssetDatabase.GetAssetPath(sprite)
            );

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return CreateFailureReport(
                sprite.name,
                "El Sprite no tiene ruta persistente."
            );
        }

        try
        {
            string absolutePath =
                ConvertUnityAssetPathToAbsolutePath(
                    assetPath
                );

            if (!File.Exists(absolutePath))
            {
                return CreateFailureReport(
                    sprite.name,
                    "No existe el PNG físico."
                );
            }

            return AnalyzePngBytes(
                File.ReadAllBytes(absolutePath),
                sprite.name
            );
        }
        catch (Exception exception)
        {
            return CreateFailureReport(
                sprite.name,
                exception.Message
            );
        }
    }

    /// <summary>
    /// Audita todas las definiciones del proyecto.
    /// </summary>
    public static BistroBuilderThumbnailQualityBatchReport
        AuditAllDefinitions()
    {
        BistroBuilderThumbnailQualityBatchReport batch =
            new BistroBuilderThumbnailQualityBatchReport();

        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (definition == null)
            {
                continue;
            }

            if (definition.CatalogIcon == null)
            {
                batch.MissingCount++;

                batch.Messages.Add(
                    definition.DisplayName +
                    ": no tiene miniatura."
                );

                continue;
            }

            BistroBuilderThumbnailQualityReport report =
                AnalyzeSprite(
                    definition.CatalogIcon
                );

            batch.Reports.Add(report);

            batch.Messages.Add(
                definition.DisplayName +
                ": " +
                report.BuildDetailedSummary()
            );

            switch (report.Status)
            {
                case BistroBuilderThumbnailQualityStatus.Good:
                    batch.GoodCount++;
                    break;

                case BistroBuilderThumbnailQualityStatus.Warning:
                    batch.WarningCount++;
                    break;

                default:
                    batch.ErrorCount++;
                    break;
            }
        }

        return batch;
    }

    /// <summary>
    /// Regenera únicamente iconos administrados que no superan la
    /// auditoría. Conserva iconos manuales.
    /// </summary>
    public static
        BistroBuilderCatalogThumbnailService.ThumbnailBatchResult
        ImproveLowQualityManagedThumbnails()
    {
        List<RestaurantPlaceableItemDefinition> definitions =
            BistroBuilderCatalogThumbnailService
                .LoadAllDefinitions();

        List<RestaurantPlaceableItemDefinition> candidates =
            new List<RestaurantPlaceableItemDefinition>();

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (definition == null ||
                definition.CatalogIcon == null ||
                !BistroBuilderCatalogThumbnailService
                    .IsGeneratedIcon(
                        definition.CatalogIcon
                    ))
            {
                continue;
            }

            BistroBuilderThumbnailQualityReport quality =
                AnalyzeSprite(
                    definition.CatalogIcon
                );

            if (quality.Status !=
                BistroBuilderThumbnailQualityStatus.Good)
            {
                candidates.Add(definition);
            }
        }

        return
            BistroBuilderCatalogThumbnailService
                .GenerateBatch(
                    candidates,
                    false,
                    false,
                    BistroBuilderCatalogThumbnailService
                        .DefaultThumbnailSize
                );
    }

    private static BistroBuilderThumbnailQualityReport
        AnalyzeTexture(
            Texture2D texture,
            string sourceName
        )
    {
        int width =
            texture.width;

        int height =
            texture.height;

        if (width <= 1 ||
            height <= 1)
        {
            return CreateFailureReport(
                sourceName,
                "La textura es demasiado pequeña."
            );
        }

        Color32[] pixels =
            texture.GetPixels32();

        Color background =
            EstimateBackgroundColor(
                pixels,
                width,
                height
            );

        bool transparentBackground =
            background.a <
            0.25f;

        int visibleCount = 0;
        int edgeVisibleCount = 0;

        double luminanceSum = 0d;
        double luminanceSquaredSum = 0d;

        int edgeMargin =
            Mathf.Max(
                2,
                Mathf.RoundToInt(
                    Mathf.Min(width, height) *
                    0.045f
                )
            );

        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                Color pixel =
                    pixels[
                        y * width +
                        x
                    ];

                if (!IsForegroundPixel(
                        pixel,
                        background,
                        transparentBackground
                    ))
                {
                    continue;
                }

                visibleCount++;

                bool touchesEdge =
                    x < edgeMargin ||
                    x >= width - edgeMargin ||
                    y < edgeMargin ||
                    y >= height - edgeMargin;

                if (touchesEdge)
                {
                    edgeVisibleCount++;
                }

                float luminance =
                    CalculateLuminance(pixel);

                luminanceSum +=
                    luminance;

                luminanceSquaredSum +=
                    luminance *
                    luminance;
            }
        }

        int totalPixelCount =
            width *
            height;

        if (visibleCount <= 0)
        {
            return CreateFailureReport(
                sourceName,
                "No se detecta ningún objeto visible."
            );
        }

        float coverage =
            visibleCount /
            (float)totalPixelCount;

        float edgeOccupancy =
            edgeVisibleCount /
            (float)visibleCount;

        float averageLuminance =
            (float)(
                luminanceSum /
                visibleCount
            );

        double variance =
            (
                luminanceSquaredSum /
                visibleCount
            ) -
            (
                averageLuminance *
                averageLuminance
            );

        float contrast =
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    (float)variance
                )
            );

        float coverageScore =
            CalculateRangeScore(
                coverage,
                0.10f,
                0.62f,
                0.22f,
                0.48f
            );

        float luminanceScore =
            CalculateRangeScore(
                averageLuminance,
                0.18f,
                0.88f,
                0.34f,
                0.68f
            );

        float contrastScore =
            Mathf.InverseLerp(
                0.025f,
                0.18f,
                contrast
            );

        float edgeScore =
            1f -
            Mathf.InverseLerp(
                0.015f,
                0.18f,
                edgeOccupancy
            );

        float score =
            Mathf.Clamp01(
                coverageScore * 0.34f +
                luminanceScore * 0.34f +
                contrastScore * 0.18f +
                edgeScore * 0.14f
            );

        BistroBuilderThumbnailQualityStatus status;

        if (score >= 0.68f &&
            coverage >= 0.08f &&
            edgeOccupancy <= 0.12f &&
            averageLuminance >= 0.16f)
        {
            status =
                BistroBuilderThumbnailQualityStatus.Good;
        }
        else if (score >= 0.44f)
        {
            status =
                BistroBuilderThumbnailQualityStatus.Warning;
        }
        else
        {
            status =
                BistroBuilderThumbnailQualityStatus.Error;
        }

        string recommendation =
            BuildRecommendation(
                coverage,
                edgeOccupancy,
                averageLuminance,
                contrast
            );

        return new BistroBuilderThumbnailQualityReport(
            sourceName,
            status,
            score,
            coverage,
            edgeOccupancy,
            averageLuminance,
            contrast,
            recommendation
        );
    }

    private static Color EstimateBackgroundColor(
        Color32[] pixels,
        int width,
        int height
    )
    {
        Color topLeft =
            pixels[height * width - width];

        Color topRight =
            pixels[height * width - 1];

        Color bottomLeft =
            pixels[0];

        Color bottomRight =
            pixels[width - 1];

        return
            (
                topLeft +
                topRight +
                bottomLeft +
                bottomRight
            ) /
            4f;
    }

    private static bool IsForegroundPixel(
        Color pixel,
        Color background,
        bool transparentBackground
    )
    {
        if (pixel.a <
            VisibleAlphaThreshold)
        {
            return false;
        }

        if (transparentBackground)
        {
            return true;
        }

        float redDistance =
            pixel.r -
            background.r;

        float greenDistance =
            pixel.g -
            background.g;

        float blueDistance =
            pixel.b -
            background.b;

        float alphaDistance =
            pixel.a -
            background.a;

        float squaredDistance =
            redDistance * redDistance +
            greenDistance * greenDistance +
            blueDistance * blueDistance +
            alphaDistance * alphaDistance;

        return squaredDistance >=
            BackgroundDistanceThreshold *
            BackgroundDistanceThreshold;
    }

    private static float CalculateLuminance(
        Color color
    )
    {
        return
            color.r * 0.2126f +
            color.g * 0.7152f +
            color.b * 0.0722f;
    }

    private static float CalculateRangeScore(
        float value,
        float minimum,
        float maximum,
        float idealMinimum,
        float idealMaximum
    )
    {
        if (value >= idealMinimum &&
            value <= idealMaximum)
        {
            return 1f;
        }

        if (value < idealMinimum)
        {
            return Mathf.InverseLerp(
                minimum,
                idealMinimum,
                value
            );
        }

        return
            1f -
            Mathf.InverseLerp(
                idealMaximum,
                maximum,
                value
            );
    }

    private static string BuildRecommendation(
        float coverage,
        float edgeOccupancy,
        float luminance,
        float contrast
    )
    {
        List<string> recommendations =
            new List<string>();

        if (coverage < 0.10f)
        {
            recommendations.Add(
                "El objeto ocupa poco espacio."
            );
        }
        else if (coverage > 0.62f)
        {
            recommendations.Add(
                "El objeto ocupa demasiado espacio."
            );
        }

        if (edgeOccupancy > 0.12f)
        {
            recommendations.Add(
                "El objeto se aproxima a los bordes."
            );
        }

        if (luminance < 0.18f)
        {
            recommendations.Add(
                "La imagen es demasiado oscura."
            );
        }
        else if (luminance > 0.88f)
        {
            recommendations.Add(
                "La imagen está sobreexpuesta."
            );
        }

        if (contrast < 0.025f)
        {
            recommendations.Add(
                "El contraste es insuficiente."
            );
        }

        return recommendations.Count > 0
            ? string.Join(
                " ",
                recommendations
            )
            : "La miniatura es legible.";
    }

    private static BistroBuilderThumbnailQualityReport
        CreateFailureReport(
            string sourceName,
            string message
        )
    {
        return new BistroBuilderThumbnailQualityReport(
            sourceName,
            BistroBuilderThumbnailQualityStatus.Error,
            0f,
            0f,
            0f,
            0f,
            0f,
            message
        );
    }

    private static string ConvertUnityAssetPathToAbsolutePath(
        string assetPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(assetPath);

        if (string.IsNullOrWhiteSpace(normalized) ||
            !normalized.StartsWith(
                "Assets/",
                StringComparison.Ordinal
            ))
        {
            throw new ArgumentException(
                "La ruta debe comenzar por Assets/: " +
                normalized
            );
        }

        string relativeToAssets =
            normalized.Substring(
                "Assets".Length
            );

        string platformRelative =
            relativeToAssets.Replace(
                '/',
                Path.DirectorySeparatorChar
            );

        return
            Application.dataPath +
            platformRelative;
    }

    private static string NormalizeUnityAssetPath(
        string path
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized =
            path.Trim()
                .Replace('\\', '/');

        while (normalized.Contains("//"))
        {
            normalized =
                normalized.Replace("//", "/");
        }

        return normalized.TrimEnd('/');
    }
}
