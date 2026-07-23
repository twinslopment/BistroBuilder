using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Valida físicamente la colocación de objetos del restaurante.
///
/// Combina:
/// - Compatibilidad con el área.
/// - Capacidades requeridas.
/// - Huella completa dentro del área.
/// - Solapamiento con otros objetos colocables.
/// - Solapamiento con obstáculos fijos.
/// - Separación mínima.
///
/// No mueve objetos, no modifica áreas y no utiliza Update.
/// Puede evaluar poses candidatas antes de confirmarlas.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlacementValidationService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio responsable de validar áreas y capacidades."
    )]
    [SerializeField]
    private RestaurantAreaAssignmentService
        areaAssignmentService;

    [Tooltip(
        "Registro central de las huellas colocables."
    )]
    [SerializeField]
    private RestaurantPlacementRegistry
        placementRegistry;

    [Tooltip(
        "Registro central de paredes, columnas y otros " +
        "obstáculos fijos."
    )]
    [SerializeField]
    private RestaurantPlacementObstacleRegistry
        obstacleRegistry;

    [Tooltip(
        "Coordina reglas modulares específicas de familias, como " +
        "asientos, puertas o equipamiento con espacio operativo."
    )]
    [SerializeField]
    private RestaurantPlacementConstraintService
        constraintService;

    [Header("Validación inicial")]

    [Tooltip(
        "Valida las colocaciones existentes al iniciar la escena."
    )]
    [SerializeField]
    private bool validatePlacementsOnStart = true;

    /// <summary>
    /// Lista reutilizable de otras huellas del área candidata.
    /// </summary>
    private readonly List<RestaurantPlacementFootprint>
        nearbyFootprints =
            new List<RestaurantPlacementFootprint>(16);

    /// <summary>
    /// Lista reutilizable de obstáculos fijos operativos.
    /// </summary>
    private readonly List<RestaurantPlacementObstacle>
        blockingObstacles =
            new List<RestaurantPlacementObstacle>(16);

    private Coroutine initialValidationRoutine;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void Start()
    {
        if (!validatePlacementsOnStart)
        {
            return;
        }

        initialValidationRoutine =
            StartCoroutine(
                ValidateAfterStartupRoutine()
            );
    }

    private void OnDisable()
    {
        if (initialValidationRoutine == null)
        {
            return;
        }

        StopCoroutine(initialValidationRoutine);
        initialValidationRoutine = null;
    }

    /// <summary>
    /// Valida una posición y una rotación candidatas sin modificar
    /// el Transform real del objeto.
    /// </summary>
    public RestaurantPlacementValidationResult
        ValidatePlacement(
            RestaurantAreaMember member,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation
        )
    {
        if (member == null)
        {
            return CreateResult(
                RestaurantPlacementValidationStatus
                    .InvalidMember,
                null
            );
        }

        if (areaAssignmentService == null)
        {
            return CreateResult(
                RestaurantPlacementValidationStatus
                    .SystemUnavailable,
                member
            );
        }

        RestaurantAreaPlacementValidationStatus
            areaStatus =
                areaAssignmentService
                    .ValidatePlacementAtPose(
                        member,
                        candidateRootPosition,
                        candidateRootRotation,
                        out RestaurantArea candidateArea,
                        out RestaurantAreaCapabilityDefinition
                            missingCapability
                    );

        RestaurantPlacementValidationStatus
            mappedAreaStatus =
                MapAreaValidationStatus(areaStatus);

        if (mappedAreaStatus !=
            RestaurantPlacementValidationStatus.Valid)
        {
            return new RestaurantPlacementValidationResult(
                mappedAreaStatus,
                member,
                null,
                candidateArea,
                missingCapability,
                null,
                null,
                RestaurantPlacementConflictType.None
            );
        }

        member.TryGetComponent(
            out RestaurantPlacementFootprint
                candidateFootprint
        );

        /*
         * Las reglas funcionales se evalúan antes que los conflictos
         * físicos genéricos. Así una quinta silla comunica la causa
         * real —capacidad máxima— aunque también esté cerca de otra
         * silla. El núcleo sigue comprobando después colisiones y
         * separación para cualquier candidato que supere sus reglas.
         */
        RestaurantPlacementConstraintEvaluation
            constraintEvaluation =
                RestaurantPlacementConstraintEvaluation.Valid();

        if (constraintService != null)
        {
            RestaurantPlacementConstraintContext context =
                new RestaurantPlacementConstraintContext(
                    member,
                    candidateRootPosition,
                    candidateRootRotation,
                    candidateArea,
                    candidateFootprint,
                    placementRegistry,
                    obstacleRegistry
                );

            constraintEvaluation =
                constraintService.Evaluate(context);

            if (!constraintEvaluation.IsValid &&
                constraintEvaluation
                    .ShouldOverrideGenericConflicts)
            {
                return CreateConstraintViolationResult(
                    member,
                    candidateFootprint,
                    candidateArea,
                    constraintEvaluation
                );
            }
        }

        if (candidateFootprint == null ||
            !candidateFootprint.BlocksOtherPlacements)
        {
            return !constraintEvaluation.IsValid
                ? CreateConstraintViolationResult(
                    member,
                    candidateFootprint,
                    candidateArea,
                    constraintEvaluation
                )
                : CreateValidResult(
                    member,
                    candidateFootprint,
                    candidateArea
                );
        }

        if (placementRegistry == null ||
            obstacleRegistry == null)
        {
            return CreateResult(
                RestaurantPlacementValidationStatus
                    .SystemUnavailable,
                member,
                candidateFootprint,
                candidateArea
            );
        }

        RestaurantPlacementShape candidateShape =
            candidateFootprint.BuildShapeAtPose(
                candidateRootPosition,
                candidateRootRotation
            );

        placementRegistry.CopyFootprintsInArea(
            candidateArea,
            nearbyFootprints,
            candidateFootprint,
            true
        );

        obstacleRegistry.CopyBlockingObstacles(
            blockingObstacles
        );

        RestaurantPlacementFootprint
            nearestPhysicalFootprint = null;

        RestaurantPlacementObstacle
            nearestPhysicalObstacle = null;

        RestaurantPlacementFootprint
            nearestClearanceFootprint = null;

        RestaurantPlacementObstacle
            nearestClearanceObstacle = null;

        float nearestPhysicalDistance =
            float.PositiveInfinity;

        float nearestClearanceDistance =
            float.PositiveInfinity;

        EvaluateFootprintConflicts(
            candidateShape,
            ref nearestPhysicalFootprint,
            ref nearestPhysicalObstacle,
            ref nearestClearanceFootprint,
            ref nearestClearanceObstacle,
            ref nearestPhysicalDistance,
            ref nearestClearanceDistance
        );

        EvaluateObstacleConflicts(
            candidateShape,
            ref nearestPhysicalFootprint,
            ref nearestPhysicalObstacle,
            ref nearestClearanceFootprint,
            ref nearestClearanceObstacle,
            ref nearestPhysicalDistance,
            ref nearestClearanceDistance
        );

        if (nearestPhysicalFootprint != null ||
            nearestPhysicalObstacle != null)
        {
            return new RestaurantPlacementValidationResult(
                RestaurantPlacementValidationStatus
                    .PhysicalOverlap,
                member,
                candidateFootprint,
                candidateArea,
                null,
                nearestPhysicalFootprint,
                nearestPhysicalObstacle,
                RestaurantPlacementConflictType
                    .PhysicalOverlap
            );
        }

        if (nearestClearanceFootprint != null ||
            nearestClearanceObstacle != null)
        {
            return new RestaurantPlacementValidationResult(
                RestaurantPlacementValidationStatus
                    .MinimumClearanceViolation,
                member,
                candidateFootprint,
                candidateArea,
                null,
                nearestClearanceFootprint,
                nearestClearanceObstacle,
                RestaurantPlacementConflictType
                    .MinimumClearanceViolation
            );
        }

        if (!constraintEvaluation.IsValid)
        {
            return CreateConstraintViolationResult(
                member,
                candidateFootprint,
                candidateArea,
                constraintEvaluation
            );
        }

        return CreateValidResult(
            member,
            candidateFootprint,
            candidateArea
        );
    }

    /// <summary>
    /// Valida la colocación actual de un miembro.
    /// </summary>
    public RestaurantPlacementValidationResult
        ValidateCurrentPlacement(
            RestaurantAreaMember member
        )
    {
        if (member == null)
        {
            return CreateResult(
                RestaurantPlacementValidationStatus
                    .InvalidMember,
                null
            );
        }

        return ValidatePlacement(
            member,
            member.transform.position,
            member.transform.rotation
        );
    }

    /// <summary>
    /// Versión simplificada destinada al controlador del modo
    /// edición.
    /// </summary>
    public bool CanPlace(
        RestaurantAreaMember member,
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        out RestaurantPlacementValidationResult result
    )
    {
        result =
            ValidatePlacement(
                member,
                candidateRootPosition,
                candidateRootRotation
            );

        return result.IsValid;
    }

    /// <summary>
    /// Valida todas las huellas actualmente registradas.
    /// </summary>
    public RestaurantPlacementValidationSummary
        ValidateAllRegisteredPlacements(
            bool logDetails = true
        )
    {
        if (placementRegistry == null ||
            obstacleRegistry == null)
        {
            return default;
        }

        placementRegistry
            .RefreshFromRegisteredMembers();

        obstacleRegistry
            .RefreshFromScene();

        int totalCount = 0;
        int validCount = 0;
        int areaErrorCount = 0;
        int physicalOverlapCount = 0;
        int clearanceViolationCount = 0;
        int constraintViolationCount = 0;
        int systemErrorCount = 0;

        foreach (RestaurantPlacementFootprint footprint
                 in placementRegistry.RegisteredFootprints)
        {
            if (footprint == null)
            {
                continue;
            }

            totalCount++;

            if (!placementRegistry.TryGetMember(
                    footprint,
                    out RestaurantAreaMember member
                ))
            {
                systemErrorCount++;

                if (logDetails)
                {
                    Debug.LogError(
                        $"{footprint.name} no tiene un miembro " +
                        "asociado en el registro de colocación.",
                        footprint
                    );
                }

                continue;
            }

            RestaurantPlacementValidationResult result =
                ValidateCurrentPlacement(member);

            switch (result.Status)
            {
                case RestaurantPlacementValidationStatus.Valid:
                    validCount++;
                    break;

                case RestaurantPlacementValidationStatus
                    .PhysicalOverlap:

                    physicalOverlapCount++;

                    if (logDetails)
                    {
                        string conflictName =
                            GetConflictName(result);

                        Debug.LogWarning(
                            $"{member.name} se solapa físicamente " +
                            $"con {conflictName}.",
                            member
                        );
                    }

                    break;

                case RestaurantPlacementValidationStatus
                    .MinimumClearanceViolation:

                    clearanceViolationCount++;

                    if (logDetails)
                    {
                        string conflictName =
                            GetConflictName(result);

                        Debug.LogWarning(
                            $"{member.name} no mantiene la " +
                            "separación mínima respecto a " +
                            $"{conflictName}.",
                            member
                        );
                    }

                    break;

                case RestaurantPlacementValidationStatus
                    .PlacementConstraintViolation:

                    constraintViolationCount++;

                    if (logDetails)
                    {
                        string message =
                            !string.IsNullOrWhiteSpace(
                                result.UserMessage
                            )
                                ? result.UserMessage
                                : "La colocación incumple una regla " +
                                  "funcional especializada.";

                        Debug.LogWarning(
                            message,
                            member
                        );
                    }

                    break;

                case RestaurantPlacementValidationStatus
                    .SystemUnavailable:

                    systemErrorCount++;

                    if (logDetails)
                    {
                        Debug.LogError(
                            $"No se puede validar la colocación " +
                            $"de {member.name} porque falta una " +
                            "dependencia del sistema.",
                            member
                        );
                    }

                    break;

                default:
                    areaErrorCount++;

                    if (logDetails)
                    {
                        string errorMessage =
                            BuildAreaErrorMessage(result);

                        Debug.LogWarning(
                            errorMessage,
                            member
                        );
                    }

                    break;
            }
        }

        RestaurantPlacementValidationSummary summary =
            new RestaurantPlacementValidationSummary(
                totalCount,
                validCount,
                areaErrorCount,
                physicalOverlapCount,
                clearanceViolationCount,
                constraintViolationCount,
                systemErrorCount
            );

        Debug.Log(
            $"Validación de colocación completada. " +
            $"Total: {summary.TotalCount}, " +
            $"correctas: {summary.ValidCount}, " +
            $"errores de área: {summary.AreaErrorCount}, " +
            $"solapamientos físicos: " +
            $"{summary.PhysicalOverlapCount}, " +
            $"separación insuficiente: " +
            $"{summary.ClearanceViolationCount}, " +
            $"reglas funcionales incumplidas: " +
            $"{summary.ConstraintViolationCount}, " +
            $"errores de sistema: " +
            $"{summary.SystemErrorCount}.",
            this
        );

        return summary;
    }

    /// <summary>
    /// Evalúa conflictos con otras huellas colocables.
    /// </summary>
    private void EvaluateFootprintConflicts(
        RestaurantPlacementShape candidateShape,
        ref RestaurantPlacementFootprint
            nearestPhysicalFootprint,
        ref RestaurantPlacementObstacle
            nearestPhysicalObstacle,
        ref RestaurantPlacementFootprint
            nearestClearanceFootprint,
        ref RestaurantPlacementObstacle
            nearestClearanceObstacle,
        ref float nearestPhysicalDistance,
        ref float nearestClearanceDistance
    )
    {
        for (int index = 0;
             index < nearbyFootprints.Count;
             index++)
        {
            RestaurantPlacementFootprint otherFootprint =
                nearbyFootprints[index];

            if (otherFootprint == null)
            {
                continue;
            }

            RestaurantPlacementShape otherShape =
                otherFootprint.BuildCurrentShape();

            RestaurantPlacementConflictType conflict =
                RestaurantPlacementCollisionUtility
                    .EvaluateConflict(
                        candidateShape,
                        otherShape
                    );

            RegisterNearestConflict(
                conflict,
                otherShape.Center,
                candidateShape.Center,
                otherFootprint,
                null,
                ref nearestPhysicalFootprint,
                ref nearestPhysicalObstacle,
                ref nearestClearanceFootprint,
                ref nearestClearanceObstacle,
                ref nearestPhysicalDistance,
                ref nearestClearanceDistance
            );
        }
    }

    /// <summary>
    /// Evalúa conflictos con paredes, columnas y demás
    /// obstáculos fijos.
    /// </summary>
    private void EvaluateObstacleConflicts(
        RestaurantPlacementShape candidateShape,
        ref RestaurantPlacementFootprint
            nearestPhysicalFootprint,
        ref RestaurantPlacementObstacle
            nearestPhysicalObstacle,
        ref RestaurantPlacementFootprint
            nearestClearanceFootprint,
        ref RestaurantPlacementObstacle
            nearestClearanceObstacle,
        ref float nearestPhysicalDistance,
        ref float nearestClearanceDistance
    )
    {
        for (int index = 0;
             index < blockingObstacles.Count;
             index++)
        {
            RestaurantPlacementObstacle obstacle =
                blockingObstacles[index];

            if (obstacle == null ||
                !obstacle.IsBlocking)
            {
                continue;
            }

            Vector2 halfExtents =
                obstacle.WorldSize *
                0.5f;

            RestaurantPlacementShape obstacleShape =
                new RestaurantPlacementShape(
                    obstacle.WorldCenter,
                    obstacle.WorldRightAxis,
                    obstacle.WorldForwardAxis,
                    halfExtents,
                    obstacle.MinimumClearance
                );

            RestaurantPlacementConflictType conflict =
                RestaurantPlacementCollisionUtility
                    .EvaluateConflict(
                        candidateShape,
                        obstacleShape
                    );

            RegisterNearestConflict(
                conflict,
                obstacleShape.Center,
                candidateShape.Center,
                null,
                obstacle,
                ref nearestPhysicalFootprint,
                ref nearestPhysicalObstacle,
                ref nearestClearanceFootprint,
                ref nearestClearanceObstacle,
                ref nearestPhysicalDistance,
                ref nearestClearanceDistance
            );
        }
    }

    /// <summary>
    /// Conserva únicamente el conflicto más cercano de cada tipo.
    /// </summary>
    private static void RegisterNearestConflict(
        RestaurantPlacementConflictType conflict,
        Vector3 otherCenter,
        Vector3 candidateCenter,
        RestaurantPlacementFootprint conflictingFootprint,
        RestaurantPlacementObstacle conflictingObstacle,
        ref RestaurantPlacementFootprint
            nearestPhysicalFootprint,
        ref RestaurantPlacementObstacle
            nearestPhysicalObstacle,
        ref RestaurantPlacementFootprint
            nearestClearanceFootprint,
        ref RestaurantPlacementObstacle
            nearestClearanceObstacle,
        ref float nearestPhysicalDistance,
        ref float nearestClearanceDistance
    )
    {
        if (conflict ==
            RestaurantPlacementConflictType.None)
        {
            return;
        }

        float squaredDistance =
            (
                otherCenter -
                candidateCenter
            ).sqrMagnitude;

        if (conflict ==
            RestaurantPlacementConflictType
                .PhysicalOverlap)
        {
            if (squaredDistance >=
                nearestPhysicalDistance)
            {
                return;
            }

            nearestPhysicalDistance =
                squaredDistance;

            nearestPhysicalFootprint =
                conflictingFootprint;

            nearestPhysicalObstacle =
                conflictingObstacle;

            return;
        }

        if (conflict !=
                RestaurantPlacementConflictType
                    .MinimumClearanceViolation ||
            squaredDistance >=
                nearestClearanceDistance)
        {
            return;
        }

        nearestClearanceDistance =
            squaredDistance;

        nearestClearanceFootprint =
            conflictingFootprint;

        nearestClearanceObstacle =
            conflictingObstacle;
    }

    /// <summary>
    /// Espera un frame para que los registros de áreas,
    /// miembros, huellas y obstáculos completen su inicialización.
    /// </summary>
    private IEnumerator ValidateAfterStartupRoutine()
    {
        yield return null;

        initialValidationRoutine = null;

        ValidateAllRegisteredPlacements();
    }

    /// <summary>
    /// Traduce el resultado del sistema de áreas al resultado
    /// general del sistema de colocación.
    /// </summary>
    private static RestaurantPlacementValidationStatus
        MapAreaValidationStatus(
            RestaurantAreaPlacementValidationStatus status
        )
    {
        switch (status)
        {
            case RestaurantAreaPlacementValidationStatus.Valid:

                return
                    RestaurantPlacementValidationStatus.Valid;

            case RestaurantAreaPlacementValidationStatus
                .InvalidMember:

                return
                    RestaurantPlacementValidationStatus
                        .InvalidMember;

            case RestaurantAreaPlacementValidationStatus
                .OutsideRegisteredAreas:

                return
                    RestaurantPlacementValidationStatus
                        .OutsideRegisteredAreas;

            case RestaurantAreaPlacementValidationStatus
                .MissingAreaDefinition:

                return
                    RestaurantPlacementValidationStatus
                        .MissingAreaDefinition;

            case RestaurantAreaPlacementValidationStatus
                .MissingRequiredCapability:

                return
                    RestaurantPlacementValidationStatus
                        .MissingRequiredCapability;

            case RestaurantAreaPlacementValidationStatus
                .FootprintOutsideCandidateArea:

                return
                    RestaurantPlacementValidationStatus
                        .FootprintOutsideCandidateArea;

            default:

                return
                    RestaurantPlacementValidationStatus
                        .SystemUnavailable;
        }
    }

    /// <summary>
    /// Construye un mensaje legible para errores relacionados
    /// con áreas, límites o capacidades.
    /// </summary>
    private static string BuildAreaErrorMessage(
        RestaurantPlacementValidationResult result
    )
    {
        string memberName =
            result.Member != null
                ? result.Member.name
                : "El objeto";

        string capabilityName =
            GetCapabilityName(
                result.MissingCapability
            );

        switch (result.Status)
        {
            case RestaurantPlacementValidationStatus
                .OutsideRegisteredAreas:

                return
                    $"{memberName} está fuera de las áreas " +
                    "registradas.";

            case RestaurantPlacementValidationStatus
                .MissingAreaDefinition:

                return
                    $"{memberName} está en un área sin " +
                    "definición.";

            case RestaurantPlacementValidationStatus
                .MissingRequiredCapability:

                return
                    $"{memberName} requiere la capacidad " +
                    $"'{capabilityName}', que el área no ofrece.";

            case RestaurantPlacementValidationStatus
                .FootprintOutsideCandidateArea:

                return
                    $"{memberName} tiene parte de su huella " +
                    "fuera del área candidata.";

            case RestaurantPlacementValidationStatus
                .InvalidMember:

                return
                    "Se intentó validar un objeto sin un " +
                    "miembro de área válido.";

            case RestaurantPlacementValidationStatus
                .PlacementConstraintViolation:

                return
                    !string.IsNullOrWhiteSpace(result.UserMessage)
                        ? result.UserMessage
                        : memberName +
                          " incumple una regla funcional de " +
                          "colocación.";

            default:

                return
                    $"{memberName} tiene una colocación inválida.";
        }
    }

    /// <summary>
    /// Obtiene el nombre del elemento que provoca un conflicto.
    /// </summary>
    private static string GetConflictName(
        RestaurantPlacementValidationResult result
    )
    {
        if (result.ConflictingFootprint != null)
        {
            return result.ConflictingFootprint.name;
        }

        if (result.ConflictingObstacle != null)
        {
            return result.ConflictingObstacle.name;
        }

        return "otro objeto";
    }

    /// <summary>
    /// Obtiene un nombre legible de una capacidad.
    /// </summary>
    private static string GetCapabilityName(
        RestaurantAreaCapabilityDefinition capability
    )
    {
        if (capability == null)
        {
            return "capacidad desconocida";
        }

        if (!string.IsNullOrWhiteSpace(
                capability.DisplayName
            ))
        {
            return capability.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(
                capability.CapabilityId
            ))
        {
            return capability.CapabilityId;
        }

        return capability.name;
    }

    /// <summary>
    /// Construye un resultado válido.
    /// </summary>
    private static RestaurantPlacementValidationResult
        CreateConstraintViolationResult(
            RestaurantAreaMember member,
            RestaurantPlacementFootprint footprint,
            RestaurantArea area,
            RestaurantPlacementConstraintEvaluation evaluation
        )
    {
        return new RestaurantPlacementValidationResult(
            RestaurantPlacementValidationStatus
                .PlacementConstraintViolation,
            member,
            footprint,
            area,
            null,
            null,
            null,
            RestaurantPlacementConflictType.None,
            evaluation
        );
    }

    private static RestaurantPlacementValidationResult
        CreateValidResult(
            RestaurantAreaMember member,
            RestaurantPlacementFootprint footprint,
            RestaurantArea area
        )
    {
        return new RestaurantPlacementValidationResult(
            RestaurantPlacementValidationStatus.Valid,
            member,
            footprint,
            area,
            null,
            null,
            null,
            RestaurantPlacementConflictType.None
        );
    }

    /// <summary>
    /// Construye un resultado sencillo sin conflicto físico.
    /// </summary>
    private static RestaurantPlacementValidationResult
        CreateResult(
            RestaurantPlacementValidationStatus status,
            RestaurantAreaMember member,
            RestaurantPlacementFootprint footprint = null,
            RestaurantArea area = null
        )
    {
        return new RestaurantPlacementValidationResult(
            status,
            member,
            footprint,
            area,
            null,
            null,
            null,
            RestaurantPlacementConflictType.None
        );
    }

    /// <summary>
    /// Recupera dependencias que estén en el mismo GameObject.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (areaAssignmentService == null)
        {
            TryGetComponent(
                out areaAssignmentService
            );
        }

        if (placementRegistry == null)
        {
            TryGetComponent(
                out placementRegistry
            );
        }

        if (obstacleRegistry == null)
        {
            TryGetComponent(
                out obstacleRegistry
            );
        }

        if (constraintService == null)
        {
            TryGetComponent(
                out constraintService
            );
        }
    }

    /// <summary>
    /// Comprueba que todas las dependencias obligatorias
    /// estén disponibles.
    /// </summary>
    private void ValidateDependencies()
    {
        if (areaAssignmentService == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantPlacementValidationService)} " +
                $"necesita un " +
                $"{nameof(RestaurantAreaAssignmentService)}.",
                this
            );
        }

        if (placementRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantPlacementValidationService)} " +
                $"necesita un " +
                $"{nameof(RestaurantPlacementRegistry)}.",
                this
            );
        }

        if (obstacleRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantPlacementValidationService)} " +
                $"necesita un " +
                $"{nameof(RestaurantPlacementObstacleRegistry)}.",
                this
            );
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}

/// <summary>
/// Resultado final de una validación de colocación.
/// </summary>
public enum RestaurantPlacementValidationStatus
{
    Valid = 0,
    InvalidMember = 1,
    OutsideRegisteredAreas = 2,
    MissingAreaDefinition = 3,
    MissingRequiredCapability = 4,
    FootprintOutsideCandidateArea = 5,
    PhysicalOverlap = 6,
    MinimumClearanceViolation = 7,
    SystemUnavailable = 8,
    PlacementConstraintViolation = 9
}

/// <summary>
/// Información completa sobre una colocación candidata.
/// </summary>
public readonly struct RestaurantPlacementValidationResult
{
    public RestaurantPlacementValidationStatus Status { get; }

    public RestaurantAreaMember Member { get; }

    public RestaurantPlacementFootprint Footprint { get; }

    public RestaurantArea CandidateArea { get; }

    public RestaurantAreaCapabilityDefinition
        MissingCapability
    { get; }

    public RestaurantPlacementFootprint
        ConflictingFootprint
    { get; }

    public RestaurantPlacementObstacle
        ConflictingObstacle
    { get; }

    public RestaurantPlacementConflictType ConflictType { get; }

    public RestaurantPlacementConstraintEvaluation
        ConstraintEvaluation
    { get; }

    public string UserMessage =>
        ConstraintEvaluation.UserMessage;

    public string TechnicalMessage =>
        ConstraintEvaluation.TechnicalMessage;

    public Object RelatedObject =>
        ConstraintEvaluation.RelatedObject;

    public bool IsValid =>
        Status ==
        RestaurantPlacementValidationStatus.Valid;

    /// <summary>
    /// Constructor compatible con consumidores anteriores que
    /// solo conocían conflictos entre huellas colocables.
    /// </summary>
    public RestaurantPlacementValidationResult(
        RestaurantPlacementValidationStatus status,
        RestaurantAreaMember member,
        RestaurantPlacementFootprint footprint,
        RestaurantArea candidateArea,
        RestaurantAreaCapabilityDefinition
            missingCapability,
        RestaurantPlacementFootprint
            conflictingFootprint,
        RestaurantPlacementConflictType conflictType
    )
        : this(
            status,
            member,
            footprint,
            candidateArea,
            missingCapability,
            conflictingFootprint,
            null,
            conflictType
        )
    {
    }

    /// <summary>
    /// Constructor completo, compatible con huellas y obstáculos.
    /// </summary>
    public RestaurantPlacementValidationResult(
        RestaurantPlacementValidationStatus status,
        RestaurantAreaMember member,
        RestaurantPlacementFootprint footprint,
        RestaurantArea candidateArea,
        RestaurantAreaCapabilityDefinition
            missingCapability,
        RestaurantPlacementFootprint
            conflictingFootprint,
        RestaurantPlacementObstacle
            conflictingObstacle,
        RestaurantPlacementConflictType conflictType
    )
        : this(
            status,
            member,
            footprint,
            candidateArea,
            missingCapability,
            conflictingFootprint,
            conflictingObstacle,
            conflictType,
            RestaurantPlacementConstraintEvaluation.Valid()
        )
    {
    }

    /// <summary>
    /// Constructor completo con diagnóstico de regla especializada.
    /// </summary>
    public RestaurantPlacementValidationResult(
        RestaurantPlacementValidationStatus status,
        RestaurantAreaMember member,
        RestaurantPlacementFootprint footprint,
        RestaurantArea candidateArea,
        RestaurantAreaCapabilityDefinition
            missingCapability,
        RestaurantPlacementFootprint
            conflictingFootprint,
        RestaurantPlacementObstacle
            conflictingObstacle,
        RestaurantPlacementConflictType conflictType,
        RestaurantPlacementConstraintEvaluation
            constraintEvaluation
    )
    {
        Status = status;
        Member = member;
        Footprint = footprint;
        CandidateArea = candidateArea;
        MissingCapability = missingCapability;
        ConflictingFootprint = conflictingFootprint;
        ConflictingObstacle = conflictingObstacle;
        ConflictType = conflictType;
        ConstraintEvaluation = constraintEvaluation;
    }
}

/// <summary>
/// Resumen de una validación global de colocaciones.
/// </summary>
public readonly struct RestaurantPlacementValidationSummary
{
    public int TotalCount { get; }

    public int ValidCount { get; }

    public int AreaErrorCount { get; }

    public int PhysicalOverlapCount { get; }

    public int ClearanceViolationCount { get; }

    public int ConstraintViolationCount { get; }

    public int SystemErrorCount { get; }

    public bool IsValid =>
        TotalCount == ValidCount &&
        SystemErrorCount == 0;

    public RestaurantPlacementValidationSummary(
        int totalCount,
        int validCount,
        int areaErrorCount,
        int physicalOverlapCount,
        int clearanceViolationCount,
        int constraintViolationCount,
        int systemErrorCount
    )
    {
        TotalCount = totalCount;
        ValidCount = validCount;
        AreaErrorCount = areaErrorCount;
        PhysicalOverlapCount = physicalOverlapCount;
        ClearanceViolationCount =
            clearanceViolationCount;
        ConstraintViolationCount =
            constraintViolationCount;
        SystemErrorCount = systemErrorCount;
    }
}
