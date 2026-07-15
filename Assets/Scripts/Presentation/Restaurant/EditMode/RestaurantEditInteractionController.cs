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
/// - Seleccionar exclusivamente objetos autorizados.
/// - Aplicar las reglas de su definición editable.
/// - Mover y rotar el objeto seleccionado.
/// - Solicitar la validación de cada pose candidata.
/// - Confirmar o cancelar la colocación.
///
/// Las reglas espaciales y transaccionales permanecen en los
/// servicios de aplicación.
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
        "Servicio que conserva las colocaciones confirmadas para " +
        "deshacerlas y rehacerlas."
    )]
    [SerializeField]
    private RestaurantPlacementHistoryService
        historyService;

    [Tooltip(
        "Cámara utilizada para seleccionar y colocar objetos."
    )]
    [SerializeField]
    private Camera interactionCamera;

    [Header("Capas")]

    [Tooltip(
        "Capas que pueden intervenir en la selección."
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

    [Header("Movimiento predeterminado")]

    [Tooltip(
        "Ajusta la posición del objeto a una cuadrícula."
    )]
    [SerializeField]
    private bool useGridSnapping = true;

    [Tooltip(
        "Tamaño predeterminado de la cuadrícula. Una definición " +
        "editable puede sustituir este valor."
    )]
    [SerializeField]
    [Min(0.01f)]
    private float gridSize = 0.25f;

    [Tooltip(
        "Mantiene la altura mundial original del objeto."
    )]
    [SerializeField]
    private bool preserveOriginalWorldHeight = true;

    [Header("Rotación predeterminada")]

    [Tooltip(
        "Ángulo predeterminado aplicado en cada rotación. Una " +
        "definición editable puede sustituir este valor."
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
        "Tecla utilizada junto a Control para deshacer."
    )]
    [SerializeField]
    private Key undoKey = Key.Z;

    [Tooltip(
        "Tecla utilizada junto a Control para rehacer."
    )]
    [SerializeField]
    private Key redoKey = Key.Y;

    [Tooltip(
        "Botón principal. 0: izquierdo, 1: derecho, 2: central."
    )]
    [SerializeField]
    [Range(0, 2)]
    private int primaryMouseButton = 0;

    [Tooltip(
        "Botón de cancelación. 0: izquierdo, 1: derecho, " +
        "2: central."
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

    private RestaurantEditableObject activeEditableObject;

    private Vector3 grabOffset;

    private Vector3 candidatePosition;

    private Quaternion candidateRotation =
        Quaternion.identity;

    private float originalWorldHeight;

    private float effectiveGridSize = 0.25f;

    private float effectiveRotationStepDegrees = 90f;

    private bool hasCandidatePose;

    private bool hasPublishedPreviewPose;

    private Vector3 lastPublishedPosition;

    private Quaternion lastPublishedRotation =
        Quaternion.identity;

    private RestaurantPlacementValidationResult
        lastValidationResult;

    /// <summary>
    /// Se ejecuta cuando cambia el miembro espacial editado.
    /// </summary>
    public event Action<RestaurantAreaMember>
        ActiveMemberChanged;

    /// <summary>
    /// Se ejecuta cuando cambia el objeto editable activo.
    /// </summary>
    public event Action<RestaurantEditableObject>
        ActiveEditableObjectChanged;

    /// <summary>
    /// Se ejecuta cuando cambia el resultado de validación.
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

    public RestaurantEditableObject ActiveEditableObject
    {
        get
        {
            return activeEditableObject;
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
         * Mantiene sincronizado el estado local de presentación con
         * la transacción real. Esto evita que el controlador quede
         * bloqueado si otra acción cancela o restaura la colocación.
         */
        SynchronizeLocalPlacementState();

        HandleEditModeToggle();

        if (!editModeService.IsEditModeActive)
        {
            return;
        }

        /*
         * Los atajos de historial se procesan antes que la
         * interacción normal para que Ctrl+Z y Ctrl+Y no puedan
         * confundirse con otras acciones.
         */
        if (HandleHistoryShortcuts())
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
            LogEvent(message);

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
            string message =
                "No se pudo cerrar el modo edición. Motivo: " +
                failureReason +
                ".";

            PublishMessage(message);
            LogEvent(message);

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
    /// Deshace la última colocación confirmada.
    /// </summary>
    public bool TryUndoLastPlacement()
    {
        if (historyService == null)
        {
            const string unavailableMessage =
                "El historial de colocaciones no está disponible.";

            PublishMessage(
                unavailableMessage
            );

            LogEvent(
                unavailableMessage
            );

            return false;
        }

        RestaurantAreaMember affectedMember;

        RestaurantPlacementHistoryFailureReason
            failureReason;

        RestaurantPlacementValidationResult
            validationResult;

        bool undone =
            historyService.TryUndo(
                out affectedMember,
                out failureReason,
                out validationResult
            );

        if (!undone)
        {
            string rejectionMessage =
                BuildHistoryRejectionMessage(
                    false,
                    failureReason,
                    validationResult
                );

            PublishMessage(
                rejectionMessage
            );

            LogEvent(
                rejectionMessage
            );

            return false;
        }

        string memberName =
            affectedMember != null
                ? affectedMember.name
                : "Objeto";

        string message =
            memberName +
            ": último cambio deshecho.";

        PublishMessage(
            message
        );

        LogEvent(
            message
        );

        return true;
    }

    /// <summary>
    /// Rehace la última colocación deshecha.
    /// </summary>
    public bool TryRedoLastPlacement()
    {
        if (historyService == null)
        {
            const string unavailableMessage =
                "El historial de colocaciones no está disponible.";

            PublishMessage(
                unavailableMessage
            );

            LogEvent(
                unavailableMessage
            );

            return false;
        }

        RestaurantAreaMember affectedMember;

        RestaurantPlacementHistoryFailureReason
            failureReason;

        RestaurantPlacementValidationResult
            validationResult;

        bool redone =
            historyService.TryRedo(
                out affectedMember,
                out failureReason,
                out validationResult
            );

        if (!redone)
        {
            string rejectionMessage =
                BuildHistoryRejectionMessage(
                    true,
                    failureReason,
                    validationResult
                );

            PublishMessage(
                rejectionMessage
            );

            LogEvent(
                rejectionMessage
            );

            return false;
        }

        string memberName =
            affectedMember != null
                ? affectedMember.name
                : "Objeto";

        string message =
            memberName +
            ": último cambio rehecho.";

        PublishMessage(
            message
        );

        LogEvent(
            message
        );

        return true;
    }

    /// <summary>
    /// Procesa Ctrl+Z y Ctrl+Y únicamente durante el modo edición.
    ///
    /// El historial no puede ejecutarse mientras existe una
    /// colocación provisional.
    /// </summary>
    private bool HandleHistoryShortcuts()
    {
        if (!IsControlModifierPressed())
        {
            return false;
        }

        if (WasKeyPressedThisFrame(
                undoKey
            ))
        {
            /*
             * Durante una colocación provisional, Ctrl+Z actúa como
             * cancelación de esa operación. La primera pulsación
             * restaura el objeto y libera completamente el
             * controlador. Una segunda pulsación podrá deshacer la
             * última colocación que sí fue confirmada.
             */
            if (transactionService.HasActiveTransaction)
            {
                bool cancelled =
                    CancelActivePlacement();

                if (!cancelled)
                {
                    const string failureMessage =
                        "No se pudo restaurar la colocación actual.";

                    PublishMessage(
                        failureMessage
                    );

                    LogEvent(
                        failureMessage
                    );
                }

                return true;
            }

            TryUndoLastPlacement();

            return true;
        }

        if (WasKeyPressedThisFrame(
                redoKey
            ))
        {
            if (transactionService.HasActiveTransaction)
            {
                const string message =
                    "Confirma o cancela la colocación actual antes " +
                    "de rehacer.";

                PublishMessage(
                    message
                );

                LogEvent(
                    message
                );

                return true;
            }

            TryRedoLastPlacement();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Repara cualquier divergencia entre la transacción de
    /// colocación y el estado local del controlador.
    ///
    /// Puede ocurrir cuando otra regla o servicio restaura una
    /// colocación sin pasar por la interacción del ratón.
    /// </summary>
    private void SynchronizeLocalPlacementState()
    {
        if (transactionService == null)
        {
            return;
        }

        bool transactionIsActive =
            transactionService.HasActiveTransaction;

        bool controllerHasPlacementState =
            activeMember != null ||
            activeEditableObject != null ||
            hasCandidatePose ||
            hasPublishedPreviewPose;

        if (!transactionIsActive)
        {
            if (controllerHasPlacementState)
            {
                ClearLocalPlacementState();
            }

            return;
        }

        /*
         * Una transacción sin miembro local no puede continuar de
         * forma segura. Se restaura y se libera inmediatamente.
         */
        if (activeMember != null)
        {
            return;
        }

        transactionService.CancelPlacement();
        ClearLocalPlacementState();

        const string message =
            "Se ha restaurado una colocación provisional que había " +
            "quedado desincronizada.";

        PublishMessage(
            message
        );

        LogEvent(
            message
        );
    }

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

        RestaurantEditableObject editableObject;
        RestaurantAreaMember member;
        Vector3 selectedWorldPoint;
        string rejectionReason;

        bool foundMember =
            TryFindSelectableMemberUnderPointer(
                out editableObject,
                out member,
                out selectedWorldPoint,
                out rejectionReason
            );

        if (!foundMember)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                rejectionReason =
                    "No hay ningún objeto editable bajo el cursor.";
            }

            PublishMessage(
                rejectionReason
            );

            LogEvent(
                rejectionReason
            );

            return;
        }

        BeginPlacement(
            editableObject,
            member,
            selectedWorldPoint
        );
    }

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
    /// Inicia una colocación respetando la definición editable.
    /// </summary>
    private bool BeginPlacement(
        RestaurantEditableObject editableObject,
        RestaurantAreaMember member,
        Vector3 selectedWorldPoint
    )
    {
        if (editableObject == null ||
            member == null)
        {
            return false;
        }

        string rejectionReason;

        if (!editableObject.CanBeginEditing(
                out rejectionReason
            ))
        {
            PublishMessage(
                rejectionReason
            );

            LogEvent(
                rejectionReason
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
            string message =
                "No se pudo iniciar la colocación. Motivo: " +
                failureReason +
                ".";

            PublishMessage(message);
            LogEvent(message);

            return false;
        }

        activeEditableObject =
            editableObject;

        activeMember =
            member;

        effectiveGridSize =
            editableObject.ResolveGridSize(
                gridSize
            );

        effectiveRotationStepDegrees =
            editableObject.ResolveRotationStepDegrees(
                rotationStepDegrees
            );

        Vector3 memberPosition =
            member.transform.position;

        grabOffset =
            memberPosition -
            selectedWorldPoint;

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

        ActiveEditableObjectChanged?.Invoke(
            editableObject
        );

        ActiveMemberChanged?.Invoke(
            member
        );

        PlacementValidationChanged?.Invoke(
            lastValidationResult
        );

        PublishMessage(
            "Moviendo " +
            editableObject.DisplayName +
            "."
        );

        LogEvent(
            "Colocación iniciada para " +
            member.name +
            ". Definición: " +
            editableObject.Definition.DefinitionId +
            "."
        );

        return true;
    }

    /// <summary>
    /// Rota el objeto cuando su definición lo permite.
    /// </summary>
    private void RotateCandidateClockwise()
    {
        if (!hasCandidatePose ||
            activeMember == null ||
            activeEditableObject == null)
        {
            return;
        }

        if (!activeEditableObject.CanRotate)
        {
            string message =
                activeEditableObject.DisplayName +
                " no permite rotación.";

            PublishMessage(message);
            LogEvent(message);

            return;
        }

        Quaternion yawRotation =
            Quaternion.AngleAxis(
                effectiveRotationStepDegrees,
                Vector3.up
            );

        candidateRotation =
            yawRotation *
            candidateRotation;

        hasPublishedPreviewPose = false;
    }

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
                    effectiveGridSize
                );

            nextPosition.z =
                SnapValue(
                    nextPosition.z,
                    effectiveGridSize
                );
        }

        candidatePosition =
            nextPosition;

        hasCandidatePose = true;
    }

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
            string message =
                "No se pudo actualizar la previsualización. " +
                "Motivo: " +
                failureReason +
                ".";

            PublishMessage(message);
            LogEvent(message);

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
    /// Obtiene el collider más cercano bajo el cursor y comprueba
    /// que pertenezca a un objeto editable autorizado.
    /// </summary>
    private bool TryFindSelectableMemberUnderPointer(
        out RestaurantEditableObject editableObject,
        out RestaurantAreaMember member,
        out Vector3 selectedWorldPoint,
        out string rejectionReason
    )
    {
        editableObject = null;
        member = null;
        selectedWorldPoint = default;
        rejectionReason = string.Empty;

        Ray ray;

        if (!TryBuildPointerRay(
                out ray
            ))
        {
            rejectionReason =
                "No se pudo obtener la posición del puntero.";

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

        RaycastHit nearestHit =
            default;

        bool foundCollider = false;

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

            nearestHit =
                hit;

            nearestDistance =
                hit.distance;

            foundCollider =
                true;
        }

        if (!foundCollider ||
            nearestHit.collider == null)
        {
            rejectionReason =
                "No hay ningún objeto bajo el cursor.";

            return false;
        }

        editableObject =
            nearestHit.collider.GetComponentInParent<
                RestaurantEditableObject
            >();

        if (editableObject == null)
        {
            rejectionReason =
                nearestHit.collider.gameObject.name +
                " no es un objeto editable.";

            return false;
        }

        if (!editableObject.CanBeginEditing(
                out rejectionReason
            ))
        {
            return false;
        }

        if (!editableObject.TryGetComponent(
                out member
            ))
        {
            rejectionReason =
                editableObject.name +
                " no tiene RestaurantAreaMember.";

            return false;
        }

        RestaurantPlacementFootprint footprint;

        if (!editableObject.TryGetComponent(
                out footprint
            ))
        {
            rejectionReason =
                editableObject.name +
                " no tiene RestaurantPlacementFootprint.";

            member = null;

            return false;
        }

        selectedWorldPoint =
            nearestHit.point;

        return true;
    }

    private bool TryGetPlacementSurfacePoint(
        out Vector3 surfacePoint
    )
    {
        surfacePoint = default;

        Ray ray;

        if (!TryBuildPointerRay(
                out ray
            ))
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

            foundSurface =
                true;
        }

        return foundSurface;
    }

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
    /// Comprueba las teclas Control izquierda y derecha mediante
    /// el nuevo Input System.
    /// </summary>
    private static bool IsControlModifierPressed()
    {
        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return false;
        }

        return keyboard.leftCtrlKey.isPressed ||
               keyboard.rightCtrlKey.isPressed;
    }

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
        activeEditableObject = null;

        grabOffset = Vector3.zero;
        candidatePosition = Vector3.zero;
        candidateRotation = Quaternion.identity;

        originalWorldHeight = 0f;

        effectiveGridSize =
            Mathf.Max(
                0.01f,
                gridSize
            );

        effectiveRotationStepDegrees =
            Mathf.Clamp(
                rotationStepDegrees,
                1f,
                180f
            );

        hasCandidatePose = false;
        hasPublishedPreviewPose = false;

        lastPublishedPosition = Vector3.zero;
        lastPublishedRotation = Quaternion.identity;

        lastValidationResult = default;

        ActiveEditableObjectChanged?.Invoke(
            null
        );

        ActiveMemberChanged?.Invoke(
            null
        );
    }

    /// <summary>
    /// Construye un mensaje legible para rechazos del historial.
    /// </summary>
    private string BuildHistoryRejectionMessage(
        bool isRedo,
        RestaurantPlacementHistoryFailureReason
            failureReason,
        RestaurantPlacementValidationResult
            validationResult
    )
    {
        switch (failureReason)
        {
            case RestaurantPlacementHistoryFailureReason
                .NothingToUndo:

                return
                    "No hay ningún cambio que deshacer.";

            case RestaurantPlacementHistoryFailureReason
                .NothingToRedo:

                return
                    "No hay ningún cambio que rehacer.";

            case RestaurantPlacementHistoryFailureReason
                .PlacementOperationActive:

                return
                    "Confirma o cancela la colocación actual antes " +
                    "de utilizar el historial.";

            case RestaurantPlacementHistoryFailureReason
                .DestinationInvalid:

                return
                    "No se puede " +
                    (
                        isRedo
                            ? "rehacer"
                            : "deshacer"
                    ) +
                    " porque la posición de destino ya no es " +
                    "válida. Estado: " +
                    validationResult.Status +
                    ".";

            case RestaurantPlacementHistoryFailureReason
                .MemberUnavailable:

                return
                    "El objeto asociado al historial ya no está " +
                    "disponible.";

            case RestaurantPlacementHistoryFailureReason
                .ValidationSystemUnavailable:

                return
                    "No se puede validar la posición del historial.";

            case RestaurantPlacementHistoryFailureReason
                .TransactionSystemUnavailable:

                return
                    "El sistema transaccional de colocación no está " +
                    "disponible.";

            case RestaurantPlacementHistoryFailureReason
                .SnapshotInvalid:

            case RestaurantPlacementHistoryFailureReason
                .RestoreFailed:

                return
                    "No se pudo restaurar el estado guardado del " +
                    "objeto.";

            default:

                return
                    "No se pudo completar la operación de historial. " +
                    "Motivo: " +
                    failureReason +
                    ".";
        }
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

        if (historyService == null)
        {
            TryGetComponent(
                out historyService
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
            Debug.LogError(
                controllerName +
                " necesita un " +
                nameof(RestaurantEditModeService) +
                ".",
                this
            );
        }

        if (transactionService == null)
        {
            Debug.LogError(
                controllerName +
                " necesita un " +
                nameof(
                    RestaurantPlacementTransactionService
                ) +
                ".",
                this
            );
        }

        if (historyService == null)
        {
            Debug.LogError(
                controllerName +
                " necesita un " +
                nameof(
                    RestaurantPlacementHistoryService
                ) +
                " para deshacer y rehacer.",
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
