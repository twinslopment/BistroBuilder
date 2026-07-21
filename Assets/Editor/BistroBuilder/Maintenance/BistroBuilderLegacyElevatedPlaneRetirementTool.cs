using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Retira de forma explícita y segura el plano visual heredado
/// GameSystems/Plane.
///
/// Antes de eliminarlo comprueba:
/// - ruta exacta;
/// - ausencia de hijos;
/// - componentes permitidos;
/// - ausencia de referencias serializadas desde otros MonoBehaviour.
///
/// También elimina la utilidad temporal del diagnóstico geométrico.
/// </summary>
public static class
    BistroBuilderLegacyElevatedPlaneRetirementTool
{
    private const string MenuPath =
        "Tools/Bistro Builder/Maintenance/" +
        "Retire Legacy Elevated Plane";

    private const string DiagnosticUtilityPath =
        "Assets/Scripts/Presentation/Restaurant/EditMode/" +
        "Diagnostics/" +
        "RestaurantPlacementGeometryDiagnosticUtility.cs";

    private const string DiagnosticFolderPath =
        "Assets/Scripts/Presentation/Restaurant/EditMode/" +
        "Diagnostics";

    [MenuItem(MenuPath, false, 210)]
    private static void RetireLegacyPlane()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de ejecutar la limpieza.",
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

        GameObject legacyPlane =
            FindLegacyPlane(
                activeScene
            );

        if (legacyPlane == null)
        {
            bool diagnosticRemoved =
                RemoveDiagnosticUtility();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                diagnosticRemoved
                    ? "GameSystems/Plane ya no existe. " +
                      "Se ha retirado la utilidad temporal " +
                      "de diagnóstico."
                    : "GameSystems/Plane ya no existe y no " +
                      "quedaban archivos temporales por retirar.",
                "Aceptar"
            );

            return;
        }

        LegacyPlaneAnalysis analysis =
            AnalyzeLegacyPlane(
                activeScene,
                legacyPlane
            );

        if (!analysis.IsSafeToDelete)
        {
            Debug.LogError(
                analysis.BuildReport(),
                legacyPlane
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se ha eliminado GameSystems/Plane porque la " +
                "revisión ha encontrado dependencias o componentes " +
                "no previstos.\n\n" +
                analysis.BuildCompactFailureSummary() +
                "\n\nEl informe completo está en Console.",
                "Aceptar"
            );

            Selection.activeGameObject =
                legacyPlane;

            EditorGUIUtility.PingObject(
                legacyPlane
            );

            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Se ha confirmado que GameSystems/Plane:\n\n" +
                "• no tiene hijos;\n" +
                "• solo contiene Transform, MeshFilter, " +
                "MeshRenderer y MeshCollider;\n" +
                "• no está referenciado por otros scripts;\n" +
                "• es el plano visual elevado que ocultaba la " +
                "parte inferior de los artículos.\n\n" +
                "Se eliminará de la escena y se retirará el " +
                "diagnóstico temporal.",
                "Retirar definitivamente",
                "Cancelar"
            );

        if (!confirmed)
        {
            return;
        }

        string objectName =
            legacyPlane.name;

        Undo.DestroyObjectImmediate(
            legacyPlane
        );

        EditorSceneManager.MarkSceneDirty(
            activeScene
        );

        bool sceneSaved =
            EditorSceneManager.SaveScene(
                activeScene
            );

        bool diagnosticUtilityRemoved =
            RemoveDiagnosticUtility();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string resultMessage =
            objectName +
            " eliminado de la escena.\n" +
            "Escena guardada: " +
            sceneSaved +
            ".\n" +
            "Diagnóstico temporal retirado: " +
            diagnosticUtilityRemoved +
            ".\n\n" +
            "Cuando Unity termine de recompilar, ejecuta " +
            "Project Health y la regresión izquierda/derecha.";

        Debug.Log(
            "BISTRO BUILDER - LIMPIEZA DE SUELO HEREDADO\n" +
            resultMessage
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            resultMessage,
            "Aceptar"
        );
    }

    private static GameObject FindLegacyPlane(
        Scene scene
    )
    {
        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            GameObject root =
                roots[index];

            if (root == null ||
                !string.Equals(
                    root.name,
                    "GameSystems",
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            Transform planeTransform =
                root.transform.Find(
                    "Plane"
                );

            return planeTransform != null
                ? planeTransform.gameObject
                : null;
        }

        return null;
    }

    private static LegacyPlaneAnalysis AnalyzeLegacyPlane(
        Scene scene,
        GameObject legacyPlane
    )
    {
        LegacyPlaneAnalysis analysis =
            new LegacyPlaneAnalysis
            {
                LegacyPlane =
                    legacyPlane
            };

        if (legacyPlane.transform.childCount > 0)
        {
            analysis.Blockers.Add(
                "El objeto contiene " +
                legacyPlane.transform.childCount +
                " hijo(s)."
            );
        }

        Component[] ownComponents =
            legacyPlane.GetComponents<Component>();

        HashSet<Type> allowedTypes =
            new HashSet<Type>
            {
                typeof(Transform),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider)
            };

        for (int index = 0;
             index < ownComponents.Length;
             index++)
        {
            Component component =
                ownComponents[index];

            if (component == null)
            {
                analysis.Blockers.Add(
                    "El objeto contiene un componente perdido."
                );

                continue;
            }

            analysis.ComponentNames.Add(
                component.GetType().Name
            );

            if (!allowedTypes.Contains(
                    component.GetType()
                ))
            {
                analysis.Blockers.Add(
                    "Componente no previsto: " +
                    component.GetType().Name +
                    "."
                );
            }
        }

        HashSet<UnityEngine.Object> targetObjects =
            new HashSet<UnityEngine.Object>
            {
                legacyPlane,
                legacyPlane.transform
            };

        for (int index = 0;
             index < ownComponents.Length;
             index++)
        {
            Component component =
                ownComponents[index];

            if (component != null)
            {
                targetObjects.Add(component);
            }
        }

        List<MonoBehaviour> behaviours =
            FindSceneComponents<MonoBehaviour>(
                scene
            );

        for (int index = 0;
             index < behaviours.Count;
             index++)
        {
            MonoBehaviour behaviour =
                behaviours[index];

            if (behaviour == null ||
                behaviour.gameObject ==
                    legacyPlane)
            {
                continue;
            }

            FindSerializedReferences(
                behaviour,
                targetObjects,
                analysis.References
            );
        }

        if (analysis.References.Count > 0)
        {
            analysis.Blockers.Add(
                "Existen " +
                analysis.References.Count +
                " referencia(s) serializada(s) desde otros scripts."
            );
        }

        return analysis;
    }

    private static void FindSerializedReferences(
        MonoBehaviour behaviour,
        HashSet<UnityEngine.Object> targetObjects,
        List<string> references
    )
    {
        try
        {
            SerializedObject serializedObject =
                new SerializedObject(
                    behaviour
                );

            SerializedProperty property =
                serializedObject.GetIterator();

            bool enterChildren =
                true;

            while (property.Next(
                       enterChildren
                   ))
            {
                enterChildren =
                    false;

                if (property.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                UnityEngine.Object referencedObject =
                    property.objectReferenceValue;

                if (referencedObject == null ||
                    !targetObjects.Contains(
                        referencedObject
                    ))
                {
                    continue;
                }

                references.Add(
                    BuildAbsolutePath(
                        behaviour.transform
                    ) +
                    " | " +
                    behaviour.GetType().Name +
                    "." +
                    property.propertyPath
                );
            }
        }
        catch (Exception exception)
        {
            references.Add(
                BuildAbsolutePath(
                    behaviour.transform
                ) +
                " | No se pudo inspeccionar " +
                behaviour.GetType().Name +
                ": " +
                exception.Message
            );
        }
    }

    private static bool RemoveDiagnosticUtility()
    {
        bool removed =
            false;

        if (AssetDatabase.LoadMainAssetAtPath(
                DiagnosticUtilityPath
            ) != null)
        {
            removed =
                AssetDatabase.DeleteAsset(
                    DiagnosticUtilityPath
                );
        }

        RemoveFolderWhenEmpty(
            DiagnosticFolderPath
        );

        return removed;
    }

    private static void RemoveFolderWhenEmpty(
        string folderPath
    )
    {
        if (!AssetDatabase.IsValidFolder(
                folderPath
            ))
        {
            return;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    folderPath
                }
            );

        bool containsAsset =
            false;

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            if (!string.Equals(
                    assetPath,
                    folderPath,
                    StringComparison.Ordinal
                ))
            {
                containsAsset =
                    true;

                break;
            }
        }

        if (!containsAsset)
        {
            AssetDatabase.DeleteAsset(
                folderPath
            );
        }
    }

    private static List<T> FindSceneComponents<T>(
        Scene scene
    )
        where T : Component
    {
        List<T> results =
            new List<T>();

        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            T[] components =
                roots[rootIndex]
                    .GetComponentsInChildren<T>(
                        true
                    );

            for (int componentIndex = 0;
                 componentIndex < components.Length;
                 componentIndex++)
            {
                T component =
                    components[componentIndex];

                if (component != null)
                {
                    results.Add(component);
                }
            }
        }

        return results;
    }

    private static string BuildAbsolutePath(
        Transform transform
    )
    {
        if (transform == null)
        {
            return "<sin transform>";
        }

        Stack<string> names =
            new Stack<string>();

        Transform current =
            transform;

        while (current != null)
        {
            names.Push(
                current.name
            );

            current =
                current.parent;
        }

        return string.Join(
            "/",
            names.ToArray()
        );
    }

    private sealed class LegacyPlaneAnalysis
    {
        public GameObject LegacyPlane;

        public readonly List<string> ComponentNames =
            new List<string>();

        public readonly List<string> References =
            new List<string>();

        public readonly List<string> Blockers =
            new List<string>();

        public bool IsSafeToDelete =>
            LegacyPlane != null &&
            Blockers.Count == 0;

        public string BuildCompactFailureSummary()
        {
            if (Blockers.Count == 0)
            {
                return "No se han encontrado bloqueos.";
            }

            return string.Join(
                "\n",
                Blockers
            );
        }

        public string BuildReport()
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "BISTRO BUILDER - REVISIÓN DE GAMESYSTEMS/PLANE"
            );

            builder.AppendLine(
                "Componentes: " +
                string.Join(
                    ", ",
                    ComponentNames
                )
            );

            if (References.Count > 0)
            {
                builder.AppendLine(
                    "Referencias:"
                );

                for (int index = 0;
                     index < References.Count;
                     index++)
                {
                    builder.AppendLine(
                        "- " +
                        References[index]
                    );
                }
            }

            if (Blockers.Count > 0)
            {
                builder.AppendLine(
                    "Bloqueos:"
                );

                for (int index = 0;
                     index < Blockers.Count;
                     index++)
                {
                    builder.AppendLine(
                        "- " +
                        Blockers[index]
                    );
                }
            }

            return builder.ToString();
        }
    }
}
