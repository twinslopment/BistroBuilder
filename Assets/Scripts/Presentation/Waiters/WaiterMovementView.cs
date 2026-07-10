using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Representa visualmente el movimiento de un camarero.
///
/// Traduce los estados operativos del camarero en desplazamientos
/// hacia mesas o cocina y notifica cuándo se alcanza el destino.
///
/// También resuelve correctamente las asignaciones cuyo destino
/// ya está alcanzado, evitando que una tarea quede bloqueada
/// esperando un movimiento de distancia cero.
/// </summary>
public sealed class WaiterMovementView : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private KitchenSystem kitchenSystem;

    [Header("Movimiento")]

    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2.5f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    /// <summary>
    /// Se emite una sola vez cuando el camarero alcanza
    /// el destino correspondiente a su tarea actual.
    /// </summary>
    public event Action<WaiterMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    /// <summary>
    /// Destino de la solicitud de movimiento actualmente activa.
    /// </summary>
    private Transform currentDestination;

    private bool isMoving;

    /// <summary>
    /// Corrutina utilizada cuando el camarero ya está dentro
    /// de la distancia de llegada al recibir una nueva tarea.
    ///
    /// La notificación se retrasa un frame para que el sistema
    /// de tareas termine primero de registrar la asignación.
    /// </summary>
    private Coroutine deferredArrivalRoutine;

    /// <summary>
    /// Identificador interno de la solicitud de movimiento.
    ///
    /// Permite descartar una llegada diferida si, antes de ejecutarse,
    /// el camarero ha recibido otro destino.
    /// </summary>
    private uint movementRequestVersion;

    private void Awake()
    {
        if (waiter == null)
        {
            waiter = GetComponent<Waiter>();
        }
    }

    private void OnEnable()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una referencia a Waiter.",
                this
            );

            enabled = false;
            return;
        }

        waiter.StateChanged += HandleWaiterStateChanged;
    }

    private void OnDisable()
    {
        if (waiter != null)
        {
            waiter.StateChanged -= HandleWaiterStateChanged;
        }

        CancelDeferredArrival();

        currentDestination = null;
        isMoving = false;
        HasReachedDestination = false;

        movementRequestVersion++;
    }

    private void Update()
    {
        if (!isMoving ||
            currentDestination == null)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            currentDestination.position,
            movementSpeed * Time.deltaTime
        );

        TryCompleteCurrentMovement();
    }

    /// <summary>
    /// Traduce los estados de desplazamiento del camarero
    /// en destinos físicos de la escena.
    ///
    /// Los estados que no implican movimiento se ignoran.
    /// No se cancela aquí un movimiento previo porque los eventos
    /// de estado pueden producir asignaciones anidadas durante
    /// el cambio del camarero a Idle.
    /// </summary>
    private void HandleWaiterStateChanged(
        Waiter changedWaiter,
        WaiterState newState
    )
    {
        Transform destination = newState switch
        {
            WaiterState.WalkingToTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToKitchen =>
                GetKitchenPickupPoint(),

            WaiterState.WalkingToServeTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToBill =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToCleanTable =>
                GetTableServicePoint(changedWaiter),

            _ => null
        };

        if (destination == null)
        {
            return;
        }

        BeginMovement(destination);
    }

    /// <summary>
    /// Obtiene el punto de servicio de la mesa
    /// actualmente asignada al camarero.
    /// </summary>
    private Transform GetTableServicePoint(
        Waiter changedWaiter
    )
    {
        RestaurantTable assignedTable =
            changedWaiter.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El camarero {changedWaiter.WaiterId} " +
                "no tiene mesa asignada.",
                this
            );

            return null;
        }

        if (assignedTable.WaiterServicePoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} " +
                "no tiene WaiterServicePoint.",
                assignedTable
            );

            return null;
        }

        return assignedTable.WaiterServicePoint;
    }

    /// <summary>
    /// Obtiene el punto de recogida de comandas de cocina.
    /// </summary>
    private Transform GetKitchenPickupPoint()
    {
        if (kitchenSystem == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una referencia " +
                "a KitchenSystem para ir a cocina.",
                this
            );

            return null;
        }

        if (kitchenSystem.PickupPoint == null)
        {
            Debug.LogError(
                "KitchenSystem no tiene PickupPoint asignado.",
                kitchenSystem
            );

            return null;
        }

        return kitchenSystem.PickupPoint;
    }

    /// <summary>
    /// Inicia una nueva solicitud de movimiento.
    ///
    /// Si el camarero ya está en el destino, programa la llegada
    /// para el siguiente frame en lugar de dejarla depender
    /// exclusivamente del desplazamiento visual.
    /// </summary>
    private void BeginMovement(
        Transform destination
    )
    {
        CancelDeferredArrival();

        currentDestination = destination;
        HasReachedDestination = false;
        isMoving = true;

        movementRequestVersion++;

        if (!IsWithinArrivalDistance(destination))
        {
            return;
        }

        uint requestedVersion =
            movementRequestVersion;

        deferredArrivalRoutine =
            StartCoroutine(
                CompleteArrivalNextFrame(
                    requestedVersion,
                    destination
                )
            );
    }

    /// <summary>
    /// Completa una llegada de distancia cero en el siguiente frame.
    ///
    /// El retraso evita que DestinationReached se emita dentro
    /// de la propia llamada que todavía está asignando la tarea.
    /// </summary>
    private IEnumerator CompleteArrivalNextFrame(
        uint requestedVersion,
        Transform expectedDestination
    )
    {
        yield return null;

        deferredArrivalRoutine = null;

        bool requestIsStillValid =
            isMoving &&
            requestedVersion == movementRequestVersion &&
            currentDestination == expectedDestination &&
            expectedDestination != null;

        if (!requestIsStillValid)
        {
            yield break;
        }

        // El destino podría haberse desplazado durante el frame.
        // En ese caso, el movimiento normal continuará en Update.
        if (!IsWithinArrivalDistance(expectedDestination))
        {
            yield break;
        }

        CompleteMovement();
    }

    /// <summary>
    /// Comprueba en cada frame si el destino activo
    /// ya se encuentra dentro del margen de llegada.
    /// </summary>
    private void TryCompleteCurrentMovement()
    {
        if (currentDestination == null)
        {
            return;
        }

        if (!IsWithinArrivalDistance(currentDestination))
        {
            return;
        }

        CompleteMovement();
    }

    /// <summary>
    /// Comprueba la distancia utilizando magnitud al cuadrado
    /// para evitar calcular una raíz cuadrada en cada frame.
    /// </summary>
    private bool IsWithinArrivalDistance(
        Transform destination
    )
    {
        if (destination == null)
        {
            return false;
        }

        float distanceSquared =
            (transform.position - destination.position)
            .sqrMagnitude;

        float arrivalDistanceSquared =
            arrivalDistance * arrivalDistance;

        return distanceSquared <= arrivalDistanceSquared;
    }

    /// <summary>
    /// Finaliza la solicitud actual y notifica a los flujos
    /// operativos que el camarero ha llegado.
    ///
    /// El movimiento se cierra antes de emitir el evento para
    /// impedir notificaciones duplicadas.
    /// </summary>
    private void CompleteMovement()
    {
        if (!isMoving ||
            currentDestination == null)
        {
            return;
        }

        CancelDeferredArrival();

        Transform reachedDestination =
            currentDestination;

        transform.position =
            reachedDestination.position;

        currentDestination = null;
        isMoving = false;
        HasReachedDestination = true;

        movementRequestVersion++;

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha llegado a su destino.",
            this
        );

        DestinationReached?.Invoke(this);
    }

    /// <summary>
    /// Cancela únicamente una llegada diferida pendiente.
    /// No modifica una solicitud de movimiento válida.
    /// </summary>
    private void CancelDeferredArrival()
    {
        if (deferredArrivalRoutine == null)
        {
            return;
        }

        StopCoroutine(deferredArrivalRoutine);
        deferredArrivalRoutine = null;
    }
}