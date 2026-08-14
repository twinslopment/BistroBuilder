using System;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador transaccional/idempotente de 2.3JKL.
/// Añade únicamente persistencia integrada, bridge 2.3H->2.2B y UI jugable.
/// No modifica los dominios cerrados 2.3C-I ni sus assets canónicos.
/// </summary>
public static class BistroBuilderSuppliers23JKLInstaller
{
    public const string UiRootName = "BB_2_3_SuppliersPlayerUI";
    private const string MenuPath =
        "Tools/Bistro Builder/Proveedores/2.3JKL - Instalar cierre integral de Proveedores";

    [MenuItem(MenuPath, false, 2900)]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder", "Sal de Play Mode antes de instalar 2.3JKL.", "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog("Bistro Builder", "Abre y guarda Prototype_Restaurant antes de instalar 2.3JKL.", "Aceptar");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog("Bistro Builder", "Guarda la escena antes de instalar 2.3JKL.", "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems = FindGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException("No se encontró GameSystems en la escena activa.");

            BistroBuilderSaveGameService save = Require<BistroBuilderSaveGameService>(gameSystems);
            BistroBuilderGoodsReceivingService receiving = Require<BistroBuilderGoodsReceivingService>(gameSystems);

            Undo.RegisterCompleteObjectUndo(gameSystems, "Instalar 2.3JKL Proveedores");

            BistroBuilderSupplierIntegratedSaveSectionProvider provider =
                GetOrAdd<BistroBuilderSupplierIntegratedSaveSectionProvider>(gameSystems);
            SetReference(provider, "saveGameService", save);

            BistroBuilderSupplierReceivingBridge23L bridge =
                GetOrAdd<BistroBuilderSupplierReceivingBridge23L>(gameSystems);
            SetReference(bridge, "goodsReceivingService", receiving);

            Canvas canvas = FindCanonicalHudCanvas(scene);
            if (canvas == null)
                throw new InvalidOperationException("No se encontró MainHUD/Canvas canónico para la UI 2.3K.");
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

            // 2.3JKL-B2: una única capa transversal de Presentation para
            // aislamiento contextual, tooltips y selectores desplazables.
            BistroBuilderUnifiedUiInteractionService uiInteraction =
                GetOrAdd<BistroBuilderUnifiedUiInteractionService>(canvas.gameObject);

            RectTransform uiRoot = FindOrCreateUiRoot(canvas.transform);
            BistroBuilderSupplierPlayerRuntimeView view =
                GetOrAdd<BistroBuilderSupplierPlayerRuntimeView>(uiRoot.gameObject);
            SetReference(view, "cameraController",
                FindSingleSceneComponent<BistroBuilderProfessionalCameraController>(scene));
            SetReference(view, "editInteractionController",
                FindSingleSceneComponent<RestaurantEditInteractionController>(scene));
            SetBoolean(view, "showOpenButton", true);
            NormalizeUiRoot(uiRoot);

            save.RefreshExtensions();

            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException("Persistencia 2.3J: " + providerError);
            if (!bridge.ValidateConfiguration(out string bridgeError))
                throw new InvalidOperationException("Bridge 2.3L: " + bridgeError);
            if (!view.ValidateConfiguration(out string viewError))
                throw new InvalidOperationException("UI 2.3K: " + viewError);
            if (!uiInteraction.ValidateConfiguration(out string uiInteractionError))
                throw new InvalidOperationException("UI transversal 2.3JKL-B2: " + uiInteractionError);
            if (!save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException("SaveGameService no registró supplier.integrated.runtime.");

            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(uiInteraction);
            EditorUtility.SetDirty(save);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena tras instalar 2.3JKL.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            save.RefreshExtensions();

            Debug.Log(
                "2.3JKL instalado/actualizado. " +
                "supplier.integrated.runtime registrado; UI jugable de Proveedores creada; " +
                "B2 de interacción transversal instalado (aislamiento + tooltips + selectores scroll); " +
                "bridge ReceivingHandoff->2.2B instalado. " +
                "No se han modificado supplier.authoring, supplier.catalog, Inventario ni los estados runtime 2.3C-I."
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.3JKL instalado correctamente.\n\n" +
                "Siguiente paso: ejecutar '2.3JKL - Validar cierre integral'.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 2.3JKL falló y la escena fue restaurada.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    public static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && string.Equals(roots[i].name, "GameSystems", StringComparison.Ordinal))
                return roots[i];
        }
        return null;
    }

    public static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            Transform parent = canvas.transform.parent;
            if (string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) &&
                parent != null && string.Equals(parent.name, "MainHUD", StringComparison.Ordinal))
                return canvas;
        }
        return null;
    }

    private static RectTransform FindOrCreateUiRoot(Transform canvas)
    {
        Transform child = canvas.Find(UiRootName);
        if (child != null)
        {
            RectTransform existing = child as RectTransform;
            if (existing == null) throw new InvalidOperationException(UiRootName + " existe sin RectTransform.");
            return existing;
        }
        GameObject created = new GameObject(UiRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, "Crear UI Proveedores 2.3K");
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
        if (root.parent != null) root.gameObject.layer = root.parent.gameObject.layer;
        root.SetAsLastSibling();
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
    }

    private static T Require<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null) throw new InvalidOperationException("Falta " + typeof(T).Name + " en GameSystems.");
        return component;
    }

    private static T FindSingleSceneComponent<T>(Scene scene) where T : Component
    {
        T[] items = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        T result = null;
        for (int i = 0; i < items.Length; i++)
        {
            T current = items[i];
            if (current == null || current.gameObject.scene != scene) continue;
            if (result != null) return result; // ambigua: no se inyecta ninguna
            result = current;
        }
        return result;
    }

    private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("No existe campo serializado " + name + " en " + target.GetType().Name + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(UnityEngine.Object target, string name, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("No existe campo serializado " + name + ".");
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreScene(string scenePath, string absoluteScenePath, byte[] backup)
    {
        try
        {
            if (backup != null && backup.Length > 0) File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception rollbackError)
        {
            Debug.LogException(rollbackError);
        }
    }
}
