using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regla externa que puede permitir o impedir la entrada
/// al modo edición.
///
/// Ejemplos futuros:
/// - Impedir editar durante un servicio activo.
/// - Impedir editar mientras se guarda la partida.
/// - Exigir que el restaurante esté cerrado.
/// </summary>
public interface IRestaurantEditModeAvailabilityRule
{
    /// <summary>
    /// Devuelve true cuando el modo edición puede activarse.
    /// </summary>
    bool CanEnterEditMode(
        out string rejectionMessage
    );
}

/// <summary>
/// Gestiona el estado global del modo edición.
///
/// Responsabilidades:
/// - Activar y desactivar el modo edición.
/// - Consultar reglas externas de disponibilidad.
/// - Impedir salir si existe una colocación activa.
/// - Cancelar de forma segura una colocación provisional.
/// - Publicar eventos para la futura interfaz.
///
/// No interpreta controles del jugador y no utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Restaurant Edit Mode Service"
)]
public sealed class RestaurantEditModeService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio transaccional encargado de las operaciones " +
        "provisionales de colocación."
    )]
    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [Header("Reglas de disponibilidad")]

    [Tooltip(
        "Componentes que implementan " +
        "IRestaurantEditModeAvailabilityRule."
    )]
    [SerializeField]
    private MonoBehaviour[] availabilityRuleSources =
        new MonoBehaviour[0];

    private readonly List<
        IRestaurantEditModeAvailabilityRule
    > availabilityRules =
        new List<IRestaurantEditModeAvailabilityRule>(4);

    private bool isEditModeActive;

    /// <summary>
    /// Se ejecuta cuando se activa el modo edición.
    /// </summary>
    public event Action EditModeEntered;

    /// <summary>
    /// Se ejecuta cuando se desactiva el modo edición.
    /// </summary>
    public event Action EditModeExited;

    /// <summary>
    /// Se ejecuta cuando se rechaza una solicitud de entrada.
    /// </summary>
    public event Action<
        RestaurantEditModeFailureReason,
        string
    > EditModeEntryRejected;

    /// <summary>
    /// Se ejecuta cuando se rechaza una solicitud de salida.
    /// </summary>
    public event Action<
        RestaurantEditModeFailureReason
    > EditModeExitRejected;

    public bool IsEditModeActive
    {
        get
        {
            return isEditModeActive;
        }
    }

    public int AvailabilityRuleCount
    {
        get
        {
            return availabilityRules.Count;
        }
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        RebuildAvailabilityRules();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        RebuildAvailabilityRules();
    }

    private void OnDisable()
    {
        ShutdownSafely();
    }

    /// <summary>
    /// Intenta activar el modo edición.
    ///
    /// Todas las reglas registradas deben permitir la entrada.
    /// </summary>
    public bool TryEnterEditMode(
        out RestaurantEditModeFailureReason failureReason,
        out string rejectionMessage
    )
    {
        failureReason =
            RestaurantEditModeFailureReason.None;

        rejectionMessage =
            string.Empty;

        if (isEditModeActive)
        {
            failureReason =
                RestaurantEditModeFailureReason
                    .AlreadyActive;

            rejectionMessage =
                "El modo edición ya está activo.";

            EditModeEntryRejected?.Invoke(
                failureReason,
                rejectionMessage
            );

            return false;
        }

        RebuildAvailabilityRules();

        for (int index = 0;
             index < availabilityRules.Count;
             index++)
        {
            IRestaurantEditModeAvailabilityRule rule =
                availabilityRules[index];

            if (rule == null)
            {
                continue;
            }

            string ruleMessage;

            bool ruleAllowsEntry =
                rule.CanEnterEditMode(
                    out ruleMessage
                );

            if (ruleAllowsEntry)
            {
                continue;
            }

            failureReason =
                RestaurantEditModeFailureReason
                    .BlockedByAvailabilityRule;

            if (string.IsNullOrWhiteSpace(ruleMessage))
            {
                rejectionMessage =
                    "El modo edición no está disponible.";
            }
            else
            {
                rejectionMessage =
                    ruleMessage;
            }

            EditModeEntryRejected?.Invoke(
                failureReason,
                rejectionMessage
            );

            return false;
        }

        isEditModeActive = true;

        EditModeEntered?.Invoke();

        return true;
    }

    /// <summary>
    /// Comprueba si el modo edición podría activarse sin modificar
    /// su estado.
    /// </summary>
    public bool CanEnterEditMode(
        out RestaurantEditModeFailureReason failureReason,
        out string rejectionMessage
    )
    {
        failureReason =
            RestaurantEditModeFailureReason.None;

        rejectionMessage =
            string.Empty;

        if (isEditModeActive)
        {
            failureReason =
                RestaurantEditModeFailureReason
                    .AlreadyActive;

            rejectionMessage =
                "El modo edición ya está activo.";

            return false;
        }

        RebuildAvailabilityRules();

        for (int index = 0;
             index < availabilityRules.Count;
             index++)
        {
            IRestaurantEditModeAvailabilityRule rule =
                availabilityRules[index];

            if (rule == null)
            {
                continue;
            }

            string ruleMessage;

            bool ruleAllowsEntry =
                rule.CanEnterEditMode(
                    out ruleMessage
                );

            if (ruleAllowsEntry)
            {
                continue;
            }

            failureReason =
                RestaurantEditModeFailureReason
                    .BlockedByAvailabilityRule;

            if (string.IsNullOrWhiteSpace(ruleMessage))
            {
                rejectionMessage =
                    "El modo edición no está disponible.";
            }
            else
            {
                rejectionMessage =
                    ruleMessage;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Intenta cerrar el modo edición.
    ///
    /// Cuando existe una colocación activa:
    /// - Puede rechazarse la salida.
    /// - Puede cancelarse automáticamente la colocación.
    /// </summary>
    public bool TryExitEditMode(
        bool cancelActivePlacement,
        out RestaurantEditModeFailureReason failureReason
    )
    {
        failureReason =
            RestaurantEditModeFailureReason.None;

        if (!isEditModeActive)
        {
            failureReason =
                RestaurantEditModeFailureReason
                    .NotActive;

            EditModeExitRejected?.Invoke(
                failureReason
            );

            return false;
        }

        if (transactionService != null &&
            transactionService.HasActiveTransaction)
        {
            if (!cancelActivePlacement)
            {
                failureReason =
                    RestaurantEditModeFailureReason
                        .PlacementOperationActive;

                EditModeExitRejected?.Invoke(
                    failureReason
                );

                return false;
            }

            bool cancelled =
                transactionService.CancelPlacement();

            if (!cancelled)
            {
                failureReason =
                    RestaurantEditModeFailureReason
                        .PlacementCancellationFailed;

                EditModeExitRejected?.Invoke(
                    failureReason
                );

                return false;
            }
        }

        isEditModeActive = false;

        EditModeExited?.Invoke();

        return true;
    }

    /// <summary>
    /// Reconstruye la lista de reglas válidas configuradas
    /// desde el Inspector.
    /// </summary>
    public void RebuildAvailabilityRules()
    {
        availabilityRules.Clear();

        if (availabilityRuleSources == null)
        {
            return;
        }

        for (int index = 0;
             index < availabilityRuleSources.Length;
             index++)
        {
            MonoBehaviour source =
                availabilityRuleSources[index];

            if (source == null)
            {
                continue;
            }

            IRestaurantEditModeAvailabilityRule rule =
                source as IRestaurantEditModeAvailabilityRule;

            if (rule == null)
            {
                string interfaceName =
                    nameof(
                        IRestaurantEditModeAvailabilityRule
                    );

                Debug.LogError(
                    source.name +
                    " está configurado como regla de modo " +
                    "edición, pero no implementa " +
                    interfaceName +
                    ".",
                    source
                );

                continue;
            }

            if (availabilityRules.Contains(rule))
            {
                continue;
            }

            availabilityRules.Add(rule);
        }
    }

    /// <summary>
    /// Cancela operaciones provisionales cuando el servicio
    /// se desactiva.
    /// </summary>
    private void ShutdownSafely()
    {
        if (transactionService != null &&
            transactionService.HasActiveTransaction)
        {
            transactionService.CancelPlacement();
        }

        if (!isEditModeActive)
        {
            return;
        }

        isEditModeActive = false;

        EditModeExited?.Invoke();
    }

    /// <summary>
    /// Busca automáticamente las dependencias situadas en el
    /// mismo GameObject.
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

    /// <summary>
    /// Verifica las dependencias obligatorias.
    /// </summary>
    private void ValidateDependencies()
    {
        if (transactionService != null)
        {
            return;
        }

        string serviceName =
            nameof(RestaurantEditModeService);

        string dependencyName =
            nameof(
                RestaurantPlacementTransactionService
            );

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
        RebuildAvailabilityRules();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
        RebuildAvailabilityRules();
    }
#endif
}

/// <summary>
/// Motivo por el que una operación del modo edición no puede
/// completarse.
/// </summary>
public enum RestaurantEditModeFailureReason
{
    None = 0,
    AlreadyActive = 1,
    NotActive = 2,
    BlockedByAvailabilityRule = 3,
    PlacementOperationActive = 4,
    PlacementCancellationFailed = 5
}