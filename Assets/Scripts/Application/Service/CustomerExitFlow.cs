using UnityEngine;

/// <summary>
/// Gestiona el final de la visita de un grupo de clientes.
///
/// Cuando el grupo llega físicamente al punto de salida:
/// - Cambia su estado a Finished.
/// - Libera la mesa que ocupaba.
/// - Deja la mesa en estado Dirty.
/// - Elimina el objeto del grupo después de un pequeño retraso.
/// </summary>
public sealed class CustomerExitFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private CustomerMovementView customerMovementView;

    [Header("Desaparición del grupo")]
    [Tooltip(
        "Tiempo que permanece el grupo en la salida antes de eliminarse."
    )]
    [SerializeField, Min(0f)]
    private float destructionDelay = 0.5f;

    private void Awake()
    {
        // Los componentes normalmente están en el mismo GameObject,
        // por lo que pueden localizarse automáticamente.
        if (customerGroup == null)
            customerGroup = GetComponent<CustomerGroup>();

        if (customerMovementView == null)
        {
            customerMovementView =
                GetComponent<CustomerMovementView>();
        }
    }

    private void OnEnable()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerExitFlow necesita una referencia " +
                "a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        if (customerMovementView == null)
        {
            Debug.LogError(
                "CustomerExitFlow necesita una referencia " +
                "a CustomerMovementView.",
                this
            );

            enabled = false;
            return;
        }

        // Escuchamos la llegada del grupo a cualquier destino.
        // Después comprobaremos si ese destino era realmente la salida.
        customerMovementView.DestinationReached +=
            HandleDestinationReached;
    }

    private void OnDisable()
    {
        if (customerMovementView != null)
        {
            customerMovementView.DestinationReached -=
                HandleDestinationReached;
        }
    }

    /// <summary>
    /// Reacciona cuando el grupo termina uno de sus desplazamientos.
    /// Solamente procesa la llegada si el grupo está abandonando
    /// el restaurante.
    /// </summary>
    private void HandleDestinationReached(
        CustomerMovementView movementView
    )
    {
        // Este evento también se ejecuta cuando el grupo llega a la mesa.
        // Por eso ignoramos cualquier estado distinto de Leaving.
        if (customerGroup.CurrentState !=
            CustomerGroupState.Leaving)
        {
            return;
        }

        CompleteCustomerExit();
    }

    /// <summary>
    /// Finaliza la visita, libera la mesa y programa la eliminación
    /// del objeto visual y lógico del grupo.
    /// </summary>
    private void CompleteCustomerExit()
    {
        RestaurantTable previousTable =
            customerGroup.AssignedTable;

        if (previousTable == null)
        {
            Debug.LogError(
                $"El grupo {customerGroup.GroupId} llegó a la salida, " +
                "pero no tiene ninguna mesa asignada.",
                this
            );

            return;
        }

        int finishedGroupId =
            customerGroup.GroupId;

        int previousTableId =
            previousTable.TableId;

        // El estado Finished provoca que TableAssignmentSystem
        // elimine al grupo de su registro interno.
        customerGroup.SetState(
            CustomerGroupState.Finished
        );

        // ClearAssignedTable también informa a RestaurantTable
        // para que elimine su referencia al grupo.
        customerGroup.ClearAssignedTable();

        // La mesa no vuelve directamente a Free.
        // Primero tendrá que atenderla el sistema de limpieza.
        previousTable.SetState(
            TableState.Dirty
        );

        Debug.Log(
            $"Grupo {finishedGroupId} ha abandonado el restaurante. " +
            $"La mesa {previousTableId} ha quedado sucia.",
            this
        );

        // El objeto ya no participa en la simulación.
        // Se destruye después de un breve retraso para que su salida
        // no resulte visualmente instantánea.
        Destroy(
            gameObject,
            destructionDelay
        );
    }
}