using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente de la base universal de asientos.
///
/// Modifica únicamente datos, prefabs y escena. Antes de escribir
/// realiza una copia binaria y restaura todo si falla la validación.
/// </summary>
public static class
    BistroBuilderUniversalSeatingFoundationInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Seating/" +
        "Install or Repair Seating Foundation";

    private const string ChairPrefabPath =
        "Assets/Prefabs/Restaurant/Generated/Furniture/" +
        "SillaBistroDeMadera.prefab";

    private const string TablePrefabPath =
        "Assets/Prefabs/Restaurant/Furniture/Table_Basic.prefab";

    private const string ChairEditableDefinitionPath =
        "Assets/Data/Restaurant/EditMode/EditableDefinitions/" +
        "EditableObjectDefinition_SillaBistroDeMadera.asset";

    private const string SeatingFolderPath =
        "Assets/Data/Restaurant/Seating";

    private const string ProfileFolderPath =
        SeatingFolderPath +
        "/SeatUseProfiles";

    private const string ConfigurationFolderPath =
        SeatingFolderPath +
        "/TableConfigurations";

    private const string ProfileAssetPath =
        ProfileFolderPath +
        "/SeatUseProfile_StandardDiningChair.asset";

    private const string StandardsAssetPath =
        SeatingFolderPath +
        "/RestaurantSeatingStandards.asset";

    private const string TableBasicConfigurationPath =
        ConfigurationFolderPath +
        "/TableSeatingConfiguration_TableBasic2.asset";

    [MenuItem(MenuPath, false, 100)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de ejecutar el instalador.",
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
                "de instalar.",
                "Aceptar"
            );

            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador. " +
                "Así la copia de seguridad representará exactamente " +
                "el estado actual.",
                "Aceptar"
            );

            return;
        }

        AssetDatabase.SaveAssets();

        string[] protectedAssetPaths =
        {
            scene.path,
            ChairPrefabPath,
            TablePrefabPath,
            ChairEditableDefinitionPath,
            ProfileAssetPath,
            StandardsAssetPath,
            TableBasicConfigurationPath
        };

        InstallationBackup backup =
            InstallationBackup.Capture(
                protectedAssetPaths
            );

        try
        {
            EnsureFolder(SeatingFolderPath);
            EnsureFolder(ProfileFolderPath);
            EnsureFolder(ConfigurationFolderPath);

            RestaurantSeatUseProfileDefinition profile =
                LoadOrCreateAsset<
                    RestaurantSeatUseProfileDefinition
                >(ProfileAssetPath);

            RestaurantSeatingStandardsDefinition standards =
                LoadOrCreateAsset<
                    RestaurantSeatingStandardsDefinition
                >(StandardsAssetPath);

            RestaurantTableSeatingConfigurationDefinition
                tableBasicConfiguration =
                    LoadOrCreateAsset<
                        RestaurantTableSeatingConfigurationDefinition
                    >(TableBasicConfigurationPath);

            ConfigureStandardChairProfile(profile);
            ConfigureChairEditableDefinition();
            ConfigureStandards(standards);

            ConfigureTableBasicDefinition(
                tableBasicConfiguration
            );

            ConfigureChairPrefab(profile);

            ConfigureTablePrefab(
                tableBasicConfiguration
            );

            ConfigureSceneTables(
                scene,
                tableBasicConfiguration
            );

            ConfigureGameSystems(scene);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(standards);
            EditorUtility.SetDirty(tableBasicConfiguration);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderSeatingFoundationValidationResult result =
                BistroBuilderSeatingFoundationValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    result.BuildReport()
                );
            }

            Debug.Log(
                "BISTRO BUILDER - SEATING FOUNDATION\n" +
                result.BuildReport()
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Base universal de asientos instalada.\n\n" +
                "Errores: " +
                result.ErrorCount +
                "\nAdvertencias: " +
                result.WarningCount +
                "\n\nEjecuta ahora Validate Seating Foundation.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            backup.Restore();

            try
            {
                EditorSceneManager.OpenScene(
                    scene.path,
                    OpenSceneMode.Single
                );
            }
            catch (Exception reloadException)
            {
                Debug.LogException(reloadException);
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación ha fallado y se han restaurado " +
                "los archivos anteriores.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ConfigureStandardChairProfile(
        RestaurantSeatUseProfileDefinition profile
    )
    {
        SerializedObject serialized =
            new SerializedObject(profile);

        SetString(serialized, "profileId", "standard_dining_chair");
        SetString(serialized, "displayName", "Silla de comedor estándar");
        SetFloat(serialized, "seatHeight", 0.46f);
        SetFloat(serialized, "pullOutDistance", 0.35f);
        SetFloat(serialized, "occupiedPullOutDistance", 0.12f);
        SetFloat(serialized, "customerApproachDistance", 0.35f);
        SetFloat(serialized, "customerApproachRadius", 0.25f);
        SetFloat(serialized, "slotPositionTolerance", 0.06f);
        SetFloat(serialized, "maximumFacingAngle", 10f);
        SetFloat(serialized, "maximumVerticalDifference", 0.12f);
        SetFloat(serialized, "pullOutDuration", 0.45f);
        SetFloat(serialized, "occupiedTransitionDuration", 0.25f);
        SetFloat(serialized, "returnDuration", 0.40f);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void ConfigureChairEditableDefinition()
    {
        RestaurantEditableObjectDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                RestaurantEditableObjectDefinition
            >(ChairEditableDefinitionPath);

        if (definition == null)
        {
            throw new InvalidOperationException(
                ChairEditableDefinitionPath +
                " no existe."
            );
        }

        SerializedObject serialized =
            new SerializedObject(definition);

        SetBoolean(
            serialized,
            "useCustomGridSize",
            true
        );

        SetFloat(
            serialized,
            "customGridSize",
            0.05f
        );

        SetBoolean(
            serialized,
            "useCustomRotationStep",
            true
        );

        SetFloat(
            serialized,
            "customRotationStepDegrees",
            15f
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureStandards(
        RestaurantSeatingStandardsDefinition standards
    )
    {
        SerializedObject serialized =
            new SerializedObject(standards);

        SerializedProperty rectangular =
            RequireProperty(
                serialized,
                "rectangularCapacities"
            );

        rectangular.arraySize = 3;
        rectangular.GetArrayElementAtIndex(0).intValue = 2;
        rectangular.GetArrayElementAtIndex(1).intValue = 4;
        rectangular.GetArrayElementAtIndex(2).intValue = 6;

        SerializedProperty round =
            RequireProperty(
                serialized,
                "roundTableStandards"
            );

        round.arraySize = 4;

        ConfigureRoundStandard(
            round.GetArrayElementAtIndex(0),
            4,
            1.00f,
            true
        );

        ConfigureRoundStandard(
            round.GetArrayElementAtIndex(1),
            6,
            1.20f,
            true
        );

        ConfigureRoundStandard(
            round.GetArrayElementAtIndex(2),
            8,
            1.50f,
            true
        );

        ConfigureRoundStandard(
            round.GetArrayElementAtIndex(3),
            10,
            0f,
            false
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRoundStandard(
        SerializedProperty property,
        int capacity,
        float diameter,
        bool approved
    )
    {
        property.FindPropertyRelative("capacity").intValue =
            capacity;

        property.FindPropertyRelative("diameterMetres").floatValue =
            diameter;

        property.FindPropertyRelative("diameterIsApproved").boolValue =
            approved;
    }

    private static void ConfigureTableBasicDefinition(
        RestaurantTableSeatingConfigurationDefinition definition
    )
    {
        SerializedObject serialized =
            new SerializedObject(definition);

        SetString(
            serialized,
            "configurationId",
            "table_basic_2_rectangular"
        );

        SetString(
            serialized,
            "displayName",
            "Mesa básica rectangular de 2 clientes"
        );

        SetInteger(serialized, "maximumCustomers", 2);

        SetEnum(
            serialized,
            "shape",
            (int)RestaurantTableSeatingShape.Rectangular
        );

        SetBoolean(
            serialized,
            "usePlacementFootprintDimensions",
            true
        );

        SetInteger(serialized, "positiveZSeats", 1);
        SetInteger(serialized, "negativeZSeats", 1);
        SetInteger(serialized, "positiveXSeats", 0);
        SetInteger(serialized, "negativeXSeats", 0);
        SetFloat(serialized, "sideEndInset", 0.10f);
        SetFloat(serialized, "minimumSpacePerCustomer", 0.55f);
        SetFloat(serialized, "parkedGapFromTableEdge", 0.10f);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureChairPrefab(
        RestaurantSeatUseProfileDefinition profile
    )
    {
        GameObject root =
            PrefabUtility.LoadPrefabContents(
                ChairPrefabPath
            );

        try
        {
            RestaurantPlacementFootprint footprint =
                root.GetComponent<RestaurantPlacementFootprint>();

            if (footprint == null)
            {
                throw new InvalidOperationException(
                    ChairPrefabPath +
                    " no contiene RestaurantPlacementFootprint."
                );
            }

            RestaurantSeat seat =
                root.GetComponent<RestaurantSeat>();

            if (seat == null)
            {
                seat = root.AddComponent<RestaurantSeat>();
            }

            RestaurantOperationalClearanceSet clearanceSet =
                root.GetComponent<RestaurantOperationalClearanceSet>();

            if (clearanceSet == null)
            {
                clearanceSet =
                    root.AddComponent<RestaurantOperationalClearanceSet>();
            }

            Transform operationalRoot =
                FindOrCreateDirectChild(
                    root.transform,
                    "OperationalMotionRoot"
                );

            operationalRoot.localPosition = Vector3.zero;
            operationalRoot.localRotation = Quaternion.identity;
            operationalRoot.localScale = Vector3.one;

            Transform visual =
                root.transform.Find("Visual");

            if (visual == null)
            {
                visual =
                    root.transform.Find(
                        "OperationalMotionRoot/Visual"
                    );
            }

            if (visual == null)
            {
                throw new InvalidOperationException(
                    ChairPrefabPath +
                    " no contiene el nodo Visual."
                );
            }

            if (!ReferenceEquals(
                    visual.parent,
                    operationalRoot
                ))
            {
                visual.SetParent(
                    operationalRoot,
                    false
                );
            }

            Vector3 footprintCenter = footprint.LocalCenter;
            float halfDepth = footprint.Size.y * 0.5f;
            float frontEdgeZ = footprintCenter.z + halfDepth;
            float backEdgeZ = footprintCenter.z - halfDepth;

            Transform associationPoint =
                FindOrCreateDirectChild(
                    root.transform,
                    "AssociationPoint"
                );

            associationPoint.localPosition =
                new Vector3(
                    footprintCenter.x,
                    0f,
                    frontEdgeZ
                );

            associationPoint.localRotation = Quaternion.identity;

            Transform seatPoint =
                FindOrCreateDirectChild(
                    operationalRoot,
                    "SeatPoint"
                );

            seatPoint.localPosition =
                new Vector3(
                    footprintCenter.x,
                    profile.SeatHeight,
                    footprintCenter.z
                );

            seatPoint.localRotation = Quaternion.identity;

            Transform approachPoint =
                FindOrCreateDirectChild(
                    root.transform,
                    "CustomerApproachPoint"
                );

            approachPoint.localPosition =
                new Vector3(
                    footprintCenter.x,
                    0f,
                    backEdgeZ -
                    profile.PullOutDistance -
                    profile.CustomerApproachDistance
                );

            approachPoint.localRotation = Quaternion.identity;

            SerializedObject serializedSeat =
                new SerializedObject(seat);

            SetObjectReference(
                serializedSeat,
                "useProfile",
                profile
            );

            SetEnum(
                serializedSeat,
                "facingAxis",
                (int)RestaurantSeatFacingAxis.PositiveZ
            );

            SetObjectReference(
                serializedSeat,
                "placeableObject",
                root.GetComponent<RestaurantPlaceableObject>()
            );

            SetObjectReference(
                serializedSeat,
                "associationPoint",
                associationPoint
            );

            SetObjectReference(
                serializedSeat,
                "operationalMotionRoot",
                operationalRoot
            );

            SetObjectReference(
                serializedSeat,
                "seatPoint",
                seatPoint
            );

            SetObjectReference(
                serializedSeat,
                "customerApproachPoint",
                approachPoint
            );

            serializedSeat.ApplyModifiedPropertiesWithoutUndo();

            ConfigureChairClearance(
                clearanceSet,
                footprint,
                profile
            );

            if (!seat.ValidateConfiguration(out string error))
            {
                throw new InvalidOperationException(error);
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                ChairPrefabPath
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureChairClearance(
        RestaurantOperationalClearanceSet clearanceSet,
        RestaurantPlacementFootprint footprint,
        RestaurantSeatUseProfileDefinition profile
    )
    {
        float halfDepth = footprint.Size.y * 0.5f;
        float backEdgeZ = footprint.LocalCenter.z - halfDepth;

        float operationalDepth =
            profile.PullOutDistance +
            profile.CustomerApproachDistance +
            profile.CustomerApproachRadius;

        SerializedObject serialized =
            new SerializedObject(clearanceSet);

        SetBoolean(
            serialized,
            "blocksOtherPlacements",
            true
        );

        SetBoolean(
            serialized,
            "requiresClearanceForOwner",
            true
        );

        SerializedProperty clearances =
            RequireProperty(
                serialized,
                "clearances"
            );

        clearances.arraySize = 1;

        SerializedProperty clearance =
            clearances.GetArrayElementAtIndex(0);

        clearance.FindPropertyRelative("clearanceId").stringValue =
            "seat_pullout_and_approach";

        clearance.FindPropertyRelative("localCenter").vector3Value =
            new Vector3(
                footprint.LocalCenter.x,
                0f,
                backEdgeZ -
                operationalDepth * 0.5f
            );

        clearance.FindPropertyRelative("size").vector2Value =
            new Vector2(
                Mathf.Max(
                    footprint.Size.x + 0.10f,
                    profile.CustomerApproachRadius * 2f
                ),
                operationalDepth
            );

        clearance.FindPropertyRelative("localYawDegrees").floatValue =
            0f;

        clearance.FindPropertyRelative("blockedUserMessage").stringValue =
            "La silla no puede retirarse para sentar al cliente.";

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTablePrefab(
        RestaurantTableSeatingConfigurationDefinition definition
    )
    {
        GameObject root =
            PrefabUtility.LoadPrefabContents(
                TablePrefabPath
            );

        try
        {
            RestaurantTable table =
                root.GetComponent<RestaurantTable>();

            RestaurantPlacementFootprint footprint =
                root.GetComponent<RestaurantPlacementFootprint>();

            RestaurantPlaceableObject placeable =
                root.GetComponent<RestaurantPlaceableObject>();

            if (table == null ||
                footprint == null ||
                placeable == null)
            {
                throw new InvalidOperationException(
                    TablePrefabPath +
                    " no contiene todos sus componentes base."
                );
            }

            RestaurantTableSeatingConfiguration configuration =
                root.GetComponent<
                    RestaurantTableSeatingConfiguration
                >();

            if (configuration == null)
            {
                configuration =
                    root.AddComponent<
                        RestaurantTableSeatingConfiguration
                    >();
            }

            ConfigureTableComponent(
                configuration,
                table,
                footprint,
                placeable.PlacementAnchor,
                definition
            );

            if (!configuration.ValidateConfiguration(
                    out string error
                ))
            {
                throw new InvalidOperationException(error);
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                TablePrefabPath
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSceneTables(
        Scene scene,
        RestaurantTableSeatingConfigurationDefinition tableBasicDefinition
    )
    {
        RestaurantTable[] tables =
            FindSceneComponents<RestaurantTable>(scene);

        for (int index = 0;
             index < tables.Length;
             index++)
        {
            RestaurantTable table = tables[index];

            if (table.Capacity != 2)
            {
                continue;
            }

            RestaurantPlacementFootprint footprint =
                table.GetComponent<RestaurantPlacementFootprint>();

            RestaurantPlaceableObject placeable =
                table.GetComponent<RestaurantPlaceableObject>();

            if (footprint == null ||
                placeable == null)
            {
                throw new InvalidOperationException(
                    table.name +
                    " no contiene Footprint o PlaceableObject."
                );
            }

            RestaurantTableSeatingConfiguration configuration =
                table.GetComponent<
                    RestaurantTableSeatingConfiguration
                >();

            if (configuration == null)
            {
                configuration =
                    Undo.AddComponent<
                        RestaurantTableSeatingConfiguration
                    >(table.gameObject);
            }

            ConfigureTableComponent(
                configuration,
                table,
                footprint,
                placeable.PlacementAnchor,
                tableBasicDefinition
            );

            EditorUtility.SetDirty(configuration);
        }
    }

    private static void ConfigureTableComponent(
        RestaurantTableSeatingConfiguration configuration,
        RestaurantTable table,
        RestaurantPlacementFootprint footprint,
        Transform seatingCenter,
        RestaurantTableSeatingConfigurationDefinition definition
    )
    {
        SerializedObject serialized =
            new SerializedObject(configuration);

        SetObjectReference(serialized, "table", table);
        SetObjectReference(
            serialized,
            "placementFootprint",
            footprint
        );
        SetObjectReference(
            serialized,
            "definition",
            definition
        );
        SetObjectReference(
            serialized,
            "seatingCenter",
            seatingCenter
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureGameSystems(Scene scene)
    {
        GameObject gameSystems =
            FindRootByName(scene, "GameSystems");

        if (gameSystems == null)
        {
            throw new InvalidOperationException(
                "La escena no contiene GameSystems."
            );
        }

        RestaurantPlacementConstraintService constraintService =
            GetOrAddComponent<
                RestaurantPlacementConstraintService
            >(gameSystems);

        RestaurantOperationalClearanceConstraintRule clearanceRule =
            GetOrAddComponent<
                RestaurantOperationalClearanceConstraintRule
            >(gameSystems);

        RestaurantSeatRegistry seatRegistry =
            GetOrAddComponent<RestaurantSeatRegistry>(
                gameSystems
            );

        RestaurantSeatingPlacementConstraintRule seatingRule =
            GetOrAddComponent<
                RestaurantSeatingPlacementConstraintRule
            >(gameSystems);

        RestaurantSeatingTopologyService topologyService =
            GetOrAddComponent<
                RestaurantSeatingTopologyService
            >(gameSystems);

        RestaurantPlaceableRegistry placeableRegistry =
            RequireComponent<RestaurantPlaceableRegistry>(
                gameSystems
            );

        RestaurantTableRegistry tableRegistry =
            RequireComponent<RestaurantTableRegistry>(
                gameSystems
            );

        RestaurantPlacementTransactionService transactionService =
            RequireComponent<
                RestaurantPlacementTransactionService
            >(gameSystems);

        RestaurantPlacementHistoryService historyService =
            RequireComponent<
                RestaurantPlacementHistoryService
            >(gameSystems);

        RestaurantPlacementValidationService validationService =
            RequireComponent<
                RestaurantPlacementValidationService
            >(gameSystems);

        SerializedObject serializedSeatRegistry =
            new SerializedObject(seatRegistry);

        SetObjectReference(
            serializedSeatRegistry,
            "placeableRegistry",
            placeableRegistry
        );

        serializedSeatRegistry.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedSeatingRule =
            new SerializedObject(seatingRule);

        SetObjectReference(
            serializedSeatingRule,
            "seatRegistry",
            seatRegistry
        );

        SetObjectReference(
            serializedSeatingRule,
            "tableRegistry",
            tableRegistry
        );

        serializedSeatingRule.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedTopology =
            new SerializedObject(topologyService);

        SetObjectReference(
            serializedTopology,
            "seatRegistry",
            seatRegistry
        );

        SetObjectReference(
            serializedTopology,
            "tableRegistry",
            tableRegistry
        );

        SetObjectReference(
            serializedTopology,
            "transactionService",
            transactionService
        );

        SetObjectReference(
            serializedTopology,
            "historyService",
            historyService
        );

        serializedTopology.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedValidation =
            new SerializedObject(validationService);

        SetObjectReference(
            serializedValidation,
            "constraintService",
            constraintService
        );

        serializedValidation.ApplyModifiedPropertiesWithoutUndo();

        constraintService.RefreshRules();

        EditorUtility.SetDirty(constraintService);
        EditorUtility.SetDirty(clearanceRule);
        EditorUtility.SetDirty(seatRegistry);
        EditorUtility.SetDirty(seatingRule);
        EditorUtility.SetDirty(topologyService);
        EditorUtility.SetDirty(validationService);
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
                target.name +
                " no contiene " +
                typeof(T).Name +
                "."
            );
        }

        return component;
    }

    private static T LoadOrCreateAsset<T>(string assetPath)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();

        AssetDatabase.CreateAsset(
            asset,
            assetPath
        );

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceSynchronousImport
        );

        return asset;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        string normalized =
            assetFolderPath.Replace('\\', '/');

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string[] segments = normalized.Split('/');
        string current = segments[0];

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string next =
                current +
                "/" +
                segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    segments[index]
                );
            }

            current = next;
        }
    }

    private static Transform FindOrCreateDirectChild(
        Transform parent,
        string childName
    )
    {
        Transform child = parent.Find(childName);

        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        child = childObject.transform;
        child.SetParent(parent, false);
        return child;
    }

    private static GameObject FindRootByName(
        Scene scene,
        string rootName
    )
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            if (string.Equals(
                    roots[index].name,
                    rootName,
                    StringComparison.Ordinal
                ))
            {
                return roots[index];
            }
        }

        return null;
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            results.AddRange(
                roots[index].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
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
                "No existe el campo serializado '" +
                propertyName +
                "' en " +
                serialized.targetObject.GetType().Name +
                "."
            );
        }

        return property;
    }

    private static void SetObjectReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value
    )
    {
        RequireProperty(serialized, propertyName)
            .objectReferenceValue = value;
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value
    )
    {
        RequireProperty(serialized, propertyName)
            .stringValue = value;
    }

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value
    )
    {
        RequireProperty(serialized, propertyName)
            .floatValue = value;
    }

    private static void SetInteger(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        RequireProperty(serialized, propertyName)
            .intValue = value;
    }

    private static void SetBoolean(
        SerializedObject serialized,
        string propertyName,
        bool value
    )
    {
        RequireProperty(serialized, propertyName)
            .boolValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        RequireProperty(serialized, propertyName)
            .enumValueIndex = value;
    }

    private sealed class InstallationBackup
    {
        private readonly List<BackupEntry> entries =
            new List<BackupEntry>();

        public static InstallationBackup Capture(
            params string[] assetPaths
        )
        {
            InstallationBackup backup =
                new InstallationBackup();

            for (int index = 0;
                 index < assetPaths.Length;
                 index++)
            {
                string assetPath = assetPaths[index];

                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string absolutePath =
                    ConvertToAbsolutePath(assetPath);

                bool existed = File.Exists(absolutePath);

                backup.entries.Add(
                    new BackupEntry(
                        assetPath,
                        existed,
                        existed
                            ? File.ReadAllBytes(absolutePath)
                            : null
                    )
                );
            }

            return backup;
        }

        public void Restore()
        {
            for (int index = 0;
                 index < entries.Count;
                 index++)
            {
                BackupEntry entry = entries[index];

                if (!entry.Existed)
                {
                    AssetDatabase.DeleteAsset(entry.AssetPath);
                    continue;
                }

                File.WriteAllBytes(
                    ConvertToAbsolutePath(entry.AssetPath),
                    entry.Bytes
                );

                AssetDatabase.ImportAsset(
                    entry.AssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate
                );
            }

            AssetDatabase.Refresh();
        }

        private static string ConvertToAbsolutePath(
            string assetPath
        )
        {
            string normalized =
                assetPath.Replace('\\', '/');

            if (string.Equals(
                    normalized,
                    "Assets",
                    StringComparison.Ordinal
                ))
            {
                return Application.dataPath;
            }

            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal
                ))
            {
                throw new ArgumentException(
                    "La ruta debe comenzar por Assets/: " +
                    normalized
                );
            }

            string relative =
                normalized.Substring("Assets".Length);

            return Application.dataPath +
                   relative.Replace(
                       '/',
                       Path.DirectorySeparatorChar
                   );
        }

        private readonly struct BackupEntry
        {
            public string AssetPath { get; }

            public bool Existed { get; }

            public byte[] Bytes { get; }

            public BackupEntry(
                string assetPath,
                bool existed,
                byte[] bytes
            )
            {
                AssetPath = assetPath;
                Existed = existed;
                Bytes = bytes;
            }
        }
    }
}
