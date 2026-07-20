using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Motor atómico de la fábrica de artículos colocables.
///
/// No modifica el modelo o prefab seleccionado. Cada artículo se crea
/// como un nuevo prefab de juego que contiene el recurso visual como
/// hijo. Si una fase falla, elimina únicamente los assets creados por
/// esa operación.
/// </summary>
public static class BistroBuilderPlaceableFactoryEngine
{
    private const string MainCatalogPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    private const string GeneratedPrefabRoot =
        "Assets/Prefabs/Restaurant/Generated";

    private const string EditableDefinitionRoot =
        "Assets/Data/Restaurant/EditMode/EditableDefinitions";

    private const string ItemDefinitionRoot =
        "Assets/Data/Restaurant/EditMode/PlaceableItems";

    private const string PlacementAnchorName =
        "PlacementAnchor";

    private const string VisualRootName =
        "Visual";

    private const string CustomerApproachPointName =
        "CustomerApproachPoint";

    private const string WaiterServicePointName =
        "WaiterServicePoint";

    /// <summary>
    /// Recoge prefabs y modelos seleccionados en Project. También
    /// admite seleccionar carpetas completas.
    /// </summary>
    public static List<GameObject> CollectSelectedSourceAssets()
    {
        HashSet<string> paths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (UnityEngine.Object selectedObject
                 in Selection.objects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            string selectedPath =
                AssetDatabase.GetAssetPath(
                    selectedObject
                );

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                string[] guids =
                    AssetDatabase.FindAssets(
                        "t:GameObject",
                        new[]
                        {
                            selectedPath
                        }
                    );

                for (int index = 0;
                     index < guids.Length;
                     index++)
                {
                    string candidatePath =
                        AssetDatabase.GUIDToAssetPath(
                            guids[index]
                        );

                    if (IsSupportedGameObjectAssetPath(
                            candidatePath
                        ))
                    {
                        paths.Add(candidatePath);
                    }
                }

                continue;
            }

            if (IsSupportedGameObjectAssetPath(selectedPath))
            {
                paths.Add(selectedPath);
            }
        }

        List<GameObject> result =
            new List<GameObject>();

        foreach (string path in paths.OrderBy(
                     value => value,
                     StringComparer.Ordinal
                 ))
        {
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path
                );

            if (asset != null)
            {
                result.Add(asset);
            }
        }

        return result;
    }

    /// <summary>
    /// Aplica capacidades sugeridas por el preset. La lista sigue
    /// siendo editable antes de ejecutar.
    /// </summary>
    public static void ApplyPresetCapabilities(
        BistroBuilderPlaceableFactorySettings settings
    )
    {
        if (settings == null)
        {
            return;
        }

        settings.RequiredCapabilities.Clear();

        switch (settings.Preset)
        {
            case BistroBuilderPlaceableFactoryPreset.Table:
            case BistroBuilderPlaceableFactoryPreset.Chair:
                AddCapabilityById(
                    settings.RequiredCapabilities,
                    "customer_seating"
                );
                break;

            case BistroBuilderPlaceableFactoryPreset
                .KitchenEquipment:
                AddCapabilityById(
                    settings.RequiredCapabilities,
                    "food_production"
                );
                break;

            case BistroBuilderPlaceableFactoryPreset
                .ServiceEquipment:
                AddCapabilityById(
                    settings.RequiredCapabilities,
                    "order_pickup"
                );
                break;
        }
    }

    /// <summary>
    /// Analiza sin modificar assets.
    /// </summary>
    public static List<BistroBuilderPlaceableFactoryPlan>
        AnalyzeSelection(
            IReadOnlyList<GameObject> sources,
            BistroBuilderPlaceableFactorySettings settings
        )
    {
        List<BistroBuilderPlaceableFactoryPlan> plans =
            new List<BistroBuilderPlaceableFactoryPlan>();

        if (sources == null ||
            settings == null)
        {
            return plans;
        }

        HashSet<string> reservedItemIds =
            CollectExistingItemIds();

        HashSet<string> reservedPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        bool singleSource =
            sources.Count == 1;

        for (int index = 0;
             index < sources.Count;
             index++)
        {
            GameObject source =
                sources[index];

            plans.Add(
                AnalyzeSource(
                    source,
                    settings,
                    singleSource,
                    reservedItemIds,
                    reservedPaths
                )
            );
        }

        return plans;
    }

    /// <summary>
    /// Ejecuta únicamente los planes Ready. Cada artículo dispone de
    /// rollback independiente para que un fallo no deje assets a medias.
    /// </summary>
    public static BistroBuilderPlaceableFactoryBatchResult
        ExecutePlans(
            IReadOnlyList<BistroBuilderPlaceableFactoryPlan> plans,
            BistroBuilderPlaceableFactorySettings settings
        )
    {
        BistroBuilderPlaceableFactoryBatchResult batchResult =
            new BistroBuilderPlaceableFactoryBatchResult();

        if (plans == null ||
            settings == null)
        {
            batchResult.FailedCount = 1;
            batchResult.Messages.Add(
                "No existe un plan válido para ejecutar."
            );

            return batchResult;
        }

        List<RestaurantPlaceableItemDefinition> createdDefinitions =
            new List<RestaurantPlaceableItemDefinition>();

        for (int index = 0;
             index < plans.Count;
             index++)
        {
            BistroBuilderPlaceableFactoryPlan plan =
                plans[index];

            if (plan == null ||
                plan.Status !=
                    BistroBuilderPlaceableFactoryPlanStatus.Ready)
            {
                batchResult.SkippedCount++;
                continue;
            }

            bool created =
                TryCreateSingleItem(
                    plan,
                    settings,
                    out RestaurantPlaceableItemDefinition
                        createdDefinition,
                    out List<string> createdAssetPaths,
                    out string message
                );

            batchResult.Messages.Add(message);

            if (!created)
            {
                batchResult.FailedCount++;
                continue;
            }

            batchResult.CreatedCount++;
            batchResult.CreatedAssets.AddRange(
                createdAssetPaths
            );

            if (createdDefinition != null)
            {
                createdDefinitions.Add(
                    createdDefinition
                );
            }
        }

        if (settings.AddToMainCatalog &&
            createdDefinitions.Count > 0)
        {
            bool catalogUpdated =
                TryAddDefinitionsToMainCatalog(
                    createdDefinitions,
                    out string catalogMessage
                );

            batchResult.Messages.Add(
                catalogMessage
            );

            if (!catalogUpdated)
            {
                batchResult.FailedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (settings.RunProjectHealthAfterCreation &&
            batchResult.CreatedCount > 0)
        {
            EditorApplication.delayCall +=
                RunProjectHealth;
        }

        return batchResult;
    }

    private static BistroBuilderPlaceableFactoryPlan AnalyzeSource(
        GameObject source,
        BistroBuilderPlaceableFactorySettings settings,
        bool singleSource,
        HashSet<string> reservedItemIds,
        HashSet<string> reservedPaths
    )
    {
        string sourcePath =
            source != null
                ? AssetDatabase.GetAssetPath(source)
                : string.Empty;

        if (source == null ||
            string.IsNullOrWhiteSpace(sourcePath))
        {
            return CreateBlockedPlan(
                source,
                sourcePath,
                "La selección no es un prefab o modelo persistente."
            );
        }

        RestaurantPlaceableObject existingPlaceable =
            source.GetComponentInChildren<
                RestaurantPlaceableObject
            >(true);

        if (existingPlaceable != null)
        {
            return new BistroBuilderPlaceableFactoryPlan(
                source,
                sourcePath,
                BistroBuilderPlaceableFactoryPlanStatus
                    .AlreadyConfigured,
                "El asset ya contiene RestaurantPlaceableObject. " +
                "No se envolverá ni duplicará.",
                existingPlaceable.ItemDefinition != null
                    ? existingPlaceable.ItemDefinition.ItemId
                    : string.Empty,
                existingPlaceable.DisplayName,
                string.Empty,
                SanitizeAssetStem(source.name),
                sourcePath,
                string.Empty,
                string.Empty,
                existingPlaceable.ItemDefinition != null
                    ? existingPlaceable.ItemDefinition.Category
                    : ResolveCategory(settings.Preset),
                default(Bounds),
                false,
                false,
                existingPlaceable.GetComponent<
                    RestaurantTable
                >() != null
            );
        }

        string automaticDisplayName =
            HumanizeName(source.name);

        string displayName =
            singleSource &&
            !string.IsNullOrWhiteSpace(
                settings.SingleDisplayNameOverride
            )
                ? settings.SingleDisplayNameOverride.Trim()
                : automaticDisplayName;

        string description =
            singleSource &&
            !string.IsNullOrWhiteSpace(
                settings.SingleDescriptionOverride
            )
                ? settings.SingleDescriptionOverride.Trim()
                : BuildDefaultDescription(
                    displayName,
                    settings.Preset
                );

        string baseItemId =
            NormalizeIdentifier(source.name);

        if (string.IsNullOrWhiteSpace(baseItemId))
        {
            baseItemId =
                "placeable_item";
        }

        string itemId =
            GenerateUniqueItemId(
                baseItemId,
                reservedItemIds
            );

        reservedItemIds.Add(itemId);

        string assetStem =
            SanitizeAssetStem(
                displayName
            );

        if (string.IsNullOrWhiteSpace(assetStem))
        {
            assetStem =
                "PlaceableItem";
        }

        RestaurantPlaceableItemCategory category =
            ResolveCategory(
                settings.Preset
            );

        string categoryFolder =
            ResolveCategoryFolder(category);

        string prefabPath =
            GenerateReservedUniquePath(
                GeneratedPrefabRoot +
                "/" +
                categoryFolder +
                "/" +
                assetStem +
                ".prefab",
                reservedPaths
            );

        string editablePath =
            GenerateReservedUniquePath(
                EditableDefinitionRoot +
                "/EditableObjectDefinition_" +
                assetStem +
                ".asset",
                reservedPaths
            );

        string itemDefinitionPath =
            GenerateReservedUniquePath(
                ItemDefinitionRoot +
                "/PlaceableItemDefinition_" +
                assetStem +
                ".asset",
                reservedPaths
            );

        if (!TryAnalyzeBounds(
                source,
                out Bounds localBounds,
                out bool hasCollider,
                out string boundsError
            ))
        {
            return new BistroBuilderPlaceableFactoryPlan(
                source,
                sourcePath,
                BistroBuilderPlaceableFactoryPlanStatus.Blocked,
                boundsError,
                itemId,
                displayName,
                description,
                assetStem,
                prefabPath,
                editablePath,
                itemDefinitionPath,
                category,
                default(Bounds),
                false,
                false,
                settings.Preset ==
                    BistroBuilderPlaceableFactoryPreset.Table
            );
        }

        bool willGenerateCollider =
            settings.GenerateColliderWhenMissing &&
            !hasCollider;

        string statusMessage =
            "Listo. Se creará un prefab nuevo sin modificar " +
            "el asset de origen.";

        if (settings.Preset ==
            BistroBuilderPlaceableFactoryPreset.Table)
        {
            statusMessage +=
                " Se añadirá RestaurantTable y sus puntos de " +
                "interacción.";
        }
        else if (PresetHasFutureFunctionalAdapter(
                     settings.Preset
                 ))
        {
            statusMessage +=
                " El componente funcional específico de este preset " +
                "se añadirá cuando exista su sistema operativo.";
        }

        return new BistroBuilderPlaceableFactoryPlan(
            source,
            sourcePath,
            BistroBuilderPlaceableFactoryPlanStatus.Ready,
            statusMessage,
            itemId,
            displayName,
            description,
            assetStem,
            prefabPath,
            editablePath,
            itemDefinitionPath,
            category,
            localBounds,
            true,
            willGenerateCollider,
            settings.Preset ==
                BistroBuilderPlaceableFactoryPreset.Table
        );
    }

    private static bool TryCreateSingleItem(
        BistroBuilderPlaceableFactoryPlan plan,
        BistroBuilderPlaceableFactorySettings settings,
        out RestaurantPlaceableItemDefinition createdDefinition,
        out List<string> createdAssetPaths,
        out string message
    )
    {
        createdDefinition = null;

        createdAssetPaths =
            new List<string>();

        Scene previewScene =
            default(Scene);

        GameObject generatedRoot =
            null;

        try
        {
            EnsureAssetFolderForPath(
                plan.EditableDefinitionPath
            );

            EnsureAssetFolderForPath(
                plan.ItemDefinitionPath
            );

            EnsureAssetFolderForPath(
                plan.PrefabPath
            );

            RestaurantEditableObjectDefinition editableDefinition =
                ScriptableObject.CreateInstance<
                    RestaurantEditableObjectDefinition
                >();

            ConfigureEditableDefinition(
                editableDefinition,
                plan,
                settings
            );

            AssetDatabase.CreateAsset(
                editableDefinition,
                plan.EditableDefinitionPath
            );

            createdAssetPaths.Add(
                plan.EditableDefinitionPath
            );

            RestaurantPlaceableItemDefinition itemDefinition =
                ScriptableObject.CreateInstance<
                    RestaurantPlaceableItemDefinition
                >();

            ConfigureItemDefinitionBeforePrefab(
                itemDefinition,
                editableDefinition,
                plan,
                settings
            );

            AssetDatabase.CreateAsset(
                itemDefinition,
                plan.ItemDefinitionPath
            );

            createdAssetPaths.Add(
                plan.ItemDefinitionPath
            );

            previewScene =
                EditorSceneManager.NewPreviewScene();

            generatedRoot =
                BuildGeneratedPrefabRoot(
                    previewScene,
                    plan,
                    settings,
                    editableDefinition,
                    itemDefinition
                );

            PrefabUtility.SaveAsPrefabAsset(
                generatedRoot,
                plan.PrefabPath,
                out bool prefabSaved
            );

            if (!prefabSaved)
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar el prefab generado."
                );
            }

            createdAssetPaths.Add(
                plan.PrefabPath
            );

            AssetDatabase.ImportAsset(
                plan.PrefabPath,
                ImportAssetOptions.ForceUpdate
            );

            RestaurantPlaceableObject prefabComponent =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableObject
                >(plan.PrefabPath);

            if (prefabComponent == null)
            {
                GameObject prefabAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        plan.PrefabPath
                    );

                prefabComponent =
                    prefabAsset != null
                        ? prefabAsset.GetComponent<
                            RestaurantPlaceableObject
                        >()
                        : null;
            }

            if (prefabComponent == null)
            {
                throw new InvalidOperationException(
                    "El prefab guardado no contiene " +
                    "RestaurantPlaceableObject."
                );
            }

            SerializedObject serializedItem =
                new SerializedObject(
                    itemDefinition
                );

            SetObjectReference(
                serializedItem,
                "prefab",
                prefabComponent
            );

            serializedItem.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                itemDefinition
            );

            if (!prefabComponent.ValidateConfiguration(
                    out string validationError
                ))
            {
                throw new InvalidOperationException(
                    "El prefab generado no supera la validación: " +
                    validationError
                );
            }

            createdDefinition =
                itemDefinition;

            message =
                "Creado " +
                plan.DisplayName +
                " [" +
                plan.ItemId +
                "].";

            return true;
        }
        catch (Exception exception)
        {
            RollbackCreatedAssets(
                createdAssetPaths
            );

            createdAssetPaths.Clear();

            message =
                "No se pudo crear " +
                (
                    plan != null
                        ? plan.DisplayName
                        : "el artículo"
                ) +
                ": " +
                exception.Message;

            Debug.LogException(exception);

            return false;
        }
        finally
        {
            if (generatedRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    generatedRoot
                );
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(
                    previewScene
                );
            }
        }
    }

    private static GameObject BuildGeneratedPrefabRoot(
        Scene previewScene,
        BistroBuilderPlaceableFactoryPlan plan,
        BistroBuilderPlaceableFactorySettings settings,
        RestaurantEditableObjectDefinition editableDefinition,
        RestaurantPlaceableItemDefinition itemDefinition
    )
    {
        GameObject root =
            new GameObject(
                plan.AssetStem
            );

        SceneManager.MoveGameObjectToScene(
            root,
            previewScene
        );

        GameObject visualInstance =
            UnityEngine.Object.Instantiate(
                plan.SourceAsset
            );

        visualInstance.name =
            VisualRootName;

        SceneManager.MoveGameObjectToScene(
            visualInstance,
            previewScene
        );

        visualInstance.transform.SetParent(
            root.transform,
            false
        );

        if (!TryCalculateLocalBounds(
                root.transform,
                out Bounds localBounds,
                out string boundsError
            ))
        {
            throw new InvalidOperationException(
                boundsError
            );
        }

        bool hasCollider =
            root.GetComponentInChildren<Collider>(
                true
            ) != null;

        if (!hasCollider &&
            settings.GenerateColliderWhenMissing)
        {
            BoxCollider boxCollider =
                root.AddComponent<BoxCollider>();

            boxCollider.center =
                localBounds.center;

            boxCollider.size =
                new Vector3(
                    Mathf.Max(0.01f, localBounds.size.x),
                    Mathf.Max(0.01f, localBounds.size.y),
                    Mathf.Max(0.01f, localBounds.size.z)
                );
        }

        RestaurantAreaMember areaMember =
            root.AddComponent<RestaurantAreaMember>();

        RestaurantPlacementFootprint footprint =
            root.AddComponent<RestaurantPlacementFootprint>();

        RestaurantEditableObject editableObject =
            root.AddComponent<RestaurantEditableObject>();

        RestaurantPlaceableObject placeableObject =
            root.AddComponent<RestaurantPlaceableObject>();

        Transform placementAnchor =
            CreateChildTransform(
                root.transform,
                PlacementAnchorName,
                new Vector3(
                    localBounds.center.x,
                    localBounds.min.y,
                    localBounds.center.z
                )
            );

        ConfigureAreaMember(
            areaMember,
            settings.RequiredCapabilities
        );

        ConfigureFootprint(
            footprint,
            localBounds,
            settings
        );

        ConfigureEditableObject(
            editableObject,
            editableDefinition
        );

        ConfigurePlaceableObject(
            placeableObject,
            itemDefinition,
            placementAnchor
        );

        if (settings.Preset ==
            BistroBuilderPlaceableFactoryPreset.Table)
        {
            ConfigureFunctionalTable(
                root,
                localBounds,
                settings
            );
        }

        return root;
    }

    private static void ConfigureEditableDefinition(
        RestaurantEditableObjectDefinition definition,
        BistroBuilderPlaceableFactoryPlan plan,
        BistroBuilderPlaceableFactorySettings settings
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                definition
            );

        SetString(
            serialized,
            "definitionId",
            plan.ItemId
        );

        SetString(
            serialized,
            "displayName",
            plan.DisplayName
        );

        SetString(
            serialized,
            "description",
            plan.Description
        );

        SetBool(
            serialized,
            "canMove",
            settings.CanMove
        );

        SetBool(
            serialized,
            "canRotate",
            settings.CanRotate
        );

        SetBool(
            serialized,
            "useCustomGridSize",
            false
        );

        SetFloat(
            serialized,
            "customGridSize",
            0.25f
        );

        SetBool(
            serialized,
            "useCustomRotationStep",
            true
        );

        SetFloat(
            serialized,
            "customRotationStepDegrees",
            Mathf.Clamp(
                settings.RotationStepDegrees,
                1f,
                180f
            )
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureItemDefinitionBeforePrefab(
        RestaurantPlaceableItemDefinition definition,
        RestaurantEditableObjectDefinition editableDefinition,
        BistroBuilderPlaceableFactoryPlan plan,
        BistroBuilderPlaceableFactorySettings settings
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                definition
            );

        SetString(
            serialized,
            "itemId",
            plan.ItemId
        );

        SetString(
            serialized,
            "displayName",
            plan.DisplayName
        );

        SetEnumIndex(
            serialized,
            "category",
            (int)plan.Category
        );

        SetString(
            serialized,
            "description",
            plan.Description
        );

        SetObjectReference(
            serialized,
            "editableDefinition",
            editableDefinition
        );

        SetInteger(
            serialized,
            "purchasePrice",
            Mathf.Max(0, settings.PurchasePrice)
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAreaMember(
        RestaurantAreaMember areaMember,
        IReadOnlyList<RestaurantAreaCapabilityDefinition>
            capabilities
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                areaMember
            );

        SetObjectReference(
            serialized,
            "assignedArea",
            null
        );

        SetObjectReference(
            serialized,
            "positionReference",
            areaMember.transform
        );

        SerializedProperty requiredCapabilities =
            serialized.FindProperty(
                "requiredCapabilities"
            );

        if (requiredCapabilities == null)
        {
            throw new InvalidOperationException(
                "RestaurantAreaMember no contiene " +
                "requiredCapabilities."
            );
        }

        int validCapabilityCount =
            capabilities != null
                ? capabilities.Count(
                    capability => capability != null
                )
                : 0;

        requiredCapabilities.arraySize =
            validCapabilityCount;

        int destinationIndex = 0;

        if (capabilities != null)
        {
            for (int index = 0;
                 index < capabilities.Count;
                 index++)
            {
                RestaurantAreaCapabilityDefinition capability =
                    capabilities[index];

                if (capability == null)
                {
                    continue;
                }

                requiredCapabilities
                    .GetArrayElementAtIndex(destinationIndex)
                    .objectReferenceValue =
                        capability;

                destinationIndex++;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFootprint(
        RestaurantPlacementFootprint footprint,
        Bounds localBounds,
        BistroBuilderPlaceableFactorySettings settings
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                footprint
            );

        SetVector3(
            serialized,
            "localCenter",
            new Vector3(
                localBounds.center.x,
                localBounds.center.y,
                localBounds.center.z
            )
        );

        SetVector2(
            serialized,
            "size",
            new Vector2(
                Mathf.Max(0.1f, localBounds.size.x),
                Mathf.Max(0.1f, localBounds.size.z)
            )
        );

        SetFloat(
            serialized,
            "boundaryInset",
            0.02f
        );

        SetFloat(
            serialized,
            "minimumClearance",
            Mathf.Max(
                0f,
                settings.MinimumClearance
            )
        );

        SetBool(
            serialized,
            "blocksOtherPlacements",
            true
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEditableObject(
        RestaurantEditableObject editableObject,
        RestaurantEditableObjectDefinition definition
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                editableObject
            );

        SetObjectReference(
            serialized,
            "definition",
            definition
        );

        SetBool(
            serialized,
            "editingEnabled",
            true
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePlaceableObject(
        RestaurantPlaceableObject placeableObject,
        RestaurantPlaceableItemDefinition itemDefinition,
        Transform placementAnchor
    )
    {
        SerializedObject serialized =
            new SerializedObject(
                placeableObject
            );

        SetObjectReference(
            serialized,
            "itemDefinition",
            itemDefinition
        );

        SetString(
            serialized,
            "instanceId",
            string.Empty
        );

        SetObjectReference(
            serialized,
            "placementAnchor",
            placementAnchor
        );

        SetBool(
            serialized,
            "synchronizeEditableDefinition",
            true
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFunctionalTable(
        GameObject root,
        Bounds localBounds,
        BistroBuilderPlaceableFactorySettings settings
    )
    {
        RestaurantTable table =
            root.AddComponent<RestaurantTable>();

        float interactionDistance =
            Mathf.Max(
                0.65f,
                localBounds.extents.z + 0.45f
            );

        float floorLocalY =
            localBounds.min.y;

        Transform customerPoint =
            CreateChildTransform(
                root.transform,
                CustomerApproachPointName,
                new Vector3(
                    localBounds.center.x,
                    floorLocalY,
                    localBounds.center.z -
                    interactionDistance
                )
            );

        Transform waiterPoint =
            CreateChildTransform(
                root.transform,
                WaiterServicePointName,
                new Vector3(
                    localBounds.center.x,
                    floorLocalY,
                    localBounds.center.z +
                    interactionDistance
                )
            );

        SerializedObject serializedTable =
            new SerializedObject(
                table
            );

        SetInteger(
            serializedTable,
            "tableId",
            1
        );

        SetInteger(
            serializedTable,
            "capacity",
            Mathf.Max(
                1,
                settings.TableCapacity
            )
        );

        SetObjectReference(
            serializedTable,
            "customerApproachPoint",
            customerPoint
        );

        SetObjectReference(
            serializedTable,
            "waiterServicePoint",
            waiterPoint
        );

        serializedTable.ApplyModifiedPropertiesWithoutUndo();

        Renderer renderer =
            root.GetComponentInChildren<Renderer>(
                true
            );

        if (renderer == null)
        {
            return;
        }

        TableStateView stateView =
            root.AddComponent<TableStateView>();

        SerializedObject serializedView =
            new SerializedObject(
                stateView
            );

        SetObjectReference(
            serializedView,
            "restaurantTable",
            table
        );

        SetObjectReference(
            serializedView,
            "tableRenderer",
            renderer
        );

        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform CreateChildTransform(
        Transform parent,
        string childName,
        Vector3 localPosition
    )
    {
        GameObject child =
            new GameObject(
                childName
            );

        child.transform.SetParent(
            parent,
            false
        );

        child.transform.localPosition =
            localPosition;

        child.transform.localRotation =
            Quaternion.identity;

        child.transform.localScale =
            Vector3.one;

        return child.transform;
    }

    private static bool TryAddDefinitionsToMainCatalog(
        IReadOnlyList<RestaurantPlaceableItemDefinition>
            definitions,
        out string message
    )
    {
        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        if (catalog == null)
        {
            EnsureAssetFolderForPath(
                MainCatalogPath
            );

            catalog =
                ScriptableObject.CreateInstance<
                    RestaurantPlaceableCatalogDefinition
                >();

            AssetDatabase.CreateAsset(
                catalog,
                MainCatalogPath
            );
        }

        SerializedObject serializedCatalog =
            new SerializedObject(
                catalog
            );

        SerializedProperty items =
            serializedCatalog.FindProperty(
                "items"
            );

        if (items == null)
        {
            message =
                "El catálogo principal no contiene la lista items.";

            return false;
        }

        HashSet<string> knownIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        List<RestaurantPlaceableItemDefinition> merged =
            new List<RestaurantPlaceableItemDefinition>();

        for (int index = 0;
             index < items.arraySize;
             index++)
        {
            RestaurantPlaceableItemDefinition existing =
                items
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue
                    as RestaurantPlaceableItemDefinition;

            if (existing == null ||
                string.IsNullOrWhiteSpace(existing.ItemId) ||
                !knownIds.Add(existing.ItemId))
            {
                continue;
            }

            merged.Add(existing);
        }

        int addedCount = 0;

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                definitions[index];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId) ||
                !knownIds.Add(definition.ItemId))
            {
                continue;
            }

            merged.Add(definition);
            addedCount++;
        }

        merged.Sort(
            CompareDefinitions
        );

        items.arraySize =
            merged.Count;

        for (int index = 0;
             index < merged.Count;
             index++)
        {
            items
                .GetArrayElementAtIndex(index)
                .objectReferenceValue =
                    merged[index];
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);

        message =
            "Catálogo actualizado. Artículos añadidos: " +
            addedCount +
            ".";

        return true;
    }

    private static bool TryAnalyzeBounds(
        GameObject source,
        out Bounds localBounds,
        out bool hasCollider,
        out string errorMessage
    )
    {
        localBounds =
            default(Bounds);

        hasCollider =
            false;

        errorMessage =
            string.Empty;

        Scene previewScene =
            default(Scene);

        GameObject root =
            null;

        try
        {
            previewScene =
                EditorSceneManager.NewPreviewScene();

            root =
                new GameObject(
                    "FactoryAnalysisRoot"
                );

            SceneManager.MoveGameObjectToScene(
                root,
                previewScene
            );

            GameObject visual =
                UnityEngine.Object.Instantiate(
                    source
                );

            SceneManager.MoveGameObjectToScene(
                visual,
                previewScene
            );

            visual.transform.SetParent(
                root.transform,
                false
            );

            hasCollider =
                root.GetComponentInChildren<Collider>(
                    true
                ) != null;

            if (!TryCalculateLocalBounds(
                    root.transform,
                    out localBounds,
                    out errorMessage
                ))
            {
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            errorMessage =
                "No se pudieron analizar los límites: " +
                exception.Message;

            return false;
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(
                    previewScene
                );
            }
        }
    }

    private static bool TryCalculateLocalBounds(
        Transform root,
        out Bounds localBounds,
        out string errorMessage
    )
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (renderer == null)
            {
                continue;
            }

            EncapsulateWorldBoundsInRootSpace(
                root,
                renderer.bounds,
                ref hasBounds,
                ref minimum,
                ref maximum
            );
        }

        if (!hasBounds)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(
                    true
                );

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider =
                    colliders[index];

                if (collider == null)
                {
                    continue;
                }

                EncapsulateWorldBoundsInRootSpace(
                    root,
                    collider.bounds,
                    ref hasBounds,
                    ref minimum,
                    ref maximum
                );
            }
        }

        if (!hasBounds)
        {
            localBounds =
                default(Bounds);

            errorMessage =
                "El asset no contiene Renderer ni Collider utilizable.";

            return false;
        }

        localBounds =
            new Bounds();

        localBounds.SetMinMax(
            minimum,
            maximum
        );

        errorMessage =
            string.Empty;

        return true;
    }

    private static void EncapsulateWorldBoundsInRootSpace(
        Transform root,
        Bounds worldBounds,
        ref bool hasBounds,
        ref Vector3 minimum,
        ref Vector3 maximum
    )
    {
        Vector3 center =
            worldBounds.center;

        Vector3 extents =
            worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)
                        );

                    Vector3 localCorner =
                        root.InverseTransformPoint(
                            worldCorner
                        );

                    if (!hasBounds)
                    {
                        minimum = localCorner;
                        maximum = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum =
                            Vector3.Min(
                                minimum,
                                localCorner
                            );

                        maximum =
                            Vector3.Max(
                                maximum,
                                localCorner
                            );
                    }
                }
            }
        }
    }

    private static BistroBuilderPlaceableFactoryPlan
        CreateBlockedPlan(
            GameObject source,
            string sourcePath,
            string message
        )
    {
        return new BistroBuilderPlaceableFactoryPlan(
            source,
            sourcePath,
            BistroBuilderPlaceableFactoryPlanStatus.Blocked,
            message,
            string.Empty,
            source != null
                ? HumanizeName(source.name)
                : "Artículo",
            string.Empty,
            source != null
                ? SanitizeAssetStem(source.name)
                : "PlaceableItem",
            string.Empty,
            string.Empty,
            string.Empty,
            RestaurantPlaceableItemCategory.Other,
            default(Bounds),
            false,
            false,
            false
        );
    }

    private static HashSet<string> CollectExistingItemIds()
    {
        HashSet<string> result =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableItemDefinition"
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(path);

            if (definition != null &&
                !string.IsNullOrWhiteSpace(
                    definition.ItemId
                ))
            {
                result.Add(
                    definition.ItemId
                );
            }
        }

        return result;
    }

    private static string GenerateUniqueItemId(
        string baseItemId,
        HashSet<string> reservedIds
    )
    {
        if (!reservedIds.Contains(baseItemId))
        {
            return baseItemId;
        }

        int suffix = 2;

        while (true)
        {
            string candidate =
                baseItemId +
                "_" +
                suffix;

            if (!reservedIds.Contains(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string GenerateReservedUniquePath(
        string desiredPath,
        HashSet<string> reservedPaths
    )
    {
        string normalizedDesiredPath =
            NormalizeUnityAssetPath(
                desiredPath
            );

        ValidateUnityAssetPath(
            normalizedDesiredPath
        );

        string directory =
            GetUnityAssetDirectory(
                normalizedDesiredPath
            );

        string fileNameWithoutExtension =
            GetUnityAssetFileNameWithoutExtension(
                normalizedDesiredPath
            );

        string extension =
            GetUnityAssetExtension(
                normalizedDesiredPath
            );

        string candidate =
            normalizedDesiredPath;

        int suffix = 2;

        while (reservedPaths.Contains(candidate) ||
               AssetDatabase.LoadMainAssetAtPath(candidate) != null)
        {
            candidate =
                directory +
                "/" +
                fileNameWithoutExtension +
                "_" +
                suffix +
                extension;

            suffix++;
        }

        reservedPaths.Add(candidate);

        return candidate;
    }

    private static bool IsSupportedGameObjectAssetPath(
        string path
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension =
            GetUnityAssetExtension(path)
                .ToLowerInvariant();

        return
            extension == ".prefab" ||
            extension == ".fbx" ||
            extension == ".obj" ||
            extension == ".dae" ||
            extension == ".blend" ||
            extension == ".3ds";
    }

    private static RestaurantPlaceableItemCategory ResolveCategory(
        BistroBuilderPlaceableFactoryPreset preset
    )
    {
        switch (preset)
        {
            case BistroBuilderPlaceableFactoryPreset.Chair:
                return RestaurantPlaceableItemCategory.Seating;

            case BistroBuilderPlaceableFactoryPreset.Decoration:
                return RestaurantPlaceableItemCategory.Decoration;

            case BistroBuilderPlaceableFactoryPreset.FloorLamp:
                return RestaurantPlaceableItemCategory.Lighting;

            case BistroBuilderPlaceableFactoryPreset
                .KitchenEquipment:
                return RestaurantPlaceableItemCategory
                    .KitchenEquipment;

            case BistroBuilderPlaceableFactoryPreset
                .ServiceEquipment:
                return RestaurantPlaceableItemCategory
                    .ServiceEquipment;

            case BistroBuilderPlaceableFactoryPreset.Structural:
                return RestaurantPlaceableItemCategory.Structural;

            default:
                return RestaurantPlaceableItemCategory.Furniture;
        }
    }

    private static string ResolveCategoryFolder(
        RestaurantPlaceableItemCategory category
    )
    {
        switch (category)
        {
            case RestaurantPlaceableItemCategory.Seating:
                return "Seating";

            case RestaurantPlaceableItemCategory.Lighting:
                return "Lighting";

            case RestaurantPlaceableItemCategory.Decoration:
                return "Decoration";

            case RestaurantPlaceableItemCategory.KitchenEquipment:
                return "KitchenEquipment";

            case RestaurantPlaceableItemCategory.ServiceEquipment:
                return "ServiceEquipment";

            case RestaurantPlaceableItemCategory.Structural:
                return "Structural";

            case RestaurantPlaceableItemCategory.Other:
                return "Other";

            default:
                return "Furniture";
        }
    }

    private static bool PresetHasFutureFunctionalAdapter(
        BistroBuilderPlaceableFactoryPreset preset
    )
    {
        return
            preset ==
                BistroBuilderPlaceableFactoryPreset.Chair ||
            preset ==
                BistroBuilderPlaceableFactoryPreset.FloorLamp ||
            preset ==
                BistroBuilderPlaceableFactoryPreset
                    .KitchenEquipment ||
            preset ==
                BistroBuilderPlaceableFactoryPreset
                    .ServiceEquipment;
    }

    private static string BuildDefaultDescription(
        string displayName,
        BistroBuilderPlaceableFactoryPreset preset
    )
    {
        string categoryText;

        switch (preset)
        {
            case BistroBuilderPlaceableFactoryPreset.Table:
                categoryText = "Mesa";
                break;

            case BistroBuilderPlaceableFactoryPreset.Chair:
                categoryText = "Asiento";
                break;

            case BistroBuilderPlaceableFactoryPreset.Decoration:
                categoryText = "Elemento decorativo";
                break;

            case BistroBuilderPlaceableFactoryPreset.FloorLamp:
                categoryText = "Elemento de iluminación";
                break;

            case BistroBuilderPlaceableFactoryPreset.KitchenEquipment:
                categoryText = "Equipamiento de cocina";
                break;

            case BistroBuilderPlaceableFactoryPreset.ServiceEquipment:
                categoryText = "Equipamiento de servicio";
                break;

            case BistroBuilderPlaceableFactoryPreset.Structural:
                categoryText = "Elemento estructural";
                break;

            default:
                categoryText = "Mobiliario";
                break;
        }

        return
            categoryText +
            ": " +
            displayName +
            ".";
    }

    private static void AddCapabilityById(
        List<RestaurantAreaCapabilityDefinition> destination,
        string capabilityId
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantAreaCapabilityDefinition"
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantAreaCapabilityDefinition capability =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantAreaCapabilityDefinition
                >(path);

            if (capability == null ||
                !string.Equals(
                    capability.CapabilityId,
                    capabilityId,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            destination.Add(capability);
            return;
        }
    }

    private static int CompareDefinitions(
        RestaurantPlaceableItemDefinition first,
        RestaurantPlaceableItemDefinition second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int categoryComparison =
            first.Category.CompareTo(second.Category);

        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );
    }

    private static void EnsureAssetFolderForPath(
        string assetPath
    )
    {
        string normalizedAssetPath =
            NormalizeUnityAssetPath(
                assetPath
            );

        ValidateUnityAssetPath(
            normalizedAssetPath
        );

        string folder =
            GetUnityAssetDirectory(
                normalizedAssetPath
            );

        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] segments =
            folder.Split('/');

        string current =
            segments[0];

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string segment =
                segments[index];

            if (string.IsNullOrWhiteSpace(segment) ||
                segment == "." ||
                segment == "..")
            {
                throw new InvalidOperationException(
                    "La ruta contiene un segmento no válido: " +
                    normalizedAssetPath
                );
            }

            string next =
                current +
                "/" +
                segment;

            if (!AssetDatabase.IsValidFolder(next))
            {
                string createdFolderGuid =
                    AssetDatabase.CreateFolder(
                        current,
                        segment
                    );

                if (string.IsNullOrWhiteSpace(
                        createdFolderGuid
                    ) ||
                    !AssetDatabase.IsValidFolder(next))
                {
                    throw new InvalidOperationException(
                        "Unity no pudo crear la carpeta " +
                        next +
                        " para el asset " +
                        normalizedAssetPath +
                        "."
                    );
                }
            }

            current = next;
        }
    }

    private static string NormalizeUnityAssetPath(
        string assetPath
    )
    {
        return
            (assetPath ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
    }

    private static void ValidateUnityAssetPath(
        string assetPath
    )
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidOperationException(
                "La fábrica recibió una ruta de asset vacía."
            );
        }

        if (!assetPath.StartsWith(
                "Assets/",
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                "La ruta debe comenzar por Assets/: " +
                assetPath
            );
        }

        if (assetPath.EndsWith(
                "/",
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                "La ruta debe incluir un nombre de archivo: " +
                assetPath
            );
        }

        if (assetPath.IndexOf(
                "//",
                StringComparison.Ordinal
            ) >= 0)
        {
            throw new InvalidOperationException(
                "La ruta contiene separadores duplicados: " +
                assetPath
            );
        }

        if (assetPath.IndexOf(
                '\0'
            ) >= 0)
        {
            throw new InvalidOperationException(
                "La ruta contiene un carácter nulo: " +
                assetPath
            );
        }

        string[] segments =
            assetPath.Split('/');

        for (int index = 0;
             index < segments.Length;
             index++)
        {
            string segment =
                segments[index];

            if (string.IsNullOrWhiteSpace(segment) ||
                segment == "." ||
                segment == "..")
            {
                throw new InvalidOperationException(
                    "La ruta contiene un segmento no válido: " +
                    assetPath
                );
            }

            if (segment.IndexOfAny(
                    new[]
                    {
                        ':',
                        '*',
                        '?',
                        '"',
                        '<',
                        '>',
                        '|'
                    }
                ) >= 0)
            {
                throw new InvalidOperationException(
                    "La ruta contiene caracteres no válidos: " +
                    assetPath
                );
            }
        }

        if (string.IsNullOrWhiteSpace(
                GetUnityAssetExtension(assetPath)
            ))
        {
            throw new InvalidOperationException(
                "La ruta no contiene una extensión de archivo: " +
                assetPath
            );
        }
    }

    private static string GetUnityAssetDirectory(
        string assetPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(
                assetPath
            );

        int separatorIndex =
            normalized.LastIndexOf('/');

        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException(
                "No se pudo determinar la carpeta de la ruta: " +
                normalized
            );
        }

        return normalized.Substring(
            0,
            separatorIndex
        );
    }

    private static string GetUnityAssetFileNameWithoutExtension(
        string assetPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(
                assetPath
            );

        int separatorIndex =
            normalized.LastIndexOf('/');

        int extensionIndex =
            normalized.LastIndexOf('.');

        int nameStart =
            separatorIndex + 1;

        if (extensionIndex <= nameStart)
        {
            throw new InvalidOperationException(
                "No se pudo determinar el nombre del archivo: " +
                normalized
            );
        }

        return normalized.Substring(
            nameStart,
            extensionIndex - nameStart
        );
    }

    private static string GetUnityAssetExtension(
        string assetPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(
                assetPath
            );

        int separatorIndex =
            normalized.LastIndexOf('/');

        int extensionIndex =
            normalized.LastIndexOf('.');

        if (extensionIndex <= separatorIndex)
        {
            return string.Empty;
        }

        return normalized.Substring(
            extensionIndex
        );
    }

    private static void RollbackCreatedAssets(
        IReadOnlyList<string> paths
    )
    {
        if (paths == null)
        {
            return;
        }

        for (int index = paths.Count - 1;
             index >= 0;
             index--)
        {
            string path =
                paths[index];

            if (!string.IsNullOrWhiteSpace(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void RunProjectHealth()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall +=
                RunProjectHealth;

            return;
        }

        BistroBuilderValidationReport report =
            BistroBuilderProjectValidator
                .RunFullValidation(true);

        BistroBuilderProjectHealthWindow.SetReport(
            report
        );
    }

    private static string NormalizeIdentifier(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed =
            value.Normalize(
                NormalizationForm.FormD
            );

        StringBuilder builder =
            new StringBuilder();

        bool previousUnderscore = false;

        for (int index = 0;
             index < decomposed.Length;
             index++)
        {
            char character =
                decomposed[index];

            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character
                );

            if (category ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(
                    char.ToLowerInvariant(character)
                );

                previousUnderscore = false;
            }
            else if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder
            .ToString()
            .Trim('_');
    }

    private static string SanitizeAssetStem(
        string value
    )
    {
        string normalized =
            NormalizeIdentifier(value);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        string[] segments =
            normalized.Split(
                new[]
                {
                    '_'
                },
                StringSplitOptions.RemoveEmptyEntries
            );

        StringBuilder builder =
            new StringBuilder();

        for (int index = 0;
             index < segments.Length;
             index++)
        {
            string segment =
                segments[index];

            builder.Append(
                char.ToUpperInvariant(segment[0])
            );

            if (segment.Length > 1)
            {
                builder.Append(
                    segment.Substring(1)
                );
            }
        }

        return builder.ToString();
    }

    private static string HumanizeName(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Artículo";
        }

        string cleaned =
            value
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim();

        StringBuilder builder =
            new StringBuilder();

        for (int index = 0;
             index < cleaned.Length;
             index++)
        {
            char character =
                cleaned[index];

            if (index > 0 &&
                char.IsUpper(character) &&
                char.IsLetterOrDigit(cleaned[index - 1]) &&
                cleaned[index - 1] != ' ')
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        string result =
            builder
                .ToString()
                .Trim();

        if (string.IsNullOrWhiteSpace(result))
        {
            return "Artículo";
        }

        return
            char.ToUpperInvariant(result[0]) +
            (
                result.Length > 1
                    ? result.Substring(1)
                    : string.Empty
            );
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value
    )
    {
        SerializedProperty property =
            RequireProperty(
                serializedObject,
                propertyName
            );

        property.stringValue =
            value ?? string.Empty;
    }

    private static void SetInteger(
        SerializedObject serializedObject,
        string propertyName,
        int value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).intValue = value;
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).floatValue = value;
    }

    private static void SetBool(
        SerializedObject serializedObject,
        string propertyName,
        bool value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).boolValue = value;
    }

    private static void SetEnumIndex(
        SerializedObject serializedObject,
        string propertyName,
        int value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).enumValueIndex = value;
    }

    private static void SetVector2(
        SerializedObject serializedObject,
        string propertyName,
        Vector2 value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).vector2Value = value;
    }

    private static void SetVector3(
        SerializedObject serializedObject,
        string propertyName,
        Vector3 value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).vector3Value = value;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value
    )
    {
        RequireProperty(
            serializedObject,
            propertyName
        ).objectReferenceValue = value;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property =
            serializedObject.FindProperty(
                propertyName
            );

        if (property == null)
        {
            throw new InvalidOperationException(
                serializedObject.targetObject.name +
                " no contiene la propiedad serializada " +
                propertyName +
                "."
            );
        }

        return property;
    }
}
