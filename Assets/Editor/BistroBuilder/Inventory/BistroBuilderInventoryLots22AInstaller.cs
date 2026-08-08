using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo e idempotente de 2.2A.
///
/// No crea almacenes jugables ni cambia la fachada consumida por cocina:
/// enlaza calendario/guardado al inventario existente y registra la migración
/// v1->v2 de inventory.canonical para habilitar lotes internos y FEFO.
/// </summary>
public static class BistroBuilderInventoryLots22AInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Install or Repair 2.2A Internal Lots, Expiration and FEFO";

    [MenuItem(MenuPath, false, 360)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.2A.",
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
                "Abre y guarda la escena principal antes de instalar 2.2A.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador 2.2A.",
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
            BistroBuilderGeneralGameStateService generalState =
                Require<BistroBuilderGeneralGameStateService>(gameSystems);
            BistroBuilderSaveGameService saveService =
                Require<BistroBuilderSaveGameService>(gameSystems);
            BistroBuilderInventorySaveSectionProvider provider =
                Require<BistroBuilderInventorySaveSectionProvider>(gameSystems);
            BistroBuilderDishAvailabilityService availability =
                Require<BistroBuilderDishAvailabilityService>(gameSystems);
            Require<BistroBuilderOrderInventoryLifecycleService>(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar lotes y caducidad FEFO 2.2A"
            );

            BistroBuilderInventoryStateV1ToV2Migration migration =
                GetOrAdd<BistroBuilderInventoryStateV1ToV2Migration>(
                    gameSystems
                );

            SetReference(
                inventory,
                "recipeCatalogService",
                recipes
            );
            SetReference(
                inventory,
                "generalGameStateService",
                generalState
            );
            SetReference(
                inventory,
                "saveGameService",
                saveService
            );

            SetReference(provider, "saveGameService", saveService);
            SetReference(provider, "inventoryService", inventory);
            SetReference(provider, "availabilityService", availability);

            saveService.RefreshExtensions();

            string error = string.Empty;
            if (!inventory.ValidateConfiguration(out error) ||
                !provider.ValidateConfiguration(out error) ||
                !saveService.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(migration);
            EditorUtility.SetDirty(saveService);
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

            BistroBuilderInventoryLots22AValidationResult result =
                BistroBuilderInventoryLots22AValidator.ValidateCurrentProject();
            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.2A INSTALADO\n" + result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.2A instalado correctamente.\n\n" +
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
                "La instalación 2.2A falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    internal static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Require<T>(GameObject target) where T : Component
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

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
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
