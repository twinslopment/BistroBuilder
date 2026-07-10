using System.Collections;
using UnityEngine;

/// <summary>
/// Genera grupos de clientes durante el servicio utilizando
/// CustomerGroupPrefab como plantilla.
///
/// Cada grupo recibe:
/// - Un identificador único.
/// - Un tamaño aleatorio.
/// - El punto de salida del restaurante.
/// - El registro en TableAssignmentSystem.
///
/// Los tiempos utilizan WaitForSeconds, por lo que respetan
/// la pausa y las velocidades x1, x2 y x3 del juego.
/// </summary>
public sealed class CustomerGroupSpawner : MonoBehaviour
{
    [Header("Plantilla")]
    [SerializeField]
    private CustomerGroup customerGroupPrefab;

    [Header("Sistemas")]
    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

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
        // No iniciamos la generación si falta alguna referencia esencial.
        // Así evitamos crear grupos incompletos o imposibles de gestionar.
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
        // Si el componente se desactiva, detenemos la generación pendiente.
        // Los grupos ya creados continúan funcionando normalmente.
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    /// <summary>
    /// Genera la cantidad configurada de grupos dejando un intervalo
    /// entre una llegada y la siguiente.
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
    private void SpawnCustomerGroup(int groupId)
    {
        // Random.Range con enteros no incluye el límite superior.
        // Por eso sumamos uno para permitir también maximumGroupSize.
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
            Debug.LogError(
                $"No se pudo inicializar el grupo {groupId}.",
                this
            );

            Destroy(newGroup.gameObject);
            return;
        }

        CustomerMovementView movementView =
            newGroup.GetComponent<CustomerMovementView>();

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

        // El prefab no puede mantener una referencia directa
        // a un objeto de la escena. El generador se la proporciona
        // inmediatamente después de crear la instancia.
        movementView.ConfigureExitPoint(
            restaurantExitPoint
        );

        bool registered =
            tableAssignmentSystem.RegisterCustomerGroup(
                newGroup
            );

        if (!registered)
        {
            Debug.LogError(
                $"No se pudo registrar el grupo {groupId} " +
                "en TableAssignmentSystem.",
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
    /// Comprueba que el generador tiene todos los datos necesarios
    /// antes de comenzar.
    /// </summary>
    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (customerGroupPrefab == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita " +
                "CustomerGroupPrefab.",
                this
            );

            isValid = false;
        }

        if (tableAssignmentSystem == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita una referencia " +
                "a TableAssignmentSystem.",
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
                "CustomerGroupSpawner necesita " +
                "RestaurantExitPoint.",
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