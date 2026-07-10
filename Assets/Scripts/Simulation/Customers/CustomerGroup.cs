using System;
using UnityEngine;

/// <summary>
/// Representa un grupo de clientes durante toda su visita al restaurante.
///
/// Esta clase conserva los datos principales del grupo:
/// - Su identificador.
/// - El número de personas.
/// - Su estado actual.
/// - La mesa que tiene asignada.
/// - El tiempo que lleva esperando.
///
/// También comunica los cambios de estado al resto de sistemas mediante
/// el evento StateChanged.
/// </summary>
public sealed class CustomerGroup : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int groupId = 1;

    [Header("Configuración")]
    [SerializeField, Min(1)]
    private int groupSize = 2;

    [Header("Estado actual")]
    [SerializeField]
    private CustomerGroupState currentState =
        CustomerGroupState.Entering;

    [Header("Asignación actual")]
    [SerializeField]
    private RestaurantTable assignedTable;

    [Header("Tiempo de espera")]
    [SerializeField, Min(0f)]
    private float waitingTime;

    /// <summary>
    /// Se ejecuta cada vez que el grupo cambia de estado.
    /// Permite que otros sistemas reaccionen sin consultar continuamente
    /// el estado del grupo.
    /// </summary>
    public event Action<CustomerGroup, CustomerGroupState>
        StateChanged;

    public int GroupId => groupId;

    public int GroupSize => groupSize;

    public CustomerGroupState CurrentState => currentState;

    public RestaurantTable AssignedTable => assignedTable;

    public float WaitingTime => waitingTime;

    /// <summary>
    /// Indica si el grupo ya tiene una mesa asignada.
    /// </summary>
    public bool HasAssignedTable => assignedTable != null;

    private void Update()
    {
        // El tiempo de espera solamente aumenta mientras el grupo
        // está esperando a que el restaurante le asigne una mesa.
        if (currentState ==
            CustomerGroupState.WaitingForTable)
        {
            waitingTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// Configura una nueva instancia creada a partir del prefab.
    ///
    /// El generador utilizará este método para dar a cada grupo
    /// un identificador y un tamaño diferentes.
    /// </summary>
    public bool Initialize(
        int newGroupId,
        int newGroupSize
    )
    {
        if (newGroupId < 1)
        {
            Debug.LogError(
                "El identificador del grupo debe ser mayor que cero.",
                this
            );

            return false;
        }

        if (newGroupSize < 1)
        {
            Debug.LogError(
                "El tamaño del grupo debe ser mayor que cero.",
                this
            );

            return false;
        }

        // Una nueva instancia no debería llegar aquí con una mesa
        // asignada. Esta comprobación evita reutilizar incorrectamente
        // un grupo que ya está participando en el servicio.
        if (assignedTable != null)
        {
            Debug.LogError(
                "No se puede inicializar un grupo que ya tiene " +
                "una mesa asignada.",
                this
            );

            return false;
        }

        groupId = newGroupId;
        groupSize = newGroupSize;

        // Todo grupo generado comienza entrando al restaurante
        // y sin haber acumulado tiempo de espera.
        waitingTime = 0f;
        currentState = CustomerGroupState.Entering;

        Debug.Log(
            $"Grupo {groupId} configurado con " +
            $"{groupSize} cliente(s).",
            this
        );

        return true;
    }

    /// <summary>
    /// Cambia el estado actual del grupo y notifica el cambio
    /// a todos los sistemas suscritos.
    /// </summary>
    public void SetState(CustomerGroupState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log(
            $"Grupo {groupId}: estado cambiado a {currentState}.",
            this
        );

        StateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Intenta asignar una mesa al grupo.
    ///
    /// La propia mesa valida que esté libre y que tenga capacidad
    /// suficiente para el número de clientes.
    /// </summary>
    public bool AssignTable(RestaurantTable table)
    {
        if (table == null)
        {
            Debug.LogError(
                $"No se puede asignar una mesa nula " +
                $"al grupo {groupId}.",
                this
            );

            return false;
        }

        if (assignedTable != null)
        {
            Debug.LogWarning(
                $"El grupo {groupId} ya tiene la mesa " +
                $"{assignedTable.TableId} asignada.",
                this
            );

            return false;
        }

        bool tableAcceptedGroup =
            table.TryAssignCustomerGroup(this);

        if (!tableAcceptedGroup)
            return false;

        assignedTable = table;

        Debug.Log(
            $"Grupo {groupId} asignado a mesa {table.TableId}.",
            this
        );

        return true;
    }

    /// <summary>
    /// Libera la mesa ocupada por el grupo.
    ///
    /// Se utiliza cuando el grupo abandona el restaurante,
    /// antes de que la mesa pase al estado Dirty.
    /// </summary>
    public void ClearAssignedTable()
    {
        if (assignedTable == null)
            return;

        RestaurantTable previousTable =
            assignedTable;

        // Se elimina primero la referencia del grupo para evitar
        // mantener una asignación antigua durante la liberación.
        assignedTable = null;

        previousTable.ReleaseCustomerGroup(this);

        Debug.Log(
            $"Grupo {groupId}: mesa asignada liberada.",
            this
        );
    }

    /// <summary>
    /// Reinicia el contador de espera.
    ///
    /// Se llama cuando el grupo recibe una mesa, porque deja de formar
    /// parte de la cola de entrada.
    /// </summary>
    public void ResetWaitingTime()
    {
        waitingTime = 0f;
    }
}