using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Instalador idempotente de la UI/UX definitiva 21A.</summary>
public static class BistroBuilderUi21AInstaller
{
    public const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/UI UX/21A - Instalar + validar", false, 4100)]
    public static void InstallFromMenu()
    {
        bool ok = InstallAndValidate(out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — UI/UX 21A", report, "Aceptar");
        if (!ok) throw new InvalidOperationException(report);
    }

    public static void InstallAndValidateBatch()
    {
        if (!InstallAndValidate(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static void ValidateRuntimeBootstrapBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!BistroBuilderUiRuntimeBootstrap.InstallForScene(scene))
            throw new InvalidOperationException("21A runtime bootstrap no encontró MainHUD/Canvas.");
        bool validation = BistroBuilderUi21AValidator.ValidateCurrentScene(out string validationReport);
        bool selfTest = BistroBuilderUi21ASelfTest.Run(out string selfTestReport);
        string report = validationReport + "\n" + selfTestReport;
        WriteReport(report);
        Debug.Log(report);
        if (!validation || !selfTest) throw new InvalidOperationException(report);
    }
    public static bool InstallAndValidate(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar 21A.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = "No se pudo abrir Prototype_Restaurant.";
            return false;
        }

        string absolute = Path.GetFullPath(ScenePath);
        byte[] backup = File.ReadAllBytes(absolute);
        try
        {
            Canvas canvas = FindCanonicalCanvas(scene);
            if (canvas == null)
                throw new InvalidOperationException("Falta MainHUD/Canvas canónico.");
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

            BistroBuilderUiDesignSystem design = GetOrAdd<BistroBuilderUiDesignSystem>(canvas.gameObject);
            BistroBuilderUiShell shell = GetOrAdd<BistroBuilderUiShell>(canvas.gameObject);
            BistroBuilderUnifiedUiInteractionService interaction =
                GetOrAdd<BistroBuilderUnifiedUiInteractionService>(canvas.gameObject);

            shell.EnsureShell();
            design.ApplyAllNow(true);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(design);
            EditorUtility.SetDirty(shell);
            EditorUtility.SetDirty(interaction);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar Prototype_Restaurant tras 21A.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool validation = BistroBuilderUi21AValidator.ValidateCurrentScene(out string validationReport);
            bool selfTest = BistroBuilderUi21ASelfTest.Run(out string selfTestReport);
            report = validationReport + "\n" + selfTestReport;
            WriteReport(report);
            return validation && selfTest;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absolute, backup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report = "21A falló y la escena fue restaurada. " + exception.Message;
            WriteReport(report);
            Debug.LogException(exception);
            return false;
        }
    }

    private static Canvas FindCanonicalCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            Transform parent = canvas.transform.parent;
            if (canvas.name == "Canvas" && parent != null && parent.name == "MainHUD")
                return canvas;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T current = target.GetComponent<T>();
        return current != null ? current : Undo.AddComponent<T>(target);
    }

    private static void WriteReport(string report)
    {
        try
        {
            File.WriteAllText("UIUX21AReport.txt", report ?? string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("No se pudo escribir UIUX21AReport.txt: " + exception.Message);
        }
    }
}
