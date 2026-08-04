using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo e idempotente de 2.1C.
///
/// Añade una única fachada de oferta y redirige hacia ella comandas, el
/// compositor y la barra. No modifica platos, inventario, menu.state ni la
/// distribución del restaurante.
/// </summary>
public static class BistroBuilderMenuOffer21CInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Install or Repair 2.1C Unified Menu Offer";

    [MenuItem(MenuPath, false, 150)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.1C.",
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

        BistroBuilderMenuFoundation21BValidationResult prerequisite =
            BistroBuilderMenuFoundation21BValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1B debe estar validado antes de instalar 2.1C.\n\n" +
                prerequisite.BuildReport(),
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

            BistroBuilderRestaurantMenuService menuService =
                RequireComponent<BistroBuilderRestaurantMenuService>(
                    gameSystems
                );
            BistroBuilderRestaurantMenuCollectionService collectionService =
                RequireComponent<
                    BistroBuilderRestaurantMenuCollectionService
                >(gameSystems);
            BistroBuilderDishCatalogService catalogService =
                RequireComponent<BistroBuilderDishCatalogService>(gameSystems);
            BistroBuilderDishAvailabilityService availabilityService =
                RequireComponent<BistroBuilderDishAvailabilityService>(
                    gameSystems
                );
            BistroBuilderCanonicalOrderIntegrationService orderIntegration =
                RequireComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >(gameSystems);
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
                "Instalar Bistro Builder 2.1C"
            );

            BistroBuilderMenuOfferService offerService =
                GetOrAddComponent<BistroBuilderMenuOfferService>(gameSystems);

            ConfigureOfferService(
                offerService,
                menuService,
                collectionService,
                catalogService,
                availabilityService,
                orderIntegration
            );
            SetReference(
                canonicalOrderService,
                "offerService",
                offerService
            );
            SetReference(
                compositionService,
                "offerService",
                offerService
            );
            SetReference(
                orderIntegration,
                "offerService",
                offerService
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
                    "Conectar oferta 2.1C con barra"
                );
                SetReference(
                    barSystems[index],
                    "offerService",
                    offerService
                );
                EditorUtility.SetDirty(barSystems[index]);
            }

            EditorUtility.SetDirty(offerService);
            EditorUtility.SetDirty(canonicalOrderService);
            EditorUtility.SetDirty(compositionService);
            EditorUtility.SetDirty(orderIntegration);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!offerService.ValidateConfiguration(out string offerError))
            {
                throw new InvalidOperationException(offerError);
            }

            if (!canonicalOrderService.ValidateConfiguration(
                    out string orderError
                ))
            {
                throw new InvalidOperationException(orderError);
            }

            if (!compositionService.ValidateConfiguration(
                    out string compositionError
                ))
            {
                throw new InvalidOperationException(compositionError);
            }

            for (int index = 0; index < barSystems.Count; index++)
            {
                if (!barSystems[index].ValidateConfiguration(
                        out string barError
                    ))
                {
                    throw new InvalidOperationException(barError);
                }
            }

            // 368EF depende ahora de la oferta 2.1C a través del sistema de
            // barra y del proveedor de guardado activo. Por eso se valida
            // después de crear y conectar temporalmente la fachada, pero
            // antes de guardar la escena. Cualquier error, sea cual sea su
            // texto, provoca rollback binario de la escena.
            BistroBuilderAvailabilityPersistenceValidationResult
                availabilityAfterBootstrap =
                    BistroBuilderAvailabilityPersistenceValidator
                        .ValidateCurrentProject();

            if (availabilityAfterBootstrap.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "368EF no quedó válido tras conectar la oferta 2.1C.\n\n" +
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

            BistroBuilderMenuOffer21CValidationResult result =
                BistroBuilderMenuOffer21CValidator.ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            string report = result.BuildReport();
            Debug.Log("BISTRO BUILDER - 2.1C INSTALADO\n" + report);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.1C instalado correctamente.\n\n" +
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
                "La instalación de 2.1C falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ConfigureOfferService(
        BistroBuilderMenuOfferService offerService,
        BistroBuilderRestaurantMenuService menuService,
        BistroBuilderRestaurantMenuCollectionService collectionService,
        BistroBuilderDishCatalogService catalogService,
        BistroBuilderDishAvailabilityService availabilityService,
        BistroBuilderCanonicalOrderIntegrationService orderIntegration
    )
    {
        SerializedObject serialized = new SerializedObject(offerService);
        RequireProperty(serialized, "menuService").objectReferenceValue =
            menuService;
        RequireProperty(serialized, "collectionService").objectReferenceValue =
            collectionService;
        RequireProperty(serialized, "catalogService").objectReferenceValue =
            catalogService;
        RequireProperty(serialized, "availabilityService").objectReferenceValue =
            availabilityService;
        RequireProperty(serialized, "orderIntegration").objectReferenceValue =
            orderIntegration;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        RequireProperty(serialized, propertyName).objectReferenceValue = value;
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

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " no contiene la propiedad " + propertyName + "."
            );
        }

        return property;
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
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
