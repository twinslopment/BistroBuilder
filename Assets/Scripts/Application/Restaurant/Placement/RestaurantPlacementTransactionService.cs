using System;
using UnityEngine;

/// <summary>
/// Gestiona operaciones transaccionales de colocación.
///
/// Permite:
/// - Iniciar la edición de un objeto existente.
/// - Evaluar y mostrar posiciones candidatas.
/// - Confirmar únicamente colocaciones válidas.
/// - Cancelar y restaurar exactamente el estado original.
/// - Actualizar el área asignada al confirmar.
/// - Informar a la presentación mediante eventos.
///
/// El servicio no interpreta controles de ratón ni dibuja la UI.
/// Esa responsabilidad pertenecerá a la capa de presentación.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlacementTransactionService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio que valida áreas, capacidades, límites, " +
        "solapamientos y separación mínima."
    )]
    [SerializeField]
    private RestaurantPlacementValidationService
        validationService;

    private RestaurantAreaMember activeMember;

    private RestaurantArea originalArea;

    private TransformSnapshot originalTransform;

    private RestaurantPlacementValidationResult
        lastValidationResult;

    private bool hasActiveTransaction;

    private bool hasEvaluatedPreview;

    /// <summary>
    /// Se ejecuta cuando comienza una operación de edición.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementStarted;

    /// <summary>
    /// Se ejecuta cada vez que cambia la pose candidata.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementPreviewChanged;

    /// <summary>
    /// Se ejecuta cuando una colocación válida se confirma.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementCommitted;

    /// <summary>
    /// Se ejecuta cuando una confirmación es rechazada.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementCommitRejected;

    /// <summary>
    /// Se ejecuta cuando se cancela la operación y se restaura
    /// el estado original.
    /// </summary>
    public event Action<RestaurantAreaMember>
        PlacementCancelled;

    public bool HasActiveTransaction =>
        hasActiveTransaction;

    public bool HasEvaluatedPreview =>
        hasEvaluatedPreview;

    public RestaurantAreaMember ActiveMember =>
        activeMember;

    public RestaurantPlacementValidationResult
        LastValidationResult =>
            lastValidationResult;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnDisable()
    {
        /*
         * Nunca debe quedar un objeto abandonado en una posición
         * provisional al desactivar el sistema.
         */
        if (hasActiveTransaction)
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Inicia la edición de un objeto existente.
    ///
    /// Guarda:
    /// - Área asignada.
    /// - Padre.
    /// - Índice entre hermanos.
    /// - Posición local.
    /// - Rotación local.
    /// - Escala local.
    ///
    /// Solo puede existir una transacción activa a la vez.
    /// </summary>
    public bool TryBeginPlacement(
        RestaurantAreaMember member,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (member == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .InvalidMember;

            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        if (hasActiveTransaction)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .OperationAlreadyActive;

            return false;
        }

        activeMember = member;

        originalArea =
            member.AssignedArea;

        originalTransform =
            TransformSnapshot.Capture(
                member.transform
            );

        lastValidationResult =
            validationService
                .ValidateCurrentPlacement(member);

        hasActiveTransaction = true;
        hasEvaluatedPreview = false;

        PlacementStarted?.Invoke(
            member,
            lastValidationResult
        );

        return true;
    }

    /// <summary>
    /// Evalúa y aplica una pose candidata.
    ///
    /// La pose se aplica aunque sea inválida para que el jugador
    /// pueda verla resaltada y continuar moviéndola.
    ///
    /// No modifica todavía el área asignada.
    /// </summary>
    public bool TryPreviewPlacement(
        Vector3 candidateWorldPosition,
        Quaternion candidateWorldRotation,
        out RestaurantPlacementValidationResult result,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        result = default;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (!TryGetValidActiveMember(
                out RestaurantAreaMember member,
                out failureReason
            ))
        {
            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        /*
         * La validación admite una pose candidata, por lo que no
         * necesita alterar primero el Transform real del objeto.
         */
        result =
            validationService.ValidatePlacement(
                member,
                candidateWorldPosition,
                candidateWorldRotation
            );

        /*
         * La pose se aplica después de obtener el resultado.
         * También se muestran físicamente las posiciones inválidas
         * mientras el jugador mantenga abierta la operación.
         */
        member.transform.SetPositionAndRotation(
            candidateWorldPosition,
            candidateWorldRotation
        );

        lastValidationResult = result;
        hasEvaluatedPreview = true;

        PlacementPreviewChanged?.Invoke(
            member,
            result
        );

        return true;
    }

    /// <summary>
    /// Revalida la pose actual y confirma la colocación.
    ///
    /// La confirmación:
    /// - Se rechaza si la posición no es válida.
    /// - Actualiza el área asignada.
    /// - Propaga el cambio a los registros mediante AreaChanged.
    /// - Cierra la transacción.
    /// </summary>
    public bool TryCommitPlacement(
        out RestaurantPlacementValidationResult result,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        result = default;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (!TryGetValidActiveMember(
                out RestaurantAreaMember member,
                out failureReason
            ))
        {
            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        /*
         * Se valida otra vez para no confiar en un resultado
         * anterior que pueda haber quedado obsoleto.
         */
        result =
            validationService.ValidateCurrentPlacement(
                member
            );

        lastValidationResult = result;
        hasEvaluatedPreview = true;

        if (!result.IsValid)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .PlacementInvalid;

            PlacementCommitRejected?.Invoke(
                member,
                result
            );

            return false;
        }

        if (result.CandidateArea == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .PlacementInvalid;

            PlacementCommitRejected?.Invoke(
                member,
                result
            );

            return false;
        }

        /*
         * SetArea dispara AreaChanged.
         *
         * RestaurantAreaMemberRegistry y
         * RestaurantPlacementRegistry actualizarán sus índices
         * sin realizar búsquedas globales.
         */
        member.SetArea(
            result.CandidateArea
        );

        RestaurantAreaMember committedMember =
            member;

        ClearActiveTransaction();

        PlacementCommitted?.Invoke(
            committedMember,
            result
        );

        return true;
    }

    /// <summary>
    /// Cancela la operación y restaura exactamente:
    /// - Jerarquía.
    /// - Posición.
    /// - Rotación.
    /// - Escala.
    /// - Área asignada.
    /// </summary>
    public bool CancelPlacement()
    {
        if (!hasActiveTransaction)
        {
            return false;
        }

        RestaurantAreaMember cancelledMember =
            activeMember;

        if (cancelledMember != null)
        {
            originalTransform.Restore(
                cancelledMember.transform
            );

            if (originalArea != null)
            {
                cancelledMember.SetArea(
                    originalArea
                );
            }
            else
            {
                cancelledMember.ClearArea();
            }
        }

        ClearActiveTransaction();

        PlacementCancelled?.Invoke(
            cancelledMember
        );

        return true;
    }

    /// <summary>
    /// Devuelve la pose original de la operación activa.
    ///
    /// Será útil para la interfaz, indicadores y acciones de
    /// restauración.
    /// </summary>
    public bool TryGetOriginalWorldPose(
        out Vector3 worldPosition,
        out Quaternion worldRotation
    )
    {
        worldPosition = default;
        worldRotation = Quaternion.identity;

        if (!hasActiveTransaction ||
            activeMember == null)
        {
            return false;
        }

        originalTransform.GetWorldPose(
            out worldPosition,
            out worldRotation
        );

        return true;
    }

    /// <summary>
    /// Comprueba que exista una operación activa y que su miembro
    /// siga disponible.
    /// </summary>
    private bool TryGetValidActiveMember(
        out RestaurantAreaMember member,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        member = null;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (!hasActiveTransaction)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .NoActiveOperation;

            return false;
        }

        if (activeMember == null)
        {
            ClearActiveTransaction();

            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ActiveMemberUnavailable;

            return false;
        }

        member = activeMember;

        return true;
    }

    /// <summary>
    /// Limpia los datos internos de la transacción sin modificar
    /// el objeto editado.
    /// </summary>
    private void ClearActiveTransaction()
    {
        activeMember = null;
        originalArea = null;
        originalTransform = default;
        lastValidationResult = default;

        hasActiveTransaction = false;
        hasEvaluatedPreview = false;
    }

    /// <summary>
    /// Recupera automáticamente dependencias situadas en el mismo
    /// GameObject.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (validationService == null)
        {
            TryGetComponent(
                out validationService
            );
        }
    }

    /// <summary>
    /// Comprueba que las dependencias obligatorias estén
    /// disponibles.
    /// </summary>
    private void ValidateDependencies()
    {
        if (validationService != null)
        {
            return;
        }

        string serviceName =
            nameof(RestaurantPlacementTransactionService);

        string dependencyName =
            nameof(RestaurantPlacementValidationService);

        Debug.LogError(
            serviceName +
            " necesita un " +
            dependencyName +
            ".",
            this
        );
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

    /// <summary>
    /// Captura completa del Transform de un objeto.
    ///
    /// Se almacena en coordenadas locales para restaurar
    /// correctamente objetos con padres transformados.
    /// </summary>
    private readonly struct TransformSnapshot
    {
        private readonly Transform parent;

        private readonly int siblingIndex;

        private readonly Vector3 localPosition;

        private readonly Quaternion localRotation;

        private readonly Vector3 localScale;

        private TransformSnapshot(
            Transform parent,
            int siblingIndex,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale
        )
        {
            this.parent = parent;
            this.siblingIndex = siblingIndex;
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.localScale = localScale;
        }

        /// <summary>
        /// Captura el estado local y jerárquico del Transform.
        /// </summary>
        public static TransformSnapshot Capture(
            Transform target
        )
        {
            if (target == null)
            {
                return default;
            }

            return new TransformSnapshot(
                target.parent,
                target.GetSiblingIndex(),
                target.localPosition,
                target.localRotation,
                target.localScale
            );
        }

        /// <summary>
        /// Restaura el Transform exactamente al estado capturado.
        /// </summary>
        public void Restore(
            Transform target
        )
        {
            if (target == null)
            {
                return;
            }

            target.SetParent(
                parent,
                false
            );

            target.localPosition =
                localPosition;

            target.localRotation =
                localRotation;

            target.localScale =
                localScale;

            if (target.parent == null)
            {
                return;
            }

            int maximumSiblingIndex =
                target.parent.childCount - 1;

            int safeSiblingIndex =
                Mathf.Clamp(
                    siblingIndex,
                    0,
                    maximumSiblingIndex
                );

            target.SetSiblingIndex(
                safeSiblingIndex
            );
        }

        /// <summary>
        /// Calcula la pose mundial correspondiente al estado
        /// capturado.
        /// </summary>
        public void GetWorldPose(
            out Vector3 worldPosition,
            out Quaternion worldRotation
        )
        {
            if (parent == null)
            {
                worldPosition =
                    localPosition;

                worldRotation =
                    localRotation;

                return;
            }

            worldPosition =
                parent.TransformPoint(
                    localPosition
                );

            worldRotation =
                parent.rotation *
                localRotation;
        }
    }
}

/// <summary>
/// Motivo por el que una operación transaccional no puede
/// completarse.
/// </summary>
public enum RestaurantPlacementTransactionFailureReason
{
    None = 0,
    InvalidMember = 1,
    OperationAlreadyActive = 2,
    NoActiveOperation = 3,
    ActiveMemberUnavailable = 4,
    ValidationSystemUnavailable = 5,
    PlacementInvalid = 6
}