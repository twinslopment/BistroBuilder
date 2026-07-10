using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Organiza físicamente a los grupos que esperan una mesa.
///
/// Los grupos se colocan por orden de llegada en los puntos de espera.
/// Cuando el primer grupo abandona la cola para dirigirse a una mesa,
/// los demás avanzan automáticamente una posición.
/// </summary>
public sealed class CustomerWaitingAreaSystem : MonoBehaviour
{
    [Header("Posiciones de espera")]
    [SerializeField]
    private Transform[] waitingPoints;

    // Grupos conocidos por este sistema.
    private readonly List<CustomerGroup> registeredGroups =
        new();

    // Grupos que actualmente están esperando una mesa.
    // El orden de esta lista representa el orden de la cola.
    private readonly List<CustomerGroup> waitingGroups =
        new();

    // Guarda el punto ocupado actualmente por cada grupo.
    private readonly Dictionary<CustomerGroup, Transform>
        assignedWaitingPoints = new();

    private void Start()
    {
        ValidateConfiguration();
    }

    private void OnDisable()
    {
        // Eliminamos las suscripciones para evitar referencias antiguas
        // cuando se desactive el sistema o se cambie de escena.
        foreach (CustomerGroup customerGroup in registeredGroups)
        {
            if (customerGroup != null)
            {
                customerGroup.StateChanged -=
                    HandleCustomerGroupStateChanged;
            }
        }

        registeredGroups.Clear();
        waitingGroups.Clear();
        assignedWaitingPoints.Clear();
    }

    /// <summary>
    /// Registra un grupo creado dinámicamente.
    ///
    /// A partir de ese momento, el sistema podrá reaccionar cuando
    /// el grupo empiece o deje de esperar una mesa.
    /// </summary>
    public bool RegisterCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null)
            return false;

        if (registeredGroups.Contains(customerGroup))
            return false;

        registeredGroups.Add(customerGroup);

        customerGroup.StateChanged +=
            HandleCustomerGroupStateChanged;

        // Esta comprobación permite registrar también grupos
        // que ya estuvieran esperando cuando entran en el sistema.
        if (customerGroup.CurrentState ==
            CustomerGroupState.WaitingForTable)
        {
            AddGroupToWaitingQueue(customerGroup);
        }

        return true;
    }

    /// <summary>
    /// Elimina un grupo del sistema y libera su posición de espera.
    /// </summary>
    public bool UnregisterCustomerGroup(
        CustomerGroup customerGroup
    )
    {
        if (customerGroup == null)
            return false;

        if (!registeredGroups.Remove(customerGroup))
            return false;

        customerGroup.StateChanged -=
            HandleCustomerGroupStateChanged;

        bool wasWaiting =
            waitingGroups.Remove(customerGroup);

        assignedWaitingPoints.Remove(customerGroup);

        if (wasWaiting)
            ReorganizeWaitingQueue();

        return true;
    }

    /// <summary>
    /// Reacciona a los cambios de estado del grupo.
    /// </summary>
    private void HandleCustomerGroupStateChanged(
        CustomerGroup customerGroup,
        CustomerGroupState newState
    )
    {
        if (customerGroup == null)
            return;

        if (newState ==
            CustomerGroupState.WaitingForTable)
        {
            // TableAssignmentSystem puede haber asignado una mesa
            // inmediatamente durante el mismo evento.
            // Comprobamos el estado real antes de enviarlo a la cola.
            if (customerGroup.CurrentState ==
                CustomerGroupState.WaitingForTable)
            {
                AddGroupToWaitingQueue(customerGroup);
            }

            return;
        }

        // Cualquier estado diferente implica que el grupo ya no debe
        // ocupar una posición de espera.
        bool wasWaiting =
            waitingGroups.Remove(customerGroup);

        assignedWaitingPoints.Remove(customerGroup);

        if (wasWaiting)
            ReorganizeWaitingQueue();

        if (newState == CustomerGroupState.Finished)
        {
            UnregisterCustomerGroup(customerGroup);
        }
    }

    /// <summary>
    /// Añade un grupo al final de la cola de espera.
    /// </summary>
    private void AddGroupToWaitingQueue(
        CustomerGroup customerGroup
    )
    {
        if (waitingGroups.Contains(customerGroup))
            return;

        waitingGroups.Add(customerGroup);

        ReorganizeWaitingQueue();

        if (assignedWaitingPoints.ContainsKey(customerGroup))
        {
            Debug.Log(
                $"Grupo {customerGroup.GroupId} ocupa una " +
                "posición en la zona de espera.",
                customerGroup
            );
        }
        else
        {
            Debug.Log(
                $"No hay una posición física de espera disponible " +
                $"para el grupo {customerGroup.GroupId}.",
                customerGroup
            );
        }
    }

    /// <summary>
    /// Reasigna los puntos de espera según el orden actual de la cola.
    ///
    /// Cuando sale el primer grupo, los demás avanzan una posición.
    /// </summary>
    private void ReorganizeWaitingQueue()
    {
        // Eliminamos referencias destruidas o grupos que ya no esperan.
        for (int index = waitingGroups.Count - 1;
             index >= 0;
             index--)
        {
            CustomerGroup customerGroup =
                waitingGroups[index];

            if (customerGroup == null ||
                customerGroup.CurrentState !=
                    CustomerGroupState.WaitingForTable)
            {
                waitingGroups.RemoveAt(index);
            }
        }

        Dictionary<CustomerGroup, Transform>
            previousAssignments =
                new(assignedWaitingPoints);

        assignedWaitingPoints.Clear();

        int groupIndex = 0;

        if (waitingPoints == null)
            return;

        for (int pointIndex = 0;
             pointIndex < waitingPoints.Length &&
             groupIndex < waitingGroups.Count;
             pointIndex++)
        {
            Transform waitingPoint =
                waitingPoints[pointIndex];

            // Los puntos nulos se ignoran sin romper el resto de la cola.
            if (waitingPoint == null)
                continue;

            CustomerGroup customerGroup =
                waitingGroups[groupIndex];

            groupIndex++;

            assignedWaitingPoints[
                customerGroup
            ] = waitingPoint;

            bool alreadyAtSamePoint =
                previousAssignments.TryGetValue(
                    customerGroup,
                    out Transform previousPoint
                ) &&
                previousPoint == waitingPoint;

            if (alreadyAtSamePoint)
                continue;

            CustomerMovementView movementView =
                customerGroup.GetComponent<
                    CustomerMovementView
                >();

            if (movementView == null)
            {
                Debug.LogError(
                    $"El grupo {customerGroup.GroupId} no contiene " +
                    "CustomerMovementView.",
                    customerGroup
                );

                continue;
            }

            movementView.MoveToWaitingPoint(
                waitingPoint
            );
        }
    }

    /// <summary>
    /// Comprueba que exista al menos una posición utilizable.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (waitingPoints == null ||
            waitingPoints.Length == 0)
        {
            Debug.LogError(
                "CustomerWaitingAreaSystem no tiene " +
                "puntos de espera configurados.",
                this
            );

            return;
        }

        int validPointCount = 0;

        foreach (Transform waitingPoint in waitingPoints)
        {
            if (waitingPoint != null)
                validPointCount++;
        }

        if (validPointCount == 0)
        {
            Debug.LogError(
                "CustomerWaitingAreaSystem no tiene ningún " +
                "punto de espera válido.",
                this
            );
        }
    }
}