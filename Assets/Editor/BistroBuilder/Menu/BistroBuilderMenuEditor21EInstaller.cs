using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador acumulativo, idempotente y transaccional del editor jugable
/// de carta 2.1E. No crea una segunda carta, no altera contenido y no mueve
/// elementos del restaurante.
/// </summary>
public static class BistroBuilderMenuEditor21EInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1E Runtime Menu Editor";

    public const string UiRootName = "BB_2_1E_MenuEditorUI";

    [MenuItem(MenuPath, false, 170)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1E.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes de instalar.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador.",
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();
        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems =
                BistroBuilderMenuFoundationValidator.FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderMenuEditSessionService editSession =
                RequireComponent<BistroBuilderMenuEditSessionService>(
                    gameSystems
                );
            BistroBuilderRestaurantMenuService menu =
                RequireComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );
            BistroBuilderRestaurantMenuCollectionService collection =
                RequireComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >(gameSystems);
            BistroBuilderDishCatalogService catalog =
                RequireComponent<BistroBuilderDishCatalogService>(
                    gameSystems
                );
            BistroBuilderDishCategoryCatalogService categories =
                RequireComponent<
                    BistroBuilderDishCategoryCatalogService
                >(gameSystems);
            BistroBuilderMenuOfferService offer =
                RequireComponent<BistroBuilderMenuOfferService>(gameSystems);
            BistroBuilderDishAvailabilityService availability =
                RequireComponent<BistroBuilderDishAvailabilityService>(
                    gameSystems
                );
            BistroBuilderRecipeCatalogService recipes =
                RequireComponent<BistroBuilderRecipeCatalogService>(
                    gameSystems
                );

            if (menu.CommercialPolicy == null)
            {
                throw new InvalidOperationException(
                    "La carta no tiene una política comercial canónica."
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar editor de carta 2.1E"
            );

            BistroBuilderMenuEditorService editorService =
                GetOrAddComponent<BistroBuilderMenuEditorService>(
                    gameSystems
                );
            SetReference(editorService, "editSessionService", editSession);
            SetReference(editorService, "menuService", menu);
            SetReference(editorService, "collectionService", collection);
            SetReference(editorService, "catalogService", catalog);
            SetReference(
                editorService,
                "categoryCatalogService",
                categories
            );
            SetReference(editorService, "offerService", offer);
            SetReference(
                editorService,
                "availabilityService",
                availability
            );
            SetReference(editorService, "recipeCatalogService", recipes);
            SetReference(
                editorService,
                "commercialPolicy",
                menu.CommercialPolicy
            );

            Canvas canvas = FindCanonicalHudCanvas(scene);

            if (canvas == null)
            {
                throw new InvalidOperationException(
                    "No se encontró el Canvas canónico bajo MainHUD."
                );
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            }

            RectTransform uiRoot = FindOrCreateUiRoot(canvas.transform);
            BistroBuilderMenuEditorRuntimeView view =
                GetOrAddComponent<BistroBuilderMenuEditorRuntimeView>(
                    uiRoot.gameObject
                );
            SetReference(view, "editorService", editorService);
            SetReference(
                view,
                "cameraController",
                FindSingleSceneComponent<
                    BistroBuilderProfessionalCameraController
                >(scene, false)
            );
            SetReference(
                view,
                "editInteractionController",
                FindSingleSceneComponent<
                    RestaurantEditInteractionController
                >(scene, false)
            );
            SetBoolean(view, "showOpenButton", true);

            NormalizeUiRoot(uiRoot);
            EditorUtility.SetDirty(editorService);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!editorService.ValidateConfiguration(out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (!view.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            // Se valida después del cableado para no repetir el error de
            // preflight circular ya detectado en 2.1C.
            BistroBuilderSignatureDish21DValidationResult prerequisite =
                BistroBuilderSignatureDish21DValidator
                    .ValidateCurrentProject();

            if (prerequisite.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "2.1D no quedó válido tras instalar 2.1E.\n\n" +
                    prerequisite.BuildReport()
                );
            }

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.Refresh();
            BistroBuilderMenuEditor21EValidationResult result =
                BistroBuilderMenuEditor21EValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log("BISTRO BUILDER - 2.1E INSTALADO\n" + result.BuildReport());
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1E instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación de 2.1E falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        List<Canvas> canvases = FindSceneComponents<Canvas>(scene);

        for (int index = 0; index < canvases.Count; index++)
        {
            Canvas canvas = canvases[index];
            Transform parent = canvas.transform.parent;

            if (string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) &&
                parent != null &&
                string.Equals(
                    parent.name,
                    "MainHUD",
                    StringComparison.Ordinal
                ))
            {
                return canvas;
            }
        }

        return null;
    }

    private static RectTransform FindOrCreateUiRoot(Transform canvas)
    {
        Transform child = canvas.Find(UiRootName);

        if (child != null)
        {
            RectTransform existing = child as RectTransform;

            if (existing == null)
            {
                throw new InvalidOperationException(
                    UiRootName + " existe, pero no tiene RectTransform."
                );
            }

            return existing;
        }

        GameObject created = new GameObject(
            UiRootName,
            typeof(RectTransform)
        );
        Undo.RegisterCreatedObjectUndo(created, "Crear UI 2.1E");
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        created.layer = canvas.gameObject.layer;
        return rect;
    }

    private static void NormalizeUiRoot(RectTransform root)
    {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        if (root.parent != null)
        {
            root.gameObject.layer = root.parent.gameObject.layer;
        }

        root.SetAsLastSibling();
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + fieldName + "."
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string fieldName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + fieldName + "."
            );
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static List<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new List<T>();
        T[] all = Resources.FindObjectsOfTypeAll<T>();

        for (int index = 0; index < all.Length; index++)
        {
            T component = all[index];

            if (component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component))
            {
                result.Add(component);
            }
        }

        return result;
    }

    internal static T FindSingleSceneComponent<T>(
        Scene scene,
        bool required
    ) where T : Component
    {
        List<T> components = FindSceneComponents<T>(scene);

        if (components.Count > 1)
        {
            throw new InvalidOperationException(
                "Hay " + components.Count + " componentes " +
                typeof(T).Name + " en la escena."
            );
        }

        if (required && components.Count == 0)
        {
            throw new InvalidOperationException(
                "Falta " + typeof(T).Name + " en la escena."
            );
        }

        return components.Count == 1 ? components[0] : null;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static T RequireComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
        {
            throw new InvalidOperationException(
                "GameSystems necesita " + typeof(T).Name + "."
            );
        }

        return component;
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup
    )
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(
                scenePath,
                ImportAssetOptions.ForceUpdate
            );
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception restoreException)
        {
            Debug.LogException(restoreException);
        }
    }
}
