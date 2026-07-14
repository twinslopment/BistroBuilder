using System.Collections;
using UnityEngine;

/// <summary>
/// Resuelve y valida la pertenencia espacial y funcional
/// de los elementos del restaurante.
///
/// Responsabilidades:
/// - Averiguar qué área contiene una posición.
/// - Validar que el área asignada coincide con la posición real.
/// - Comprobar que el área ofrece las capacidades requeridas.
/// - Validar posiciones candidatas para el modo edición.
/// - Validar la huella completa de los objetos colocables.
/// - Actualizar la asignación cuando un objeto sea colocado.
///
/// No utiliza Update ni realiza comprobaciones continuas.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantAreaAssignmentService :
    MonoBehaviour
{
    private const int FootprintSamplePointCount = 5;

    [Header("Dependencias")]

    [Tooltip("Registro central de las áreas del restaurante.")]
    [SerializeField]
    private RestaurantAreaRegistry areaRegistry;

    [Tooltip(
        "Registro central de los elementos asociados a áreas."
    )]
    [SerializeField]
    private RestaurantAreaMemberRegistry memberRegistry;

    [Header("Validación inicial")]

    [Tooltip(
        "Comprueba una sola vez al iniciar la escena que los " +
        "miembros están correctamente situados y que sus áreas " +
        "ofrecen las capacidades necesarias."
    )]
    [SerializeField]
    private bool validateMembersOnStart = true;

    [Tooltip(
        "Cuando está activado, solo se consideran áreas operativas " +
        "al resolver posiciones."
    )]
    [SerializeField]
    private bool operationalAreasOnly;

    /// <summary>
    /// Array reutilizable para validar el centro y las cuatro
    /// esquinas de una huella sin generar basura de memoria.
    /// </summary>
    private readonly Vector3[] footprintSamplePoints =
        new Vector3[FootprintSamplePointCount];

    private Coroutine initialValidationRoutine;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void Start()
    {
        if (!validateMembersOnStart)
        {
            return;
        }

        initialValidationRoutine =
            StartCoroutine(
                ValidateMembersAfterStartupRoutine()
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
    /// Localiza el área que contiene una posición arbitraria.
    ///
    /// Este método permite comprobar una posición candidata
    /// antes de mover realmente un objeto.
    /// </summary>
    public bool TryResolveAreaAtPosition(
        Vector3 worldPosition,
        out RestaurantArea resolvedArea
    )
    {
        resolvedArea = null;

        if (areaRegistry == null)
        {
            return false;
        }

        return areaRegistry.TryFindAreaContainingPosition(
            worldPosition,
            out resolvedArea,
            operationalAreasOnly
        );
    }

    /// <summary>
    /// Localiza el área que contiene la posición de referencia
    /// actual de un miembro.
    /// </summary>
    public bool TryResolveArea(
        RestaurantAreaMember member,
        out RestaurantArea resolvedArea
    )
    {
        resolvedArea = null;

        if (member == null)
        {
            return false;
        }

        return TryResolveAreaAtPosition(
            member.ReferencePosition,
            out resolvedArea
        );
    }

    /// <summary>
    /// Comprueba si un miembro es funcionalmente compatible
    /// con un área.
    ///
    /// No comprueba la huella ni las colisiones físicas.
    /// </summary>
    public bool CanPlaceMemberInArea(
        RestaurantAreaMember member,
        RestaurantArea candidateArea,
        out RestaurantAreaCapabilityDefinition
            missingCapability
    )
    {
        missingCapability = null;

        if (member == null ||
            candidateArea == null ||
            candidateArea.Definition == null)
        {
            return false;
        }

        return member.AreRequirementsSatisfiedBy(
            candidateArea,
            out missingCapability
        );
    }

    /// <summary>
    /// Valida un único punto candidato.
    ///
    /// Se conserva para objetos sin huella o sistemas que ya
    /// proporcionan directamente una posición de referencia.
    /// </summary>
    public RestaurantAreaPlacementValidationStatus
        ValidateAreaCompatibilityAtPosition(
            RestaurantAreaMember member,
            Vector3 candidateWorldPosition,
            out RestaurantArea candidateArea,
            out RestaurantAreaCapabilityDefinition
                missingCapability
        )
    {
        candidateArea = null;
        missingCapability = null;

        if (member == null)
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .InvalidMember;
        }

        if (!TryResolveAreaAtPosition(
                candidateWorldPosition,
                out candidateArea
            ))
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .OutsideRegisteredAreas;
        }

        if (candidateArea.Definition == null)
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .MissingAreaDefinition;
        }

        if (!member.AreRequirementsSatisfiedBy(
                candidateArea,
                out missingCapability
            ))
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .MissingRequiredCapability;
        }

        return
            RestaurantAreaPlacementValidationStatus.Valid;
    }

    /// <summary>
    /// Valida la posición y rotación candidatas completas
    /// de un objeto.
    ///
    /// Cuando el objeto tiene RestaurantPlacementFootprint,
    /// comprueba el centro y las cuatro esquinas.
    ///
    /// Cuando no tiene huella, utiliza su posición de referencia.
    /// </summary>
    public RestaurantAreaPlacementValidationStatus
        ValidatePlacementAtPose(
            RestaurantAreaMember member,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation,
            out RestaurantArea candidateArea,
            out RestaurantAreaCapabilityDefinition
                missingCapability
        )
    {
        candidateArea = null;
        missingCapability = null;

        if (member == null)
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .InvalidMember;
        }

        if (member.TryGetComponent(
                out RestaurantPlacementFootprint footprint
            ))
        {
            return ValidateFootprintAtPose(
                member,
                footprint,
                candidateRootPosition,
                candidateRootRotation,
                out candidateArea,
                out missingCapability
            );
        }

        Vector3 candidateReferencePosition =
            CalculateCandidateReferencePosition(
                member,
                candidateRootPosition,
                candidateRootRotation
            );

        return ValidateAreaCompatibilityAtPosition(
            member,
            candidateReferencePosition,
            out candidateArea,
            out missingCapability
        );
    }

    /// <summary>
    /// Versión simplificada para comprobar un único punto.
    /// </summary>
    public bool CanPlaceMemberAtPosition(
        RestaurantAreaMember member,
        Vector3 candidateWorldPosition,
        out RestaurantArea candidateArea,
        out RestaurantAreaCapabilityDefinition
            missingCapability
    )
    {
        RestaurantAreaPlacementValidationStatus status =
            ValidateAreaCompatibilityAtPosition(
                member,
                candidateWorldPosition,
                out candidateArea,
                out missingCapability
            );

        return status ==
               RestaurantAreaPlacementValidationStatus.Valid;
    }

    /// <summary>
    /// Versión que valida la pose completa y la huella,
    /// pensada para el modo edición.
    /// </summary>
    public bool CanPlaceMemberAtPose(
        RestaurantAreaMember member,
        Vector3 candidateRootPosition,
        Quaternion candidateRootRotation,
        out RestaurantArea candidateArea,
        out RestaurantAreaCapabilityDefinition
            missingCapability
    )
    {
        RestaurantAreaPlacementValidationStatus status =
            ValidatePlacementAtPose(
                member,
                candidateRootPosition,
                candidateRootRotation,
                out candidateArea,
                out missingCapability
            );

        return status ==
               RestaurantAreaPlacementValidationStatus.Valid;
    }

    /// <summary>
    /// Calcula el área correspondiente a la posición actual
    /// y actualiza la asignación del miembro.
    ///
    /// Cuando requireCompatibleArea está activado, también
    /// comprueba capacidades y huella completa.
    /// </summary>
    public bool TryAssignResolvedArea(
        RestaurantAreaMember member,
        bool clearAreaWhenOutside = false,
        bool requireCompatibleArea = true
    )
    {
        if (member == null)
        {
            return false;
        }

        RestaurantArea resolvedArea;

        if (!requireCompatibleArea)
        {
            if (!TryResolveArea(
                    member,
                    out resolvedArea
                ))
            {
                if (clearAreaWhenOutside)
                {
                    member.ClearArea();
                }

                return false;
            }

            member.SetArea(resolvedArea);
            return true;
        }

        RestaurantAreaPlacementValidationStatus status =
            ValidatePlacementAtPose(
                member,
                member.transform.position,
                member.transform.rotation,
                out resolvedArea,
                out _
            );

        if (status !=
            RestaurantAreaPlacementValidationStatus.Valid)
        {
            if (clearAreaWhenOutside &&
                status ==
                RestaurantAreaPlacementValidationStatus
                    .OutsideRegisteredAreas)
            {
                member.ClearArea();
            }

            return false;
        }

        member.SetArea(resolvedArea);
        return true;
    }

    /// <summary>
    /// Valida completamente un miembro ya colocado.
    ///
    /// Incluye:
    /// - Área asignada.
    /// - Área física bajo el objeto.
    /// - Coincidencia entre ambas.
    /// - Definición de área.
    /// - Capacidades requeridas.
    /// - Huella completa dentro de la misma área.
    /// </summary>
    public RestaurantAreaMemberValidationStatus ValidateMember(
        RestaurantAreaMember member,
        out RestaurantArea resolvedArea,
        out RestaurantAreaCapabilityDefinition
            missingCapability
    )
    {
        resolvedArea = null;
        missingCapability = null;

        if (member == null)
        {
            return
                RestaurantAreaMemberValidationStatus.InvalidMember;
        }

        if (member.AssignedArea == null)
        {
            return
                RestaurantAreaMemberValidationStatus
                    .MissingAssignedArea;
        }

        RestaurantAreaPlacementValidationStatus
            placementStatus =
                ValidatePlacementAtPose(
                    member,
                    member.transform.position,
                    member.transform.rotation,
                    out resolvedArea,
                    out missingCapability
                );

        if (placementStatus ==
            RestaurantAreaPlacementValidationStatus
                .OutsideRegisteredAreas)
        {
            return
                RestaurantAreaMemberValidationStatus
                    .OutsideRegisteredAreas;
        }

        if (placementStatus ==
            RestaurantAreaPlacementValidationStatus
                .InvalidMember)
        {
            return
                RestaurantAreaMemberValidationStatus
                    .InvalidMember;
        }

        if (!ReferenceEquals(
                member.AssignedArea,
                resolvedArea
            ))
        {
            return
                RestaurantAreaMemberValidationStatus
                    .AssignedAreaMismatch;
        }

        switch (placementStatus)
        {
            case RestaurantAreaPlacementValidationStatus.Valid:
                return
                    RestaurantAreaMemberValidationStatus.Valid;

            case RestaurantAreaPlacementValidationStatus
                .MissingAreaDefinition:

                return
                    RestaurantAreaMemberValidationStatus
                        .MissingAreaDefinition;

            case RestaurantAreaPlacementValidationStatus
                .MissingRequiredCapability:

                return
                    RestaurantAreaMemberValidationStatus
                        .MissingRequiredCapability;

            case RestaurantAreaPlacementValidationStatus
                .FootprintOutsideCandidateArea:

                return
                    RestaurantAreaMemberValidationStatus
                        .FootprintOutsideAssignedArea;

            default:
                return
                    RestaurantAreaMemberValidationStatus
                        .InvalidMember;
        }
    }

    /// <summary>
    /// Indica si la asignación actual de un miembro es válida.
    /// </summary>
    public bool IsAssignmentValid(
        RestaurantAreaMember member,
        out RestaurantArea resolvedArea
    )
    {
        RestaurantAreaMemberValidationStatus status =
            ValidateMember(
                member,
                out resolvedArea,
                out _
            );

        return status ==
               RestaurantAreaMemberValidationStatus.Valid;
    }

    /// <summary>
    /// Valida todos los miembros registrados y devuelve
    /// un resumen completo.
    /// </summary>
    public RestaurantAreaValidationResult
        ValidateAllRegisteredMembers(
            bool logDetails = true
        )
    {
        if (memberRegistry == null)
        {
            return default;
        }

        int totalCount = 0;
        int validCount = 0;
        int unassignedCount = 0;
        int outsideAreaCount = 0;
        int mismatchedCount = 0;
        int missingDefinitionCount = 0;
        int incompatibleCapabilityCount = 0;
        int invalidFootprintCount = 0;

        foreach (RestaurantAreaMember member
                 in memberRegistry.RegisteredMembers)
        {
            if (member == null)
            {
                continue;
            }

            totalCount++;

            RestaurantAreaMemberValidationStatus status =
                ValidateMember(
                    member,
                    out RestaurantArea resolvedArea,
                    out RestaurantAreaCapabilityDefinition
                        missingCapability
                );

            switch (status)
            {
                case RestaurantAreaMemberValidationStatus.Valid:
                    validCount++;
                    break;

                case RestaurantAreaMemberValidationStatus
                    .MissingAssignedArea:

                    unassignedCount++;

                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"{member.name} no tiene un área asignada.",
                            member
                        );
                    }

                    break;

                case RestaurantAreaMemberValidationStatus
                    .OutsideRegisteredAreas:

                    outsideAreaCount++;

                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"{member.name} está fuera de todas las " +
                            $"áreas registradas. Área asignada: " +
                            $"'{member.AssignedArea.AreaId}'.",
                            member
                        );
                    }

                    break;

                case RestaurantAreaMemberValidationStatus
                    .AssignedAreaMismatch:

                    mismatchedCount++;

                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"{member.name} tiene asignada el área " +
                            $"'{member.AssignedArea.AreaId}', pero su " +
                            $"posición pertenece a " +
                            $"'{resolvedArea.AreaId}'.",
                            member
                        );
                    }

                    break;

                case RestaurantAreaMemberValidationStatus
                    .MissingAreaDefinition:

                    missingDefinitionCount++;

                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"El área '{member.AssignedArea.AreaId}' " +
                            $"asignada a {member.name} no tiene una " +
                            $"definición configurada.",
                            member.AssignedArea
                        );
                    }

                    break;

                case RestaurantAreaMemberValidationStatus
                    .MissingRequiredCapability:

                    incompatibleCapabilityCount++;

                    if (logDetails)
                    {
                        string capabilityName =
                            GetCapabilityDisplayName(
                                missingCapability
                            );

                        Debug.LogWarning(
                            $"{member.name} no puede utilizar el área " +
                            $"'{member.AssignedArea.AreaId}'. " +
                            $"Falta la capacidad requerida " +
                            $"'{capabilityName}'.",
                            member
                        );
                    }

                    break;

                case RestaurantAreaMemberValidationStatus
                    .FootprintOutsideAssignedArea:

                    invalidFootprintCount++;

                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"{member.name} tiene parte de su huella " +
                            $"fuera del área asignada " +
                            $"'{member.AssignedArea.AreaId}'.",
                            member
                        );
                    }

                    break;
            }
        }

        RestaurantAreaValidationResult result =
            new RestaurantAreaValidationResult(
                totalCount,
                validCount,
                unassignedCount,
                outsideAreaCount,
                mismatchedCount,
                missingDefinitionCount,
                incompatibleCapabilityCount,
                invalidFootprintCount
            );

        Debug.Log(
            $"Validación espacial y funcional completada. " +
            $"Total: {result.TotalCount}, " +
            $"correctos: {result.ValidCount}, " +
            $"sin asignar: {result.UnassignedCount}, " +
            $"fuera de áreas: {result.OutsideAreaCount}, " +
            $"asignación incorrecta: " +
            $"{result.MismatchedCount}, " +
            $"sin definición: " +
            $"{result.MissingDefinitionCount}, " +
            $"capacidad incompatible: " +
            $"{result.IncompatibleCapabilityCount}, " +
            $"huella fuera del área: " +
            $"{result.InvalidFootprintCount}.",
            this
        );

        return result;
    }

    /// <summary>
    /// Valida el centro y las cuatro esquinas de una huella.
    /// </summary>
    private RestaurantAreaPlacementValidationStatus
        ValidateFootprintAtPose(
            RestaurantAreaMember member,
            RestaurantPlacementFootprint footprint,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation,
            out RestaurantArea candidateArea,
            out RestaurantAreaCapabilityDefinition
                missingCapability
        )
    {
        candidateArea = null;
        missingCapability = null;

        int writtenPointCount =
            footprint.WriteWorldSamplePoints(
                candidateRootPosition,
                candidateRootRotation,
                footprintSamplePoints
            );

        /*
         * El centro de la huella determina el área candidata.
         */
        if (!TryResolveAreaAtPosition(
                footprintSamplePoints[0],
                out candidateArea
            ))
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .OutsideRegisteredAreas;
        }

        if (candidateArea.Definition == null)
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .MissingAreaDefinition;
        }

        if (!member.AreRequirementsSatisfiedBy(
                candidateArea,
                out missingCapability
            ))
        {
            return
                RestaurantAreaPlacementValidationStatus
                    .MissingRequiredCapability;
        }

        /*
         * Todas las esquinas deben permanecer dentro del área
         * determinada por el centro.
         *
         * Se consulta directamente candidateArea para evitar que
         * dos áreas adyacentes o solapadas produzcan resultados
         * ambiguos.
         */
        for (int index = 1;
             index < writtenPointCount;
             index++)
        {
            if (!candidateArea.ContainsPosition(
                    footprintSamplePoints[index]
                ))
            {
                return
                    RestaurantAreaPlacementValidationStatus
                        .FootprintOutsideCandidateArea;
            }
        }

        return
            RestaurantAreaPlacementValidationStatus.Valid;
    }

    /// <summary>
    /// Calcula dónde estaría el punto de referencia de un miembro
    /// al aplicar una posición y rotación candidatas a su raíz.
    /// </summary>
    private static Vector3
        CalculateCandidateReferencePosition(
            RestaurantAreaMember member,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation
        )
    {
        Transform memberTransform =
            member.transform;

        Transform referenceTransform =
            member.PositionReference;

        if (referenceTransform == null ||
            ReferenceEquals(
                referenceTransform,
                memberTransform
            ))
        {
            return candidateRootPosition;
        }

        Vector3 localReferencePosition =
            memberTransform.InverseTransformPoint(
                referenceTransform.position
            );

        Matrix4x4 candidateMatrix =
            Matrix4x4.TRS(
                candidateRootPosition,
                candidateRootRotation,
                memberTransform.lossyScale
            );

        return candidateMatrix.MultiplyPoint3x4(
            localReferencePosition
        );
    }

    private IEnumerator
        ValidateMembersAfterStartupRoutine()
    {
        yield return null;

        initialValidationRoutine = null;

        ValidateAllRegisteredMembers();
    }

    private static string GetCapabilityDisplayName(
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

    private void CacheDependenciesIfNeeded()
    {
        if (areaRegistry == null)
        {
            TryGetComponent(out areaRegistry);
        }

        if (memberRegistry == null)
        {
            TryGetComponent(out memberRegistry);
        }
    }

    private void ValidateDependencies()
    {
        if (areaRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantAreaAssignmentService)} " +
                $"necesita un {nameof(RestaurantAreaRegistry)}.",
                this
            );
        }

        if (memberRegistry == null)
        {
            Debug.LogError(
                $"{nameof(RestaurantAreaAssignmentService)} " +
                $"necesita un " +
                $"{nameof(RestaurantAreaMemberRegistry)}.",
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
/// Resultado al validar un miembro ya colocado.
/// </summary>
public enum RestaurantAreaMemberValidationStatus
{
    Valid = 0,
    InvalidMember = 1,
    MissingAssignedArea = 2,
    OutsideRegisteredAreas = 3,
    AssignedAreaMismatch = 4,
    MissingAreaDefinition = 5,
    MissingRequiredCapability = 6,
    FootprintOutsideAssignedArea = 7
}

/// <summary>
/// Resultado al validar una posición o pose candidata.
/// </summary>
public enum RestaurantAreaPlacementValidationStatus
{
    Valid = 0,
    InvalidMember = 1,
    OutsideRegisteredAreas = 2,
    MissingAreaDefinition = 3,
    MissingRequiredCapability = 4,
    FootprintOutsideCandidateArea = 5
}

/// <summary>
/// Resultado inmutable de una validación completa.
/// </summary>
public readonly struct RestaurantAreaValidationResult
{
    public int TotalCount { get; }
    public int ValidCount { get; }
    public int UnassignedCount { get; }
    public int OutsideAreaCount { get; }
    public int MismatchedCount { get; }
    public int MissingDefinitionCount { get; }
    public int IncompatibleCapabilityCount { get; }
    public int InvalidFootprintCount { get; }

    public bool IsValid =>
        TotalCount == ValidCount;

    public RestaurantAreaValidationResult(
        int totalCount,
        int validCount,
        int unassignedCount,
        int outsideAreaCount,
        int mismatchedCount,
        int missingDefinitionCount,
        int incompatibleCapabilityCount,
        int invalidFootprintCount
    )
    {
        TotalCount = totalCount;
        ValidCount = validCount;
        UnassignedCount = unassignedCount;
        OutsideAreaCount = outsideAreaCount;
        MismatchedCount = mismatchedCount;
        MissingDefinitionCount = missingDefinitionCount;
        IncompatibleCapabilityCount =
            incompatibleCapabilityCount;
        InvalidFootprintCount =
            invalidFootprintCount;
    }
}