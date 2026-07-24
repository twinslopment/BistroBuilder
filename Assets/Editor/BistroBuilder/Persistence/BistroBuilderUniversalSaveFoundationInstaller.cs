using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente de BistroBuilder 366B.
///
/// Solo añade y configura componentes en GameSystems. Antes de guardar
/// conserva una copia binaria de la escena y la restaura si cualquier
/// comprobación falla.
/// </summary>
public static class BistroBuilderUniversalSaveFoundationInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Persistence/" +
        "Install or Repair Save Foundation";

    [MenuItem(MenuPath, false, 100)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar la persistencia.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes " +
                "de ejecutar el instalador.",
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

        string absoluteScenePath = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems =
                BistroBuilderPersistenceFoundationValidator
                    .FindGameSystems(scene);

            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar persistencia BistroBuilder 366B"
            );

            BistroBuilderSaveGameService service =
                GetOrAddComponent<BistroBuilderSaveGameService>(
                    gameSystems
                );
            BistroBuilderSaveDefinitionCatalog catalog =
                GetOrAddComponent<BistroBuilderSaveDefinitionCatalog>(
                    gameSystems
                );
            RestaurantStructureSaveSectionProvider provider =
                GetOrAddComponent<RestaurantStructureSaveSectionProvider>(
                    gameSystems
                );
            BistroBuilderPlacementOperationSaveGuard guard =
                GetOrAddComponent<BistroBuilderPlacementOperationSaveGuard>(
                    gameSystems
                );
            BistroBuilderEditInteractionSaveParticipant participant =
                GetOrAddComponent<
                    BistroBuilderEditInteractionSaveParticipant
                >(gameSystems);
            BistroBuilderGeneralGameStateService generalState =
                GetOrAddComponent<BistroBuilderGeneralGameStateService>(
                    gameSystems
                );
            BistroBuilderGeneralGameSaveSectionProvider generalProvider =
                GetOrAddComponent<
                    BistroBuilderGeneralGameSaveSectionProvider
                >(gameSystems);
            BistroBuilderSimulationSaveParticipant simulationParticipant =
                GetOrAddComponent<
                    BistroBuilderSimulationSaveParticipant
                >(gameSystems);
            BistroBuilderActiveServiceSaveGuard activeServiceGuard =
                GetOrAddComponent<BistroBuilderActiveServiceSaveGuard>(
                    gameSystems
                );

            ConfigureService(service);
            ConfigureCatalog(catalog);
            ConfigureProvider(gameSystems, provider, catalog);
            ConfigureGuard(gameSystems, guard);
            ConfigureParticipant(gameSystems, participant);
            ConfigureGeneralState(gameSystems, generalState);
            ConfigureGeneralProvider(
                gameSystems,
                service,
                generalState,
                generalProvider
            );
            ConfigureSimulationParticipant(
                gameSystems,
                simulationParticipant
            );
            ConfigureActiveServiceGuard(
                gameSystems,
                service,
                activeServiceGuard
            );

            service.RefreshExtensions();

            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(guard);
            EditorUtility.SetDirty(participant);
            EditorUtility.SetDirty(generalState);
            EditorUtility.SetDirty(generalProvider);
            EditorUtility.SetDirty(simulationParticipant);
            EditorUtility.SetDirty(activeServiceGuard);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderPersistenceValidationResult result =
                BistroBuilderPersistenceFoundationValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    result.BuildReport()
                );
            }

            Debug.Log(
                "BISTRO BUILDER - SAVE FOUNDATION 366B\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Persistencia general 366B instalada sobre la plataforma universal.\n\n" +
                "Errores: " + result.ErrorCount +
                "\nAdvertencias: " + result.WarningCount +
                "\n\nEjecuta ahora Validate Save Foundation.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllBytes(absoluteScenePath, sceneBackup);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(
                    scene.path,
                    OpenSceneMode.Single
                );
            }
            catch (Exception rollbackException)
            {
                Debug.LogException(rollbackException);
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación ha fallado y se ha restaurado la " +
                "escena anterior.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ConfigureService(
        BistroBuilderSaveGameService service
    )
    {
        SerializedObject serialized = new SerializedObject(service);

        SetString(
            serialized,
            "saveRootFolderName",
            "BistroBuilder/Saves"
        );
        SetInt(serialized, "retainedGenerationsPerSlot", 3);
        SetBool(serialized, "prettyPrintJson", true);
        SetInt(serialized, "objectsPerFrame", 32);
        SetBool(serialized, "logOperations", true);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCatalog(
        BistroBuilderSaveDefinitionCatalog catalog
    )
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:RestaurantPlaceableItemDefinition"
        );

        List<RestaurantPlaceableItemDefinition> definitions =
            new List<RestaurantPlaceableItemDefinition>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            (first, second) => string.Compare(
                first != null ? first.ItemId : string.Empty,
                second != null ? second.ItemId : string.Empty,
                StringComparison.Ordinal
            )
        );

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty list =
            serialized.FindProperty("definitions");

        list.arraySize = definitions.Count;

        for (int index = 0; index < definitions.Count; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        catalog.RebuildIndex();
    }

    private static void ConfigureProvider(
        GameObject gameSystems,
        RestaurantStructureSaveSectionProvider provider,
        BistroBuilderSaveDefinitionCatalog catalog
    )
    {
        SerializedObject serialized = new SerializedObject(provider);

        SetReference(
            serialized,
            "placeableRegistry",
            RequireComponent<RestaurantPlaceableRegistry>(gameSystems)
        );
        SetReference(
            serialized,
            "lifecycleService",
            RequireComponent<RestaurantPlaceableLifecycleService>(
                gameSystems
            )
        );
        SetReference(
            serialized,
            "validationService",
            RequireComponent<RestaurantPlacementValidationService>(
                gameSystems
            )
        );
        SetReference(
            serialized,
            "historyService",
            RequireComponent<RestaurantPlacementHistoryService>(
                gameSystems
            )
        );
        SetReference(
            serialized,
            "seatingTopologyService",
            RequireComponent<RestaurantSeatingTopologyService>(
                gameSystems
            )
        );
        SetReference(serialized, "definitionCatalog", catalog);
        SetInt(serialized, "captureObjectsPerFrame", 64);
        SetBool(serialized, "logLoadSummary", true);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGuard(
        GameObject gameSystems,
        BistroBuilderPlacementOperationSaveGuard guard
    )
    {
        SerializedObject serialized = new SerializedObject(guard);

        SetReference(
            serialized,
            "transactionService",
            RequireComponent<RestaurantPlacementTransactionService>(
                gameSystems
            )
        );
        SetReference(
            serialized,
            "creationService",
            RequireComponent<RestaurantPlaceableCreationService>(
                gameSystems
            )
        );
        SetInt(serialized, "priority", 100);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureParticipant(
        GameObject gameSystems,
        BistroBuilderEditInteractionSaveParticipant participant
    )
    {
        SerializedObject serialized = new SerializedObject(participant);

        SetReference(
            serialized,
            "editInteractionController",
            RequireComponent<RestaurantEditInteractionController>(
                gameSystems
            )
        );
        SetInt(serialized, "priority", 1000);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGeneralState(
        GameObject gameSystems,
        BistroBuilderGeneralGameStateService generalState
    )
    {
        SerializedObject serialized = new SerializedObject(generalState);
        SetReference(
            serialized,
            "gameClock",
            RequireComponent<GameClock>(gameSystems)
        );
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGeneralProvider(
        GameObject gameSystems,
        BistroBuilderSaveGameService service,
        BistroBuilderGeneralGameStateService generalState,
        BistroBuilderGeneralGameSaveSectionProvider provider
    )
    {
        SerializedObject serialized = new SerializedObject(provider);
        SetReference(serialized, "saveGameService", service);
        SetReference(serialized, "generalGameState", generalState);
        SetReference(
            serialized,
            "gameClock",
            RequireComponent<GameClock>(gameSystems)
        );
        SetReference(
            serialized,
            "serviceStateService",
            RequireComponent<RestaurantServiceStateService>(gameSystems)
        );
        SetBool(serialized, "logLoadSummary", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSimulationParticipant(
        GameObject gameSystems,
        BistroBuilderSimulationSaveParticipant participant
    )
    {
        SerializedObject serialized = new SerializedObject(participant);
        SetReference(
            serialized,
            "gameClock",
            RequireComponent<GameClock>(gameSystems)
        );
        SetInt(serialized, "priority", 2000);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureActiveServiceGuard(
        GameObject gameSystems,
        BistroBuilderSaveGameService service,
        BistroBuilderActiveServiceSaveGuard guard
    )
    {
        SerializedObject serialized = new SerializedObject(guard);
        SetReference(serialized, "saveGameService", service);
        SetReference(
            serialized,
            "serviceStateService",
            RequireComponent<RestaurantServiceStateService>(gameSystems)
        );
        SetString(
            serialized,
            "requiredRuntimeSectionId",
            BistroBuilderGeneralGameSaveSectionProvider
                .FutureActiveServiceSectionId
        );
        SetInt(serialized, "priority", 500);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
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

    private static void SetReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property =
            RequireProperty(serialized, propertyName);
        property.objectReferenceValue = value;
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value
    )
    {
        RequireProperty(serialized, propertyName).stringValue =
            value ?? string.Empty;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        RequireProperty(serialized, propertyName).intValue = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value
    )
    {
        RequireProperty(serialized, propertyName).boolValue = value;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " no contiene la propiedad " + propertyName + "."
            );
        }

        return property;
    }
}
