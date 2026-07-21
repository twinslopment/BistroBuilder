using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validador técnico de solo lectura para el modo edición y el sistema
/// universal de artículos colocables.
///
/// No modifica escenas, prefabs, assets ni ProjectSettings.
/// </summary>
public static class BistroBuilderProjectValidator
{
    private const string GameSystemsObjectName =
        "GameSystems";

    private const string EditModeCanvasObjectName =
        "Canvas_BistroBuilder_EditMode";

    private const string PlacementSurfaceLayerName =
        "PlacementSurface";

    private const string MainCatalogAssetPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    private const string CategoryScene =
        "Escena";

    private const string CategorySystems =
        "Servicios";

    private const string CategoryAreas =
        "Áreas";

    private const string CategoryPlaceables =
        "Artículos colocables";

    private const string CategoryCatalog =
        "Catálogo";

    private const string CategoryInterface =
        "Interfaz";

    private const string CategoryProject =
        "Proyecto";

    private const string CategorySource =
        "Código fuente";

    /// <summary>
    /// Ejecuta todas las comprobaciones disponibles.
    /// </summary>
    public static BistroBuilderValidationReport
        RunFullValidation(
            bool logReport = true
        )
    {
        Scene activeScene =
            SceneManager.GetActiveScene();

        BistroBuilderValidationReport report =
            new BistroBuilderValidationReport(
                "Proyecto completo",
                activeScene.IsValid()
                    ? activeScene.path
                    : string.Empty
            );

        ValidateActiveScene(
            report,
            activeScene
        );

        ValidateDataAssets(
            report
        );

        ValidatePlaceableDefinitionsAndPrefabs(
            report
        );

        ValidateCatalogAssets(
            report,
            activeScene
        );

        ValidateProjectConfiguration(
            report,
            activeScene
        );

        ValidateSourceHygiene(
            report
        );

        CompleteReport(
            report,
            logReport
        );

        return report;
    }

    /// <summary>
    /// Ejecuta únicamente las comprobaciones necesarias antes de Play.
    /// </summary>
    public static BistroBuilderValidationReport
        RunScenePreflight(
            bool logReport = true
        )
    {
        Scene activeScene =
            SceneManager.GetActiveScene();

        BistroBuilderValidationReport report =
            new BistroBuilderValidationReport(
                "Preflight de escena",
                activeScene.IsValid()
                    ? activeScene.path
                    : string.Empty
            );

        ValidateActiveScene(
            report,
            activeScene
        );

        ValidatePlaceableDefinitionsAndPrefabs(
            report
        );

        ValidateCatalogAssets(
            report,
            activeScene
        );

        ValidateProjectConfiguration(
            report,
            activeScene
        );

        CompleteReport(
            report,
            logReport
        );

        return report;
    }

    private static void CompleteReport(
        BistroBuilderValidationReport report,
        bool logReport
    )
    {
        if (report.IssueCount == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Info,
                "BB-OK-001",
                CategoryProject,
                "La validación ha terminado sin incidencias.",
                "El proyecto supera todas las comprobaciones " +
                "implementadas por esta versión del validador."
            );
        }

        report.Sort();

        if (!logReport)
        {
            return;
        }

        string reportText =
            report.BuildPlainText();

        if (report.HasBlockingProblems)
        {
            Debug.LogError(
                reportText
            );
        }
        else if (report.WarningCount > 0)
        {
            Debug.LogWarning(
                reportText
            );
        }
        else
        {
            Debug.Log(
                reportText
            );
        }
    }

    private static void ValidateActiveScene(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-SCENE-001",
                CategoryScene,
                "No hay una escena válida y cargada.",
                "Abre Prototype_Restaurant.unity antes de validar."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(scene.path))
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-SCENE-002",
                CategoryScene,
                "La escena activa todavía no se ha guardado.",
                "Guarda la escena para que el informe pueda " +
                "identificarla de forma estable."
            );
        }

        ValidateMissingScriptsInScene(
            report,
            scene
        );

        GameObject[] gameSystemsObjects =
            FindSceneGameObjectsByName(
                scene,
                GameSystemsObjectName
            );

        if (gameSystemsObjects.Length == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-SYS-001",
                CategorySystems,
                "No existe GameSystems en la escena activa.",
                "Los servicios centrales no pueden inicializarse."
            );

            return;
        }

        if (gameSystemsObjects.Length > 1)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-SYS-002",
                CategorySystems,
                "Existen varios objetos llamados GameSystems.",
                "Debe existir exactamente un contenedor de servicios.",
                gameSystemsObjects[0]
            );
        }

        GameObject gameSystems =
            gameSystemsObjects[0];

        ValidateRequiredServices(
            report,
            gameSystems
        );

        ValidateServiceReferences(
            report,
            gameSystems
        );

        ValidateSceneAreas(
            report,
            scene
        );

        ValidateSceneMembers(
            report,
            scene
        );

        ValidateScenePlaceables(
            report,
            scene
        );

        ValidateSceneTables(
            report,
            scene
        );

        ValidateSceneObstacles(
            report,
            scene
        );

        ValidateElevatedVisualSurfaceOverlaps(
            report,
            scene
        );

        ValidateEditModeInterface(
            report,
            scene,
            gameSystems
        );
    }

    private static void ValidateMissingScriptsInScene(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            Transform[] transforms =
                roots[rootIndex]
                    .GetComponentsInChildren<Transform>(true);

            for (int transformIndex = 0;
                 transformIndex < transforms.Length;
                 transformIndex++)
            {
                GameObject currentObject =
                    transforms[transformIndex].gameObject;

                int missingScriptCount =
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            currentObject
                        );

                if (missingScriptCount <= 0)
                {
                    continue;
                }

                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-SCENE-003",
                    CategoryScene,
                    currentObject.name +
                    " contiene " +
                    missingScriptCount +
                    " script(s) perdido(s).",
                    "Elimina o restaura los componentes Missing " +
                    "antes de continuar.",
                    currentObject
                );
            }
        }
    }

    private static void ValidateRequiredServices(
        BistroBuilderValidationReport report,
        GameObject gameSystems
    )
    {
        Type[] requiredServiceTypes =
        {
            typeof(RestaurantAreaRegistry),
            typeof(RestaurantAreaMemberRegistry),
            typeof(RestaurantAreaAssignmentService),
            typeof(RestaurantPlacementRegistry),
            typeof(RestaurantPlacementValidationService),
            typeof(RestaurantPlacementObstacleRegistry),
            typeof(RestaurantPlacementTransactionService),
            typeof(RestaurantPlacementHistoryService),
            typeof(RestaurantEditModeService),
            typeof(RestaurantServiceStateService),
            typeof(RestaurantServiceEditModeAvailabilityRule),
            typeof(RestaurantPlaceableRegistry),
            typeof(RestaurantPlaceableLifecycleService),
            typeof(RestaurantPlaceableCreationService),
            typeof(RestaurantPlaceableDeletionService),
            typeof(RestaurantTableRegistry),
            typeof(RestaurantTablePlaceableRegistrationService),
            typeof(RestaurantTableOperationalRegistrationService),
            typeof(RestaurantPlaceableCatalogService),
            typeof(RestaurantEditInteractionController),
            typeof(RestaurantEditPlacementVisualFeedback)
        };

        for (int index = 0;
             index < requiredServiceTypes.Length;
             index++)
        {
            Type requiredType =
                requiredServiceTypes[index];

            Component[] components =
                gameSystems.GetComponents(
                    requiredType
                );

            if (components.Length == 0)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-SYS-010",
                    CategorySystems,
                    "GameSystems no contiene " +
                    requiredType.Name +
                    ".",
                    "Ejecuta el instalador correspondiente o " +
                    "restaura el componente.",
                    gameSystems
                );

                continue;
            }

            if (components.Length > 1)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-SYS-011",
                    CategorySystems,
                    "GameSystems contiene " +
                    components.Length +
                    " copias de " +
                    requiredType.Name +
                    ".",
                    "El servicio debe existir una sola vez.",
                    gameSystems
                );
            }

            Behaviour behaviour =
                components[0] as Behaviour;

            if (behaviour != null &&
                !behaviour.enabled)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-SYS-012",
                    CategorySystems,
                    requiredType.Name +
                    " está desactivado.",
                    "El servicio no participará en Play.",
                    behaviour
                );
            }
        }
    }

    private static void ValidateServiceReferences(
        BistroBuilderValidationReport report,
        GameObject gameSystems
    )
    {
        RestaurantAreaAssignmentService areaAssignmentService =
            gameSystems.GetComponent<
                RestaurantAreaAssignmentService
            >();

        CheckRequiredReference(
            report,
            areaAssignmentService,
            "areaRegistry",
            "BB-REF-001"
        );

        CheckRequiredReference(
            report,
            areaAssignmentService,
            "memberRegistry",
            "BB-REF-002"
        );

        RestaurantPlacementRegistry placementRegistry =
            gameSystems.GetComponent<
                RestaurantPlacementRegistry
            >();

        CheckRequiredReference(
            report,
            placementRegistry,
            "memberRegistry",
            "BB-REF-003"
        );

        RestaurantPlacementValidationService validationService =
            gameSystems.GetComponent<
                RestaurantPlacementValidationService
            >();

        CheckRequiredReference(
            report,
            validationService,
            "areaAssignmentService",
            "BB-REF-004"
        );

        CheckRequiredReference(
            report,
            validationService,
            "placementRegistry",
            "BB-REF-005"
        );

        CheckRequiredReference(
            report,
            validationService,
            "obstacleRegistry",
            "BB-REF-006"
        );

        RestaurantPlacementTransactionService transactionService =
            gameSystems.GetComponent<
                RestaurantPlacementTransactionService
            >();

        CheckRequiredReference(
            report,
            transactionService,
            "validationService",
            "BB-REF-007"
        );

        RestaurantPlacementHistoryService historyService =
            gameSystems.GetComponent<
                RestaurantPlacementHistoryService
            >();

        CheckRequiredReference(
            report,
            historyService,
            "transactionService",
            "BB-REF-008"
        );

        CheckRequiredReference(
            report,
            historyService,
            "validationService",
            "BB-REF-009"
        );

        RestaurantEditModeService editModeService =
            gameSystems.GetComponent<
                RestaurantEditModeService
            >();

        CheckRequiredReference(
            report,
            editModeService,
            "transactionService",
            "BB-REF-010"
        );

        CheckRequiredArray(
            report,
            editModeService,
            "availabilityRuleSources",
            "BB-REF-011"
        );

        RestaurantServiceEditModeAvailabilityRule availabilityRule =
            gameSystems.GetComponent<
                RestaurantServiceEditModeAvailabilityRule
            >();

        CheckRequiredReference(
            report,
            availabilityRule,
            "serviceStateService",
            "BB-REF-012"
        );

        CheckRequiredReference(
            report,
            availabilityRule,
            "editModeService",
            "BB-REF-013"
        );

        RestaurantPlaceableLifecycleService lifecycleService =
            gameSystems.GetComponent<
                RestaurantPlaceableLifecycleService
            >();

        CheckRequiredReference(
            report,
            lifecycleService,
            "placeableRegistry",
            "BB-REF-014"
        );

        CheckRequiredReference(
            report,
            lifecycleService,
            "memberRegistry",
            "BB-REF-015"
        );

        RestaurantPlaceableCreationService creationService =
            gameSystems.GetComponent<
                RestaurantPlaceableCreationService
            >();

        CheckRequiredReference(
            report,
            creationService,
            "lifecycleService",
            "BB-REF-016"
        );

        CheckRequiredReference(
            report,
            creationService,
            "transactionService",
            "BB-REF-017"
        );

        CheckRequiredReference(
            report,
            creationService,
            "historyService",
            "BB-REF-018"
        );

        RestaurantPlaceableDeletionService deletionService =
            gameSystems.GetComponent<
                RestaurantPlaceableDeletionService
            >();

        CheckRequiredReference(
            report,
            deletionService,
            "editModeService",
            "BB-REF-019"
        );

        CheckRequiredReference(
            report,
            deletionService,
            "transactionService",
            "BB-REF-020"
        );

        CheckRequiredReference(
            report,
            deletionService,
            "lifecycleService",
            "BB-REF-021"
        );

        CheckRequiredReference(
            report,
            deletionService,
            "historyService",
            "BB-REF-022"
        );

        RestaurantTablePlaceableRegistrationService
            tablePlaceableRegistrationService =
                gameSystems.GetComponent<
                    RestaurantTablePlaceableRegistrationService
                >();

        CheckRequiredReference(
            report,
            tablePlaceableRegistrationService,
            "placeableRegistry",
            "BB-REF-023"
        );

        CheckRequiredReference(
            report,
            tablePlaceableRegistrationService,
            "tableRegistry",
            "BB-REF-024"
        );

        RestaurantTableOperationalRegistrationService
            operationalRegistrationService =
                gameSystems.GetComponent<
                    RestaurantTableOperationalRegistrationService
                >();

        CheckRequiredReference(
            report,
            operationalRegistrationService,
            "tableRegistry",
            "BB-REF-025"
        );

        CheckRequiredReference(
            report,
            operationalRegistrationService,
            "waiterTaskCoordinator",
            "BB-REF-026"
        );

        RestaurantEditInteractionController interactionController =
            gameSystems.GetComponent<
                RestaurantEditInteractionController
            >();

        CheckRequiredReference(
            report,
            interactionController,
            "editModeService",
            "BB-REF-027"
        );

        CheckRequiredReference(
            report,
            interactionController,
            "transactionService",
            "BB-REF-028"
        );

        CheckRequiredReference(
            report,
            interactionController,
            "historyService",
            "BB-REF-029"
        );

        CheckRequiredReference(
            report,
            interactionController,
            "creationService",
            "BB-REF-030"
        );

        CheckRequiredReference(
            report,
            interactionController,
            "interactionCamera",
            "BB-REF-031"
        );

        CheckLayerMask(
            report,
            interactionController,
            "selectableLayerMask",
            string.Empty,
            "BB-REF-032"
        );

        CheckLayerMask(
            report,
            interactionController,
            "placementSurfaceLayerMask",
            PlacementSurfaceLayerName,
            "BB-REF-033"
        );

        RestaurantEditPlacementVisualFeedback visualFeedback =
            gameSystems.GetComponent<
                RestaurantEditPlacementVisualFeedback
            >();

        CheckRequiredReference(
            report,
            visualFeedback,
            "interactionController",
            "BB-REF-034"
        );

        RestaurantPlaceableCatalogService catalogService =
            gameSystems.GetComponent<
                RestaurantPlaceableCatalogService
            >();

        CheckRequiredReference(
            report,
            catalogService,
            "catalogDefinition",
            "BB-REF-035"
        );
    }

    private static void ValidateSceneAreas(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        List<RestaurantArea> areas =
            FindSceneComponents<RestaurantArea>(
                scene
            );

        if (areas.Count == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-AREA-001",
                CategoryAreas,
                "La escena no contiene áreas de restaurante.",
                "La colocación funcional necesita al menos un " +
                "RestaurantArea."
            );

            return;
        }

        Dictionary<string, RestaurantArea> areaById =
            new Dictionary<string, RestaurantArea>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < areas.Count;
             index++)
        {
            RestaurantArea area =
                areas[index];

            string areaId =
                area.AreaId;

            if (string.IsNullOrWhiteSpace(areaId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-AREA-002",
                    CategoryAreas,
                    area.name +
                    " no tiene AreaId.",
                    "Cada área necesita una identidad estable.",
                    area
                );
            }
            else if (areaById.TryGetValue(
                         areaId,
                         out RestaurantArea duplicateArea
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-AREA-003",
                    CategoryAreas,
                    "AreaId duplicado: " + areaId + ".",
                    duplicateArea.name +
                    " y " +
                    area.name +
                    " comparten la misma identidad.",
                    area
                );
            }
            else
            {
                areaById.Add(
                    areaId,
                    area
                );
            }

            if (area.Definition == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-AREA-004",
                    CategoryAreas,
                    area.name +
                    " no tiene RestaurantAreaDefinition.",
                    "No se pueden resolver las capacidades del área.",
                    area
                );
            }

            IReadOnlyList<Collider> boundaries =
                area.BoundaryColliders;

            int validBoundaryCount =
                0;

            if (boundaries != null)
            {
                for (int boundaryIndex = 0;
                     boundaryIndex < boundaries.Count;
                     boundaryIndex++)
                {
                    Collider boundary =
                        boundaries[boundaryIndex];

                    if (boundary == null)
                    {
                        report.Add(
                            BistroBuilderValidationSeverity.Error,
                            "BB-AREA-005",
                            CategoryAreas,
                            area.name +
                            " contiene un límite nulo.",
                            "Revisa Boundary Colliders.",
                            area
                        );

                        continue;
                    }

                    validBoundaryCount++;

                    if (!boundary.isTrigger)
                    {
                        report.Add(
                            BistroBuilderValidationSeverity.Warning,
                            "BB-AREA-006",
                            CategoryAreas,
                            boundary.name +
                            " no está configurado como Trigger.",
                            "Los límites de área suelen ser volúmenes " +
                            "lógicos y no deben bloquear la física.",
                            boundary
                        );
                    }
                }
            }

            if (validBoundaryCount == 0)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-AREA-007",
                    CategoryAreas,
                    area.name +
                    " no tiene ningún límite válido.",
                    "No se podrá determinar si un artículo está " +
                    "dentro del área.",
                    area
                );
            }
        }
    }

    private static void ValidateSceneMembers(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        List<RestaurantAreaMember> members =
            FindSceneComponents<RestaurantAreaMember>(
                scene
            );

        for (int index = 0;
             index < members.Count;
             index++)
        {
            RestaurantAreaMember member =
                members[index];

            if (!member.HasAssignedArea)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-MEMBER-001",
                    CategoryAreas,
                    member.name +
                    " no tiene un área asignada en la escena.",
                    "Los prefabs pueden dejar el área vacía, pero los " +
                    "miembros existentes deben estar asignados.",
                    member
                );
            }
            else if (!member.AreRequirementsSatisfiedBy(
                         member.AssignedArea,
                         out RestaurantAreaCapabilityDefinition
                             missingCapability
                     ))
            {
                string capabilityName =
                    missingCapability != null
                        ? missingCapability.DisplayName
                        : "desconocida";

                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-MEMBER-002",
                    CategoryAreas,
                    member.name +
                    " está asignado a un área incompatible.",
                    "Capacidad ausente: " +
                    capabilityName +
                    ".",
                    member
                );
            }

            ValidateMemberRequirements(
                report,
                member,
                member,
                string.Empty
            );

            CheckTransformReferenceInsideHierarchy(
                report,
                member,
                "positionReference",
                member.transform,
                "BB-MEMBER-003",
                CategoryAreas,
                "Position Reference"
            );
        }
    }

    private static void ValidateScenePlaceables(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        List<RestaurantPlaceableObject> placeables =
            FindSceneComponents<RestaurantPlaceableObject>(
                scene
            );

        Dictionary<string, RestaurantPlaceableObject> byInstanceId =
            new Dictionary<string, RestaurantPlaceableObject>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < placeables.Count;
             index++)
        {
            RestaurantPlaceableObject placeable =
                placeables[index];

            if (!placeable.ValidateConfiguration(
                    out string configurationError
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PLACEABLE-001",
                    CategoryPlaceables,
                    placeable.name +
                    " no supera su validación.",
                    configurationError,
                    placeable
                );
            }

            string instanceId =
                placeable.InstanceId;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Info,
                    "BB-PLACEABLE-002",
                    CategoryPlaceables,
                    placeable.name +
                    " recibirá InstanceId al iniciar Play.",
                    "El registro runtime reparará la identidad. " +
                    "La persistencia definitiva podrá materializarla " +
                    "más adelante.",
                    placeable
                );
            }
            else if (byInstanceId.TryGetValue(
                         instanceId,
                         out RestaurantPlaceableObject duplicate
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PLACEABLE-003",
                    CategoryPlaceables,
                    "InstanceId duplicado: " +
                    instanceId +
                    ".",
                    duplicate.name +
                    " y " +
                    placeable.name +
                    " comparten identidad.",
                    placeable
                );
            }
            else
            {
                byInstanceId.Add(
                    instanceId,
                    placeable
                );
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(
                    placeable
                );

            SerializedProperty anchorProperty =
                serializedPlaceable.FindProperty(
                    "placementAnchor"
                );

            if (anchorProperty != null &&
                anchorProperty.objectReferenceValue == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-PLACEABLE-004",
                    CategoryPlaceables,
                    placeable.name +
                    " no tiene PlacementAnchor explícito.",
                    "La raíz se usa como respaldo. Los nuevos prefabs " +
                    "deben tener un anclaje configurado.",
                    placeable
                );
            }
        }
    }

    private static void ValidateSceneTables(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        List<RestaurantTable> tables =
            FindSceneComponents<RestaurantTable>(
                scene
            );

        Dictionary<int, RestaurantTable> byTableId =
            new Dictionary<int, RestaurantTable>();

        for (int index = 0;
             index < tables.Count;
             index++)
        {
            RestaurantTable table =
                tables[index];

            if (table.TableId <= 0)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-TABLE-001",
                    CategoryPlaceables,
                    table.name +
                    " tiene un TableId no válido.",
                    "El identificador debe ser mayor que cero.",
                    table
                );
            }
            else if (byTableId.TryGetValue(
                         table.TableId,
                         out RestaurantTable duplicate
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-TABLE-002",
                    CategoryPlaceables,
                    "TableId duplicado: " +
                    table.TableId +
                    ".",
                    duplicate.name +
                    " y " +
                    table.name +
                    " dependen de la reparación runtime.",
                    table
                );
            }
            else
            {
                byTableId.Add(
                    table.TableId,
                    table
                );
            }

            ValidateTableInteractionPoint(
                report,
                table,
                table.CustomerApproachPoint,
                "Customer Approach Point",
                "BB-TABLE-003"
            );

            ValidateTableInteractionPoint(
                report,
                table,
                table.WaiterServicePoint,
                "Waiter Service Point",
                "BB-TABLE-004"
            );

            if (table.CustomerApproachPoint != null &&
                ReferenceEquals(
                    table.CustomerApproachPoint,
                    table.WaiterServicePoint
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-TABLE-005",
                    CategoryPlaceables,
                    table.name +
                    " utiliza el mismo punto para cliente y camarero.",
                    "Cada función debe tener su propia referencia.",
                    table
                );
            }
        }
    }

    private static void ValidateSceneObstacles(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        List<RestaurantPlacementObstacle> obstacles =
            FindSceneComponents<RestaurantPlacementObstacle>(
                scene
            );

        Dictionary<string, RestaurantPlacementObstacle> byId =
            new Dictionary<string, RestaurantPlacementObstacle>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < obstacles.Count;
             index++)
        {
            RestaurantPlacementObstacle obstacle =
                obstacles[index];

            string obstacleId =
                obstacle.ObstacleId;

            if (string.IsNullOrWhiteSpace(obstacleId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-OBSTACLE-001",
                    CategoryAreas,
                    obstacle.name +
                    " no tiene ObstacleId.",
                    "Los obstáculos necesitan identidad estable.",
                    obstacle
                );
            }
            else if (byId.TryGetValue(
                         obstacleId,
                         out RestaurantPlacementObstacle duplicate
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-OBSTACLE-002",
                    CategoryAreas,
                    "ObstacleId duplicado: " +
                    obstacleId +
                    ".",
                    duplicate.name +
                    " y " +
                    obstacle.name +
                    " comparten identidad.",
                    obstacle
                );
            }
            else
            {
                byId.Add(
                    obstacleId,
                    obstacle
                );
            }

            Vector2 size =
                obstacle.LocalSize;

            if (size.x <= 0f ||
                size.y <= 0f)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-OBSTACLE-003",
                    CategoryAreas,
                    obstacle.name +
                    " tiene un tamaño no válido.",
                    "Local Size debe ser positivo en ambos ejes.",
                    obstacle
                );
            }
        }
    }

    /// <summary>
    /// Detecta superficies visuales planas y extensas situadas por
    /// encima de una superficie de colocación, pero fuera de la capa
    /// PlacementSurface.
    ///
    /// Este patrón puede ocultar la parte inferior de artículos
    /// correctamente colocados y producir una falsa apariencia de
    /// hundimiento, aunque la raíz y el PlacementAnchor estén en la
    /// altura correcta.
    /// </summary>
    private static void ValidateElevatedVisualSurfaceOverlaps(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        int placementSurfaceLayer =
            LayerMask.NameToLayer(
                PlacementSurfaceLayerName
            );

        if (placementSurfaceLayer < 0)
        {
            return;
        }

        List<Collider> sceneColliders =
            FindSceneComponents<Collider>(
                scene
            );

        List<Renderer> sceneRenderers =
            FindSceneComponents<Renderer>(
                scene
            );

        List<Collider> placementSurfaces =
            new List<Collider>();

        for (int index = 0;
             index < sceneColliders.Count;
             index++)
        {
            Collider collider =
                sceneColliders[index];

            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                collider.gameObject.layer !=
                    placementSurfaceLayer)
            {
                continue;
            }

            placementSurfaces.Add(collider);
        }

        if (placementSurfaces.Count == 0)
        {
            return;
        }

        HashSet<int> reportedRendererIds =
            new HashSet<int>();

        const float maximumFlatThickness =
            0.10f;

        const float minimumHorizontalArea =
            4f;

        const float minimumElevation =
            0.02f;

        const float maximumRelevantElevation =
            2f;

        for (int rendererIndex = 0;
             rendererIndex < sceneRenderers.Count;
             rendererIndex++)
        {
            Renderer renderer =
                sceneRenderers[rendererIndex];

            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer.gameObject.layer ==
                    placementSurfaceLayer)
            {
                continue;
            }

            Bounds rendererBounds =
                renderer.bounds;

            float rendererArea =
                rendererBounds.size.x *
                rendererBounds.size.z;

            if (rendererBounds.size.y >
                    maximumFlatThickness ||
                rendererArea <
                    minimumHorizontalArea)
            {
                continue;
            }

            for (int surfaceIndex = 0;
                 surfaceIndex < placementSurfaces.Count;
                 surfaceIndex++)
            {
                Collider surface =
                    placementSurfaces[surfaceIndex];

                if (surface == null ||
                    ReferenceEquals(
                        surface.gameObject,
                        renderer.gameObject
                    ))
                {
                    continue;
                }

                Bounds surfaceBounds =
                    surface.bounds;

                float overlapX =
                    Mathf.Min(
                        rendererBounds.max.x,
                        surfaceBounds.max.x
                    ) -
                    Mathf.Max(
                        rendererBounds.min.x,
                        surfaceBounds.min.x
                    );

                float overlapZ =
                    Mathf.Min(
                        rendererBounds.max.z,
                        surfaceBounds.max.z
                    ) -
                    Mathf.Max(
                        rendererBounds.min.z,
                        surfaceBounds.min.z
                    );

                if (overlapX <= 0f ||
                    overlapZ <= 0f)
                {
                    continue;
                }

                float overlapArea =
                    overlapX *
                    overlapZ;

                if (overlapArea <
                    minimumHorizontalArea)
                {
                    continue;
                }

                float elevation =
                    rendererBounds.min.y -
                    surfaceBounds.max.y;

                if (elevation <
                        minimumElevation ||
                    elevation >
                        maximumRelevantElevation)
                {
                    continue;
                }

                int rendererId =
                    renderer.GetInstanceID();

                if (!reportedRendererIds.Add(
                        rendererId
                    ))
                {
                    break;
                }

                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-SURFACE-001",
                    CategoryScene,
                    renderer.name +
                    " dibuja una superficie elevada sobre una " +
                    "superficie de colocación.",
                    "Elevación detectada: " +
                    elevation.ToString("0.###") +
                    " m. Área horizontal solapada: " +
                    overlapArea.ToString("0.###") +
                    " m². Superficie de colocación: " +
                    surface.name +
                    ". Un objeto colocado correctamente sobre " +
                    surface.name +
                    " puede parecer hundido porque " +
                    renderer.name +
                    " oculta su parte inferior. Elimina la " +
                    "superficie heredada o conviértela en una " +
                    "superficie de colocación coherente.",
                    renderer
                );

                break;
            }
        }
    }

    private static void ValidateEditModeInterface(
        BistroBuilderValidationReport report,
        Scene scene,
        GameObject gameSystems
    )
    {
        GameObject[] canvases =
            FindSceneGameObjectsByName(
                scene,
                EditModeCanvasObjectName
            );

        if (canvases.Length == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-UI-001",
                CategoryInterface,
                "No existe " +
                EditModeCanvasObjectName +
                ".",
                "El catálogo y el panel contextual no podrán mostrarse."
            );
        }
        else
        {
            if (canvases.Length > 1)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-UI-002",
                    CategoryInterface,
                    "Existen varios Canvas de modo edición.",
                    "Debe existir exactamente uno.",
                    canvases[0]
                );
            }

            Canvas canvas =
                canvases[0].GetComponent<Canvas>();

            if (canvas == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-UI-003",
                    CategoryInterface,
                    EditModeCanvasObjectName +
                    " no contiene Canvas.",
                    "Ejecuta el instalador del catálogo.",
                    canvases[0]
                );
            }

            if (canvases[0].GetComponent<GraphicRaycaster>() == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-UI-004",
                    CategoryInterface,
                    EditModeCanvasObjectName +
                    " no contiene GraphicRaycaster.",
                    "Los botones no recibirán interacción.",
                    canvases[0]
                );
            }
        }

        List<EventSystem> eventSystems =
            FindSceneComponents<EventSystem>(
                scene
            );

        if (eventSystems.Count == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-UI-005",
                CategoryInterface,
                "La escena no contiene EventSystem.",
                "La UI no podrá recibir clics."
            );
        }
        else if (eventSystems.Count > 1)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-UI-006",
                CategoryInterface,
                "La escena contiene varios EventSystem.",
                "Varios sistemas de eventos pueden duplicar entradas.",
                eventSystems[0]
            );
        }

        List<RestaurantPlaceableCatalogPanel> catalogPanels =
            FindSceneComponents<RestaurantPlaceableCatalogPanel>(
                scene
            );

        ValidateSinglePresentationComponent(
            report,
            catalogPanels,
            "RestaurantPlaceableCatalogPanel",
            "BB-UI-010"
        );

        if (catalogPanels.Count > 0)
        {
            RestaurantPlaceableCatalogPanel panel =
                catalogPanels[0];

            string[] requiredFields =
            {
                "editModeService",
                "interactionController",
                "catalogService",
                "contentRoot",
                "categoryContainer",
                "itemContainer",
                "categoryTemplate",
                "itemTemplate",
                "titleText",
                "statusText"
            };

            CheckRequiredReferences(
                report,
                panel,
                requiredFields,
                "BB-UI-011"
            );
        }

        List<RestaurantPlaceableContextPanel> contextPanels =
            FindSceneComponents<RestaurantPlaceableContextPanel>(
                scene
            );

        ValidateSinglePresentationComponent(
            report,
            contextPanels,
            "RestaurantPlaceableContextPanel",
            "BB-UI-020"
        );

        if (contextPanels.Count > 0)
        {
            RestaurantPlaceableContextPanel panel =
                contextPanels[0];

            string[] requiredFields =
            {
                "editModeService",
                "interactionController",
                "deletionService",
                "contentRoot",
                "nameText",
                "categoryText",
                "statusText",
                "moveButton",
                "deleteButton"
            };

            CheckRequiredReferences(
                report,
                panel,
                requiredFields,
                "BB-UI-021"
            );

            ValidateContextButtonLabel(
                report,
                panel,
                "moveButton",
                "Mover",
                "BB-UI-022"
            );

            ValidateContextButtonLabel(
                report,
                panel,
                "deleteButton",
                "Eliminar",
                "BB-UI-023"
            );
        }

        RestaurantEditInteractionController controller =
            gameSystems.GetComponent<
                RestaurantEditInteractionController
            >();

        if (controller != null)
        {
            SerializedObject serializedController =
                new SerializedObject(controller);

            SerializedProperty cameraProperty =
                serializedController.FindProperty(
                    "interactionCamera"
                );

            Camera camera =
                cameraProperty != null
                    ? cameraProperty.objectReferenceValue as Camera
                    : null;

            if (camera != null &&
                !camera.CompareTag("MainCamera"))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-UI-030",
                    CategoryInterface,
                    "La cámara de interacción no tiene la etiqueta " +
                    "MainCamera.",
                    "No bloquea el modo edición, pero dificulta " +
                    "otras integraciones.",
                    camera
                );
            }
        }
    }

    private static void ValidateDataAssets(
        BistroBuilderValidationReport report
    )
    {
        ValidateCapabilityAssets(
            report
        );

        ValidateAreaDefinitionAssets(
            report
        );

        ValidateEditableDefinitionAssets(
            report
        );
    }

    private static void ValidateCapabilityAssets(
        BistroBuilderValidationReport report
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantAreaCapabilityDefinition"
            );

        Dictionary<string, RestaurantAreaCapabilityDefinition> byId =
            new Dictionary<string, RestaurantAreaCapabilityDefinition>(
                StringComparer.Ordinal
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

            if (capability == null)
            {
                continue;
            }

            string capabilityId =
                capability.CapabilityId;

            if (string.IsNullOrWhiteSpace(capabilityId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-001",
                    CategoryAreas,
                    capability.name +
                    " no tiene CapabilityId.",
                    "El identificador es obligatorio.",
                    capability,
                    path
                );

                continue;
            }

            if (byId.TryGetValue(
                    capabilityId,
                    out RestaurantAreaCapabilityDefinition duplicate
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-002",
                    CategoryAreas,
                    "CapabilityId duplicado: " +
                    capabilityId +
                    ".",
                    duplicate.name +
                    " y " +
                    capability.name +
                    " comparten identidad.",
                    capability,
                    path
                );
            }
            else
            {
                byId.Add(
                    capabilityId,
                    capability
                );
            }
        }
    }

    private static void ValidateAreaDefinitionAssets(
        BistroBuilderValidationReport report
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantAreaDefinition"
            );

        Dictionary<string, RestaurantAreaDefinition> byId =
            new Dictionary<string, RestaurantAreaDefinition>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantAreaDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantAreaDefinition
                >(path);

            if (definition == null)
            {
                continue;
            }

            string definitionId =
                definition.AreaTypeId;

            if (string.IsNullOrWhiteSpace(definitionId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-010",
                    CategoryAreas,
                    definition.name +
                    " no tiene AreaTypeId.",
                    "El identificador es obligatorio.",
                    definition,
                    path
                );
            }
            else if (byId.TryGetValue(
                         definitionId,
                         out RestaurantAreaDefinition duplicate
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-011",
                    CategoryAreas,
                    "AreaTypeId duplicado: " +
                    definitionId +
                    ".",
                    duplicate.name +
                    " y " +
                    definition.name +
                    " comparten identidad.",
                    definition,
                    path
                );
            }
            else
            {
                byId.Add(
                    definitionId,
                    definition
                );
            }

            IReadOnlyList<RestaurantAreaCapabilityDefinition>
                capabilities =
                    definition.Capabilities;

            HashSet<RestaurantAreaCapabilityDefinition> knownCapabilities =
                new HashSet<RestaurantAreaCapabilityDefinition>();

            for (int capabilityIndex = 0;
                 capabilityIndex < capabilities.Count;
                 capabilityIndex++)
            {
                RestaurantAreaCapabilityDefinition capability =
                    capabilities[capabilityIndex];

                if (capability == null)
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Error,
                        "BB-DATA-012",
                        CategoryAreas,
                        definition.name +
                        " contiene una capacidad nula.",
                        "Revisa el array Capabilities.",
                        definition,
                        path
                    );

                    continue;
                }

                if (!knownCapabilities.Add(capability))
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Warning,
                        "BB-DATA-013",
                        CategoryAreas,
                        definition.name +
                        " repite la capacidad " +
                        capability.DisplayName +
                        ".",
                        "El duplicado no aporta funcionalidad.",
                        definition,
                        path
                    );
                }
            }
        }
    }

    private static void ValidateEditableDefinitionAssets(
        BistroBuilderValidationReport report
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantEditableObjectDefinition"
            );

        Dictionary<string, RestaurantEditableObjectDefinition> byId =
            new Dictionary<string, RestaurantEditableObjectDefinition>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantEditableObjectDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantEditableObjectDefinition
                >(path);

            if (definition == null)
            {
                continue;
            }

            string definitionId =
                definition.DefinitionId;

            if (string.IsNullOrWhiteSpace(definitionId))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-020",
                    CategoryPlaceables,
                    definition.name +
                    " no tiene DefinitionId.",
                    "El identificador es obligatorio.",
                    definition,
                    path
                );
            }
            else if (byId.TryGetValue(
                         definitionId,
                         out RestaurantEditableObjectDefinition duplicate
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-DATA-021",
                    CategoryPlaceables,
                    "DefinitionId duplicado: " +
                    definitionId +
                    ".",
                    duplicate.name +
                    " y " +
                    definition.name +
                    " comparten identidad.",
                    definition,
                    path
                );
            }
            else
            {
                byId.Add(
                    definitionId,
                    definition
                );
            }

            if (!definition.CanMove &&
                !definition.CanRotate)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Info,
                    "BB-DATA-022",
                    CategoryPlaceables,
                    definition.name +
                    " no permite mover ni rotar.",
                    "Es válido para elementos fijos; confirma que sea " +
                    "intencionado.",
                    definition,
                    path
                );
            }
        }
    }

    private static void ValidatePlaceableDefinitionsAndPrefabs(
        BistroBuilderValidationReport report
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableItemDefinition"
            );

        if (guids.Length == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-ASSET-001",
                CategoryPlaceables,
                "No existen definiciones de artículos colocables.",
                "El catálogo no podrá ofrecer ningún artículo."
            );

            return;
        }

        Dictionary<string, RestaurantPlaceableItemDefinition> byId =
            new Dictionary<string, RestaurantPlaceableItemDefinition>(
                StringComparer.Ordinal
            );

        HashSet<string> validatedPrefabPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string definitionPath =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(definitionPath);

            if (definition == null)
            {
                continue;
            }

            ValidatePlaceableDefinition(
                report,
                definition,
                definitionPath,
                byId
            );

            if (!definition.HasValidPrefab)
            {
                continue;
            }

            string prefabPath =
                AssetDatabase.GetAssetPath(
                    definition.Prefab
                );

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-ASSET-002",
                    CategoryPlaceables,
                    definition.DisplayName +
                    " referencia un prefab sin ruta válida.",
                    "Reasigna el prefab en la definición.",
                    definition,
                    definitionPath
                );

                continue;
            }

            if (!validatedPrefabPaths.Add(prefabPath))
            {
                continue;
            }

            ValidatePlaceablePrefab(
                report,
                definition,
                prefabPath
            );
        }
    }

    private static void ValidatePlaceableDefinition(
        BistroBuilderValidationReport report,
        RestaurantPlaceableItemDefinition definition,
        string definitionPath,
        Dictionary<string, RestaurantPlaceableItemDefinition> byId
    )
    {
        string itemId =
            definition.ItemId;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-ITEM-001",
                CategoryPlaceables,
                definition.name +
                " no tiene ItemId.",
                "El identificador estable es obligatorio.",
                definition,
                definitionPath
            );
        }
        else if (byId.TryGetValue(
                     itemId,
                     out RestaurantPlaceableItemDefinition duplicate
                 ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-ITEM-002",
                CategoryPlaceables,
                "ItemId duplicado: " +
                itemId +
                ".",
                duplicate.name +
                " y " +
                definition.name +
                " comparten identidad.",
                definition,
                definitionPath
            );
        }
        else
        {
            byId.Add(
                itemId,
                definition
            );
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-ITEM-003",
                CategoryPlaceables,
                definition.name +
                " no tiene nombre visible.",
                "El catálogo necesita un Display Name.",
                definition,
                definitionPath
            );
        }

        if (!definition.HasValidPrefab)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-ITEM-004",
                CategoryPlaceables,
                definition.DisplayName +
                " no tiene prefab.",
                "No se puede crear el artículo.",
                definition,
                definitionPath
            );
        }

        if (definition.EditableDefinition == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-ITEM-005",
                CategoryPlaceables,
                definition.DisplayName +
                " no tiene definición editable.",
                "Asigna sus reglas de movimiento, rotación y cuadrícula.",
                definition,
                definitionPath
            );
        }

        if (definition.CatalogIcon == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Info,
                "BB-ITEM-006",
                CategoryCatalog,
                definition.DisplayName +
                " utiliza la letra de respaldo como icono.",
                "Es válido, pero la herramienta de mantenimiento puede " +
                "generar una miniatura automática.",
                definition,
                definitionPath
            );
        }
        else
        {
            string iconPath =
                AssetDatabase.GetAssetPath(
                    definition.CatalogIcon
                );

            if (!string.IsNullOrWhiteSpace(iconPath) &&
                iconPath.StartsWith(
                    BistroBuilderCatalogThumbnailService
                        .GeneratedIconFolder +
                    "/",
                    StringComparison.Ordinal
                ))
            {
                TextureImporter importer =
                    AssetImporter.GetAtPath(iconPath)
                    as TextureImporter;

                if (importer == null)
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Warning,
                        "BB-ITEM-007",
                        CategoryCatalog,
                        definition.DisplayName +
                        " tiene una miniatura generada sin " +
                        "TextureImporter válido.",
                        "Regenera la miniatura desde Placeable " +
                        "Maintenance.",
                        definition.CatalogIcon,
                        iconPath
                    );
                }
                else if (importer.textureType !=
                         TextureImporterType.Sprite ||
                         importer.mipmapEnabled ||
                         importer.wrapMode != TextureWrapMode.Clamp)
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Warning,
                        "BB-ITEM-008",
                        CategoryCatalog,
                        definition.DisplayName +
                        " tiene una miniatura con ajustes de " +
                        "importación incorrectos.",
                        "Regenera la miniatura para aplicar Sprite, " +
                        "sin mipmaps y Wrap Mode Clamp.",
                        definition.CatalogIcon,
                        iconPath
                    );
                }
            }
        }
    }

    private static void ValidatePlaceablePrefab(
        BistroBuilderValidationReport report,
        RestaurantPlaceableItemDefinition expectedDefinition,
        string prefabPath
    )
    {
        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath
            );

        GameObject prefabRoot =
            null;

        try
        {
            prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );

            if (prefabRoot == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-PREFAB-001",
                    CategoryPlaceables,
                    "Unity no pudo cargar " +
                    prefabPath +
                    ".",
                    "Comprueba que el prefab no esté dañado.",
                    prefabAsset,
                    prefabPath
                );

                return;
            }

            ValidateMissingScriptsInPrefab(
                report,
                prefabRoot,
                prefabAsset,
                prefabPath
            );

            RestaurantPlaceableObject placeable =
                prefabRoot.GetComponent<
                    RestaurantPlaceableObject
                >();

            RestaurantEditableObject editable =
                prefabRoot.GetComponent<
                    RestaurantEditableObject
                >();

            RestaurantAreaMember member =
                prefabRoot.GetComponent<
                    RestaurantAreaMember
                >();

            RestaurantPlacementFootprint footprint =
                prefabRoot.GetComponent<
                    RestaurantPlacementFootprint
                >();

            if (placeable == null)
            {
                AddMissingPrefabComponent(
                    report,
                    prefabAsset,
                    prefabPath,
                    nameof(RestaurantPlaceableObject)
                );
            }

            if (editable == null)
            {
                AddMissingPrefabComponent(
                    report,
                    prefabAsset,
                    prefabPath,
                    nameof(RestaurantEditableObject)
                );
            }

            if (member == null)
            {
                AddMissingPrefabComponent(
                    report,
                    prefabAsset,
                    prefabPath,
                    nameof(RestaurantAreaMember)
                );
            }

            if (footprint == null)
            {
                AddMissingPrefabComponent(
                    report,
                    prefabAsset,
                    prefabPath,
                    nameof(RestaurantPlacementFootprint)
                );
            }

            if (placeable == null ||
                editable == null ||
                member == null ||
                footprint == null)
            {
                return;
            }

            if (!placeable.ValidateConfiguration(
                    out string configurationError
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-PREFAB-003",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " no supera ValidateConfiguration.",
                    configurationError,
                    prefabAsset,
                    prefabPath
                );
            }

            if (!ReferenceEquals(
                    placeable.ItemDefinition,
                    expectedDefinition
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PREFAB-004",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " no apunta a la definición que lo referencia.",
                    "Esperado: " +
                    expectedDefinition.name +
                    ".",
                    prefabAsset,
                    prefabPath
                );
            }

            if (placeable.HasInstanceId)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PREFAB-005",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " contiene InstanceId en el prefab.",
                    "Los prefabs deben dejarlo vacío para que cada " +
                    "instancia reciba identidad propia.",
                    prefabAsset,
                    prefabPath
                );
            }

            SerializedObject serializedPlaceable =
                new SerializedObject(
                    placeable
                );

            SerializedProperty anchorProperty =
                serializedPlaceable.FindProperty(
                    "placementAnchor"
                );

            Transform anchor =
                anchorProperty != null
                    ? anchorProperty.objectReferenceValue as Transform
                    : null;

            if (anchor == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-PREFAB-006",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " no tiene PlacementAnchor explícito.",
                    "La creación sobre superficies puede colocar la " +
                    "raíz a una altura incorrecta.",
                    prefabAsset,
                    prefabPath
                );
            }
            else if (!BelongsToHierarchy(
                         prefabRoot.transform,
                         anchor
                     ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-PREFAB-007",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " referencia un PlacementAnchor externo.",
                    "El anclaje debe pertenecer al propio prefab.",
                    prefabAsset,
                    prefabPath
                );
            }

            if (member.AssignedArea != null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PREFAB-008",
                    CategoryAreas,
                    prefabRoot.name +
                    " guarda un área de escena dentro del prefab.",
                    "Assigned Area debe permanecer vacío.",
                    prefabAsset,
                    prefabPath
                );
            }

            ValidateMemberRequirements(
                report,
                member,
                prefabAsset,
                prefabPath
            );

            CheckTransformReferenceInsideHierarchy(
                report,
                member,
                "positionReference",
                prefabRoot.transform,
                "BB-PREFAB-009",
                CategoryPlaceables,
                "Position Reference",
                prefabAsset,
                prefabPath
            );

            ValidateFootprint(
                report,
                footprint,
                prefabAsset,
                prefabPath
            );

            if (!ReferenceEquals(
                    editable.Definition,
                    expectedDefinition.EditableDefinition
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-PREFAB-010",
                    CategoryPlaceables,
                    prefabRoot.name +
                    " utiliza una definición editable distinta.",
                    "La definición del prefab debe coincidir con " +
                    expectedDefinition.name +
                    ".",
                    prefabAsset,
                    prefabPath
                );
            }

            RestaurantTable table =
                prefabRoot.GetComponent<RestaurantTable>();

            if (table != null)
            {
                ValidateTablePrefab(
                    report,
                    table,
                    prefabRoot.transform,
                    prefabAsset,
                    prefabPath
                );
            }
        }
        catch (Exception exception)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-PREFAB-099",
                CategoryPlaceables,
                "Excepción al validar " +
                prefabPath +
                ".",
                exception.ToString(),
                prefabAsset,
                prefabPath
            );
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );
            }
        }
    }

    private static void ValidateCatalogAssets(
        BistroBuilderValidationReport report,
        Scene activeScene
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableCatalogDefinition"
            );

        if (guids.Length == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-CATALOG-001",
                CategoryCatalog,
                "No existe ningún catálogo de artículos.",
                "Ejecuta Install or Repair Runtime Catalog."
            );

            return;
        }

        HashSet<RestaurantPlaceableItemDefinition> cataloguedItems =
            new HashSet<RestaurantPlaceableItemDefinition>();

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableCatalogDefinition
                >(path);

            if (catalog == null)
            {
                continue;
            }

            HashSet<string> itemIds =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int itemIndex = 0;
                 itemIndex < items.Count;
                 itemIndex++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[itemIndex];

                if (item == null)
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Error,
                        "BB-CATALOG-002",
                        CategoryCatalog,
                        catalog.name +
                        " contiene una referencia nula.",
                        "Regenera o limpia el catálogo.",
                        catalog,
                        path
                    );

                    continue;
                }

                if (!itemIds.Add(item.ItemId))
                {
                    report.Add(
                        BistroBuilderValidationSeverity.Error,
                        "BB-CATALOG-003",
                        CategoryCatalog,
                        catalog.name +
                        " contiene dos veces " +
                        item.ItemId +
                        ".",
                        "Cada artículo debe aparecer una sola vez.",
                        catalog,
                        path
                    );
                }

                cataloguedItems.Add(item);
            }
        }

        string[] itemGuids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableItemDefinition"
            );

        for (int index = 0;
             index < itemGuids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    itemGuids[index]
                );

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(path);

            if (item != null &&
                !cataloguedItems.Contains(item))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-CATALOG-004",
                    CategoryCatalog,
                    item.DisplayName +
                    " no aparece en ningún catálogo.",
                    "No será visible para el jugador.",
                    item,
                    path
                );
            }
        }

        RestaurantPlaceableCatalogDefinition mainCatalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogAssetPath);

        if (mainCatalog == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-CATALOG-005",
                CategoryCatalog,
                "No existe el catálogo principal en su ruta estándar.",
                MainCatalogAssetPath
            );
        }

        if (!activeScene.IsValid() ||
            !activeScene.isLoaded)
        {
            return;
        }

        List<RestaurantPlaceableCatalogService> services =
            FindSceneComponents<RestaurantPlaceableCatalogService>(
                activeScene
            );

        if (services.Count == 0)
        {
            return;
        }

        RestaurantPlaceableCatalogDefinition assignedCatalog =
            services[0].CatalogDefinition;

        if (assignedCatalog == null)
        {
            return;
        }

        if (mainCatalog != null &&
            !ReferenceEquals(
                assignedCatalog,
                mainCatalog
            ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-CATALOG-006",
                CategoryCatalog,
                "GameSystems utiliza un catálogo distinto del principal.",
                "Asignado: " +
                AssetDatabase.GetAssetPath(assignedCatalog) +
                ".",
                services[0]
            );
        }
    }

    private static void ValidateProjectConfiguration(
        BistroBuilderValidationReport report,
        Scene scene
    )
    {
        int placementSurfaceLayer =
            LayerMask.NameToLayer(
                PlacementSurfaceLayerName
            );

        if (placementSurfaceLayer < 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-PROJECT-001",
                CategoryProject,
                "No existe la capa " +
                PlacementSurfaceLayerName +
                ".",
                "El raycast de creación no podrá localizar superficies."
            );

            return;
        }

        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return;
        }

        List<Collider> colliders =
            FindSceneComponents<Collider>(
                scene
            );

        int placementSurfaceColliderCount =
            0;

        for (int index = 0;
             index < colliders.Count;
             index++)
        {
            Collider collider =
                colliders[index];

            if (collider.gameObject.layer ==
                placementSurfaceLayer)
            {
                placementSurfaceColliderCount++;
            }
        }

        if (placementSurfaceColliderCount == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-PROJECT-002",
                CategoryProject,
                "No hay ningún Collider en la capa " +
                PlacementSurfaceLayerName +
                ".",
                "La creación no podrá seguir el cursor."
            );
        }

        string packageManifestPath =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "Packages",
                "manifest.json"
            );

        if (!File.Exists(packageManifestPath))
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-PROJECT-003",
                CategoryProject,
                "No se pudo localizar Packages/manifest.json.",
                "El validador no puede confirmar las dependencias."
            );
        }
        else
        {
            string manifestText =
                File.ReadAllText(
                    packageManifestPath
                );

            if (!manifestText.Contains(
                    "\"com.unity.inputsystem\""
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Blocker,
                    "BB-PROJECT-004",
                    CategoryProject,
                    "El nuevo Input System no figura en manifest.json.",
                    "RestaurantEditInteractionController depende de él."
                );
            }

            if (!manifestText.Contains(
                    "\"com.unity.test-framework\""
                ))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Info,
                    "BB-PROJECT-005",
                    CategoryProject,
                    "Unity Test Framework no figura en manifest.json.",
                    "Las futuras pruebas automáticas no podrán ejecutarse."
                );
            }
        }
    }

    private static void ValidateSourceHygiene(
        BistroBuilderValidationReport report
    )
    {
        CheckSourceFileName(
            report,
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "estaurantPlacementObstacle.cs",
            "RestaurantPlacementObstacle.cs",
            "BB-SOURCE-001"
        );

        CheckSourceFileName(
            report,
            "Assets/Scripts/Simulation/Restaurant/Placement/" +
            "NewMonoBehaviourScript.cs",
            "RestaurantPlacementCollisionUtility.cs",
            "BB-SOURCE-002"
        );

        string smokeTestPath =
            "Assets/Scripts/Development/Restaurant/Placement/" +
            "RestaurantPlacementTransactionSmokeTest.cs";

        if (File.Exists(smokeTestPath))
        {
            report.Add(
                BistroBuilderValidationSeverity.Info,
                "BB-SOURCE-003",
                CategorySource,
                "El smoke test manual permanece en el proyecto.",
                "No es un error. Antes de una build pública conviene " +
                "mover las pruebas a un ensamblado de tests.",
                AssetDatabase.LoadAssetAtPath<MonoScript>(
                    smokeTestPath
                ),
                smokeTestPath
            );
        }
    }

    private static void ValidateMissingScriptsInPrefab(
        BistroBuilderValidationReport report,
        GameObject prefabRoot,
        GameObject prefabAsset,
        string prefabPath
    )
    {
        Transform[] transforms =
            prefabRoot.GetComponentsInChildren<Transform>(
                true
            );

        for (int index = 0;
             index < transforms.Length;
             index++)
        {
            GameObject currentObject =
                transforms[index].gameObject;

            int missingScriptCount =
                GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(
                        currentObject
                    );

            if (missingScriptCount <= 0)
            {
                continue;
            }

            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-PREFAB-020",
                CategoryPlaceables,
                prefabRoot.name +
                " contiene scripts perdidos.",
                GetHierarchyPath(
                    currentObject.transform,
                    prefabRoot.transform
                ) +
                " tiene " +
                missingScriptCount +
                " componente(s) Missing.",
                prefabAsset,
                prefabPath
            );
        }
    }

    private static void ValidateMemberRequirements(
        BistroBuilderValidationReport report,
        RestaurantAreaMember member,
        UnityEngine.Object context,
        string assetPath
    )
    {
        IReadOnlyList<RestaurantAreaCapabilityDefinition> requirements =
            member.RequiredCapabilities;

        if (requirements == null)
        {
            return;
        }

        HashSet<RestaurantAreaCapabilityDefinition> knownRequirements =
            new HashSet<RestaurantAreaCapabilityDefinition>();

        for (int index = 0;
             index < requirements.Count;
             index++)
        {
            RestaurantAreaCapabilityDefinition capability =
                requirements[index];

            if (capability == null)
            {
                report.Add(
                    BistroBuilderValidationSeverity.Error,
                    "BB-MEMBER-010",
                    CategoryAreas,
                    member.name +
                    " contiene un requisito de área nulo.",
                    "Revisa Required Capabilities.",
                    context,
                    assetPath
                );

                continue;
            }

            if (!knownRequirements.Add(capability))
            {
                report.Add(
                    BistroBuilderValidationSeverity.Warning,
                    "BB-MEMBER-011",
                    CategoryAreas,
                    member.name +
                    " repite el requisito " +
                    capability.DisplayName +
                    ".",
                    "El duplicado puede eliminarse.",
                    context,
                    assetPath
                );
            }
        }
    }

    private static void ValidateFootprint(
        BistroBuilderValidationReport report,
        RestaurantPlacementFootprint footprint,
        UnityEngine.Object context,
        string assetPath
    )
    {
        Vector2 size =
            footprint.Size;

        if (size.x <= 0f ||
            size.y <= 0f)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                "BB-FOOTPRINT-001",
                CategoryPlaceables,
                footprint.name +
                " tiene una huella no válida.",
                "Size debe ser positivo en ambos ejes.",
                context,
                assetPath
            );

            return;
        }

        float maximumUsefulInset =
            Mathf.Min(
                Mathf.Abs(size.x),
                Mathf.Abs(size.y)
            ) *
            0.5f;

        if (footprint.BoundaryInset >=
            maximumUsefulInset)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-FOOTPRINT-002",
                CategoryPlaceables,
                footprint.name +
                " tiene un Boundary Inset excesivo.",
                "El margen consume toda la huella.",
                context,
                assetPath
            );
        }

        if (footprint.MinimumClearance < 0f)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-FOOTPRINT-003",
                CategoryPlaceables,
                footprint.name +
                " tiene Minimum Clearance negativo.",
                "La separación debe ser cero o positiva.",
                context,
                assetPath
            );
        }
    }

    private static void ValidateTablePrefab(
        BistroBuilderValidationReport report,
        RestaurantTable table,
        Transform prefabRoot,
        GameObject prefabAsset,
        string prefabPath
    )
    {
        if (table.Capacity <= 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-TABLE-010",
                CategoryPlaceables,
                prefabRoot.name +
                " tiene una capacidad de mesa no válida.",
                "Capacity debe ser mayor que cero.",
                prefabAsset,
                prefabPath
            );
        }

        ValidatePrefabTransformReference(
            report,
            table.CustomerApproachPoint,
            prefabRoot,
            "Customer Approach Point",
            "BB-TABLE-011",
            prefabAsset,
            prefabPath
        );

        ValidatePrefabTransformReference(
            report,
            table.WaiterServicePoint,
            prefabRoot,
            "Waiter Service Point",
            "BB-TABLE-012",
            prefabAsset,
            prefabPath
        );

        if (table.CustomerApproachPoint != null &&
            ReferenceEquals(
                table.CustomerApproachPoint,
                table.WaiterServicePoint
            ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                "BB-TABLE-013",
                CategoryPlaceables,
                prefabRoot.name +
                " comparte los puntos de cliente y camarero.",
                "Deben ser dos hijos distintos.",
                prefabAsset,
                prefabPath
            );
        }
    }

    private static void ValidateTableInteractionPoint(
        BistroBuilderValidationReport report,
        RestaurantTable table,
        Transform point,
        string pointName,
        string code
    )
    {
        if (point == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryPlaceables,
                table.name +
                " no tiene " +
                pointName +
                ".",
                "La simulación no podrá calcular el destino.",
                table
            );

            return;
        }

        if (!BelongsToHierarchy(
                table.transform,
                point
            ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryPlaceables,
                table.name +
                " utiliza un " +
                pointName +
                " externo.",
                "El punto debe pertenecer a su propia jerarquía.",
                table
            );
        }
    }

    private static void ValidatePrefabTransformReference(
        BistroBuilderValidationReport report,
        Transform reference,
        Transform prefabRoot,
        string referenceName,
        string code,
        GameObject prefabAsset,
        string prefabPath
    )
    {
        if (reference == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryPlaceables,
                prefabRoot.name +
                " no tiene " +
                referenceName +
                ".",
                "La simulación no podrá utilizar el artículo.",
                prefabAsset,
                prefabPath
            );

            return;
        }

        if (!BelongsToHierarchy(
                prefabRoot,
                reference
            ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryPlaceables,
                prefabRoot.name +
                " utiliza un " +
                referenceName +
                " externo.",
                "La referencia debe pertenecer al prefab.",
                prefabAsset,
                prefabPath
            );
        }
    }

    private static void AddMissingPrefabComponent(
        BistroBuilderValidationReport report,
        GameObject prefabAsset,
        string prefabPath,
        string componentName
    )
    {
        report.Add(
            BistroBuilderValidationSeverity.Blocker,
            "BB-PREFAB-002",
            CategoryPlaceables,
            prefabAsset.name +
            " no contiene " +
            componentName +
            " en su raíz.",
            "Ejecuta Configure Selected Prefab(s) o revisa el prefab.",
            prefabAsset,
            prefabPath
        );
    }

    private static void ValidateSinglePresentationComponent<T>(
        BistroBuilderValidationReport report,
        List<T> components,
        string displayName,
        string code
    )
        where T : Component
    {
        if (components.Count == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                code,
                CategoryInterface,
                "No existe " +
                displayName +
                " en la escena.",
                "Ejecuta su instalador automático."
            );

            return;
        }

        if (components.Count > 1)
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryInterface,
                "Existen " +
                components.Count +
                " copias de " +
                displayName +
                ".",
                "Debe existir una sola instancia.",
                components[0]
            );
        }
    }

    private static void ValidateContextButtonLabel(
        BistroBuilderValidationReport report,
        RestaurantPlaceableContextPanel panel,
        string buttonFieldName,
        string expectedLabel,
        string code
    )
    {
        SerializedObject serializedPanel =
            new SerializedObject(panel);

        SerializedProperty buttonProperty =
            serializedPanel.FindProperty(
                buttonFieldName
            );

        Button button =
            buttonProperty != null
                ? buttonProperty.objectReferenceValue as Button
                : null;

        if (button == null)
        {
            return;
        }

        Text label =
            button.GetComponentInChildren<Text>(
                true
            );

        if (label == null ||
            string.IsNullOrWhiteSpace(label.text))
        {
            report.Add(
                BistroBuilderValidationSeverity.Error,
                code,
                CategoryInterface,
                "El botón " +
                expectedLabel +
                " no tiene texto visible.",
                "Ejecuta Install or Repair Context Panel.",
                button
            );

            return;
        }

        if (!string.Equals(
                label.text.Trim(),
                expectedLabel,
                StringComparison.CurrentCultureIgnoreCase
            ))
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                code,
                CategoryInterface,
                "El botón esperado como " +
                expectedLabel +
                " muestra \"" +
                label.text +
                "\".",
                "Confirma que la etiqueta sea intencionada.",
                button
            );
        }
    }

    private static void CheckRequiredReferences(
        BistroBuilderValidationReport report,
        Component component,
        string[] fieldNames,
        string code
    )
    {
        if (component == null)
        {
            return;
        }

        for (int index = 0;
             index < fieldNames.Length;
             index++)
        {
            CheckRequiredReference(
                report,
                component,
                fieldNames[index],
                code
            );
        }
    }

    private static void CheckRequiredReference(
        BistroBuilderValidationReport report,
        Component component,
        string fieldName,
        string code
    )
    {
        if (component == null)
        {
            return;
        }

        SerializedObject serializedObject =
            new SerializedObject(component);

        SerializedProperty property =
            serializedObject.FindProperty(
                fieldName
            );

        if (property == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-VALIDATOR-001",
                CategoryProject,
                "El validador no reconoce " +
                component.GetType().Name +
                "." +
                fieldName +
                ".",
                "El código puede haber evolucionado y el validador " +
                "necesita actualizarse.",
                component
            );

            return;
        }

        if (property.propertyType !=
            SerializedPropertyType.ObjectReference)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-VALIDATOR-002",
                CategoryProject,
                component.GetType().Name +
                "." +
                fieldName +
                " ya no es una referencia.",
                "Revisa la versión del validador.",
                component
            );

            return;
        }

        if (property.objectReferenceValue != null)
        {
            return;
        }

        report.Add(
            BistroBuilderValidationSeverity.Blocker,
            code,
            CategorySystems,
            component.GetType().Name +
            " no tiene asignado " +
            fieldName +
            ".",
            "La dependencia es obligatoria.",
            component
        );
    }

    private static void CheckRequiredArray(
        BistroBuilderValidationReport report,
        Component component,
        string fieldName,
        string code
    )
    {
        if (component == null)
        {
            return;
        }

        SerializedObject serializedObject =
            new SerializedObject(component);

        SerializedProperty property =
            serializedObject.FindProperty(
                fieldName
            );

        if (property == null ||
            !property.isArray)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-VALIDATOR-003",
                CategoryProject,
                "El validador no reconoce el array " +
                component.GetType().Name +
                "." +
                fieldName +
                ".",
                "Revisa la versión del validador.",
                component
            );

            return;
        }

        int validReferenceCount =
            0;

        for (int index = 0;
             index < property.arraySize;
             index++)
        {
            SerializedProperty element =
                property.GetArrayElementAtIndex(
                    index
                );

            if (element.propertyType ==
                    SerializedPropertyType.ObjectReference &&
                element.objectReferenceValue != null)
            {
                validReferenceCount++;
            }
        }

        if (validReferenceCount > 0)
        {
            return;
        }

        report.Add(
            BistroBuilderValidationSeverity.Blocker,
            code,
            CategorySystems,
            component.GetType().Name +
            " no tiene ninguna regla en " +
            fieldName +
            ".",
            "El modo edición quedaría sin política de disponibilidad.",
            component
        );
    }

    private static void CheckLayerMask(
        BistroBuilderValidationReport report,
        Component component,
        string fieldName,
        string requiredLayerName,
        string code
    )
    {
        if (component == null)
        {
            return;
        }

        SerializedObject serializedObject =
            new SerializedObject(component);

        SerializedProperty property =
            serializedObject.FindProperty(
                fieldName
            );

        if (property == null)
        {
            report.Add(
                BistroBuilderValidationSeverity.Warning,
                "BB-VALIDATOR-004",
                CategoryProject,
                "El validador no reconoce " +
                component.GetType().Name +
                "." +
                fieldName +
                ".",
                "Revisa la versión del validador.",
                component
            );

            return;
        }

        int mask =
            property.intValue;

        if (mask == 0)
        {
            report.Add(
                BistroBuilderValidationSeverity.Blocker,
                code,
                CategorySystems,
                component.GetType().Name +
                "." +
                fieldName +
                " está vacío.",
                "El raycast no encontrará ningún objeto.",
                component
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(requiredLayerName))
        {
            return;
        }

        int requiredLayer =
            LayerMask.NameToLayer(
                requiredLayerName
            );

        if (requiredLayer < 0)
        {
            return;
        }

        int requiredLayerBit =
            1 << requiredLayer;

        if ((mask & requiredLayerBit) != 0)
        {
            return;
        }

        report.Add(
            BistroBuilderValidationSeverity.Blocker,
            code,
            CategorySystems,
            component.GetType().Name +
            "." +
            fieldName +
            " no incluye " +
            requiredLayerName +
            ".",
            "La superficie no responderá al cursor.",
            component
        );
    }

    private static void CheckTransformReferenceInsideHierarchy(
        BistroBuilderValidationReport report,
        Component owner,
        string fieldName,
        Transform hierarchyRoot,
        string code,
        string category,
        string displayName,
        UnityEngine.Object overrideContext = null,
        string assetPath = ""
    )
    {
        SerializedObject serializedOwner =
            new SerializedObject(owner);

        SerializedProperty property =
            serializedOwner.FindProperty(
                fieldName
            );

        if (property == null)
        {
            return;
        }

        Transform reference =
            property.objectReferenceValue as Transform;

        if (reference == null)
        {
            return;
        }

        if (BelongsToHierarchy(
                hierarchyRoot,
                reference
            ))
        {
            return;
        }

        report.Add(
            BistroBuilderValidationSeverity.Error,
            code,
            category,
            owner.name +
            " utiliza un " +
            displayName +
            " externo.",
            "La referencia debe pertenecer a su propia jerarquía.",
            overrideContext != null
                ? overrideContext
                : owner,
            assetPath
        );
    }

    private static void CheckSourceFileName(
        BistroBuilderValidationReport report,
        string existingPath,
        string recommendedFileName,
        string code
    )
    {
        if (!File.Exists(existingPath))
        {
            return;
        }

        MonoScript script =
            AssetDatabase.LoadAssetAtPath<MonoScript>(
                existingPath
            );

        report.Add(
            BistroBuilderValidationSeverity.Warning,
            code,
            CategorySource,
            Path.GetFileName(existingPath) +
            " conserva un nombre técnico incorrecto.",
            "Nombre recomendado: " +
            recommendedFileName +
            ". No rompe la compilación actual, pero dificulta " +
            "búsqueda, diagnóstico y mantenimiento.",
            script,
            existingPath
        );
    }

    private static List<T> FindSceneComponents<T>(
        Scene scene
    )
        where T : Component
    {
        List<T> results =
            new List<T>(64);

        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return results;
        }

        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            T[] components =
                roots[rootIndex]
                    .GetComponentsInChildren<T>(true);

            for (int componentIndex = 0;
                 componentIndex < components.Length;
                 componentIndex++)
            {
                T component =
                    components[componentIndex];

                if (component != null)
                {
                    results.Add(component);
                }
            }
        }

        return results;
    }

    private static GameObject[] FindSceneGameObjectsByName(
        Scene scene,
        string objectName
    )
    {
        List<GameObject> results =
            new List<GameObject>(4);

        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return results.ToArray();
        }

        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            Transform[] transforms =
                roots[rootIndex]
                    .GetComponentsInChildren<Transform>(true);

            for (int transformIndex = 0;
                 transformIndex < transforms.Length;
                 transformIndex++)
            {
                Transform currentTransform =
                    transforms[transformIndex];

                if (string.Equals(
                        currentTransform.name,
                        objectName,
                        StringComparison.Ordinal
                    ))
                {
                    results.Add(
                        currentTransform.gameObject
                    );
                }
            }
        }

        return results.ToArray();
    }

    private static bool BelongsToHierarchy(
        Transform root,
        Transform candidate
    )
    {
        return root != null &&
               candidate != null &&
               (
                   ReferenceEquals(root, candidate) ||
                   candidate.IsChildOf(root)
               );
    }

    private static string GetHierarchyPath(
        Transform target,
        Transform root
    )
    {
        if (target == null)
        {
            return "<null>";
        }

        List<string> segments =
            new List<string>(8);

        Transform current =
            target;

        while (current != null)
        {
            segments.Add(
                current.name
            );

            if (ReferenceEquals(current, root))
            {
                break;
            }

            current =
                current.parent;
        }

        segments.Reverse();

        return string.Join(
            "/",
            segments.ToArray()
        );
    }
}
