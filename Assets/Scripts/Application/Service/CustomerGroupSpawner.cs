using System.Collections;
using UnityEngine;

/// <summary>
/// Genera grupos de clientes durante el servicio utilizando
/// CustomerGroupPrefab como plantilla.
///
/// Cada grupo recibe:
/// - Un identificador único.
/// - Un tamaño aleatorio.
/// - El punto de salida.
/// - Registro en el sistema de mesas.
/// - Registro en la zona física de espera.
/// </summary>
public sealed class CustomerGroupSpawner : MonoBehaviour
{
    [Header("Plantilla")]
    [SerializeField]
    private CustomerGroup customerGroupPrefab;

    [Header("Sistemas")]
    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    [SerializeField]
    private CustomerWaitingAreaSystem customerWaitingAreaSystem;

    [Header("Puntos del restaurante")]
    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Transform restaurantExitPoint;

    [Header("Generación provisional")]
    [SerializeField, Min(1)]
    private int numberOfGroups = 3;

    [SerializeField, Min(0f)]
    private float firstSpawnDelay = 1f;

    [SerializeField, Min(0.1f)]
    private float timeBetweenGroups = 8f;

    [Header("Tamaño de los grupos")]
    [SerializeField, Min(1)]
    private int minimumGroupSize = 1;

    [SerializeField, Min(1)]
    private int maximumGroupSize = 2;

    [Header("Identificación")]
    [SerializeField, Min(1)]
    private int firstGroupId = 1;

    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        spawnRoutine =
            StartCoroutine(SpawnGroupsRoutine());
    }

    private void OnDisable()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    /// <summary>
    /// Genera los grupos dejando un intervalo entre llegadas.
    /// </summary>
    private IEnumerator SpawnGroupsRoutine()
    {
        if (firstSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(
                firstSpawnDelay
            );
        }

        for (int index = 0;
             index < numberOfGroups;
             index++)
        {
            int groupId =
                firstGroupId + index;

            SpawnCustomerGroup(groupId);

            bool isLastGroup =
                index == numberOfGroups - 1;

            if (!isLastGroup)
            {
                yield return new WaitForSeconds(
                    timeBetweenGroups
                );
            }
        }

        Debug.Log(
            $"CustomerGroupSpawner ha generado " +
            $"{numberOfGroups} grupo(s).",
            this
        );

        spawnRoutine = null;
    }

    /// <summary>
    /// Crea y configura una instancia concreta del prefab.
    /// </summary>
    private void SpawnCustomerGroup(
        int groupId
    )
    {
        // Random.Range con enteros no incluye el límite superior.
        int groupSize = Random.Range(
            minimumGroupSize,
            maximumGroupSize + 1
        );

        CustomerGroup newGroup = Instantiate(
            customerGroupPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        newGroup.gameObject.name =
            $"CustomerGroup_{groupId}";

        bool initialized =
            newGroup.Initialize(
                groupId,
                groupSize
            );

        if (!initialized)
        {
            Destroy(newGroup.gameObject);
            return;
        }

        CustomerMovementView movementView =
            newGroup.GetComponent<
                CustomerMovementView
            >();

        if (movementView == null)
        {
            Debug.LogError(
                $"El prefab del grupo {groupId} no contiene " +
                "CustomerMovementView.",
                newGroup
            );

            Destroy(newGroup.gameObject);
            return;
        }

        // La salida pertenece a la escena, por lo que se configura
        // después de instanciar el prefab.
        movementView.ConfigureExitPoint(
            restaurantExitPoint
        );

        bool registeredInTableSystem =
            tableAssignmentSystem.RegisterCustomerGroup(
                newGroup
            );

        if (!registeredInTableSystem)
        {
            Debug.LogError(
                $"No se pudo registrar el grupo {groupId} " +
                "en TableAssignmentSystem.",
                newGroup
            );

            Destroy(newGroup.gameObject);
            return;
        }

        bool registeredInWaitingArea =
            customerWaitingAreaSystem.RegisterCustomerGroup(
                newGroup
            );

        if (!registeredInWaitingArea)
        {
            Debug.LogError(
                $"No se pudo registrar el grupo {groupId} " +
                "en CustomerWaitingAreaSystem.",
                newGroup
            );

            // Deshacemos también el registro anterior para no dejar
            // referencias a un objeto que será destruido.
            tableAssignmentSystem.UnregisterCustomerGroup(
                newGroup
            );

            Destroy(newGroup.gameObject);
            return;
        }

        Debug.Log(
            $"Generado grupo {groupId} de " +
            $"{groupSize} cliente(s).",
            newGroup
        );
    }

    /// <summary>
    /// Valida todas las referencias antes de comenzar a generar grupos.
    /// </summary>
    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (customerGroupPrefab == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita CustomerGroupPrefab.",
                this
            );

            isValid = false;
        }

        if (tableAssignmentSystem == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita TableAssignmentSystem.",
                this
            );

            isValid = false;
        }

        if (customerWaitingAreaSystem == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita " +
                "CustomerWaitingAreaSystem.",
                this
            );

            isValid = false;
        }

        if (spawnPoint == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita un punto de entrada.",
                this
            );

            isValid = false;
        }

        if (restaurantExitPoint == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita RestaurantExitPoint.",
                this
            );

            isValid = false;
        }

        if (minimumGroupSize > maximumGroupSize)
        {
            Debug.LogError(
                "Minimum Group Size no puede ser mayor que " +
                "Maximum Group Size.",
                this
            );

            isValid = false;
        }

        return isValid;
    }
}