using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera un prefab visual aislado para validar la fábrica universal
/// sin importar assets externos ni modificar artículos reales.
///
/// La herramienta es idempotente:
/// - Si el prefab de prueba ya existe y sigue siendo visual, lo reutiliza.
/// - Si ese prefab ya fue convertido en artículo colocable, crea una
///   variante nueva con una ruta única.
/// - Nunca sobrescribe ni borra assets del usuario.
/// </summary>
public static class BistroBuilderPlaceableFactoryTestSource
{
    private const string MenuPath =
        "Tools/Bistro Builder/Placeables/Tests/" +
        "Create or Select Factory Test Source";

    private const string TestFolder =
        "Assets/Development/PlaceableFactoryTests";

    private const string PreferredPrefabPath =
        TestFolder +
        "/Factory_Test_Plant.prefab";

    [MenuItem(MenuPath, false, 180)]
    private static void CreateOrSelectTestSource()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de crear el asset de prueba.",
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

        EnsureFolderExists(TestFolder);

        string targetPath =
            ResolveSafeTargetPath();

        GameObject existingAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                targetPath
            );

        if (existingAsset == null)
        {
            if (!TryCreateTestPrefab(
                    targetPath,
                    out string errorMessage
                ))
            {
                EditorUtility.DisplayDialog(
                    "Bistro Builder",
                    "No se pudo crear el prefab de prueba.\n\n" +
                    errorMessage,
                    "Aceptar"
                );

                return;
            }

            existingAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    targetPath
                );
        }

        if (existingAsset == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Unity no pudo cargar el prefab de prueba creado.",
                "Aceptar"
            );

            return;
        }

        Selection.activeObject =
            existingAsset;

        EditorGUIUtility.PingObject(
            existingAsset
        );

        BistroBuilderPlaceableFactoryWindow.OpenWindow();

        Debug.Log(
            "BISTRO BUILDER - ASSET DE PRUEBA PREPARADO\n" +
            targetPath +
            "\n\nEl prefab visual original no contiene componentes " +
            "de Bistro Builder."
        );
    }

    private static string ResolveSafeTargetPath()
    {
        GameObject preferredAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PreferredPrefabPath
            );

        if (preferredAsset == null)
        {
            return PreferredPrefabPath;
        }

        bool alreadyConfigured =
            preferredAsset.GetComponentInChildren<
                RestaurantPlaceableObject
            >(true) != null;

        if (!alreadyConfigured)
        {
            return PreferredPrefabPath;
        }

        return AssetDatabase.GenerateUniqueAssetPath(
            TestFolder +
            "/Factory_Test_Plant_Visual.prefab"
        );
    }

    private static bool TryCreateTestPrefab(
        string targetPath,
        out string errorMessage
    )
    {
        GameObject root =
            null;

        try
        {
            root =
                new GameObject(
                    "Factory_Test_Plant"
                );

            GameObject pot =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder
                );

            pot.name =
                "Pot";

            pot.transform.SetParent(
                root.transform,
                false
            );

            pot.transform.localPosition =
                new Vector3(
                    0f,
                    0.3f,
                    0f
                );

            pot.transform.localRotation =
                Quaternion.identity;

            pot.transform.localScale =
                new Vector3(
                    0.7f,
                    0.3f,
                    0.7f
                );

            GameObject foliage =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere
                );

            foliage.name =
                "Foliage";

            foliage.transform.SetParent(
                root.transform,
                false
            );

            foliage.transform.localPosition =
                new Vector3(
                    0f,
                    1.15f,
                    0f
                );

            foliage.transform.localRotation =
                Quaternion.identity;

            foliage.transform.localScale =
                new Vector3(
                    1.05f,
                    1.25f,
                    1.05f
                );

            GameObject savedPrefab =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    targetPath,
                    out bool success
                );

            if (!success ||
                savedPrefab == null)
            {
                errorMessage =
                    "PrefabUtility.SaveAsPrefabAsset no confirmó " +
                    "el guardado.";

                return false;
            }

            AssetDatabase.SaveAssets();

            AssetDatabase.ImportAsset(
                targetPath,
                ImportAssetOptions.ForceUpdate
            );

            errorMessage =
                string.Empty;

            return true;
        }
        catch (Exception exception)
        {
            errorMessage =
                exception.Message;

            Debug.LogException(exception);

            return false;
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

    private static void EnsureFolderExists(
        string folderPath
    )
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string normalized =
            folderPath.Replace("\\", "/");

        string[] segments =
            normalized.Split('/');

        if (segments.Length == 0 ||
            !string.Equals(
                segments[0],
                "Assets",
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                "La ruta debe comenzar por Assets."
            );
        }

        string current =
            segments[0];

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
                AssetDatabase.CreateFolder(
                    current,
                    segments[index]
                );
            }

            current =
                next;
        }
    }
}
