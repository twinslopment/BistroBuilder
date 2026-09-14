using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Instala y valida el contrato 21C de scroll/navegación interna.</summary>
public static class BistroBuilderUiScrollNavigationInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string MenuPath = "Tools/Bistro Builder/UI/Instalar scroll y navegación 21C";

    [MenuItem(MenuPath, false, 50020)]
    private static void InstallFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = InstallScene(scene, true);
        ValidateOrThrow(scene);
        Debug.Log($"[BB 21C] INSTALL PASS — {count} scrolls normalizados en {scene.path}.");
    }

    public static void InstallPrototypeAndValidateBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int count = InstallScene(scene, true);
        ValidateOrThrow(scene);
        Debug.Log($"[BB 21C] BATCH PASS — {count} scrolls, vertical-only, wheel scoped, headers fijas.");
    }

    private static int InstallScene(Scene scene, bool save)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("BB 21C: escena inválida o no cargada.");

        ScrollRect[] scrolls = UnityEngine.Object.FindObjectsByType<ScrollRect>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < scrolls.Length; i++)
        {
            ScrollRect scroll = scrolls[i];
            if (scroll == null || scroll.gameObject.scene != scene) continue;
            Undo.RegisterCompleteObjectUndo(scroll.gameObject, "BB 21C scroll contract");
            BistroBuilderUiScrollRegion.Configure(scroll);
            EditorUtility.SetDirty(scroll.gameObject);
            if (scroll.content != null) EditorUtility.SetDirty(scroll.content.gameObject);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (save && !EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("BB 21C: no se pudo guardar la escena.");
        return count;
    }

    private static void ValidateOrThrow(Scene scene)
    {
        ScrollRect[] scrolls = UnityEngine.Object.FindObjectsByType<ScrollRect>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var errors = new List<string>();
        int count = 0;
        bool catalogFound = false;

        for (int i = 0; i < scrolls.Length; i++)
        {
            ScrollRect scroll = scrolls[i];
            if (scroll == null || scroll.gameObject.scene != scene) continue;
            count++;
            if (scroll.horizontal) errors.Add(scroll.name + ": horizontal sigue activo.");
            if (!scroll.vertical) errors.Add(scroll.name + ": vertical está desactivado.");
            if (scroll.content == null) errors.Add(scroll.name + ": sin Content.");
            if (scroll.viewport == null) errors.Add(scroll.name + ": sin Viewport.");
            if (scroll.GetComponent<BistroBuilderUiScrollRegion>() == null)
                errors.Add(scroll.name + ": sin BistroBuilderUiScrollRegion.");
            ValidateViewportRaycast(scroll, errors);
            ValidateHeader(scroll, errors);
            if (string.Equals(scroll.name, "ItemsScroll", StringComparison.OrdinalIgnoreCase))
            {
                catalogFound = true;
                ValidateCatalogScroll(scroll, errors);
            }
        }

        if (count == 0) errors.Add("La escena no contiene ningún ScrollRect para validar.");
        if (!catalogFound) errors.Add("No se encontró ItemsScroll del catálogo de edición.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "BB 21C VALIDATION FAIL\n - " + string.Join("\n - ", errors));

        Debug.Log($"[BB 21C] VALIDATION PASS — {count} scrolls, 0 errores.");
    }

    private static void ValidateViewportRaycast(ScrollRect scroll, List<string> errors)
    {
        if (scroll.viewport == null) return;
        Graphic graphic = scroll.viewport.GetComponent<Graphic>();
        if (graphic == null || !graphic.raycastTarget)
            errors.Add(scroll.name + ": el viewport no captura el puntero; la rueda podría llegar a cámara.");
    }

    private static void ValidateCatalogScroll(ScrollRect scroll, List<string> errors)
    {
        if (scroll.content == null) return;
        HorizontalLayoutGroup horizontal = scroll.content.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null && horizontal.enabled)
            errors.Add("ItemsScroll: conserva layout horizontal activo.");
        if (scroll.content.GetComponent<GridLayoutGroup>() == null)
            errors.Add("ItemsScroll: falta grid vertical para evitar desplazamiento horizontal.");
    }

    private static void ValidateHeader(ScrollRect scroll, List<string> errors)
    {
        if (scroll.content == null) return;
        for (int i = 0; i < scroll.content.childCount; i++)
        {
            RectTransform child = scroll.content.GetChild(i) as RectTransform;
            if (child == null || !LooksLikeHeader(child.name)) continue;
            if (child.GetComponent<BistroBuilderUiStickyTableHeader>() == null)
                errors.Add(scroll.name + ": cabecera detectada sin fijación sticky.");
            return;
        }
    }

    private static bool LooksLikeHeader(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string key = value.ToLowerInvariant();
        return key.Contains("header") || key.Contains("cabecera") ||
               key.Contains("columnhead") || key.Contains("columns") ||
               key.Contains("tablehead");
    }
}
