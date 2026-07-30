using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAvailabilityPersistenceInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Install or Repair 368EF Availability, Persistence & Active Save";

    [MenuItem(MenuPath, false, 350)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 368EF.",
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
                "Abre y guarda la escena principal antes de instalar 368EF.",
                "Aceptar"
            );
            return;
        }

        try
        {
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems."
                );
            }

            Require<BistroBuilderSaveGameService>(gameSystems);
            Require<BistroBuilderInventoryService>(gameSystems);
            Require<BistroBuilderRestaurantMenuService>(gameSystems);
            Require<BistroBuilderRecipeCatalogService>(gameSystems);
            Require<BistroBuilderOrderInventoryLifecycleService>(gameSystems);
            Require<CustomerGroupSpawner>(gameSystems);
            Require<OrderSystem>(gameSystems);

            AddIfMissing<BistroBuilderDishAvailabilityService>(gameSystems);
            AddIfMissing<BistroBuilderInventorySaveSectionProvider>(gameSystems);
            AddIfMissing<BistroBuilderActiveServiceSaveSectionProvider>(
                gameSystems
            );

            BistroBuilderSaveGameService saveService =
                gameSystems.GetComponent<BistroBuilderSaveGameService>();
            saveService.RefreshExtensions();

            EditorUtility.SetDirty(gameSystems);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena."
                );
            }

            saveService.RefreshExtensions();

            BistroBuilderAvailabilityPersistenceValidationResult result =
                BistroBuilderAvailabilityPersistenceValidator
                    .ValidateCurrentProject();
            string report = result.BuildReport();

            if (result.ErrorCount > 0)
            {
                Debug.LogError(report);
            }
            else
            {
                Debug.Log(report);
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Disponibilidad dinámica, persistencia y guardado activo " +
                "368EF instalados.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 368EF.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 368EF ha fallado.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static void AddIfMissing<T>(GameObject target)
        where T : Component
    {
        if (target.GetComponent<T>() == null)
        {
            Undo.AddComponent<T>(target);
        }
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
}
