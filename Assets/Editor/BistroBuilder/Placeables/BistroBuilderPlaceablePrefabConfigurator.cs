using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura de forma segura el anclaje de colocación de uno o varios
/// prefabs seleccionados en la ventana Project.
///
/// La herramienta es universal para artículos apoyados en el suelo:
/// mesas, sillas, plantas, lámparas de pie, hornos, estanterías,
/// equipamiento de servicio y otros elementos colocables.
///
/// Principios:
/// - No depende de RestaurantTable ni de ninguna función específica.
/// - Es idempotente: no duplica anclajes ni reconfigura uno válido.
/// - No modifica prefabs que no tengan RestaurantPlaceableObject.
/// - No sobrescribe un anclaje asignado correctamente.
/// - Genera un informe completo de cada operación.
/// </summary>
public static class BistroBuilderPlaceablePrefabConfigurator
{
    private const string ConfigureMenuPath =
        "Tools/Bistro Builder/Placeables/" +
        "Configure Selected Prefab(s)";

    private const string ValidateMenuPath =
        "Tools/Bistro Builder/Placeables/" +
        "Validate Selected Prefab(s)";

    private const string PlacementAnchorObjectName =
        "PlacementAnchor";

    /// <summary>
    /// Configura todos los prefabs seleccionados en Project.
    /// </summary>
    [MenuItem(ConfigureMenuPath, false, 100)]
    private static void ConfigureSelectedPrefabs()
    {
        List<string> prefabPaths =
            CollectSelectedPrefabPaths();

        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Selecciona en Project uno o varios prefabs " +
                "colocables antes de ejecutar la herramienta.",
                "Aceptar"
            );

            return;
        }

        int configuredCount = 0;
        int alreadyValidCount = 0;
        int warningCount = 0;
        int errorCount = 0;

        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "BISTRO BUILDER - CONFIGURACIÓN DE PREFABS"
        );

        report.AppendLine(
            "========================================"
        );

        foreach (string prefabPath in prefabPaths)
        {
            PrefabConfigurationResult result =
                ConfigurePrefabAtPath(
                    prefabPath
                );

            report.AppendLine();
            report.AppendLine(
                result.Message
            );

            switch (result.Status)
            {
                case PrefabConfigurationStatus.Configured:
                    configuredCount++;
                    break;

                case PrefabConfigurationStatus.AlreadyValid:
                    alreadyValidCount++;
                    break;

                case PrefabConfigurationStatus.Warning:
                    warningCount++;
                    break;

                default:
                    errorCount++;
                    break;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine(
            "----------------------------------------"
        );

        report.AppendLine(
            "Configurados: " + configuredCount
        );

        report.AppendLine(
            "Ya correctos: " + alreadyValidCount
        );

        report.AppendLine(
            "Advertencias: " + warningCount
        );

        report.AppendLine(
            "Errores: " + errorCount
        );

        Debug.Log(
            report.ToString()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            BuildSummaryMessage(
                configuredCount,
                alreadyValidCount,
                warningCount,
                errorCount
            ),
            "Aceptar"
        );
    }

    /// <summary>
    /// Valida sin modificar los prefabs seleccionados.
    /// </summary>
    [MenuItem(ValidateMenuPath, false, 101)]
    private static void ValidateSelectedPrefabs()
    {
        List<string> prefabPaths =
            CollectSelectedPrefabPaths();

        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Selecciona en Project uno o varios prefabs " +
                "colocables antes de ejecutar la validación.",
                "Aceptar"
            );

            return;
        }

        int validCount = 0;
        int warningCount = 0;
        int errorCount = 0;

        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "BISTRO BUILDER - VALIDACIÓN DE PREFABS"
        );

        report.AppendLine(
            "====================================="
        );

        foreach (string prefabPath in prefabPaths)
        {
            PrefabValidationResult result =
                ValidatePrefabAtPath(
                    prefabPath
                );

            report.AppendLine();
            report.AppendLine(
                result.Message
            );

            switch (result.Status)
            {
                case PrefabValidationStatus.Valid:
                    validCount++;
                    break;

                case PrefabValidationStatus.Warning:
                    warningCount++;
                    break;

                default:
                    errorCount++;
                    break;
            }
        }

        report.AppendLine();
        report.AppendLine(
            "-------------------------------------"
        );

        report.AppendLine(
            "Correctos: " + validCount
        );

        report.AppendLine(
            "Advertencias: " + warningCount
        );

        report.AppendLine(
            "Errores: " + errorCount
        );

        Debug.Log(
            report.ToString()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            "Validación terminada.\n\n" +
            "Correctos: " + validCount + "\n" +
            "Advertencias: " + warningCount + "\n" +
            "Errores: " + errorCount + "\n\n" +
            "Consulta la Console para ver el informe completo.",
            "Aceptar"
        );
    }

    [MenuItem(ConfigureMenuPath, true)]
    [MenuItem(ValidateMenuPath, true)]
    private static bool ValidateMenuAvailability()
    {
        return
            CollectSelectedPrefabPaths().Count > 0;
    }

    /// <summary>
    /// Configura un único prefab y devuelve un resultado detallado.
    /// </summary>
    private static PrefabConfigurationResult ConfigurePrefabAtPath(
        string prefabPath
    )
    {
        GameObject prefabRoot = null;

        try
        {
            prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );

            if (prefabRoot == null)
            {
                return PrefabConfigurationResult.Error(
                    prefabPath +
                    ": Unity no pudo cargar el contenido del prefab."
                );
            }

            RestaurantPlaceableObject placeable =
                prefabRoot.GetComponent<
                    RestaurantPlaceableObject
                >();

            if (placeable == null)
            {
                return PrefabConfigurationResult.Warning(
                    prefabPath +
                    ": la raíz no contiene " +
                    "RestaurantPlaceableObject. No se ha modificado."
                );
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(
                    placeable
                );

            SerializedProperty anchorProperty =
                serializedPlaceable.FindProperty(
                    "placementAnchor"
                );

            SerializedProperty instanceIdProperty =
                serializedPlaceable.FindProperty(
                    "instanceId"
                );

            if (anchorProperty == null)
            {
                return PrefabConfigurationResult.Error(
                    prefabPath +
                    ": RestaurantPlaceableObject no contiene el " +
                    "campo serializado placementAnchor. Comprueba " +
                    "que el script nuevo ha compilado."
                );
            }

            Transform currentAnchor =
                anchorProperty.objectReferenceValue as Transform;

            bool clearedPrefabInstanceId =
                ClearPrefabInstanceIdIfNeeded(
                    serializedPlaceable,
                    instanceIdProperty
                );

            if (currentAnchor != null)
            {
                if (!BelongsToPrefabHierarchy(
                        prefabRoot.transform,
                        currentAnchor
                    ))
                {
                    return PrefabConfigurationResult.Error(
                        prefabPath +
                        ": el Placement Anchor asignado no pertenece " +
                        "a la jerarquía del propio prefab."
                    );
                }

                serializedPlaceable
                    .ApplyModifiedPropertiesWithoutUndo();

                if (clearedPrefabInstanceId)
                {
                    SavePrefabContents(
                        prefabRoot,
                        prefabPath
                    );

                    return PrefabConfigurationResult.Configured(
                        prefabPath +
                        ": el anclaje ya era válido. Se ha vaciado " +
                        "el InstanceId del prefab."
                    );
                }

                return PrefabConfigurationResult.AlreadyValid(
                    prefabPath +
                    ": ya tiene un Placement Anchor válido. " +
                    "No se ha modificado."
                );
            }

            if (!TryCalculateLocalPlacementBounds(
                    prefabRoot.transform,
                    out Bounds localBounds,
                    out string boundsSource,
                    out string boundsError
                ))
            {
                return PrefabConfigurationResult.Error(
                    prefabPath +
                    ": no se pudo calcular la base del artículo. " +
                    boundsError
                );
            }

            Transform anchor =
                FindReusablePlacementAnchor(
                    prefabRoot.transform
                );

            bool createdAnchor =
                anchor == null;

            if (createdAnchor)
            {
                GameObject anchorObject =
                    new GameObject(
                        PlacementAnchorObjectName
                    );

                anchor =
                    anchorObject.transform;

                anchor.SetParent(
                    prefabRoot.transform,
                    false
                );
            }
            else if (!CanSafelyReuseAnchor(anchor))
            {
                return PrefabConfigurationResult.Error(
                    prefabPath +
                    ": existe un hijo llamado PlacementAnchor, pero " +
                    "contiene componentes o hijos adicionales. No se " +
                    "ha movido para evitar perder una configuración."
                );
            }

            Vector3 anchorLocalPosition =
                new Vector3(
                    localBounds.center.x,
                    localBounds.min.y,
                    localBounds.center.z
                );

            anchor.localPosition =
                anchorLocalPosition;

            anchor.localRotation =
                Quaternion.identity;

            anchor.localScale =
                Vector3.one;

            anchorProperty.objectReferenceValue =
                anchor;

            serializedPlaceable
                .ApplyModifiedPropertiesWithoutUndo();

            if (!placeable.ValidateConfiguration(
                    out string validationError
                ))
            {
                return PrefabConfigurationResult.Error(
                    prefabPath +
                    ": el prefab no supera la validación después " +
                    "de configurar el anclaje. " +
                    validationError
                );
            }

            SavePrefabContents(
                prefabRoot,
                prefabPath
            );

            string creationText =
                createdAnchor
                    ? "creado"
                    : "reutilizado";

            string identityText =
                clearedPrefabInstanceId
                    ? " InstanceId del prefab vaciado."
                    : string.Empty;

            return PrefabConfigurationResult.Configured(
                prefabPath +
                ": PlacementAnchor " +
                creationText +
                " en " +
                FormatVector3(anchorLocalPosition) +
                " usando " +
                boundsSource +
                "." +
                identityText
            );
        }
        catch (Exception exception)
        {
            return PrefabConfigurationResult.Error(
                prefabPath +
                ": excepción durante la configuración.\n" +
                exception
            );
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );
            }
        }
    }

    /// <summary>
    /// Valida un prefab sin modificarlo.
    /// </summary>
    private static PrefabValidationResult ValidatePrefabAtPath(
        string prefabPath
    )
    {
        GameObject prefabRoot = null;

        try
        {
            prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );

            if (prefabRoot == null)
            {
                return PrefabValidationResult.Error(
                    prefabPath +
                    ": Unity no pudo cargar el prefab."
                );
            }

            RestaurantPlaceableObject placeable =
                prefabRoot.GetComponent<
                    RestaurantPlaceableObject
                >();

            if (placeable == null)
            {
                return PrefabValidationResult.Warning(
                    prefabPath +
                    ": la raíz no contiene " +
                    "RestaurantPlaceableObject."
                );
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(
                    placeable
                );

            SerializedProperty anchorProperty =
                serializedPlaceable.FindProperty(
                    "placementAnchor"
                );

            SerializedProperty instanceIdProperty =
                serializedPlaceable.FindProperty(
                    "instanceId"
                );

            if (anchorProperty == null)
            {
                return PrefabValidationResult.Error(
                    prefabPath +
                    ": falta el campo placementAnchor."
                );
            }

            Transform anchor =
                anchorProperty.objectReferenceValue as Transform;

            if (anchor == null)
            {
                return PrefabValidationResult.Error(
                    prefabPath +
                    ": no tiene Placement Anchor asignado."
                );
            }

            if (!BelongsToPrefabHierarchy(
                    prefabRoot.transform,
                    anchor
                ))
            {
                return PrefabValidationResult.Error(
                    prefabPath +
                    ": el Placement Anchor no pertenece al prefab."
                );
            }

            if (instanceIdProperty != null &&
                !string.IsNullOrWhiteSpace(
                    instanceIdProperty.stringValue
                ))
            {
                return PrefabValidationResult.Warning(
                    prefabPath +
                    ": el anclaje es válido, pero el InstanceId " +
                    "del prefab no está vacío."
                );
            }

            if (!placeable.ValidateConfiguration(
                    out string validationError
                ))
            {
                return PrefabValidationResult.Error(
                    prefabPath +
                    ": " +
                    validationError
                );
            }

            return PrefabValidationResult.Valid(
                prefabPath +
                ": configuración válida. PlacementAnchor en " +
                FormatVector3(anchor.localPosition) +
                "."
            );
        }
        catch (Exception exception)
        {
            return PrefabValidationResult.Error(
                prefabPath +
                ": excepción durante la validación.\n" +
                exception
            );
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );
            }
        }
    }

    /// <summary>
    /// Recoge rutas únicas de prefabs seleccionados en Project.
    /// También admite seleccionar una carpeta; se procesan sus prefabs.
    /// </summary>
    private static List<string> CollectSelectedPrefabPaths()
    {
        HashSet<string> uniquePaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (UnityEngine.Object selectedObject
                 in Selection.objects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            string selectedPath =
                AssetDatabase.GetAssetPath(
                    selectedObject
                );

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                continue;
            }

            if (Directory.Exists(selectedPath))
            {
                string[] prefabGuids =
                    AssetDatabase.FindAssets(
                        "t:Prefab",
                        new[]
                        {
                            selectedPath
                        }
                    );

                foreach (string prefabGuid in prefabGuids)
                {
                    string prefabPath =
                        AssetDatabase.GUIDToAssetPath(
                            prefabGuid
                        );

                    if (!string.IsNullOrWhiteSpace(prefabPath))
                    {
                        uniquePaths.Add(
                            prefabPath
                        );
                    }
                }

                continue;
            }

            if (selectedPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                uniquePaths.Add(
                    selectedPath
                );
            }
        }

        List<string> result =
            new List<string>(
                uniquePaths
            );

        result.Sort(
            StringComparer.Ordinal
        );

        return result;
    }

    /// <summary>
    /// Calcula límites en el espacio local de la raíz.
    ///
    /// Se priorizan Renderers porque describen la base visual real.
    /// Cuando no existen, se utilizan Colliders no trigger y, como
    /// último respaldo, cualquier Collider disponible.
    /// </summary>
    private static bool TryCalculateLocalPlacementBounds(
        Transform root,
        out Bounds localBounds,
        out string boundsSource,
        out string errorMessage
    )
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        if (TryBuildLocalBoundsFromRenderers(
                root,
                renderers,
                out localBounds
            ))
        {
            boundsSource =
                "límites visuales";

            errorMessage =
                string.Empty;

            return true;
        }

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(
                true
            );

        if (TryBuildLocalBoundsFromColliders(
                root,
                colliders,
                false,
                out localBounds
            ))
        {
            boundsSource =
                "colliders físicos";

            errorMessage =
                string.Empty;

            return true;
        }

        if (TryBuildLocalBoundsFromColliders(
                root,
                colliders,
                true,
                out localBounds
            ))
        {
            boundsSource =
                "colliders disponibles";

            errorMessage =
                string.Empty;

            return true;
        }

        boundsSource =
            string.Empty;

        errorMessage =
            "El prefab no contiene Renderer ni Collider utilizable.";

        return false;
    }

    private static bool TryBuildLocalBoundsFromRenderers(
        Transform root,
        Renderer[] renderers,
        out Bounds localBounds
    )
    {
        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;

        foreach (Renderer currentRenderer in renderers)
        {
            if (currentRenderer == null ||
                IsPlacementAnchorTransform(
                    currentRenderer.transform
                ))
            {
                continue;
            }

            EncapsulateWorldBoundsInLocalSpace(
                root,
                currentRenderer.bounds,
                ref hasBounds,
                ref minimum,
                ref maximum
            );
        }

        return TryCreateBounds(
            hasBounds,
            minimum,
            maximum,
            out localBounds
        );
    }

    private static bool TryBuildLocalBoundsFromColliders(
        Transform root,
        Collider[] colliders,
        bool includeTriggers,
        out Bounds localBounds
    )
    {
        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;

        foreach (Collider currentCollider in colliders)
        {
            if (currentCollider == null ||
                (!includeTriggers &&
                 currentCollider.isTrigger) ||
                IsPlacementAnchorTransform(
                    currentCollider.transform
                ))
            {
                continue;
            }

            EncapsulateWorldBoundsInLocalSpace(
                root,
                currentCollider.bounds,
                ref hasBounds,
                ref minimum,
                ref maximum
            );
        }

        return TryCreateBounds(
            hasBounds,
            minimum,
            maximum,
            out localBounds
        );
    }

    private static void EncapsulateWorldBoundsInLocalSpace(
        Transform root,
        Bounds worldBounds,
        ref bool hasBounds,
        ref Vector3 minimum,
        ref Vector3 maximum
    )
    {
        Vector3 center =
            worldBounds.center;

        Vector3 extents =
            worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(
                                x,
                                y,
                                z
                            )
                        );

                    Vector3 localCorner =
                        root.InverseTransformPoint(
                            worldCorner
                        );

                    if (!hasBounds)
                    {
                        minimum =
                            localCorner;

                        maximum =
                            localCorner;

                        hasBounds =
                            true;

                        continue;
                    }

                    minimum =
                        Vector3.Min(
                            minimum,
                            localCorner
                        );

                    maximum =
                        Vector3.Max(
                            maximum,
                            localCorner
                        );
                }
            }
        }
    }

    private static bool TryCreateBounds(
        bool hasBounds,
        Vector3 minimum,
        Vector3 maximum,
        out Bounds bounds
    )
    {
        if (!hasBounds)
        {
            bounds =
                default(Bounds);

            return false;
        }

        bounds =
            new Bounds();

        bounds.SetMinMax(
            minimum,
            maximum
        );

        return true;
    }

    private static Transform FindReusablePlacementAnchor(
        Transform root
    )
    {
        return root.Find(
            PlacementAnchorObjectName
        );
    }

    private static bool CanSafelyReuseAnchor(
        Transform anchor
    )
    {
        if (anchor == null ||
            anchor.childCount > 0)
        {
            return false;
        }

        Component[] components =
            anchor.GetComponents<Component>();

        return
            components.Length == 1 &&
            components[0] is Transform;
    }

    private static bool BelongsToPrefabHierarchy(
        Transform root,
        Transform candidate
    )
    {
        return
            candidate == root ||
            candidate.IsChildOf(root);
    }

    private static bool IsPlacementAnchorTransform(
        Transform candidate
    )
    {
        if (candidate == null)
        {
            return false;
        }

        return string.Equals(
            candidate.name,
            PlacementAnchorObjectName,
            StringComparison.Ordinal
        );
    }

    private static bool ClearPrefabInstanceIdIfNeeded(
        SerializedObject serializedPlaceable,
        SerializedProperty instanceIdProperty
    )
    {
        if (instanceIdProperty == null ||
            string.IsNullOrWhiteSpace(
                instanceIdProperty.stringValue
            ))
        {
            return false;
        }

        instanceIdProperty.stringValue =
            string.Empty;

        serializedPlaceable
            .ApplyModifiedPropertiesWithoutUndo();

        return true;
    }

    private static void SavePrefabContents(
        GameObject prefabRoot,
        string prefabPath
    )
    {
        PrefabUtility.SaveAsPrefabAsset(
            prefabRoot,
            prefabPath,
            out bool saveSucceeded
        );

        if (!saveSucceeded)
        {
            throw new InvalidOperationException(
                "Unity no pudo guardar el prefab " +
                prefabPath +
                "."
            );
        }
    }

    private static string BuildSummaryMessage(
        int configuredCount,
        int alreadyValidCount,
        int warningCount,
        int errorCount
    )
    {
        return
            "Configuración terminada.\n\n" +
            "Configurados: " + configuredCount + "\n" +
            "Ya correctos: " + alreadyValidCount + "\n" +
            "Advertencias: " + warningCount + "\n" +
            "Errores: " + errorCount + "\n\n" +
            "Consulta la Console para ver el informe completo.";
    }

    private static string FormatVector3(
        Vector3 value
    )
    {
        return
            "(" +
            value.x.ToString("0.###") +
            ", " +
            value.y.ToString("0.###") +
            ", " +
            value.z.ToString("0.###") +
            ")";
    }

    private enum PrefabConfigurationStatus
    {
        Configured = 0,
        AlreadyValid = 1,
        Warning = 2,
        Error = 3
    }

    private enum PrefabValidationStatus
    {
        Valid = 0,
        Warning = 1,
        Error = 2
    }

    private readonly struct PrefabConfigurationResult
    {
        public PrefabConfigurationStatus Status
        {
            get;
        }

        public string Message
        {
            get;
        }

        private PrefabConfigurationResult(
            PrefabConfigurationStatus status,
            string message
        )
        {
            Status =
                status;

            Message =
                message;
        }

        public static PrefabConfigurationResult Configured(
            string message
        )
        {
            return new PrefabConfigurationResult(
                PrefabConfigurationStatus.Configured,
                message
            );
        }

        public static PrefabConfigurationResult AlreadyValid(
            string message
        )
        {
            return new PrefabConfigurationResult(
                PrefabConfigurationStatus.AlreadyValid,
                message
            );
        }

        public static PrefabConfigurationResult Warning(
            string message
        )
        {
            return new PrefabConfigurationResult(
                PrefabConfigurationStatus.Warning,
                message
            );
        }

        public static PrefabConfigurationResult Error(
            string message
        )
        {
            return new PrefabConfigurationResult(
                PrefabConfigurationStatus.Error,
                message
            );
        }
    }

    private readonly struct PrefabValidationResult
    {
        public PrefabValidationStatus Status
        {
            get;
        }

        public string Message
        {
            get;
        }

        private PrefabValidationResult(
            PrefabValidationStatus status,
            string message
        )
        {
            Status =
                status;

            Message =
                message;
        }

        public static PrefabValidationResult Valid(
            string message
        )
        {
            return new PrefabValidationResult(
                PrefabValidationStatus.Valid,
                message
            );
        }

        public static PrefabValidationResult Warning(
            string message
        )
        {
            return new PrefabValidationResult(
                PrefabValidationStatus.Warning,
                message
            );
        }

        public static PrefabValidationResult Error(
            string message
        )
        {
            return new PrefabValidationResult(
                PrefabValidationStatus.Error,
                message
            );
        }
    }
}
