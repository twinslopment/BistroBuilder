using System;
using UnityEngine;

/// <summary>
/// Controla el desplazamiento visual de un grupo de clientes.
///
/// Puede mover al grupo hacia:
/// - Una posición de espera.
/// - La mesa asignada.
/// - La salida del restaurante.
///
/// La lógica del grupo permanece en CustomerGroup. Esta clase solamente
/// representa físicamente sus desplazamientos por la escena.
/// </summary>
public sealed class CustomerMovementView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private Transform restaurantExitPoint;

    [Header("Movimiento")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    /// <summary>
    /// Se ejecuta cuando el grupo llega al destino actual.
    ///
    /// Los distintos flujos comprueban el estado del grupo para saber
    /// si ha llegado a una mesa, a una posición de espera o a la salida.
    /// </summary>
    public event Action<CustomerMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    private Transform currentDestination;
    private bool isMoving;

    private void Awake()
    {
        if (customerGroup == null)
        {
            customerGroup =
                GetComponent<CustomerGroup>();
        }
    }

    private void OnEnable()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerMovementView necesita una referencia " +
                "a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        customerGroup.StateChanged +=
            HandleStateChanged;
    }

    private void OnDisable()
    {
        if (customerGroup != null)
        {
            customerGroup.StateChanged -=
                HandleStateChanged;
        }
    }

    private void Update()
    {
        if (!isMoving || currentDestination == null)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            currentDestination.position,
            movementSpeed * Time.deltaTime
        );

        float remainingDistance = Vector3.Distance(
            transform.position,
            currentDestination.position
        );

        if (remainingDistance > arrivalDistance)
            return;

        CompleteMovement();
    }

    /// <summary>
    /// Configura el punto de salida después de crear el grupo desde
    /// un prefab.
    ///
    /// El prefab no puede guardar directamente una referencia
    /// a un objeto perteneciente a una escena concreta.
    /// </summary>
    public void ConfigureExitPoint(
        Transform exitPoint
    )
    {
        if (exitPoint == null)
        {
            Debug.LogError(
                "No se puede configurar un punto de salida nulo.",
                this
            );

            return;
        }

        restaurantExitPoint = exitPoint;
    }

    /// <summary>
    /// Envía al grupo hacia una posición de la zona de espera.
    ///
    /// Solamente puede utilizarse mientras el grupo se encuentra
    /// esperando una mesa.
    /// </summary>
    public bool MoveToWaitingPoint(
        Transform waitingPoint
    )
    {
        if (waitingPoint == null)
        {
            Debug.LogError(
                "No se puede mover un grupo hacia un punto de espera nulo.",
                this
            );

            return false;
        }

        if (customerGroup == null ||
            customerGroup.CurrentState !=
                CustomerGroupState.WaitingForTable)
        {
            return false;
        }

        BeginMovement(waitingPoint);
        return true;
    }

    /// <summary>
    /// Reacciona a los estados que implican un desplazamiento automático.
    /// </summary>
    private void HandleStateChanged(
        CustomerGroup group,
        CustomerGroupState newState
    )
    {
        Transform destination = newState switch
        {
            CustomerGroupState.WalkingToTable =>
                GetTableDestination(group),

            CustomerGroupState.Leaving =>
                GetExitDestination(),

            _ => null
        };

        if (destination == null)
            return;

        // Si el grupo estaba caminando hacia una posición de espera,
        // el nuevo destino sustituye inmediatamente al anterior.
        BeginMovement(destination);
    }

    /// <summary>
    /// Obtiene el punto de aproximación de la mesa asignada.
    /// </summary>
    private Transform GetTableDestination(
        CustomerGroup group
    )
    {
        RestaurantTable assignedTable =
            group.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El grupo {group.GroupId} no tiene " +
                "una mesa asignada.",
                this
            );

            return null;
        }

        if (assignedTable.CustomerApproachPoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} no tiene " +
                "CustomerApproachPoint.",
                assignedTable
            );

            return null;
        }

        return assignedTable.CustomerApproachPoint;
    }

    /// <summary>
    /// Obtiene el punto por el que los clientes abandonan el restaurante.
    /// </summary>
    private Transform GetExitDestination()
    {
        if (restaurantExitPoint == null)
        {
            Debug.LogError(
                "CustomerMovementView necesita RestaurantExitPoint.",
                this
            );

            return null;
        }

        return restaurantExitPoint;
    }

    /// <summary>
    /// Inicia un desplazamiento hacia el destino indicado.
    /// </summary>
    private void BeginMovement(
        Transform destination
    )
    {
        currentDestination = destination;
        HasReachedDestination = false;
        isMoving = true;
    }

    /// <summary>
    /// Finaliza el desplazamiento y avisa a los flujos interesados.
    /// </summary>
    private void CompleteMovement()
    {
        transform.position =
            currentDestination.position;

        currentDestination = null;
        isMoving = false;
        HasReachedDestination = true;

        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha llegado a su destino.",
            this
        );

        DestinationReached?.Invoke(this);
    }
}