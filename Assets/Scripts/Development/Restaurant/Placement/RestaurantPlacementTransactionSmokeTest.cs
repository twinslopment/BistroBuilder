using System.Collections;
using UnityEngine;

/// <summary>
/// Prueba de integración temporal para el sistema transaccional
/// de colocación.
///
/// Comprueba automáticamente:
/// - Una confirmación válida en la posición actual.
/// - Un solapamiento físico provocado deliberadamente.
/// - El rechazo de una confirmación inválida.
/// - La restauración exacta al cancelar.
///
/// Este componente pertenece exclusivamente al desarrollo.
/// No utiliza Update y solo ejecuta la prueba una vez.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Development/" +
    "Restaurant Placement Transaction Smoke Test"
)]
public sealed class RestaurantPlacementTransactionSmokeTest :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio transaccional que se desea comprobar."
    )]
    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [Header("Objetos de prueba")]

    [Tooltip(
        "Objeto que se moverá temporalmente durante la prueba."
    )]
    [SerializeField]
    private RestaurantAreaMember targetMember;

    [Tooltip(
        "Objeto cuya posición se utilizará para provocar " +
        "un solapamiento físico."
    )]
    [SerializeField]
    private RestaurantAreaMember blockingMember;

    [Header("Ejecución")]

    [Tooltip(
        "Ejecuta la prueba automáticamente al entrar en Play."
    )]
    [SerializeField]
    private bool runOnStart = true;

    [Tooltip(
        "Muestra información detallada de cada comprobación."
    )]
    [SerializeField]
    private bool logDetailedResults = true;

    private Coroutine testRoutine;

    private bool testHasRun;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void Start()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#else
        if (!runOnStart)
        {
            return;
        }

        RunTest();
#endif
    }

    private void OnDisable()
    {
        if (testRoutine != null)
        {
            StopCoroutine(testRoutine);
            testRoutine = null;
        }

        /*
         * Una prueba interrumpida nunca debe dejar una
         * transacción provisional abierta.
         */
        if (transactionService != null &&
            transactionService.HasActiveTransaction)
        {
            transactionService.CancelPlacement();
        }
    }

    /// <summary>
    /// Inicia manualmente la prueba.
    /// </summary>
    [ContextMenu("Ejecutar prueba transaccional")]
    public void RunTest()
    {
        if (testHasRun)
        {
            Debug.LogWarning(
                "La prueba transaccional ya se ha ejecutado " +
                "durante esta sesión de Play.",
                this
            );

            return;
        }

        if (testRoutine != null)
        {
            return;
        }

        if (!ValidateConfiguration())
        {
            return;
        }

        testRoutine =
            StartCoroutine(
                RunTestRoutine()
            );
    }

    /// <summary>
    /// Ejecuta la secuencia completa después de que los registros
    /// del restaurante hayan terminado de inicializarse.
    /// </summary>
    private IEnumerator RunTestRoutine()
    {
        /*
         * Se esperan dos frames para no depender del orden de
         * Start de los registros y servicios de GameSystems.
         */
        yield return null;
        yield return null;

        testHasRun = true;

        Vector3 originalPosition =
            targetMember.transform.position;

        Quaternion originalRotation =
            targetMember.transform.rotation;

        Vector3 originalScale =
            targetMember.transform.localScale;

        Transform originalParent =
            targetMember.transform.parent;

        int originalSiblingIndex =
            targetMember.transform.GetSiblingIndex();

        RestaurantArea originalArea =
            targetMember.AssignedArea;

        bool validCommitPassed =
            TestValidCommit(
                originalPosition,
                originalRotation,
                originalArea
            );

        bool invalidCommitRejected =
            TestInvalidCommitAndCancel(
                originalPosition,
                originalRotation
            );

        bool restorationPassed =
            ValidateRestoration(
                originalPosition,
                originalRotation,
                originalScale,
                originalParent,
                originalSiblingIndex,
                originalArea
            );

        bool finalResult =
            validCommitPassed &&
            invalidCommitRejected &&
            restorationPassed;

        if (finalResult)
        {
            Debug.Log(
                "Prueba transaccional completada correctamente. " +
                "Confirmación válida: OK, " +
                "rechazo de colocación inválida: OK, " +
                "restauración tras cancelar: OK.",
                this
            );
        }
        else
        {
            Debug.LogError(
                "La prueba transaccional no se ha completado " +
                "correctamente. Revisa los errores anteriores.",
                this
            );
        }

        testRoutine = null;
    }

    /// <summary>
    /// Comprueba que una operación pueda confirmarse sin mover
    /// el objeto fuera de su pose válida actual.
    /// </summary>
    private bool TestValidCommit(
        Vector3 originalPosition,
        Quaternion originalRotation,
        RestaurantArea originalArea
    )
    {
        bool began =
            transactionService.TryBeginPlacement(
                targetMember,
                out RestaurantPlacementTransactionFailureReason
                    beginFailure
            );

        if (!began)
        {
            LogFailure(
                "No se pudo iniciar la colocación válida. Motivo: " +
                beginFailure +
                "."
            );

            return false;
        }

        bool previewed =
            transactionService.TryPreviewPlacement(
                originalPosition,
                originalRotation,
                out RestaurantPlacementValidationResult
                    previewResult,
                out RestaurantPlacementTransactionFailureReason
                    previewFailure
            );

        if (!previewed)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "No se pudo previsualizar la posición válida. " +
                "Motivo: " +
                previewFailure +
                "."
            );

            return false;
        }

        if (!previewResult.IsValid)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "La posición original fue considerada inválida. " +
                "Estado: " +
                previewResult.Status +
                "."
            );

            return false;
        }

        bool committed =
            transactionService.TryCommitPlacement(
                out RestaurantPlacementValidationResult
                    commitResult,
                out RestaurantPlacementTransactionFailureReason
                    commitFailure
            );

        if (!committed)
        {
            if (transactionService.HasActiveTransaction)
            {
                transactionService.CancelPlacement();
            }

            LogFailure(
                "La confirmación válida fue rechazada. Motivo: " +
                commitFailure +
                ". Estado: " +
                commitResult.Status +
                "."
            );

            return false;
        }

        if (transactionService.HasActiveTransaction)
        {
            LogFailure(
                "La transacción continúa abierta después de una " +
                "confirmación válida."
            );

            return false;
        }

        if (targetMember.AssignedArea != originalArea)
        {
            LogFailure(
                "La confirmación válida modificó inesperadamente " +
                "el área asignada."
            );

            return false;
        }

        if (logDetailedResults)
        {
            Debug.Log(
                "Prueba 1 superada: la posición válida se " +
                "previsualizó y confirmó correctamente.",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Coloca temporalmente el objeto sobre otro, comprueba que
    /// la confirmación sea rechazada y cancela la operación.
    /// </summary>
    private bool TestInvalidCommitAndCancel(
        Vector3 originalPosition,
        Quaternion originalRotation
    )
    {
        bool began =
            transactionService.TryBeginPlacement(
                targetMember,
                out RestaurantPlacementTransactionFailureReason
                    beginFailure
            );

        if (!began)
        {
            LogFailure(
                "No se pudo iniciar la prueba inválida. Motivo: " +
                beginFailure +
                "."
            );

            return false;
        }

        Vector3 blockingPosition =
            blockingMember.transform.position;

        Quaternion blockingRotation =
            blockingMember.transform.rotation;

        bool previewed =
            transactionService.TryPreviewPlacement(
                blockingPosition,
                blockingRotation,
                out RestaurantPlacementValidationResult
                    previewResult,
                out RestaurantPlacementTransactionFailureReason
                    previewFailure
            );

        if (!previewed)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "No se pudo crear la previsualización inválida. " +
                "Motivo: " +
                previewFailure +
                "."
            );

            return false;
        }

        if (previewResult.Status !=
            RestaurantPlacementValidationStatus.PhysicalOverlap)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "La superposición deliberada no devolvió " +
                "PhysicalOverlap. Estado recibido: " +
                previewResult.Status +
                "."
            );

            return false;
        }

        bool committed =
            transactionService.TryCommitPlacement(
                out RestaurantPlacementValidationResult
                    commitResult,
                out RestaurantPlacementTransactionFailureReason
                    commitFailure
            );

        if (committed)
        {
            LogFailure(
                "El sistema confirmó una colocación que se " +
                "solapaba físicamente con otro objeto."
            );

            return false;
        }

        if (commitFailure !=
            RestaurantPlacementTransactionFailureReason
                .PlacementInvalid)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "La colocación inválida se rechazó con un motivo " +
                "inesperado: " +
                commitFailure +
                "."
            );

            return false;
        }

        if (commitResult.Status !=
            RestaurantPlacementValidationStatus.PhysicalOverlap)
        {
            transactionService.CancelPlacement();

            LogFailure(
                "La revalidación de la confirmación no conservó " +
                "el estado PhysicalOverlap. Estado recibido: " +
                commitResult.Status +
                "."
            );

            return false;
        }

        if (!transactionService.HasActiveTransaction)
        {
            LogFailure(
                "La transacción inválida se cerró antes de que " +
                "el jugador pudiera corregirla o cancelarla."
            );

            return false;
        }

        bool cancelled =
            transactionService.CancelPlacement();

        if (!cancelled)
        {
            LogFailure(
                "No se pudo cancelar la colocación inválida."
            );

            return false;
        }

        if (transactionService.HasActiveTransaction)
        {
            LogFailure(
                "La transacción continúa abierta después de " +
                "cancelarla."
            );

            return false;
        }

        bool positionRestored =
            IsPositionEqual(
                targetMember.transform.position,
                originalPosition
            );

        bool rotationRestored =
            IsRotationEqual(
                targetMember.transform.rotation,
                originalRotation
            );

        if (!positionRestored ||
            !rotationRestored)
        {
            LogFailure(
                "La cancelación no restauró la pose mundial " +
                "original del objeto."
            );

            return false;
        }

        if (logDetailedResults)
        {
            Debug.Log(
                "Prueba 2 superada: el solapamiento físico fue " +
                "rechazado y la cancelación restauró la pose.",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Comprueba el estado final completo del objeto.
    /// </summary>
    private bool ValidateRestoration(
        Vector3 originalPosition,
        Quaternion originalRotation,
        Vector3 originalScale,
        Transform originalParent,
        int originalSiblingIndex,
        RestaurantArea originalArea
    )
    {
        bool positionMatches =
            IsPositionEqual(
                targetMember.transform.position,
                originalPosition
            );

        bool rotationMatches =
            IsRotationEqual(
                targetMember.transform.rotation,
                originalRotation
            );

        bool scaleMatches =
            IsPositionEqual(
                targetMember.transform.localScale,
                originalScale
            );

        bool parentMatches =
            targetMember.transform.parent ==
            originalParent;

        bool siblingMatches =
            targetMember.transform.GetSiblingIndex() ==
            originalSiblingIndex;

        bool areaMatches =
            targetMember.AssignedArea ==
            originalArea;

        if (!positionMatches)
        {
            LogFailure(
                "La posición final no coincide con la posición " +
                "original."
            );
        }

        if (!rotationMatches)
        {
            LogFailure(
                "La rotación final no coincide con la rotación " +
                "original."
            );
        }

        if (!scaleMatches)
        {
            LogFailure(
                "La escala final no coincide con la escala " +
                "original."
            );
        }

        if (!parentMatches)
        {
            LogFailure(
                "El padre final no coincide con el padre original."
            );
        }

        if (!siblingMatches)
        {
            LogFailure(
                "El índice jerárquico final no coincide con el " +
                "índice original."
            );
        }

        if (!areaMatches)
        {
            LogFailure(
                "El área final no coincide con el área original."
            );
        }

        return
            positionMatches &&
            rotationMatches &&
            scaleMatches &&
            parentMatches &&
            siblingMatches &&
            areaMatches;
    }

    /// <summary>
    /// Valida las referencias necesarias para ejecutar la prueba.
    /// </summary>
    private bool ValidateConfiguration()
    {
        CacheDependenciesIfNeeded();

        if (transactionService == null)
        {
            LogFailure(
                "Falta RestaurantPlacementTransactionService."
            );

            return false;
        }

        if (targetMember == null)
        {
            LogFailure(
                "No se ha asignado Target Member."
            );

            return false;
        }

        if (blockingMember == null)
        {
            LogFailure(
                "No se ha asignado Blocking Member."
            );

            return false;
        }

        if (targetMember == blockingMember)
        {
            LogFailure(
                "Target Member y Blocking Member no pueden ser " +
                "el mismo objeto."
            );

            return false;
        }

        if (!targetMember.TryGetComponent(
                out RestaurantPlacementFootprint
                    targetFootprint
            ))
        {
            LogFailure(
                "Target Member no tiene " +
                "RestaurantPlacementFootprint."
            );

            return false;
        }

        if (!blockingMember.TryGetComponent(
                out RestaurantPlacementFootprint
                    blockingFootprint
            ))
        {
            LogFailure(
                "Blocking Member no tiene " +
                "RestaurantPlacementFootprint."
            );

            return false;
        }

        if (!targetFootprint.BlocksOtherPlacements ||
            !blockingFootprint.BlocksOtherPlacements)
        {
            LogFailure(
                "Las dos huellas deben tener activado " +
                "Blocks Other Placements."
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// Recupera automáticamente el servicio situado en el mismo
    /// GameObject.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (transactionService == null)
        {
            TryGetComponent(
                out transactionService
            );
        }
    }

    private void LogFailure(
        string message
    )
    {
        Debug.LogError(
            "Prueba transaccional: " +
            message,
            this
        );
    }

    private static bool IsPositionEqual(
        Vector3 first,
        Vector3 second
    )
    {
        const float positionTolerance = 0.0001f;

        return
            (
                first -
                second
            ).sqrMagnitude <=
            positionTolerance *
            positionTolerance;
    }

    private static bool IsRotationEqual(
        Quaternion first,
        Quaternion second
    )
    {
        const float rotationTolerance = 0.01f;

        return Quaternion.Angle(
            first,
            second
        ) <= rotationTolerance;
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