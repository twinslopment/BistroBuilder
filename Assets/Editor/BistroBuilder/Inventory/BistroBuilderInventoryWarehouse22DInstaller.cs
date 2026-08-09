using System;
using System.Collections.Generic;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador acumulativo, idempotente y transaccional de 2.2D.
/// Añade planificación, persistencia de mínimos y UI agregada sin crear un
/// segundo inventario, un segundo almacén ni una fuente paralela de stock.
/// </summary>
public static class BistroBuilderInventoryWarehouse22DInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Install or Repair 2.2D Definitive Inventory Warehouse UI";

    public const string UiRootName = "BB_2_2D_InventoryWarehouseUI";

    [MenuItem(MenuPath, false, 380)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.2D.",
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
                "Abre y guarda la escena principal antes de instalar 2.2D.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador 2.2D.",
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();
        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderInventoryService inventory =
                Require<BistroBuilderInventoryService>(gameSystems);
            BistroBuilderRecipeCatalogService recipes =
                Require<BistroBuilderRecipeCatalogService>(gameSystems);
            Require<BistroBuilderGeneralGameStateService>(gameSystems);
            Require<RestaurantServiceStateService>(gameSystems);
            BistroBuilderSaveGameService saveService =
                Require<BistroBuilderSaveGameService>(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar UI definitiva de inventario 2.2D"
            );

            BistroBuilderInventoryPlanningService planning =
                Require<BistroBuilderInventoryPlanningService>(gameSystems);
            BistroBuilderGoodsReceivingService receiving =
                Require<BistroBuilderGoodsReceivingService>(gameSystems);
            BistroBuilderInventoryPolicySaveSectionProvider provider =
                Require<BistroBuilderInventoryPolicySaveSectionProvider>(
                    gameSystems
                );

            BistroBuilderInventoryWarehouseService warehouse =
                GetOrAdd<BistroBuilderInventoryWarehouseService>(gameSystems);
            SetReference(warehouse, "inventoryService", inventory);
            SetReference(warehouse, "planningService", planning);
            SetReference(warehouse, "recipeCatalogService", recipes);
            SetReference(warehouse, "goodsReceivingService", receiving);

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
            BistroBuilderInventoryPlanningRuntimeView legacyView =
                FindSingleSceneComponent<BistroBuilderInventoryPlanningRuntimeView>(scene);
            if (legacyView != null)
            {
                SetBoolean(legacyView, "showOpenButton", false);
                EditorUtility.SetDirty(legacyView);
            }

            BistroBuilderInventoryWarehouseRuntimeView view =
                GetOrAdd<BistroBuilderInventoryWarehouseRuntimeView>(
                    uiRoot.gameObject
                );
            SetReference(view, "warehouseService", warehouse);
            SetReference(
                view,
                "cameraController",
                FindSingleSceneComponent<
                    BistroBuilderProfessionalCameraController
                >(scene)
            );
            SetReference(
                view,
                "editInteractionController",
                FindSingleSceneComponent<
                    RestaurantEditInteractionController
                >(scene)
            );
            SetBoolean(view, "showOpenButton", true);
            NormalizeUiRoot(uiRoot);

            saveService.RefreshExtensions();

            string error = string.Empty;
            if (!warehouse.ValidateConfiguration(out error) ||
                !provider.ValidateConfiguration(out error) ||
                !view.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(warehouse);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(saveService);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            saveService.RefreshExtensions();

            BistroBuilderInventoryWarehouse22DValidationResult result =
                BistroBuilderInventoryWarehouse22DValidator
                    .ValidateCurrentProject();
            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.2D INSTALADO\n" + result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.2D instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 2.2D falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];
            if (canvas == null || canvas.gameObject.scene != scene)
            {
                continue;
            }

            Transform parent = canvas.transform.parent;
            if (string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) &&
                parent != null &&
                string.Equals(parent.name, "MainHUD", StringComparison.Ordinal))
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

        GameObject created = new GameObject(UiRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, "Crear UI definitiva de inventario 2.2D");
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

    private static T FindSingleSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        T result = null;
        for (int index = 0; index < components.Length; index++)
        {
            T component = components[index];
            if (component == null || component.gameObject.scene != scene)
            {
                continue;
            }

            if (result != null)
            {
                return null;
            }
            result = component;
        }
        return result;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static T Require<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException(
                "Falta " + typeof(T).Name + " en GameSystems."
            );
        }
        return component;
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName +
                " en " + target.GetType().Name + "."
            );
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName + "."
            );
        }
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup
    )
    {
        try
        {
            if (backup != null && backup.Length > 0)
            {
                File.WriteAllBytes(absoluteScenePath, backup);
            }
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception restoreException)
        {
            Debug.LogException(restoreException);
        }
    }
}
