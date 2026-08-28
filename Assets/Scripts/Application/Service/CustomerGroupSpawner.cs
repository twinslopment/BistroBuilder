using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera grupos de clientes durante un servicio abierto utilizando
/// CustomerGroupPrefab como plantilla.
///
/// La generación ya no comienza simplemente al habilitar el componente.
/// Solo se inicia cuando RestaurantServiceStateService está en Open.
///
/// Cada grupo recibe:
/// - Un identificador único.
/// - Un tamaño aleatorio.
/// - El punto de salida.
/// - Registro en el sistema de mesas.
/// - Registro en la zona física de espera.
/// </summary>
public sealed class CustomerGroupSpawner :
    MonoBehaviour
{
    [Header("Plantilla")]

    [SerializeField]
    private CustomerGroup customerGroupPrefab;

    [Header("Sistemas")]

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    [SerializeField]
    private CustomerWaitingAreaSystem customerWaitingAreaSystem;

    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Tooltip(
        "Estado operativo que decide cuándo pueden llegar clientes."
    )]
    [SerializeField]
    private RestaurantServiceStateService
        serviceStateService;

    [Header("Puntos del restaurante")]

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Transform restaurantExitPoint;

    [Header("Generación provisional")]

    [SerializeField]
    [Min(1)]
    private int numberOfGroups = 3;

    [SerializeField]
    [Min(0f)]
    private float firstSpawnDelay = 1f;

    [SerializeField]
    [Min(0.1f)]
    private float timeBetweenGroups = 8f;

    [Header("Tamaño de los grupos")]

    [SerializeField]
    [Min(1)]
    private int minimumGroupSize = 1;

    [SerializeField]
    [Min(1)]
    private int maximumGroupSize = 2;

    [Header("Modalidades de servicio 367H")]

    [Tooltip(
        "Probabilidad de que un grupo de una sola persona elija consumir " +
        "exclusivamente en barra."
    )]
    [SerializeField, Range(0f, 1f)]
    private float barServiceProbability = 0.15f;

    [Tooltip(
        "Probabilidad adicional de que un grupo espere mesa consumiendo " +
        "en barra cuando exista una plaza compatible."
    )]
    [SerializeField, Range(0f, 1f)]
    private float waitingAtBarProbability = 0.25f;

    [Header("Identificación")]

    [SerializeField]
    [Min(1)]
    private int firstGroupId = 1;

    private Coroutine spawnRoutine;

    private int nextGroupId;

    private bool configurationIsValid;

    // Secuencia opcional de tamaños usada exclusivamente por herramientas de
    // diagnóstico en Play Mode. Permanece vacía en la simulación normal.
    private readonly Queue<int> diagnosticGroupSizes = new Queue<int>();
    private readonly Queue<BistroBuilderServiceMode>
        diagnosticServiceModes =
            new Queue<BistroBuilderServiceMode>();

    private readonly Queue<PlannedArrival> plannedArrivals =
        new Queue<PlannedArrival>();

    // Plan externo y genérico para el siguiente servicio. El Spawner sigue
    // siendo la única autoridad que materializa CustomerGroup.
    private BistroBuilderCustomerDemandPlan queuedDemandPlan;
    private string lastConsumedDemandPlanId = string.Empty;
    private int lastPlannedGroupCount;

    private bool spawnScheduleInitialized;
    private bool spawnScheduleCompleted;
    private bool restoredScheduleAwaitingServiceActivation;
    private float secondsUntilNextArrival;

    public int NextGroupId => Mathf.Max(1, nextGroupId);
    public int PendingArrivalCount => plannedArrivals.Count;
    public bool HasInitializedSpawnSchedule => spawnScheduleInitialized;
    public bool HasCompletedSpawnSchedule => spawnScheduleCompleted;
    public int BaselineGroupCount => Mathf.Max(1, numberOfGroups);
    public int LastPlannedGroupCount => lastPlannedGroupCount;
    public string LastConsumedDemandPlanId => lastConsumedDemandPlanId;
    public bool HasQueuedDemandPlan => queuedDemandPlan != null;

    /// <summary>
    /// Cola un plan genérico para el siguiente servicio. Se acepta únicamente
    /// antes de que exista un calendario activo para no reescribir llegadas
    /// que ya forman parte del runtime persistible.
    /// </summary>
    public bool TryQueueDemandPlanForNextService(
        BistroBuilderCustomerDemandPlan plan,
        out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();
        if (plan == null || !plan.TryValidate(out error))
            return false;

        if (serviceStateService != null &&
            serviceStateService.AcceptsNewCustomers)
        {
            error = "No puede sustituirse el plan de demanda con el servicio abierto.";
            return false;
        }

        if (spawnScheduleInitialized && !spawnScheduleCompleted)
        {
            error = "Ya existe un calendario de llegadas activo.";
            return false;
        }

        queuedDemandPlan = plan.DeepClone();
        error = string.Empty;
        return true;
    }

    public bool TryGetQueuedDemandPlan(out BistroBuilderCustomerDemandPlan plan)
    {
        plan = queuedDemandPlan != null ? queuedDemandPlan.DeepClone() : null;
        return plan != null;
    }

    /// <summary>
    /// Crea un único grupo de mesa solicitado por una integración externa.
    /// Reutiliza exactamente el pipeline normal de prefab, registros y flujo
    /// de llegada; no altera el calendario aleatorio de clientes del servicio.
    /// </summary>
    public bool TrySpawnExternalTableServiceGroup(
        int groupSize,
        out CustomerGroup group,
        out string error)
    {
        group = null;
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (!isActiveAndEnabled || !configurationIsValid ||
            serviceStateService == null || !serviceStateService.AcceptsNewCustomers)
        {
            error = "El generador no está preparado para una llegada externa.";
            return false;
        }

        if (groupSize < 1)
        {
            error = "El grupo externo necesita al menos un cliente.";
            return false;
        }

        int groupId = nextGroupId;
        nextGroupId++;
        SpawnCustomerGroup(groupId, groupSize, BistroBuilderServiceMode.TableService);

        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int index = 0; index < groups.Count; index++)
        {
            CustomerGroup candidate = groups[index];
            if (candidate != null && candidate.GroupId == groupId)
            {
                group = candidate;
                return true;
            }
        }

        error = "El pipeline canónico no pudo materializar el grupo externo.";
        return false;
    }

    public void StopForRuntimeLoad()
    {
        StopSpawning();
        ClearSpawnSchedule();
        diagnosticGroupSizes.Clear();
        diagnosticServiceModes.Clear();
        queuedDemandPlan = null;
    }

    public void UnregisterAndDestroyGroupForRuntimeLoad(CustomerGroup group)
    {
        if (group == null)
        {
            return;
        }

        barServiceSystem?.UnregisterCustomerGroupForRuntimeLoad(group);
        customerWaitingAreaSystem?.UnregisterCustomerGroup(group);
        tableAssignmentSystem?.UnregisterCustomerGroup(group);

        if (group.HasAssignedTable)
        {
            group.ClearAssignedTable();
        }

        if (group.HasAssignedBarSpot)
        {
            BistroBuilderBarServiceRegistry registry =
                FindFirstObjectByType<BistroBuilderBarServiceRegistry>();

            if (registry != null)
            {
                registry.ReleaseGroup(group);
            }
            else
            {
                group.TryReleaseBarSpot();
            }
        }

        group.gameObject.SetActive(false);
        Destroy(group.gameObject);
    }

    public bool TryCreateRestoredGroup(
        BistroBuilderCustomerGroupSaveRecord record,
        out CustomerGroup group,
        out string error
    )
    {
        group = null;
        error = string.Empty;

        if (record == null || !record.TryValidate(out error) ||
            customerGroupPrefab == null || spawnPoint == null ||
            restaurantExitPoint == null || tableAssignmentSystem == null ||
            customerWaitingAreaSystem == null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "El generador no puede reconstruir grupos.";
            }
            return false;
        }

        CustomerGroup candidate = Instantiate(
            customerGroupPrefab,
            record.worldPosition.ToVector3(),
            record.worldRotation.ToQuaternion()
        );
        candidate.gameObject.name = "CustomerGroup_" + record.groupId;

        if (!candidate.TryRestoreRuntimeIdentity(
                record.groupId,
                record.groupSize,
                (BistroBuilderServiceMode)record.requestedServiceMode,
                record.waitingTime,
                out error
            ))
        {
            Destroy(candidate.gameObject);
            return false;
        }

        BistroBuilderCustomerAcquisitionTag acquisitionTag =
            candidate.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        if (acquisitionTag == null)
            acquisitionTag = candidate.gameObject.AddComponent<
                BistroBuilderCustomerAcquisitionTag>();
        BistroBuilderCustomerAcquisitionProfile acquisition =
            record.acquisition != null
                ? record.acquisition.DeepClone()
                : BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        if (!acquisitionTag.TryConfigure(acquisition, out error))
        {
            Destroy(candidate.gameObject);
            return false;
        }

        CustomerMovementView movement =
            candidate.GetComponent<CustomerMovementView>();

        if (movement == null)
        {
            error = "El prefab restaurado no contiene CustomerMovementView.";
            Destroy(candidate.gameObject);
            return false;
        }

        movement.ConfigureExitPoint(restaurantExitPoint);

        if (!tableAssignmentSystem.RegisterCustomerGroup(candidate) ||
            !customerWaitingAreaSystem.RegisterCustomerGroup(candidate) ||
            (barServiceSystem != null &&
             !barServiceSystem.RegisterCustomerGroup(candidate)))
        {
            customerWaitingAreaSystem.UnregisterCustomerGroup(candidate);
            tableAssignmentSystem.UnregisterCustomerGroup(candidate);
            error = "No se pudo registrar un grupo restaurado.";
            Destroy(candidate.gameObject);
            return false;
        }

        nextGroupId = Mathf.Max(nextGroupId, record.groupId + 1);
        group = candidate;
        return true;
    }

    public void RestoreNextGroupId(int value)
    {
        nextGroupId = Mathf.Max(1, value);
    }

    /// <summary>
    /// Captura el calendario pendiente de llegadas. Los grupos futuros ya
    /// están materializados como tamaño + modalidad, por lo que una carga no
    /// vuelve a tirar números aleatorios ni duplica el servicio.
    /// </summary>
    public bool TryCaptureRuntimeSpawnState(
        out BistroBuilderCustomerSpawnerRuntimeSaveRecord snapshot,
        out string error
    )
    {
        snapshot = new BistroBuilderCustomerSpawnerRuntimeSaveRecord
        {
            scheduleInitialized = spawnScheduleInitialized,
            scheduleCompleted = spawnScheduleCompleted,
            secondsUntilNextArrival = Mathf.Max(
                0f,
                secondsUntilNextArrival
            )
        };

        foreach (PlannedArrival arrival in plannedArrivals)
        {
            if (arrival == null || arrival.GroupSize < 1 ||
                !BistroBuilderServiceModeUtility.IsDefined(
                    arrival.ServiceMode
                ))
            {
                error = "El calendario runtime de llegadas es inválido.";
                snapshot = null;
                return false;
            }

            snapshot.pendingArrivals.Add(
                new BistroBuilderCustomerArrivalPlanSaveRecord
                {
                    groupSize = arrival.GroupSize,
                    serviceMode = (int)arrival.ServiceMode,
                    acquisition = arrival.Acquisition != null
                        ? arrival.Acquisition.DeepClone()
                        : BistroBuilderCustomerAcquisitionProfile.CreateBaseline()
                }
            );
        }

        return snapshot.TryValidate(out error);
    }

    /// <summary>
    /// Restaura el calendario pendiente sin generar clientes hasta que
    /// game.general reactive el estado Open al final de la carga.
    /// </summary>
    public bool TryRestoreRuntimeSpawnState(
        BistroBuilderCustomerSpawnerRuntimeSaveRecord snapshot,
        out string error
    )
    {
        error = string.Empty;
        StopSpawning();
        ClearSpawnSchedule();

        if (snapshot == null || !snapshot.TryValidate(out error))
        {
            return false;
        }

        spawnScheduleInitialized = snapshot.scheduleInitialized;
        spawnScheduleCompleted = snapshot.scheduleCompleted;
        secondsUntilNextArrival = Mathf.Max(
            0f,
            snapshot.secondsUntilNextArrival
        );

        for (int index = 0; index < snapshot.pendingArrivals.Count; index++)
        {
            BistroBuilderCustomerArrivalPlanSaveRecord record =
                snapshot.pendingArrivals[index];
            plannedArrivals.Enqueue(
                new PlannedArrival(
                    record.groupSize,
                    (BistroBuilderServiceMode)record.serviceMode,
                    record.acquisition != null
                        ? record.acquisition.DeepClone()
                        : BistroBuilderCustomerAcquisitionProfile.CreateBaseline()
                )
            );
        }

        restoredScheduleAwaitingServiceActivation =
            spawnScheduleInitialized;

        if (serviceStateService != null &&
            serviceStateService.AcceptsNewCustomers)
        {
            restoredScheduleAwaitingServiceActivation = false;
            StartSpawningIfNeeded(false);
        }

        return true;
    }

    /// <summary>
    /// Reanuda el calendario restaurado cuando service.runtime ya ha
    /// finalizado todas sus referencias cruzadas. No crea un plan nuevo ni
    /// altera las próximas llegadas guardadas.
    /// </summary>
    public void ResumeAfterRuntimeLoad()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            serviceStateService == null ||
            !serviceStateService.AcceptsNewCustomers)
        {
            return;
        }

        restoredScheduleAwaitingServiceActivation = false;
        StartSpawningIfNeeded(false);
    }

    /// <summary>
    /// Configura una secuencia temporal y determinista de tamaños de grupo.
    /// No se serializa y desaparece al salir de Play Mode.
    /// </summary>
    public bool TryConfigureDiagnosticGroupSizes(
        IList<int> groupSizes,
        out string error
    )
    {
        if (!Application.isPlaying)
        {
            error = "La secuencia diagnóstica solo puede usarse en Play Mode.";
            return false;
        }

        if (groupSizes == null || groupSizes.Count == 0)
        {
            error = "La secuencia diagnóstica está vacía.";
            return false;
        }

        for (int index = 0; index < groupSizes.Count; index++)
        {
            if (groupSizes[index] < 1)
            {
                error = "Todos los tamaños diagnósticos deben ser positivos.";
                return false;
            }
        }

        diagnosticGroupSizes.Clear();

        for (int index = 0; index < groupSizes.Count; index++)
            diagnosticGroupSizes.Enqueue(groupSizes[index]);

        error = string.Empty;
        return true;
    }

    public bool TryConfigureDiagnosticServiceModes(
        IList<BistroBuilderServiceMode> serviceModes,
        out string error
    )
    {
        if (!Application.isPlaying)
        {
            error = "La secuencia diagnóstica solo puede usarse en Play Mode.";
            return false;
        }

        if (serviceModes == null || serviceModes.Count == 0)
        {
            error = "La secuencia de modalidades está vacía.";
            return false;
        }

        for (int index = 0; index < serviceModes.Count; index++)
        {
            if (!BistroBuilderServiceModeUtility.IsDefined(serviceModes[index]))
            {
                error = "La secuencia contiene una modalidad desconocida.";
                return false;
            }
        }

        diagnosticServiceModes.Clear();

        for (int index = 0; index < serviceModes.Count; index++)
        {
            diagnosticServiceModes.Enqueue(serviceModes[index]);
        }

        error = string.Empty;
        return true;
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();

        nextGroupId =
            Mathf.Max(
                1,
                firstGroupId
            );
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();

        configurationIsValid =
            ValidateConfiguration();

        if (!configurationIsValid)
        {
            enabled = false;
            return;
        }

        SubscribeToServiceState();
    }

    private void Start()
    {
        /*
         * Start se ejecuta después de todos los Awake de la escena.
         * Esto garantiza que el estado inicial del servicio ya esté
         * completamente inicializado.
         */
        SynchronizeWithServiceState();
    }

    private void OnDisable()
    {
        UnsubscribeFromServiceState();
        StopSpawning();
    }

    /// <summary>
    /// Inicia o detiene la generación al cambiar la fase operativa.
    /// </summary>
    private void HandleServiceStateChanged(
        RestaurantServiceState previousState,
        RestaurantServiceState currentState
    )
    {
        if (!configurationIsValid)
        {
            return;
        }

        if (currentState == RestaurantServiceState.Open)
        {
            if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            {
                // game.general reactiva Open antes de que service.runtime
                // termine de publicar mesas, grupos, barra y tareas. Incluso
                // con Time.timeScale=0 una corrutina con espera cero podría
                // generar un grupo de forma síncrona al iniciarse.
                restoredScheduleAwaitingServiceActivation =
                    spawnScheduleInitialized;
                return;
            }

            if (restoredScheduleAwaitingServiceActivation)
            {
                restoredScheduleAwaitingServiceActivation = false;
                StartSpawningIfNeeded(false);
                return;
            }

            if (previousState != RestaurantServiceState.Open)
            {
                BeginNewServiceSchedule();
            }

            StartSpawningIfNeeded(false);
            return;
        }

        StopSpawning();

        if (currentState == RestaurantServiceState.Closed)
        {
            ClearSpawnSchedule();
        }
        else if (currentState == RestaurantServiceState.Closing)
        {
            MarkSpawnScheduleCompleted();
        }
    }

    /// <summary>
    /// Mantiene la rutina alineada con el estado real del servicio.
    /// </summary>
    private void SynchronizeWithServiceState()
    {
        if (!configurationIsValid ||
            serviceStateService == null)
        {
            return;
        }

        if (serviceStateService.AcceptsNewCustomers)
        {
            if (!spawnScheduleInitialized)
            {
                BeginNewServiceSchedule();
            }

            StartSpawningIfNeeded(false);
            return;
        }

        StopSpawning();
    }

    private void BeginNewServiceSchedule()
    {
        StopSpawning();
        plannedArrivals.Clear();

        BistroBuilderCustomerDemandPlan demandPlan = queuedDemandPlan;
        queuedDemandPlan = null;
        int plannedGroupCount = demandPlan != null
            ? demandPlan.walkInGroupCount
            : numberOfGroups;

        for (int index = 0; index < plannedGroupCount; index++)
        {
            int groupSize = diagnosticGroupSizes.Count > 0
                ? diagnosticGroupSizes.Dequeue()
                : Random.Range(
                    minimumGroupSize,
                    maximumGroupSize + 1
                );
            BistroBuilderServiceMode mode = ResolveServiceMode(groupSize);
            BistroBuilderCustomerAcquisitionProfile acquisition =
                demandPlan != null && index < demandPlan.profiles.Count
                    ? demandPlan.profiles[index].DeepClone()
                    : CreateBaselineAcquisitionProfile(index);

            plannedArrivals.Enqueue(
                new PlannedArrival(groupSize, mode, acquisition)
            );
        }

        lastPlannedGroupCount = plannedGroupCount;
        lastConsumedDemandPlanId = demandPlan != null
            ? demandPlan.planId
            : string.Empty;
        spawnScheduleInitialized = true;
        spawnScheduleCompleted = plannedArrivals.Count == 0;
        restoredScheduleAwaitingServiceActivation = false;
        secondsUntilNextArrival = spawnScheduleCompleted
            ? 0f
            : Mathf.Max(0f, firstSpawnDelay);
    }

    /// <summary>
    /// Inicia una única rutina para el calendario actual.
    /// </summary>
    private void StartSpawningIfNeeded(bool createScheduleIfMissing)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            restoredScheduleAwaitingServiceActivation =
                spawnScheduleInitialized;
            return;
        }

        if (spawnRoutine != null || !isActiveAndEnabled ||
            serviceStateService == null ||
            !serviceStateService.AcceptsNewCustomers)
        {
            return;
        }

        if (!spawnScheduleInitialized && createScheduleIfMissing)
        {
            BeginNewServiceSchedule();
        }

        if (!spawnScheduleInitialized || spawnScheduleCompleted ||
            plannedArrivals.Count == 0)
        {
            return;
        }

        spawnRoutine = StartCoroutine(SpawnGroupsRoutine());
    }

    /// <summary>
    /// Detiene inmediatamente futuras llegadas sin perder el calendario.
    /// Los grupos ya generados continúan su flujo normal.
    /// </summary>
    private void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void ClearSpawnSchedule()
    {
        plannedArrivals.Clear();
        spawnScheduleInitialized = false;
        spawnScheduleCompleted = false;
        restoredScheduleAwaitingServiceActivation = false;
        secondsUntilNextArrival = 0f;
    }

    private void MarkSpawnScheduleCompleted()
    {
        plannedArrivals.Clear();
        spawnScheduleInitialized = true;
        spawnScheduleCompleted = true;
        restoredScheduleAwaitingServiceActivation = false;
        secondsUntilNextArrival = 0f;
    }

    /// <summary>
    /// Genera grupos desde un plan materializado y persistible. El contador
    /// de espera se conserva para poder guardar en mitad del intervalo.
    /// </summary>
    private IEnumerator SpawnGroupsRoutine()
    {
        while (plannedArrivals.Count > 0)
        {
            while (secondsUntilNextArrival > 0f)
            {
                if (serviceStateService == null ||
                    !serviceStateService.AcceptsNewCustomers)
                {
                    spawnRoutine = null;
                    yield break;
                }

                secondsUntilNextArrival = Mathf.Max(
                    0f,
                    secondsUntilNextArrival - Time.deltaTime
                );
                yield return null;
            }

            if (serviceStateService == null ||
                !serviceStateService.AcceptsNewCustomers)
            {
                spawnRoutine = null;
                yield break;
            }

            PlannedArrival arrival = plannedArrivals.Dequeue();
            int groupId = nextGroupId;
            nextGroupId++;

            SpawnCustomerGroup(
                groupId,
                arrival.GroupSize,
                arrival.ServiceMode,
                arrival.Acquisition
            );

            secondsUntilNextArrival = plannedArrivals.Count > 0
                ? Mathf.Max(0.1f, timeBetweenGroups)
                : 0f;

            yield return null;
        }

        spawnScheduleCompleted = true;
        spawnRoutine = null;

        Debug.Log(
            "CustomerGroupSpawner ha completado el calendario de " +
            "llegadas del servicio.",
            this
        );
    }

    /// <summary>
    /// Crea y configura una instancia concreta del prefab.
    /// </summary>
    private void SpawnCustomerGroup(
        int groupId,
        int groupSize,
        BistroBuilderServiceMode serviceMode,
        BistroBuilderCustomerAcquisitionProfile acquisition = null
    )
    {
        CustomerGroup newGroup =
            Instantiate(
                customerGroupPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

        newGroup.gameObject.name =
            "CustomerGroup_" +
            groupId;

        bool initialized =
            newGroup.Initialize(
                groupId,
                groupSize,
                serviceMode
            );

        if (!initialized)
        {
            Destroy(
                newGroup.gameObject
            );

            return;
        }

        BistroBuilderCustomerAcquisitionProfile resolvedAcquisition =
            acquisition ?? CreateBaselineAcquisitionProfile(groupId);
        if (!resolvedAcquisition.TryValidate(out string acquisitionError))
        {
            Debug.LogError(
                "Perfil de captación inválido para grupo " + groupId +
                ": " + acquisitionError,
                newGroup
            );
            Destroy(newGroup.gameObject);
            return;
        }

        BistroBuilderCustomerAcquisitionTag acquisitionTag =
            newGroup.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        if (acquisitionTag == null)
            acquisitionTag = newGroup.gameObject.AddComponent<
                BistroBuilderCustomerAcquisitionTag>();
        if (!acquisitionTag.TryConfigure(
                resolvedAcquisition,
                out acquisitionError))
        {
            Debug.LogError(acquisitionError, newGroup);
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
                "El prefab del grupo " +
                groupId +
                " no contiene CustomerMovementView.",
                newGroup
            );

            Destroy(
                newGroup.gameObject
            );

            return;
        }

        /*
         * La salida pertenece a la escena, por lo que se configura
         * después de instanciar el prefab.
         */
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
                "No se pudo registrar el grupo " +
                groupId +
                " en TableAssignmentSystem.",
                newGroup
            );

            Destroy(
                newGroup.gameObject
            );

            return;
        }

        bool registeredInWaitingArea =
            customerWaitingAreaSystem.RegisterCustomerGroup(
                newGroup
            );

        if (!registeredInWaitingArea)
        {
            Debug.LogError(
                "No se pudo registrar el grupo " +
                groupId +
                " en CustomerWaitingAreaSystem.",
                newGroup
            );

            /*
             * Deshacemos también el registro anterior para no dejar
             * referencias a un objeto que será destruido.
             */
            tableAssignmentSystem.UnregisterCustomerGroup(
                newGroup
            );

            Destroy(
                newGroup.gameObject
            );

            return;
        }

        if (barServiceSystem != null &&
            !barServiceSystem.RegisterCustomerGroup(newGroup))
        {
            Debug.LogError(
                "No se pudo registrar el grupo " + groupId +
                " en BistroBuilderBarServiceSystem.",
                newGroup
            );

            customerWaitingAreaSystem.UnregisterCustomerGroup(newGroup);
            tableAssignmentSystem.UnregisterCustomerGroup(newGroup);
            Destroy(newGroup.gameObject);
            return;
        }

        Debug.Log(
            "Generado grupo " +
            groupId +
            " de " +
            groupSize +
            " cliente(s).",
            newGroup
        );
    }

    private static BistroBuilderCustomerAcquisitionProfile
        CreateBaselineAcquisitionProfile(int referenceIndex)
    {
        return new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = "general",
            sourceSystemId = "service.baseline",
            sourceReferenceId = string.Empty,
            marketingInfluenced = false
        };
    }

    private BistroBuilderServiceMode ResolveServiceMode(int groupSize)
    {
        if (diagnosticServiceModes.Count > 0)
        {
            return diagnosticServiceModes.Dequeue();
        }

        float value = Random.value;

        // El servicio exclusivo de barra se limita inicialmente a grupos de
        // una persona porque cada plaza representa un puesto individual.
        if (groupSize == 1 && value < barServiceProbability)
        {
            return BistroBuilderServiceMode.BarService;
        }

        if (value < barServiceProbability + waitingAtBarProbability)
        {
            return BistroBuilderServiceMode.WaitingAtBar;
        }

        return BistroBuilderServiceMode.TableService;
    }

    /// <summary>
    /// Busca dependencias situadas en el mismo GameObject.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (serviceStateService == null)
        {
            TryGetComponent(out serviceStateService);
        }

        if (barServiceSystem == null)
        {
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem
            >();
        }
    }

    private void SubscribeToServiceState()
    {
        if (serviceStateService == null)
        {
            return;
        }

        serviceStateService.StateChanged -=
            HandleServiceStateChanged;

        serviceStateService.StateChanged +=
            HandleServiceStateChanged;
    }

    private void UnsubscribeFromServiceState()
    {
        if (serviceStateService == null)
        {
            return;
        }

        serviceStateService.StateChanged -=
            HandleServiceStateChanged;
    }

    /// <summary>
    /// Valida todas las referencias antes de generar grupos.
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

        if (serviceStateService == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita " +
                "RestaurantServiceStateService.",
                this
            );

            isValid = false;
        }

        if (barServiceSystem == null)
        {
            Debug.LogError(
                "CustomerGroupSpawner necesita BistroBuilderBarServiceSystem.",
                this
            );
            isValid = false;
        }

        if (barServiceProbability < 0f ||
            waitingAtBarProbability < 0f ||
            barServiceProbability + waitingAtBarProbability > 1f)
        {
            Debug.LogError(
                "Las probabilidades de modalidades 367H deben sumar como " +
                "máximo 1.",
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

        if (minimumGroupSize >
            maximumGroupSize)
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

    private sealed class PlannedArrival
    {
        public int GroupSize { get; }
        public BistroBuilderServiceMode ServiceMode { get; }
        public BistroBuilderCustomerAcquisitionProfile Acquisition { get; }

        public PlannedArrival(
            int groupSize,
            BistroBuilderServiceMode serviceMode,
            BistroBuilderCustomerAcquisitionProfile acquisition = null
        )
        {
            GroupSize = Mathf.Max(1, groupSize);
            ServiceMode = serviceMode;
            Acquisition = acquisition != null
                ? acquisition.DeepClone()
                : CreateBaselineAcquisitionProfile(groupSize);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();

        numberOfGroups =
            Mathf.Max(
                1,
                numberOfGroups
            );

        firstSpawnDelay =
            Mathf.Max(
                0f,
                firstSpawnDelay
            );

        timeBetweenGroups =
            Mathf.Max(
                0.1f,
                timeBetweenGroups
            );

        minimumGroupSize =
            Mathf.Max(
                1,
                minimumGroupSize
            );

        maximumGroupSize =
            Mathf.Max(
                minimumGroupSize,
                maximumGroupSize
            );

        firstGroupId =
            Mathf.Max(
                1,
                firstGroupId
            );
    }
#endif
}
