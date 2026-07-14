using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Controla la interacción del jugador durante el modo edición.
///
/// Responsabilidades:
/// - Leer teclado y ratón mediante el nuevo Input System.
/// - Activar y cerrar el modo edición.
/// - Seleccionar objetos colocables.
/// - Mover y rotar el objeto seleccionado.
/// - Solicitar la validación de cada pose candidata.
/// - Confirmar o cancelar la colocación.
///
/// Las reglas de negocio permanecen en los servicios de
/// aplicación. Este componente pertenece a presentación.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Edit Interaction Controller"
)]
public sealed class RestaurantEditInteractionController :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio que controla la entrada y salida del modo edición."
    )]
    [SerializeField]
    private RestaurantEditModeService editModeService;

    [Tooltip(
        "Servicio que controla las transacciones de colocación."
    )]
    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [Tooltip(
        "Cámara utilizada para seleccionar y colocar objetos."
    )]
    [SerializeField]
    private Camera interactionCamera;

    [Header("Capas")]

    [Tooltip(
        "Capas que pueden contener objetos seleccionables."
    )]
    [SerializeField]
    private LayerMask selectableLayerMask = ~0;

    [Tooltip(
        "Capas que pueden actuar como superficie de colocación."
    )]
    [SerializeField]
    private LayerMask placementSurfaceLayerMask = ~0;

    [Header("Raycast")]

    [Tooltip(
        "Distancia máxima de selección y colocación."
    )]
    [SerializeField]
    [Min(1f)]
    private float maximumRayDistance = 500f;

    [Header("Movimiento")]

    [Tooltip(
        "Ajusta la posición del objeto a una cuadrícula."
    )]
    [SerializeField]
    private bool useGridSnapping = true;

    [Tooltip(
        "Tamaño de la celda de la cuadrícula."
    )]
    [SerializeField]
    [Min(0.01f)]
    private float gridSize = 0.25f;

    [Tooltip(
        "Mantiene la altura mundial original del objeto."
    )]
    [SerializeField]
    private bool preserveOriginalWorldHeight = true;

    [Header("Rotación")]

    [Tooltip(
        "Ángulo aplicado en cada rotación."
    )]
    [SerializeField]
    [Range(1f, 180f)]
    private float rotationStepDegrees = 90f;

    [Header("Controles")]

    [Tooltip(
        "Tecla para activar o cerrar el modo edición."
    )]
    [SerializeField]
    private Key toggleEditModeKey = Key.F2;

    [Tooltip(
        "Tecla para rotar el objeto seleccionado."
    )]
    [SerializeField]
    private Key rotateClockwiseKey = Key.R;

    [Tooltip(
        "Tecla para cancelar una colocación o cerrar el modo."
    )]
    [SerializeField]
    private Key cancelKey = Key.Escape;

    [Tooltip(
        "Botón principal del ratón. " +
        "0: izquierdo, 1: derecho, 2: central."
    )]
    [SerializeField]
    [Range(0, 2)]
    private int primaryMouseButton = 0;

    [Tooltip(
        "Botón de cancelación. " +
        "0: izquierdo, 1: derecho, 2: central."
    )]
    [SerializeField]
    [Range(0, 2)]
    private int cancelMouseButton = 1;

    [Header("Depuración")]

    [Tooltip(
        "Escribe las acciones principales en la Console."
    )]
    [SerializeField]
    private bool logInteractionEvents = true;

    private const int RaycastBufferSize = 32;

    private readonly RaycastHit[] selectionHitBuffer =
        new RaycastHit[RaycastBufferSize];

    private readonly RaycastHit[] surfaceHitBuffer =
        new RaycastHit[RaycastBufferSize];

    private RestaurantAreaMember activeMember;

    private Vector3 grabOffset;

    private Vector3 candidatePosition;

    private Quaternion candidateRotation =
        Quaternion.identity;

    private float originalWorldHeight;

    private bool hasCandidatePose;

    private bool hasPublishedPreviewPose;

    private Vector3 lastPublishedPosition;

    private Quaternion lastPublishedRotation =
        Quaternion.identity;

    private RestaurantPlacementValidationResult
        lastValidationResult;

    /// <summary>
    /// Se ejecuta cuando cambia el objeto editado.
    /// </summary>
    public event Action<RestaurantAreaMember>
        ActiveMemberChanged;

    /// <summary>
    /// Se ejecuta cuando cambia el resultado de la validación.
    /// </summary>
    public event Action<
        RestaurantPlacementValidationResult
    > PlacementValidationChanged;

    /// <summary>
    /// Se ejecuta cuando cambia el mensaje informativo.
    /// </summary>
    public event Action<string>
        InteractionMessageChanged;

    public RestaurantAreaMember ActiveMember
    {
        get
        {
            return activeMember;
        }
    }

    public bool HasActivePlacement
    {
        get
        {
            return transactionService != null &&
                   transactionService.HasActiveTransaction;
        }
    }

    public RestaurantPlacementValidationResult
        LastValidationResult
    {
        get
        {
            return lastValidationResult;
        }
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnDisable()
    {
        CancelActivePlacementIfNeeded();
        ClearLocalPlacementState();
    }

    private void Update()
    {
        if (!DependenciesAreAvailable())
        {
            return;
        }

        /*
         * Sin teclado no se pueden procesar atajos, pero el
         * componente permanece seguro para dispositivos sin él.
         */
        HandleEditModeToggle();

        if (!editModeService.IsEditModeActive)
        {
            return;
        }

        if (transactionService.HasActiveTransaction)
        {
            HandleActivePlacement();
        }
        else
        {
            HandleEditModeWithoutPlacement();
        }
    }

    /// <summary>
    /// Activa el modo edición.
    /// </summary>
    public bool TryEnterEditMode()
    {
        if (editModeService == null)
        {
            PublishMessage(
                "El servicio de modo edición no está disponible."
            );

            return false;
        }

        RestaurantEditModeFailureReason failureReason;
        string rejectionMessage;

        bool entered =
            editModeService.TryEnterEditMode(
                out failureReason,
                out rejectionMessage
            );

        if (!entered)
        {
            string message;

            if (string.IsNullOrWhiteSpace(rejectionMessage))
            {
                message =
                    "No se pudo activar el modo edición. Motivo: " +
                    failureReason +
                    ".";
            }
            else
            {
                message =
                    rejectionMessage;
            }

            PublishMessage(message);

            return false;
        }

        PublishMessage(
            "Modo edición activado."
        );

        LogEvent(
            "Modo edición activado."
        );

        return true;
    }

    /// <summary>
    /// Cierra el modo edición.
    /// </summary>
    public bool TryExitEditMode(
        bool cancelActivePlacement
    )
    {
        if (editModeService == null)
        {
            PublishMessage(
                "El servicio de modo edición no está disponible."
            );

            return false;
        }

        RestaurantEditModeFailureReason failureReason;

        bool exited =
            editModeService.TryExitEditMode(
                cancelActivePlacement,
                out failureReason
            );

        if (!exited)
        {
            PublishMessage(
                "No se pudo cerrar el modo edición. Motivo: " +
                failureReason +
                "."
            );

            return false;
        }

        ClearLocalPlacementState();

        PublishMessage(
            "Modo edición cerrado."
        );

        LogEvent(
            "Modo edición cerrado."
        );

        return true;
    }

    /// <summary>
    /// Cancela la colocación activa y restaura el objeto.
    /// </summary>
    public bool CancelActivePlacement()
    {
        if (transactionService == null ||
            !transactionService.HasActiveTransaction)
        {
            return false;
        }

        RestaurantAreaMember cancelledMember =
            activeMember;

        bool cancelled =
            transactionService.CancelPlacement();

        if (!cancelled)
        {
            PublishMessage(
                "No se pudo cancelar la colocación."
            );

            return false;
        }

        ClearLocalPlacementState();

        PublishMessage(
            "Colocación cancelada."
        );

        if (cancelledMember != null)
        {
            LogEvent(
                "Colocación cancelada para " +
                cancelledMember.name +
                "."
            );
        }

        return true;
    }

    /// <summary>
    /// Procesa el atajo que activa o desactiva el modo edición.
    /// </summary>
    private void HandleEditModeToggle()
    {
        if (!WasKeyPressedThisFrame(
                toggleEditModeKey
            ))
        {
            return;
        }

        if (!editModeService.IsEditModeActive)
        {
            TryEnterEditMode();
            return;
        }

        TryExitEditMode(
            true
        );
    }

    /// <summary>
    /// Procesa la interacción cuando el modo edición está activo,
    /// pero no se está moviendo ningún objeto.
    /// </summary>
    private void HandleEditModeWithoutPlacement()
    {
        if (WasKeyPressedThisFrame(cancelKey))
        {
            TryExitEditMode(
                false
            );

            return;
        }

        if (IsPointerOverUserInterface())
        {
            return;
        }

        if (!WasMouseButtonPressedThisFrame(
                primaryMouseButton
            ))
        {
            return;
        }

        RestaurantAreaMember member;
        Vector3 selectedWorldPoint;

        bool foundMember =
            TryFindSelectableMemberUnderPointer(
                out member,
                out selectedWorldPoint
            );

        if (!foundMember)
        {
            PublishMessage(
                "No hay ningún objeto editable bajo el cursor."
            );

            return;
        }

        BeginPlacement(
            member,
            selectedWorldPoint
        );
    }

    /// <summary>
    /// Procesa la interacción mientras existe una colocación
    /// transaccional activa.
    /// </summary>
    private void HandleActivePlacement()
    {
        bool keyboardCancellation =
            WasKeyPressedThisFrame(cancelKey);

        bool mouseCancellation =
            WasMouseButtonPressedThisFrame(
                cancelMouseButton
            );

        if (keyboardCancellation ||
            mouseCancellation)
        {
            CancelActivePlacement();
            return;
        }

        if (WasKeyPressedThisFrame(
                rotateClockwiseKey
            ))
        {
            RotateCandidateClockwise();
        }

        if (IsPointerOverUserInterface())
        {
            return;
        }

        UpdateCandidatePositionFromPointer();
        PublishPreviewIfChanged();

        if (WasMouseButtonPressedThisFrame(
                primaryMouseButton
            ))
        {
            CommitActivePlacement();
        }
    }

    /// <summary>
    /// Inicia una operación para el miembro seleccionado.
    /// </summary>
    private bool BeginPlacement(
        RestaurantAreaMember member,
        Vector3 selectedWorldPoint
    )
    {
        if (member == null)
        {
            return false;
        }

        RestaurantPlacementFootprint footprint;

        bool hasFootprint =
            member.TryGetComponent(
                out footprint
            );

        if (!hasFootprint)
        {
            PublishMessage(
                member.name +
                " no tiene una huella de colocación."
            );

            return false;
        }

        RestaurantPlacementTransactionFailureReason
            failureReason;

        bool began =
            transactionService.TryBeginPlacement(
                member,
                out failureReason
            );

        if (!began)
        {
            PublishMessage(
                "No se pudo iniciar la colocación. Motivo: " +
                failureReason +
                "."
            );

            return false;
        }

        activeMember =
            member;

        Vector3 memberPosition =
            member.transform.position;

        grabOffset =
            memberPosition -
            selectedWorldPoint;

        /*
         * La altura se controla por separado.
         * El desplazamiento de agarre solo afecta al plano XZ.
         */
        grabOffset.y = 0f;

        originalWorldHeight =
            memberPosition.y;

        candidatePosition =
            memberPosition;

        candidateRotation =
            member.transform.rotation;

        hasCandidatePose = true;
        hasPublishedPreviewPose = false;

        lastValidationResult =
            transactionService.LastValidationResult;

        ActiveMemberChanged?.Invoke(
            member
        );

        PlacementValidationChanged?.Invoke(
            lastValidationResult
        );

        PublishMessage(
            "Moviendo " +
            member.name +
            "."
        );

        LogEvent(
            "Colocación iniciada para " +
            member.name +
            "."
        );

        return true;
    }

    /// <summary>
    /// Rota la pose candidata en torno al eje vertical.
    /// </summary>
    private void RotateCandidateClockwise()
    {
        if (!hasCandidatePose ||
            activeMember == null)
        {
            return;
        }

        Quaternion yawRotation =
            Quaternion.AngleAxis(
                rotationStepDegrees,
                Vector3.up
            );

        candidateRotation =
            yawRotation *
            candidateRotation;

        hasPublishedPreviewPose = false;
    }

    /// <summary>
    /// Actualiza la posición candidata desde la posición actual
    /// del puntero sobre la superficie de colocación.
    /// </summary>
    private void UpdateCandidatePositionFromPointer()
    {
        Vector3 surfacePoint;

        if (!TryGetPlacementSurfacePoint(
                out surfacePoint
            ))
        {
            return;
        }

        Vector3 nextPosition =
            surfacePoint +
            grabOffset;

        if (preserveOriginalWorldHeight)
        {
            nextPosition.y =
                originalWorldHeight;
        }

        if (useGridSnapping)
        {
            nextPosition.x =
                SnapValue(
                    nextPosition.x,
                    gridSize
                );

            nextPosition.z =
                SnapValue(
                    nextPosition.z,
                    gridSize
                );
        }

        candidatePosition =
            nextPosition;

        hasCandidatePose = true;
    }

    /// <summary>
    /// Publica una previsualización únicamente cuando la pose
    /// candidata ha cambiado.
    /// </summary>
    private void PublishPreviewIfChanged()
    {
        if (!hasCandidatePose ||
            activeMember == null)
        {
            return;
        }

        if (hasPublishedPreviewPose &&
            ArePositionsEquivalent(
                candidatePosition,
                lastPublishedPosition
            ) &&
            AreRotationsEquivalent(
                candidateRotation,
                lastPublishedRotation
            ))
        {
            return;
        }

        RestaurantPlacementValidationResult result;

        RestaurantPlacementTransactionFailureReason
            failureReason;

        bool previewed =
            transactionService.TryPreviewPlacement(
                candidatePosition,
                candidateRotation,
                out result,
                out failureReason
            );

        if (!previewed)
        {
            PublishMessage(
                "No se pudo actualizar la previsualización. " +
                "Motivo: " +
                failureReason +
                "."
            );

            return;
        }

        lastPublishedPosition =
            candidatePosition;

        lastPublishedRotation =
            candidateRotation;

        hasPublishedPreviewPose = true;

        lastValidationResult =
            result;

        PlacementValidationChanged?.Invoke(
            result
        );
    }

    /// <summary>
    /// Solicita la confirmación de la pose actual.
    /// </summary>
    private void CommitActivePlacement()
    {
        if (activeMember == null)
        {
            return;
        }

        RestaurantAreaMember memberBeingCommitted =
            activeMember;

        RestaurantPlacementValidationResult result;

        RestaurantPlacementTransactionFailureReason
            failureReason;

        bool committed =
            transactionService.TryCommitPlacement(
                out result,
                out failureReason
            );

        lastValidationResult =
            result;

        PlacementValidationChanged?.Invoke(
            result
        );

        if (!committed)
        {
            PublishMessage(
                BuildInvalidPlacementMessage(
                    result,
                    failureReason
                )
            );

            LogEvent(
                "Confirmación rechazada para " +
                memberBeingCommitted.name +
                ". Estado: " +
                result.Status +
                "."
            );

            return;
        }

        ClearLocalPlacementState();

        PublishMessage(
            memberBeingCommitted.name +
            " colocado correctamente."
        );

        LogEvent(
            "Colocación confirmada para " +
            memberBeingCommitted.name +
            "."
        );
    }

    /// <summary>
    /// Busca el miembro colocable más cercano bajo el puntero.
    /// </summary>
    private bool TryFindSelectableMemberUnderPointer(
        out RestaurantAreaMember member,
        out Vector3 selectedWorldPoint
    )
    {
        member = null;
        selectedWorldPoint = default;

        if (!TryBuildPointerRay(out Ray ray))
        {
            return false;
        }

        int hitCount =
            Physics.RaycastNonAlloc(
                ray,
                selectionHitBuffer,
                maximumRayDistance,
                selectableLayerMask,
                QueryTriggerInteraction.Ignore
            );

        float nearestDistance =
            float.PositiveInfinity;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit hit =
                selectionHitBuffer[index];

            if (hit.collider == null ||
                hit.distance >= nearestDistance)
            {
                continue;
            }

            RestaurantAreaMember candidateMember =
                hit.collider.GetComponentInParent<
                    RestaurantAreaMember
                >();

            if (candidateMember == null)
            {
                continue;
            }

            RestaurantPlacementFootprint footprint;

            if (!candidateMember.TryGetComponent(
                    out footprint
                ))
            {
                continue;
            }

            member =
                candidateMember;

            selectedWorldPoint =
                hit.point;

            nearestDistance =
                hit.distance;
        }

        return member != null;
    }

    /// <summary>
    /// Obtiene el punto más cercano de una superficie válida bajo
    /// el puntero.
    /// </summary>
    private bool TryGetPlacementSurfacePoint(
        out Vector3 surfacePoint
    )
    {
        surfacePoint = default;

        if (!TryBuildPointerRay(out Ray ray))
        {
            return false;
        }

        int hitCount =
            Physics.RaycastNonAlloc(
                ray,
                surfaceHitBuffer,
                maximumRayDistance,
                placementSurfaceLayerMask,
                QueryTriggerInteraction.Ignore
            );

        float nearestDistance =
            float.PositiveInfinity;

        bool foundSurface = false;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit hit =
                surfaceHitBuffer[index];

            if (hit.collider == null ||
                hit.distance >= nearestDistance)
            {
                continue;
            }

            if (IsColliderPartOfActiveMember(
                    hit.collider
                ))
            {
                continue;
            }

            nearestDistance =
                hit.distance;

            surfacePoint =
                hit.point;

            foundSurface = true;
        }

        return foundSurface;
    }

    /// <summary>
    /// Crea un rayo desde la cámara utilizando la posición del
    /// ratón obtenida del nuevo Input System.
    /// </summary>
    private bool TryBuildPointerRay(
        out Ray ray
    )
    {
        ray = default;

        if (interactionCamera == null)
        {
            return false;
        }

        Mouse mouse =
            Mouse.current;

        if (mouse == null)
        {
            return false;
        }

        Vector2 pointerPosition =
            mouse.position.ReadValue();

        Vector3 screenPosition =
            new Vector3(
                pointerPosition.x,
                pointerPosition.y,
                0f
            );

        ray =
            interactionCamera.ScreenPointToRay(
                screenPosition
            );

        return true;
    }

    /// <summary>
    /// Evita interpretar como suelo un collider perteneciente al
    /// objeto que se está moviendo.
    /// </summary>
    private bool IsColliderPartOfActiveMember(
        Collider candidateCollider
    )
    {
        if (candidateCollider == null ||
            activeMember == null)
        {
            return false;
        }

        Transform colliderTransform =
            candidateCollider.transform;

        return colliderTransform ==
                   activeMember.transform ||
               colliderTransform.IsChildOf(
                   activeMember.transform
               );
    }

    /// <summary>
    /// Lee una tecla mediante Keyboard.current.
    /// </summary>
    private static bool WasKeyPressedThisFrame(
        Key key
    )
    {
        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null ||
            key == Key.None)
        {
            return false;
        }

        return keyboard[key].wasPressedThisFrame;
    }

    /// <summary>
    /// Lee uno de los tres botones principales del ratón mediante
    /// Mouse.current.
    /// </summary>
    private static bool WasMouseButtonPressedThisFrame(
        int buttonIndex
    )
    {
        Mouse mouse =
            Mouse.current;

        if (mouse == null)
        {
            return false;
        }

        switch (buttonIndex)
        {
            case 0:
                return
                    mouse.leftButton.wasPressedThisFrame;

            case 1:
                return
                    mouse.rightButton.wasPressedThisFrame;

            case 2:
                return
                    mouse.middleButton.wasPressedThisFrame;

            default:
                return false;
        }
    }

    private void CancelActivePlacementIfNeeded()
    {
        if (transactionService == null ||
            !transactionService.HasActiveTransaction)
        {
            return;
        }

        transactionService.CancelPlacement();
    }

    private void ClearLocalPlacementState()
    {
        activeMember = null;

        grabOffset = Vector3.zero;
        candidatePosition = Vector3.zero;
        candidateRotation = Quaternion.identity;

        originalWorldHeight = 0f;

        hasCandidatePose = false;
        hasPublishedPreviewPose = false;

        lastPublishedPosition = Vector3.zero;
        lastPublishedRotation = Quaternion.identity;

        lastValidationResult = default;

        ActiveMemberChanged?.Invoke(
            null
        );
    }

    private string BuildInvalidPlacementMessage(
        RestaurantPlacementValidationResult result,
        RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        switch (result.Status)
        {
            case RestaurantPlacementValidationStatus
                .OutsideRegisteredAreas:

                return
                    "No se puede colocar el objeto fuera de " +
                    "las áreas registradas.";

            case RestaurantPlacementValidationStatus
                .MissingAreaDefinition:

                return
                    "El área seleccionada no tiene una " +
                    "definición válida.";

            case RestaurantPlacementValidationStatus
                .MissingRequiredCapability:

                return
                    "El área no admite este tipo de objeto.";

            case RestaurantPlacementValidationStatus
                .FootprintOutsideCandidateArea:

                return
                    "Parte del objeto queda fuera del área.";

            case RestaurantPlacementValidationStatus
                .PhysicalOverlap:

                return
                    "El objeto se solapa con otro elemento.";

            case RestaurantPlacementValidationStatus
                .MinimumClearanceViolation:

                return
                    "No se mantiene la separación mínima.";

            case RestaurantPlacementValidationStatus
                .SystemUnavailable:

                return
                    "El sistema de colocación no está disponible.";

            default:

                return
                    "No se puede confirmar la colocación. Motivo: " +
                    failureReason +
                    ".";
        }
    }

    private void PublishMessage(
        string message
    )
    {
        InteractionMessageChanged?.Invoke(
            message
        );
    }

    private void LogEvent(
        string message
    )
    {
        if (!logInteractionEvents)
        {
            return;
        }

        Debug.Log(
            message,
            this
        );
    }

    private bool IsPointerOverUserInterface()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private bool DependenciesAreAvailable()
    {
        return editModeService != null &&
               transactionService != null &&
               interactionCamera != null;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editModeService == null)
        {
            TryGetComponent(
                out editModeService
            );
        }

        if (transactionService == null)
        {
            TryGetComponent(
                out transactionService
            );
        }

        if (interactionCamera == null)
        {
            interactionCamera =
                Camera.main;
        }
    }

    private void ValidateDependencies()
    {
        string controllerName =
            nameof(
                RestaurantEditInteractionController
            );

        if (editModeService == null)
        {
            string dependencyName =
                nameof(RestaurantEditModeService);

            Debug.LogError(
                controllerName +
                " necesita un " +
                dependencyName +
                ".",
                this
            );
        }

        if (transactionService == null)
        {
            string dependencyName =
                nameof(
                    RestaurantPlacementTransactionService
                );

            Debug.LogError(
                controllerName +
                " necesita un " +
                dependencyName +
                ".",
                this
            );
        }

        if (interactionCamera == null)
        {
            Debug.LogError(
                controllerName +
                " necesita una cámara de interacción.",
                this
            );
        }
    }

    private static float SnapValue(
        float value,
        float step
    )
    {
        if (step <= 0f)
        {
            return value;
        }

        return Mathf.Round(
                   value / step
               ) *
               step;
    }

    private static bool ArePositionsEquivalent(
        Vector3 first,
        Vector3 second
    )
    {
        const float tolerance = 0.0001f;

        return
            (
                first -
                second
            ).sqrMagnitude <=
            tolerance *
            tolerance;
    }

    private static bool AreRotationsEquivalent(
        Quaternion first,
        Quaternion second
    )
    {
        const float toleranceDegrees = 0.01f;

        return Quaternion.Angle(
            first,
            second
        ) <= toleranceDegrees;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();

        maximumRayDistance =
            Mathf.Max(
                1f,
                maximumRayDistance
            );

        gridSize =
            Mathf.Max(
                0.01f,
                gridSize
            );

        rotationStepDegrees =
            Mathf.Clamp(
                rotationStepDegrees,
                1f,
                180f
            );

        primaryMouseButton =
            Mathf.Clamp(
                primaryMouseButton,
                0,
                2
            );

        cancelMouseButton =
            Mathf.Clamp(
                cancelMouseButton,
                0,
                2
            );
    }
#endif
}