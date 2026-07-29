using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderOrderInventoryLifecycleInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Install or Repair 368CD Order Inventory Lifecycle";

    [MenuItem(MenuPath, false, 340)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder", "Sal de Play Mode antes de instalar 368CD.", "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog("Bistro Builder", "Abre y guarda la escena principal antes de instalar 368CD.", "Aceptar");
            return;
        }

        try
        {
            GameObject gameSystems = BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(scene);
            if (gameSystems == null) throw new InvalidOperationException("No se encontró GameSystems.");

            if (gameSystems.GetComponent<OrderSystem>() == null ||
                gameSystems.GetComponent<BistroBuilderInventoryService>() == null ||
                gameSystems.GetComponent<BistroBuilderRecipeCatalogService>() == null)
            {
                throw new InvalidOperationException("Faltan dependencias de 367/368B2 en GameSystems.");
            }

            if (gameSystems.GetComponent<BistroBuilderOrderInventoryLifecycleService>() == null)
            {
                Undo.AddComponent<BistroBuilderOrderInventoryLifecycleService>(gameSystems);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena.");

            BistroBuilderOrderInventoryLifecycleValidationResult result =
                BistroBuilderOrderInventoryLifecycleValidator.ValidateCurrentProject();
            Debug.Log(result.BuildReport());
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Integración comanda-receta-inventario-cocina 368CD instalada.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 368CD.",
                "Aceptar");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Bistro Builder", "La instalación 368CD ha fallado.\n\n" + ex.Message, "Aceptar");
        }
    }
}
