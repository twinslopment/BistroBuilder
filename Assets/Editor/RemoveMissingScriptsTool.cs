using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para eliminar componentes cuyo script
/// ya no puede cargarse.
///
/// Funciona sobre el GameObject seleccionado y todos sus hijos.
/// Solo se compila dentro del Editor porque debe guardarse bajo
/// una carpeta llamada Editor.
/// </summary>
public static class RemoveMissingScriptsTool
{
    private const string MenuPath =
        "Tools/Bistro Builder/Remove Missing Scripts From Selection";

    [MenuItem(MenuPath)]
    private static void RemoveMissingScriptsFromSelection()
    {
        GameObject selectedObject =
            Selection.activeGameObject;

        if (selectedObject == null)
        {
            Debug.LogWarning(
                "Selecciona un GameObject antes de eliminar scripts perdidos."
            );

            return;
        }

        Transform[] hierarchy =
            selectedObject.GetComponentsInChildren<Transform>(
                true
            );

        int removedCount = 0;

        Undo.RegisterFullObjectHierarchyUndo(
            selectedObject,
            "Remove Missing Scripts"
        );

        for (int index = 0;
             index < hierarchy.Length;
             index++)
        {
            Transform currentTransform =
                hierarchy[index];

            if (currentTransform == null)
            {
                continue;
            }

            GameObject currentObject =
                currentTransform.gameObject;

            removedCount +=
                GameObjectUtility
                    .RemoveMonoBehavioursWithMissingScript(
                        currentObject
                    );

            EditorUtility.SetDirty(
                currentObject
            );
        }

        Debug.Log(
            "Scripts perdidos eliminados de " +
            selectedObject.name +
            " y sus hijos: " +
            removedCount +
            ".",
            selectedObject
        );
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRemoveMissingScriptsFromSelection()
    {
        return Selection.activeGameObject != null;
    }
}
