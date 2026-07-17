using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantenimiento técnico controlado para incidencias conocidas del
/// proyecto actual.
///
/// Esta herramienta:
/// - Añade PlacementAnchor explícito a artículos de escena que usan
///   la raíz como respaldo.
/// - Renombra scripts cuyo archivo no coincide con la clase.
/// - Conserva GUID mediante AssetDatabase.MoveAsset.
/// - Reejecuta Project Health al terminar.
///
/// Es idempotente: volver a ejecutarla no duplica anclajes ni vuelve
/// a renombrar archivos ya corregidos.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderProjectMaintenance
{
    private const string MenuPath =
        "Tools/Bistro Builder/Maintenance/" +
        "Repair Current Project Warnings";

    private const string DeferredValidationKey =
        "BistroBuilder.RunValidationAfterMaintenance";

    private const string PlacementAnchorName =
        "PlacementAnchor";

    private static readonly SourceRename[] SourceRenames =
    {
        new SourceRename(
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "estaurantPlacementObstacle.cs",
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "RestaurantPlacementObstacle.cs"
        ),
        new SourceRename(
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "NewMonoBehaviourScript.cs",
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "RestaurantPlacementCollisionUtility.cs"
        )
    };

    static BistroBuilderProjectMaintenance()
    {
        EditorApplication.delayCall +=
            TryRunDeferredValidation;
    }

    [MenuItem(MenuPath, false, 320)]
    private static void RepairCurrentProjectWarnings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de ejecutar el mantenimiento.",
                "Aceptar"
            );

            return;
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

            return;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            !activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No hay una escena válida y cargada.",
                "Aceptar"
            );

            return;
        }

        Undo.IncrementCurrentGroup();

        int undoGroup =
            Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Reparar advertencias de Bistro Builder"
        );

        MaintenanceReport report =
            new MaintenanceReport();

        try
        {
            RepairScenePlacementAnchors(
                activeScene,
                report
            );

            RenameKnownSourceFiles(
                report
            );

            if (report.SceneWasModified)
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene
                );

                EditorSceneManager.SaveScene(
                    activeScene
                );
            }

            AssetDatabase.SaveAssets();

            SessionState.SetBool(
                DeferredValidationKey,
                true
            );

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceUpdate
            );

            Undo.CollapseUndoOperations(
                undoGroup
            );

            Debug.Log(
                report.BuildDetailedMessage()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                report.BuildDialogMessage(),
                "Aceptar"
            );

            EditorApplication.delayCall +=
                TryRunDeferredValidation;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "El mantenimiento no pudo completarse.\n\n" +
                "Consulta el primer error rojo de Console.",
                "Aceptar"
            );
        }
    }

    private static void RepairScenePlacementAnchors(
        Scene activeScene,
        MaintenanceReport report
    )
    {
        RestaurantPlaceableObject[] placeables =
            UnityEngine.Object.FindObjectsByType<
                RestaurantPlaceableObject
            >(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        Array.Sort(
            placeables,
            ComparePlaceables
        );

        for (int index = 0;
             index < placeables.Length;
             index++)
        {
            RestaurantPlaceableObject placeable =
                placeables[index];

            if (placeable == null ||
                placeable.gameObject.scene != activeScene)
            {
                continue;
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(
                    placeable
                );

            SerializedProperty anchorProperty =
                serializedPlaceable.FindProperty(
                    "placementAnchor"
                );

            if (anchorProperty == null)
            {
                report.Errors.Add(
                    placeable.name +
                    ": no se encontró la propiedad placementAnchor."
                );

                continue;
            }

            Transform assignedAnchor =
                anchorProperty.objectReferenceValue as Transform;

            if (assignedAnchor != null)
            {
                report.AlreadyCorrectAnchors++;
                continue;
            }

            if (!TryResolveOrCreateAnchor(
                    placeable,
                    out Transform anchor,
                    out string anchorError
                ))
            {
                report.Errors.Add(
                    placeable.name +
                    ": " +
                    anchorError
                );

                continue;
            }

            if (!TryCalculateLocalPlacementBounds(
                    placeable.transform,
                    out Bounds localBounds,
                    out string boundsSource,
                    out string boundsError
                ))
            {
                report.Errors.Add(
                    placeable.name +
                    ": " +
                    boundsError
                );

                continue;
            }

            Vector3 anchorLocalPosition =
                new Vector3(
                    localBounds.center.x,
                    localBounds.min.y,
                    localBounds.center.z
                );

            Undo.RecordObject(
                anchor,
                "Configurar PlacementAnchor"
            );

            anchor.localPosition =
                anchorLocalPosition;

            anchor.localRotation =
                Quaternion.identity;

            anchor.localScale =
                Vector3.one;

            Undo.RecordObject(
                placeable,
                "Asignar PlacementAnchor"
            );

            serializedPlaceable.Update();

            anchorProperty.objectReferenceValue =
                anchor;

            serializedPlaceable.ApplyModifiedProperties();

            EditorUtility.SetDirty(
                placeable
            );

            EditorUtility.SetDirty(
                anchor
            );

            report.SceneWasModified =
                true;

            report.ConfiguredAnchors++;

            report.Details.Add(
                placeable.name +
                ": PlacementAnchor configurado en " +
                FormatVector3(anchorLocalPosition) +
                " usando " +
                boundsSource +
                "."
            );
        }
    }

    private static bool TryResolveOrCreateAnchor(
        RestaurantPlaceableObject placeable,
        out Transform anchor,
        out string errorMessage
    )
    {
        anchor =
            placeable.transform.Find(
                PlacementAnchorName
            );

        if (anchor != null)
        {
            if (!CanSafelyReuseAnchor(anchor))
            {
                errorMessage =
                    "existe un hijo PlacementAnchor con contenido " +
                    "adicional y no se ha modificado.";

                return false;
            }

            errorMessage =
                string.Empty;

            return true;
        }

        GameObject anchorObject =
            new GameObject(
                PlacementAnchorName
            );

        Undo.RegisterCreatedObjectUndo(
            anchorObject,
            "Crear PlacementAnchor"
        );

        Undo.SetTransformParent(
            anchorObject.transform,
            placeable.transform,
            "Asignar PlacementAnchor"
        );

        anchor =
            anchorObject.transform;

        errorMessage =
            string.Empty;

        return true;
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

        localBounds =
            default(Bounds);

        boundsSource =
            string.Empty;

        errorMessage =
            "no contiene Renderer ni Collider utilizable.";

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

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (renderer == null ||
                IsPlacementAnchor(
                    renderer.transform
                ))
            {
                continue;
            }

            EncapsulateWorldBoundsInLocalSpace(
                root,
                renderer.bounds,
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

        for (int index = 0;
             index < colliders.Length;
             index++)
        {
            Collider collider =
                colliders[index];

            if (collider == null ||
                (!includeTriggers &&
                 collider.isTrigger) ||
                IsPlacementAnchor(
                    collider.transform
                ))
            {
                continue;
            }

            EncapsulateWorldBoundsInLocalSpace(
                root,
                collider.bounds,
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

    private static bool IsPlacementAnchor(
        Transform transform
    )
    {
        return
            transform != null &&
            string.Equals(
                transform.name,
                PlacementAnchorName,
                StringComparison.Ordinal
            );
    }

    private static void RenameKnownSourceFiles(
        MaintenanceReport report
    )
    {
        for (int index = 0;
             index < SourceRenames.Length;
             index++)
        {
            SourceRename rename =
                SourceRenames[index];

            bool sourceExists =
                AssetDatabase.LoadAssetAtPath<
                    MonoScript
                >(rename.SourcePath) != null;

            bool destinationExists =
                AssetDatabase.LoadAssetAtPath<
                    MonoScript
                >(rename.DestinationPath) != null;

            if (!sourceExists &&
                destinationExists)
            {
                report.AlreadyCorrectSourceFiles++;
                continue;
            }

            if (!sourceExists &&
                !destinationExists)
            {
                report.Errors.Add(
                    "No se encontró " +
                    rename.SourcePath +
                    " ni " +
                    rename.DestinationPath +
                    "."
                );

                continue;
            }

            if (destinationExists)
            {
                report.Errors.Add(
                    "No se puede renombrar " +
                    rename.SourcePath +
                    " porque ya existe " +
                    rename.DestinationPath +
                    "."
                );

                continue;
            }

            string moveError =
                AssetDatabase.MoveAsset(
                    rename.SourcePath,
                    rename.DestinationPath
                );

            if (!string.IsNullOrWhiteSpace(moveError))
            {
                report.Errors.Add(
                    "No se pudo renombrar " +
                    rename.SourcePath +
                    ": " +
                    moveError
                );

                continue;
            }

            report.RenamedSourceFiles++;

            report.Details.Add(
                rename.SourcePath +
                " → " +
                rename.DestinationPath +
                " (GUID conservado)."
            );
        }
    }

    private static void TryRunDeferredValidation()
    {
        if (!SessionState.GetBool(
                DeferredValidationKey,
                false
            ))
        {
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall +=
                TryRunDeferredValidation;

            return;
        }

        SessionState.EraseBool(
            DeferredValidationKey
        );

        BistroBuilderValidationReport validationReport =
            BistroBuilderProjectValidator
                .RunFullValidation(true);

        BistroBuilderProjectHealthWindow.SetReport(
            validationReport
        );
    }

    private static int ComparePlaceables(
        RestaurantPlaceableObject first,
        RestaurantPlaceableObject second
    )
    {
        string firstName =
            first != null
                ? first.name
                : string.Empty;

        string secondName =
            second != null
                ? second.name
                : string.Empty;

        return string.Compare(
            firstName,
            secondName,
            StringComparison.CurrentCultureIgnoreCase
        );
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

    private readonly struct SourceRename
    {
        public string SourcePath
        {
            get;
        }

        public string DestinationPath
        {
            get;
        }

        public SourceRename(
            string sourcePath,
            string destinationPath
        )
        {
            SourcePath =
                sourcePath;

            DestinationPath =
                destinationPath;
        }
    }

    private sealed class MaintenanceReport
    {
        public int ConfiguredAnchors;
        public int AlreadyCorrectAnchors;
        public int RenamedSourceFiles;
        public int AlreadyCorrectSourceFiles;
        public bool SceneWasModified;

        public readonly List<string> Details =
            new List<string>();

        public readonly List<string> Errors =
            new List<string>();

        public string BuildDialogMessage()
        {
            return
                "Mantenimiento terminado.\n\n" +
                "Anclajes configurados: " +
                ConfiguredAnchors +
                "\nAnclajes ya correctos: " +
                AlreadyCorrectAnchors +
                "\nScripts renombrados: " +
                RenamedSourceFiles +
                "\nScripts ya correctos: " +
                AlreadyCorrectSourceFiles +
                "\nErrores: " +
                Errors.Count +
                "\n\nProject Health se actualizará " +
                "automáticamente al terminar la compilación.";
        }

        public string BuildDetailedMessage()
        {
            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            builder.AppendLine(
                "BISTRO BUILDER - MANTENIMIENTO"
            );

            builder.AppendLine(
                "=============================="
            );

            builder.AppendLine(
                BuildDialogMessage()
            );

            if (Details.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Cambios:");

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

            if (Errors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Errores:");

                for (int index = 0;
                     index < Errors.Count;
                     index++)
                {
                    builder.AppendLine(
                        "- " +
                        Errors[index]
                    );
                }
            }

            return builder.ToString();
        }
    }
}
