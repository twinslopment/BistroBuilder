using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo e idempotente de 2.1D.
///
/// Añade selección ponderada y telemetría sin modificar platos, carta,
/// inventario, menu.state ni distribución de escena. La escena se restaura
/// byte por byte ante cualquier fallo posterior a la primera mutación.
/// </summary>
public static class BistroBuilderSignatureDish21DInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1D Signature Dishes";

    [MenuItem(MenuPath, false, 160)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1D.",
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

        // No se ejecuta un preflight completo de 2.1C antes de reparar. Los
        // consumidores compilados con 2.1D consideran válida la selección
        // cuando existe; una instalación parcial podría bloquear su propia
        // reparación. La escena se respalda primero, se reconstruye 2.1D y
        // solo entonces se validan 2.1C, 368EF y 2.1D de forma transaccional.
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

            BistroBuilderMenuOfferService offerService =
                RequireComponent<BistroBuilderMenuOfferService>(gameSystems);
            BistroBuilderRestaurantMenuService menuService =
                RequireComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );
            BistroBuilderCanonicalOrderService canonicalOrderService =
                RequireComponent<BistroBuilderCanonicalOrderService>(
                    gameSystems
                );
            BistroBuilderOrderCompositionService compositionService =
                RequireComponent<BistroBuilderOrderCompositionService>(
                    gameSystems
                );

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar Bistro Builder 2.1D"
            );

            BistroBuilderMenuSelectionService selectionService =
                GetOrAddComponent<BistroBuilderMenuSelectionService>(
                    gameSystems
                );

            // La selección se deja completamente enlazada antes de añadir la
            // telemetría. Así OnEnable nunca observa una autoridad a medio
            // construir, ni siquiera durante la primera instalación.
            SetReference(selectionService, "offerService", offerService);
            SetReference(selectionService, "menuService", menuService);
            SetReference(
                canonicalOrderService,
                "selectionService",
                selectionService
            );
            SetReference(
                compositionService,
                "selectionService",
                selectionService
            );

            BistroBuilderSignatureDishTelemetryService telemetryService =
                GetOrAddComponent<
                    BistroBuilderSignatureDishTelemetryService
                >(gameSystems);
            SetReference(
                telemetryService,
                "selectionService",
                selectionService
            );
            SetReference(
                telemetryService,
                "canonicalOrderService",
                canonicalOrderService
            );

            List<BistroBuilderBarServiceSystem> barSystems =
                FindSceneComponents<BistroBuilderBarServiceSystem>(scene);

            if (barSystems.Count == 0)
            {
                throw new InvalidOperationException(
                    "No existe BistroBuilderBarServiceSystem en la escena."
                );
            }

            for (int index = 0; index < barSystems.Count; index++)
            {
                Undo.RecordObject(
                    barSystems[index],
                    "Conectar selección 2.1D con barra"
                );
                SetReference(
                    barSystems[index],
                    "selectionService",
                    selectionService
                );
                EditorUtility.SetDirty(barSystems[index]);
            }

            EditorUtility.SetDirty(selectionService);
            EditorUtility.SetDirty(telemetryService);
            EditorUtility.SetDirty(canonicalOrderService);
            EditorUtility.SetDirty(compositionService);
            EditorSceneManager.MarkSceneDirty(scene);

            ValidateRuntimeComponents(
                selectionService,
                telemetryService,
                canonicalOrderService,
                compositionService,
                barSystems
            );

            // 2.1C y 368EF se validan después de conectar 2.1D, antes de
            // guardar. Así cualquier dependencia circular o regresión del
            // inventario provoca rollback binario, no una escena parcial.
            BistroBuilderMenuOffer21CValidationResult offerAfterBootstrap =
                BistroBuilderMenuOffer21CValidator.ValidateCurrentProject();

            if (offerAfterBootstrap.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "2.1C no quedó válido tras conectar 2.1D.\n\n" +
                    offerAfterBootstrap.BuildReport()
                );
            }

            BistroBuilderAvailabilityPersistenceValidationResult
                availabilityAfterBootstrap =
                    BistroBuilderAvailabilityPersistenceValidator
                        .ValidateCurrentProject();

            if (availabilityAfterBootstrap.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "368EF no quedó válido tras conectar 2.1D.\n\n" +
                    availabilityAfterBootstrap.BuildReport()
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

            BistroBuilderSignatureDish21DValidationResult result =
                BistroBuilderSignatureDish21DValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            string report = result.BuildReport();
            Debug.Log("BISTRO BUILDER - 2.1D INSTALADO\n" + report);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1D instalado correctamente.\n\n" +
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
                "La instalación de 2.1D falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ValidateRuntimeComponents(
        BistroBuilderMenuSelectionService selectionService,
        BistroBuilderSignatureDishTelemetryService telemetryService,
        BistroBuilderCanonicalOrderService canonicalOrderService,
        BistroBuilderOrderCompositionService compositionService,
        IList<BistroBuilderBarServiceSystem> barSystems
    )
    {
        if (!selectionService.ValidateConfiguration(out string error))
        {
            throw new InvalidOperationException(error);
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            throw new InvalidOperationException(error);
        }

        if (!compositionService.ValidateConfiguration(out error))
        {
            throw new InvalidOperationException(error);
        }

        for (int index = 0; index < barSystems.Count; index++)
        {
            if (!barSystems[index].ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        if (!telemetryService.ValidateConfiguration(out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void SetReference(
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

    private static List<T> FindSceneComponents<T>(Scene scene)
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
