using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resultado del autotest de la canalización de artículos.
/// </summary>
public sealed class BistroBuilderPlaceablePipelineSelfTestResult
{
    public bool Succeeded;
    public bool CleanupSucceeded;

    public readonly List<string> PassedChecks =
        new List<string>();

    public readonly List<string> FailedChecks =
        new List<string>();

    public readonly List<string> Details =
        new List<string>();

    public string BuildSummary()
    {
        return
            "Resultado: " +
            (
                Succeeded
                    ? "CORRECTO"
                    : "FALLO"
            ) +
            "\nPruebas superadas: " +
            PassedChecks.Count +
            "\nPruebas fallidas: " +
            FailedChecks.Count +
            "\nLimpieza: " +
            (
                CleanupSucceeded
                    ? "correcta"
                    : "requiere revisión"
            );
    }

    public string BuildDetailedReport()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "BISTRO BUILDER — AUTOTEST DE ARTÍCULOS"
        );

        builder.AppendLine(
            "====================================="
        );

        builder.AppendLine(
            BuildSummary()
        );

        builder.AppendLine();

        if (PassedChecks.Count > 0)
        {
            builder.AppendLine(
                "Comprobaciones superadas:"
            );

            for (int index = 0;
                 index < PassedChecks.Count;
                 index++)
            {
                builder.AppendLine(
                    "- " +
                    PassedChecks[index]
                );
            }

            builder.AppendLine();
        }

        if (FailedChecks.Count > 0)
        {
            builder.AppendLine(
                "Comprobaciones fallidas:"
            );

            for (int index = 0;
                 index < FailedChecks.Count;
                 index++)
            {
                builder.AppendLine(
                    "- " +
                    FailedChecks[index]
                );
            }

            builder.AppendLine();
        }

        if (Details.Count > 0)
        {
            builder.AppendLine(
                "Detalle:"
            );

            for (int index = 0;
                 index < Details.Count;
                 index++)
            {
                builder.AppendLine(
                    "- " +
                    Details[index]
                );
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// Ejecuta una creación real en un sandbox temporal y limpia todo al
/// terminar. Sirve como regresión automática de:
/// - simulación previa;
/// - creación atómica;
/// - prefab universal;
/// - miniatura;
/// - catálogo;
/// - detección de duplicados;
/// - mantenimiento;
/// - rollback/limpieza.
/// </summary>
public static class
    BistroBuilderPlaceablePipelineSelfTest
{
    private const string MainCatalogPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    private const string TemporaryFolder =
        "Assets/Temp/BistroBuilder/PlaceablePipelineSelfTest";

    public static BistroBuilderPlaceablePipelineSelfTestResult
        Run()
    {
        BistroBuilderPlaceablePipelineSelfTestResult result =
            new BistroBuilderPlaceablePipelineSelfTestResult();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            result.FailedChecks.Add(
                "El autotest debe ejecutarse fuera de Play."
            );

            result.CleanupSucceeded = true;
            return result;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            result.FailedChecks.Add(
                "Unity está compilando o importando assets."
            );

            result.CleanupSucceeded = true;
            return result;
        }

        string uniqueToken =
            Guid.NewGuid()
                .ToString("N")
                .Substring(0, 10);

        string sourcePath =
            TemporaryFolder +
            "/SelfTestVisual_" +
            uniqueToken +
            ".prefab";

        List<string> createdPaths =
            new List<string>();

        byte[] catalogBackup =
            null;

        bool catalogExisted =
            AssetDatabase.LoadMainAssetAtPath(
                MainCatalogPath
            ) != null;

        try
        {
            AssetDatabase.SaveAssets();

            if (catalogExisted)
            {
                catalogBackup =
                    ReadAssetBytes(
                        MainCatalogPath
                    );
            }

            EnsureUnityAssetFolderExists(
                TemporaryFolder
            );

            GameObject sourceAsset =
                CreateTemporaryVisualPrefab(
                    sourcePath
                );

            createdPaths.Add(sourcePath);

            Assert(
                sourceAsset != null,
                "Prefab visual temporal creado.",
                result
            );

            Assert(
                sourceAsset != null &&
                sourceAsset.GetComponentInChildren<
                    RestaurantPlaceableObject
                >(true) == null,
                "La fuente temporal no contiene componentes " +
                "de Bistro Builder.",
                result
            );

            BistroBuilderPlaceableFactorySettings settings =
                new BistroBuilderPlaceableFactorySettings
                {
                    Preset =
                        BistroBuilderPlaceableFactoryPreset.Decoration,
                    PurchasePrice = 1,
                    CanMove = true,
                    CanRotate = true,
                    RotationStepDegrees = 90f,
                    MinimumClearance = 0f,
                    GenerateColliderWhenMissing = true,
                    AddToMainCatalog = true,
                    RunProjectHealthAfterCreation = false,
                    SingleDisplayNameOverride =
                        "Self Test " +
                        uniqueToken,
                    SingleDescriptionOverride =
                        "Artículo temporal de prueba automática."
                };

            List<BistroBuilderPlaceableFactoryPlan> plans =
                BistroBuilderPlaceableFactoryEngine
                    .AnalyzeSelection(
                        new[]
                        {
                            sourceAsset
                        },
                        settings
                    );

            Assert(
                plans.Count == 1,
                "La simulación previa genera un plan.",
                result
            );

            BistroBuilderPlaceableFactoryPlan plan =
                plans.Count > 0
                    ? plans[0]
                    : null;

            Assert(
                plan != null &&
                plan.Status ==
                    BistroBuilderPlaceableFactoryPlanStatus.Ready,
                "El plan temporal queda listo para crear.",
                result
            );

            if (plan == null ||
                plan.Status !=
                    BistroBuilderPlaceableFactoryPlanStatus.Ready)
            {
                throw new InvalidOperationException(
                    "La simulación previa no produjo un plan Ready."
                );
            }

            BistroBuilderPlaceableFactoryBatchResult creation =
                BistroBuilderPlaceableFactoryEngine
                    .ExecutePlans(
                        plans,
                        settings
                    );

            createdPaths.AddRange(
                creation.CreatedAssets
            );

            Assert(
                creation.CreatedCount == 1 &&
                creation.FailedCount == 0,
                "La creación atómica termina sin errores.",
                result
            );

            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(plan.ItemDefinitionPath);

            Assert(
                definition != null,
                "La definición de catálogo existe.",
                result
            );

            Assert(
                definition != null &&
                string.Equals(
                    definition.ItemId,
                    plan.ItemId,
                    StringComparison.Ordinal
                ),
                "El ItemId generado se conserva.",
                result
            );

            Assert(
                definition != null &&
                definition.EditableDefinition != null,
                "La definición editable está conectada.",
                result
            );

            Assert(
                definition != null &&
                definition.Prefab != null,
                "El prefab de juego está conectado.",
                result
            );

            string configurationError =
                string.Empty;

            bool prefabConfigurationIsValid =
                definition != null &&
                definition.Prefab != null &&
                definition.Prefab.ValidateConfiguration(
                    out configurationError
                );

            Assert(
                prefabConfigurationIsValid,
                "El prefab supera ValidateConfiguration." +
                (
                    string.IsNullOrWhiteSpace(
                        configurationError
                    )
                        ? string.Empty
                        : " " +
                          configurationError
                ),
                result
            );

            Assert(
                definition != null &&
                definition.CatalogIcon != null,
                "La miniatura se genera automáticamente.",
                result
            );

            if (definition != null &&
                definition.CatalogIcon != null)
            {
                BistroBuilderThumbnailQualityReport quality =
                    BistroBuilderCatalogThumbnailQualityService
                        .AnalyzeSprite(
                            definition.CatalogIcon
                        );

                Assert(
                    quality.Status !=
                        BistroBuilderThumbnailQualityStatus.Error,
                    "La miniatura supera el umbral mínimo de calidad.",
                    result
                );

                result.Details.Add(
                    quality.BuildDetailedSummary()
                );
            }

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableCatalogDefinition
                >(MainCatalogPath);

            int catalogOccurrences =
                catalog != null
                    ? catalog.Items.Count(
                        item =>
                            item != null &&
                            string.Equals(
                                item.ItemId,
                                plan.ItemId,
                                StringComparison.Ordinal
                            )
                    )
                    : 0;

            Assert(
                catalogOccurrences == 1,
                "El catálogo contiene una sola entrada temporal.",
                result
            );

            List<BistroBuilderPlaceableFactoryPlan>
                duplicatePlans =
                    BistroBuilderPlaceableFactoryEngine
                        .AnalyzeSelection(
                            new[]
                            {
                                definition.Prefab.gameObject
                            },
                            settings
                        );

            Assert(
                duplicatePlans.Count == 1 &&
                duplicatePlans[0].Status ==
                    BistroBuilderPlaceableFactoryPlanStatus
                        .AlreadyConfigured,
                "La fábrica detecta el artículo ya configurado.",
                result
            );

            BistroBuilderPlaceableMaintenanceReport
                maintenanceReport =
                    BistroBuilderPlaceableMaintenanceService
                        .AnalyzeProject();

            bool temporaryHasBlockingMaintenanceIssue =
                maintenanceReport.Findings.Any(
                    finding =>
                        finding != null &&
                        (
                            finding.Severity ==
                                BistroBuilderPlaceableMaintenanceSeverity
                                    .Blocker ||
                            finding.Severity ==
                                BistroBuilderPlaceableMaintenanceSeverity
                                    .Error
                        ) &&
                        (
                            finding.Context == definition ||
                            string.Equals(
                                finding.AssetPath,
                                plan.ItemDefinitionPath,
                                StringComparison.Ordinal
                            ) ||
                            string.Equals(
                                finding.AssetPath,
                                plan.PrefabPath,
                                StringComparison.Ordinal
                            )
                        )
                );

            Assert(
                !temporaryHasBlockingMaintenanceIssue,
                "El mantenimiento no detecta errores en el artículo.",
                result
            );
        }
        catch (Exception exception)
        {
            result.FailedChecks.Add(
                "Excepción: " +
                exception.Message
            );

            result.Details.Add(
                exception.ToString()
            );

            Debug.LogException(exception);
        }
        finally
        {
            result.CleanupSucceeded =
                Cleanup(
                    createdPaths,
                    sourcePath,
                    catalogExisted,
                    catalogBackup,
                    result
                );
        }

        result.Succeeded =
            result.FailedChecks.Count == 0 &&
            result.CleanupSucceeded;

        return result;
    }

    private static GameObject CreateTemporaryVisualPrefab(
        string sourcePath
    )
    {
        GameObject root =
            null;

        try
        {
            root =
                new GameObject(
                    "SelfTestVisual"
                );

            GameObject baseObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            baseObject.name =
                "Base";

            baseObject.transform.SetParent(
                root.transform,
                false
            );

            baseObject.transform.localPosition =
                new Vector3(
                    0f,
                    0.2f,
                    0f
                );

            baseObject.transform.localScale =
                new Vector3(
                    0.8f,
                    0.4f,
                    0.8f
                );

            GameObject topObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere
                );

            topObject.name =
                "Top";

            topObject.transform.SetParent(
                root.transform,
                false
            );

            topObject.transform.localPosition =
                new Vector3(
                    0f,
                    0.85f,
                    0f
                );

            topObject.transform.localScale =
                new Vector3(
                    0.75f,
                    0.95f,
                    0.75f
                );

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    sourcePath,
                    out bool success
                );

            if (!success ||
                saved == null)
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar el prefab temporal."
                );
            }

            AssetDatabase.ImportAsset(
                sourcePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate
            );

            return
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath
                );
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    root
                );
            }
        }
    }

    private static void Assert(
        bool condition,
        string message,
        BistroBuilderPlaceablePipelineSelfTestResult result
    )
    {
        if (condition)
        {
            result.PassedChecks.Add(message);
        }
        else
        {
            result.FailedChecks.Add(message);
        }
    }

    private static bool Cleanup(
        IReadOnlyList<string> createdPaths,
        string sourcePath,
        bool catalogExisted,
        byte[] catalogBackup,
        BistroBuilderPlaceablePipelineSelfTestResult result
    )
    {
        bool succeeded = true;

        try
        {
            HashSet<string> uniquePaths =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            if (createdPaths != null)
            {
                for (int index = 0;
                     index < createdPaths.Count;
                     index++)
                {
                    string path =
                        NormalizeUnityAssetPath(
                            createdPaths[index]
                        );

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        uniquePaths.Add(path);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                uniquePaths.Add(
                    NormalizeUnityAssetPath(
                        sourcePath
                    )
                );
            }

            List<string> orderedPaths =
                uniquePaths
                    .OrderByDescending(
                        path => path.Length
                    )
                    .ToList();

            for (int index = 0;
                 index < orderedPaths.Count;
                 index++)
            {
                string path =
                    orderedPaths[index];

                if (AssetDatabase.LoadMainAssetAtPath(path) != null ||
                    File.Exists(
                        ConvertUnityAssetPathToAbsolutePath(path)
                    ))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            if (catalogExisted &&
                catalogBackup != null)
            {
                WriteAssetBytes(
                    MainCatalogPath,
                    catalogBackup
                );

                AssetDatabase.ImportAsset(
                    MainCatalogPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate
                );
            }
            else if (!catalogExisted)
            {
                AssetDatabase.DeleteAsset(
                    MainCatalogPath
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject leftoverSource =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath
                );

            if (leftoverSource != null)
            {
                succeeded = false;

                result.Details.Add(
                    "La fuente temporal no se eliminó: " +
                    sourcePath
                );
            }
        }
        catch (Exception cleanupException)
        {
            succeeded = false;

            result.Details.Add(
                "Error de limpieza: " +
                cleanupException.Message
            );

            Debug.LogException(cleanupException);
        }

        return succeeded;
    }

    private static byte[] ReadAssetBytes(
        string assetPath
    )
    {
        string absolutePath =
            ConvertUnityAssetPathToAbsolutePath(
                assetPath
            );

        return File.ReadAllBytes(
            absolutePath
        );
    }

    private static void WriteAssetBytes(
        string assetPath,
        byte[] bytes
    )
    {
        string absolutePath =
            ConvertUnityAssetPathToAbsolutePath(
                assetPath
            );

        File.WriteAllBytes(
            absolutePath,
            bytes
        );
    }

    private static void EnsureUnityAssetFolderExists(
        string folderPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(
                folderPath
            );

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        if (!normalized.StartsWith(
                "Assets/",
                StringComparison.Ordinal
            ))
        {
            throw new ArgumentException(
                "La carpeta debe comenzar por Assets/: " +
                normalized
            );
        }

        string[] segments =
            normalized.Split('/');

        string current =
            "Assets";

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string next =
                current +
                "/" +
                segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                string guid =
                    AssetDatabase.CreateFolder(
                        current,
                        segments[index]
                    );

                if (string.IsNullOrWhiteSpace(guid) ||
                    !AssetDatabase.IsValidFolder(next))
                {
                    throw new InvalidOperationException(
                        "Unity no pudo crear " +
                        next +
                        "."
                    );
                }
            }

            current =
                next;
        }
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
