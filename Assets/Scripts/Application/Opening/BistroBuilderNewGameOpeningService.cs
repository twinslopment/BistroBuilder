using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Opening/New Game Opening Service")]
public sealed class BistroBuilderNewGameOpeningService : MonoBehaviour
{
    [Header("Autoridades canonicas")]
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private BistroBuilderInventoryService inventoryService;
    [SerializeField] private BistroBuilderRestaurantMenuService menuService;
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffRecruitmentService recruitmentService;
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderAdvancedCustomerHistoryService customerHistoryService;
    [SerializeField] private BistroBuilderEndOfDayService endOfDayService;
    [SerializeField] private BistroBuilderAdvancedKitchenService advancedKitchenService;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private RestaurantPlacementValidationService placementValidationService;
    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private BistroBuilderEditDocumentRuntimeService editDocumentService;
    [SerializeField] private RestaurantPlaceableRegistry placeableRegistry;
    [SerializeField] private RestaurantPlaceableLifecycleService placeableLifecycleService;

    [Header("Nueva partida")]
    [SerializeField, Range(1, 999)] private int defaultSaveSlot = 1;
    [SerializeField, Range(0, 23)] private int initialOpeningHour = 12;
    [SerializeField, Range(0, 23)] private int initialClosingHour = 15;
    [SerializeField, Min(1)] private int minimumStockedIngredients = 4;
    [SerializeField, Min(1)] private int minimumDiningSeats = 4;

    private readonly List<BistroBuilderEmployeeRecord> employeeBuffer = new();
    private readonly List<BistroBuilderInventoryStockSnapshot> stockBuffer = new();
    private BistroBuilderNewGameStateSnapshot state = new();
    private BistroBuilderOpeningPreflightReport lastPreflight = new();

    public event Action StateChanged;
    public event Action<BistroBuilderOpeningPreflightReport> PreflightCompleted;
    public event Action FirstServiceOpened;

    public BistroBuilderNewGamePhase Phase => state != null ? state.phase : BistroBuilderNewGamePhase.StartMenu;
    public bool CanContinue => saveGameService != null && saveGameService.SlotExists(EffectiveSaveSlot);
    public string RestaurantName => state != null ? state.restaurantName : string.Empty;
    public string Briefing => state != null ? state.lastBriefing : string.Empty;
    public BistroBuilderOpeningPreflightReport LastPreflight => lastPreflight?.DeepClone();
    public bool IsInitialDesignPhase => Phase == BistroBuilderNewGamePhase.InitialSetup;
    public bool IsInitialEditModeActive => IsInitialDesignPhase && editModeService != null && editModeService.IsEditModeActive;
    public BistroBuilderStartingPremisesProfile PremisesProfile => state != null
        ? state.premisesProfile : BistroBuilderStartingPremisesProfile.Balanced;
    public bool IsSaveBusy => saveGameService != null && saveGameService.IsBusy;
    public float SaveProgress => saveGameService != null ? saveGameService.CurrentProgress : 0f;
    public string SaveStatusMessage => saveGameService != null
        ? saveGameService.CurrentStatusMessage : string.Empty;
    public BistroBuilderSaveOperationResult LastSaveResult => saveGameService != null
        ? saveGameService.LastResult : null;
    private int EffectiveSaveSlot => Mathf.Clamp(defaultSaveSlot, 1, 999);

    private void Awake()
    {
        CacheDependencies();
        defaultSaveSlot = EffectiveSaveSlot;
        EnsureState();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (generalGameStateService == null || gameClock == null || serviceStateService == null ||
            inventoryService == null || menuService == null || staffService == null ||
            recruitmentService == null || scheduleService == null || financeService == null ||
            reputationService == null || customerHistoryService == null || endOfDayService == null ||
            advancedKitchenService == null || tableRegistry == null ||
            placementValidationService == null || saveGameService == null || editModeService == null)
        {
            error = "Bloque 16 necesita calendario, reloj, servicio, inventario, carta, personal, cocina, sala, finanzas y guardado.";
            return false;
        }
        if (!generalGameStateService.ValidateConfiguration(out error) ||
            !inventoryService.ValidateConfiguration(out error) ||
            !menuService.ValidateConfiguration(out error) ||
            !staffService.ValidateConfiguration(out error) ||
            !recruitmentService.ValidateConfiguration(out error) ||
            !scheduleService.ValidateConfiguration(out error) ||
            !financeService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !advancedKitchenService.ValidateConfiguration(out error) ||
            !saveGameService.ValidateConfiguration(out error))
            return false;
        if (defaultSaveSlot < 1 || defaultSaveSlot > 999 || initialOpeningHour == initialClosingHour || minimumStockedIngredients < 1 || minimumDiningSeats < 1)
        {
            error = "La configuracion inicial de apertura es invalida.";
            return false;
        }
        return BistroBuilderNewGameEngine.TryValidateSnapshot(state, out error);
    }

    public bool TryCreateNewGame(
        string restaurantName,
        BistroBuilderStartingPremisesProfile premisesProfile,
        out string error)
    {
        error = string.Empty;
        EnsureState();
        if (!ValidateConfiguration(out error)) return false;
        if (!serviceStateService.IsClosed)
        {
            error = "La nueva partida solo puede configurarse con el restaurante cerrado.";
            return false;
        }
        if (!editModeService.CanEnterEditMode(out _, out string editRejection))
        {
            error = "No puede iniciarse el diseno inicial: " + editRejection;
            return false;
        }
        string normalizedName = string.IsNullOrWhiteSpace(restaurantName)
            ? "Mi restaurante"
            : restaurantName.Trim();
        if (normalizedName.Length > 80) normalizedName = normalizedName.Substring(0, 80);

        if (!generalGameStateService.TryRestoreState(
                Guid.NewGuid().ToString("N"), normalizedName, DateTime.UtcNow.ToString("O"),
                1, 1, 1, 1, "new_restaurant", 1, true))
        {
            error = "No pudo crearse la identidad/calendario de la nueva partida.";
            return false;
        }
        if (!gameClock.TryRestoreState(10, 0, 1f, true, 0f, true))
        {
            error = "No pudo prepararse el reloj inicial.";
            return false;
        }
        if (!financeService.TryInitializeFresh(out error) ||
            !inventoryService.TryInitialize(out error))
            return false;

        BistroBuilderMenuMutationResult menuReset = menuService.ResetToCatalogDefaults();
        if (!menuReset.Succeeded)
        {
            error = "No pudo prepararse la carta inicial: " + menuReset.Message;
            return false;
        }
        if (!staffService.TryInitializeFresh(out error) ||
            !recruitmentService.EnsureMarketReady(out error) ||
            !scheduleService.TryResetForLegacyLoad(out error) ||
            !reputationService.TryResetForLegacyLoad(out error) ||
            !customerHistoryService.TryResetForLegacyLoad(out error) ||
            !endOfDayService.TryResetForLegacyLoad(out error))
            return false;

        if (editDocumentService != null &&
            !editDocumentService.ReplaceCommittedForLoad(new BistroBuilderEditDocument(), out error))
            return false;
        if (premisesProfile == BistroBuilderStartingPremisesProfile.Empty &&
            !TryPrepareEmptyPremises(out error))
            return false;

        state = new BistroBuilderNewGameStateSnapshot
        {
            revision = 1,
            phase = BistroBuilderNewGamePhase.InitialSetup,
            setupCompleted = true,
            restaurantName = normalizedName,
            premisesProfile = premisesProfile,
            initialOpeningHour = initialOpeningHour,
            initialClosingHour = initialClosingHour,
            initialSaveSlot = EffectiveSaveSlot
        };

        lastPreflight = new BistroBuilderOpeningPreflightReport();
        state.lastBriefing = string.Empty;
        state.revision++;
        StateChanged?.Invoke();
        TryRequestInitialSave(out _);

        if (!editModeService.TryEnterEditMode(out _, out string enterEditError))
        {
            error = "La partida se creo, pero no pudo abrirse el modo edicion: " + enterEditError;
            return false;
        }
        return true;
    }

    public bool TryRunOpeningPreflight(
        out BistroBuilderOpeningPreflightReport report,
        out string error)
    {
        error = string.Empty;
        report = new BistroBuilderOpeningPreflightReport();
        CacheDependencies();
        if (!ValidateConfiguration(out error)) return false;

        AddObjectCheck(report, "entrance", "Entrada valida", "RestaurantEntrancePoint", true,
            "Existe un punto de entrada operativo.", "Falta el punto de entrada del restaurante.");

        bool kitchenOk = advancedKitchenService != null && advancedKitchenService.ValidateConfiguration(out _);
        BistroBuilderNewGameEngine.AddCheck(report, "kitchen", "Cocina accesible",
            kitchenOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            kitchenOk ? "La cocina avanzada esta operativa." : "La cocina no esta preparada para el primer servicio.");

        int seats = 0;
        if (tableRegistry != null)
            foreach (RestaurantTable table in tableRegistry.RegisteredTables)
                if (table != null && table.Capacity > 0) seats += table.Capacity;
        bool diningOk = tableRegistry != null && tableRegistry.RegisteredTableCount > 0 && seats >= minimumDiningSeats;
        BistroBuilderNewGameEngine.AddCheck(report, "dining", "Comedor operativo",
            diningOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            diningOk ? "Hay " + seats + " plazas de mesa utilizables." : "No hay suficientes mesas y sillas utilizables.");

        bool bathrooms = FindSceneObjectContaining("bathroom", "restroom", "toilet", "bano", "wc");
        BistroBuilderNewGameEngine.AddCheck(report, "bathrooms", "Banos accesibles",
            bathrooms ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Warning,
            bathrooms ? "Se detecta una zona de banos accesible." : "No hay un marcador de banos verificable; no bloquea la apertura inicial.");

        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, false);
        int waiterCount = 0;
        int cookCount = 0;
        for (int i = 0; i < employeeBuffer.Count; i++)
        {
            BistroBuilderEmployeeRecord employee = employeeBuffer[i];
            if (employee == null || employee.availability != BistroBuilderEmployeeAvailability.Available) continue;
            if (string.Equals(employee.roleId, "waiter", StringComparison.Ordinal)) waiterCount++;
            if (string.Equals(employee.roleId, "cook", StringComparison.Ordinal)) cookCount++;
        }
        bool staffOk = waiterCount >= 1 && cookCount >= 1;
        BistroBuilderNewGameEngine.AddCheck(report, "staff", "Personal suficiente",
            staffOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            staffOk ? "Plantilla inicial: " + waiterCount + " camarero(s) y " + cookCount + " cocinero(s)." :
                "Hace falta al menos un camarero y un cocinero disponibles.");

        bool menuOk = menuService.ItemCount > 0 && menuService.ValidateConfiguration(out _);
        BistroBuilderNewGameEngine.AddCheck(report, "menu", "Carta valida",
            menuOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            menuOk ? "La carta inicial contiene " + menuService.ItemCount + " platos." : "La carta inicial no es valida.");

        stockBuffer.Clear();
        inventoryService.CopyStockSnapshotsTo(stockBuffer);
        int stocked = 0;
        long available = 0L;
        for (int i = 0; i < stockBuffer.Count; i++)
        {
            if (stockBuffer[i].AvailableCanonicalMilliUnits > 0)
            {
                stocked++;
                available += stockBuffer[i].AvailableCanonicalMilliUnits;
            }
        }
        bool stockOk = stocked >= minimumStockedIngredients && available > 0L;
        BistroBuilderNewGameEngine.AddCheck(report, "stock", "Stock minimo suficiente",
            stockOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            stockOk ? "Hay existencias iniciales en " + stocked + " ingredientes." : "El inventario inicial no cubre el minimo de apertura.");

        bool scheduleOk = scheduleService.TryBuildCoverage(
            1, BistroBuilderMealServiceAvailability.Lunch,
            out BistroBuilderStaffScheduleCoverage coverage, out _) && coverage != null && coverage.isSufficient;
        BistroBuilderNewGameEngine.AddCheck(report, "schedule", "Horarios iniciales",
            scheduleOk ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            scheduleOk ? "El primer servicio tiene cobertura de sala suficiente." : "El horario inicial no tiene cobertura suficiente.");

        RestaurantPlacementValidationSummary placement = placementValidationService.ValidateAllRegisteredPlacements(false);
        BistroBuilderNewGameEngine.AddCheck(report, "layout", "Local utilizable",
            placement.IsValid ? BistroBuilderOpeningCheckLevel.Passed : BistroBuilderOpeningCheckLevel.Blocker,
            placement.IsValid ? "La distribucion fisica actual supera la validacion." : "Hay conflictos fisicos que impiden abrir con seguridad.");

        lastPreflight = report.DeepClone();
        PreflightCompleted?.Invoke(lastPreflight.DeepClone());
        return true;
    }

    public bool TryAcknowledgeBriefing(out string error)
    {
        error = string.Empty;
        if (!state.setupCompleted)
        {
            error = "Primero hay que crear y configurar la nueva partida.";
            return false;
        }
        if (state.phase != BistroBuilderNewGamePhase.Briefing)
        {
            error = "Primero debes terminar el diseño inicial del restaurante.";
            return false;
        }
        if (!TryRunOpeningPreflight(out BistroBuilderOpeningPreflightReport report, out error)) return false;
        if (!report.CanOpen)
        {
            error = "La apertura sigue teniendo requisitos bloqueantes.";
            return false;
        }
        state.briefingAcknowledged = true;
        state.phase = BistroBuilderNewGamePhase.ReadyToOpen;
        state.revision++;
        StateChanged?.Invoke();
        return true;
    }

    public bool TryOpenFirstService(out string error)
    {
        error = string.Empty;
        if (!state.setupCompleted || !state.briefingAcknowledged ||
            state.phase != BistroBuilderNewGamePhase.ReadyToOpen)
        {
            error = "El briefing inicial debe completarse antes de abrir.";
            return false;
        }
        if (!TryRunOpeningPreflight(out BistroBuilderOpeningPreflightReport report, out error) || !report.CanOpen)
        {
            if (string.IsNullOrEmpty(error)) error = "La validacion previa impide abrir.";
            return false;
        }
        if (serviceStateService.IsClosed && !serviceStateService.TryBeginPreparation())
        {
            error = "No pudo comenzar la preparacion del primer servicio.";
            return false;
        }
        if (serviceStateService.CurrentState == RestaurantServiceState.Preparing &&
            !serviceStateService.TryOpenService())
        {
            error = "No pudo abrirse el primer servicio.";
            return false;
        }
        gameClock.SetPaused(false);
        state.firstOpeningCompleted = true;
        state.firstServiceStarted = true;
        state.phase = BistroBuilderNewGamePhase.FirstService;
        state.revision++;
        StateChanged?.Invoke();
        FirstServiceOpened?.Invoke();
        return true;
    }

    public bool TryTransitionToNormalPlay(out string error)
    {
        error = string.Empty;
        if (!state.firstServiceStarted || state.phase != BistroBuilderNewGamePhase.FirstService)
        {
            error = "El primer servicio todavia no ha comenzado.";
            return false;
        }
        state.transitionedToNormalPlay = true;
        state.phase = BistroBuilderNewGamePhase.NormalPlay;
        state.revision++;
        StateChanged?.Invoke();
        return true;
    }

    public bool TryRequestInitialSave(out string error)
    {
        error = string.Empty;
        if (!state.setupCompleted)
        {
            error = "No existe una nueva partida configurada para guardar.";
            return false;
        }
        if (saveGameService.IsBusy)
        {
            error = "El sistema de guardado esta ocupado.";
            return false;
        }
        if (!saveGameService.TrySaveSlot(EffectiveSaveSlot, state.restaurantName, out error)) return false;
        state.initialSaveRequested = true;
        state.initialSaveSlot = EffectiveSaveSlot;
        state.revision++;
        StateChanged?.Invoke();
        return true;
    }

    public bool TryContinue(out string error)
    {
        error = string.Empty;
        if (!CanContinue)
        {
            error = "No existe una partida guardada para continuar.";
            return false;
        }
        return saveGameService.TryLoadSlot(EffectiveSaveSlot, out error);
    }

    public bool TryEnterInitialEditMode(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        EnsureState();
        if (state.phase != BistroBuilderNewGamePhase.InitialSetup)
        {
            error = "La partida no esta en la fase de diseno inicial.";
            return false;
        }
        if (editModeService == null)
        {
            error = "El modo edicion no esta disponible.";
            return false;
        }
        if (editModeService.IsEditModeActive) return true;
        if (!editModeService.TryEnterEditMode(out _, out string rejection))
        {
            error = string.IsNullOrWhiteSpace(rejection)
                ? "No pudo activarse el modo edicion inicial."
                : rejection;
            return false;
        }
        return true;
    }

    public bool TryValidateInitialDesign(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        EnsureState();
        if (state.phase != BistroBuilderNewGamePhase.InitialSetup)
        {
            error = "La partida no esta en la fase de diseño inicial.";
            return false;
        }
        if (!serviceStateService.IsClosed)
        {
            error = "El restaurante debe permanecer cerrado durante el diseño inicial.";
            return false;
        }
        if (GameObject.Find("RestaurantEntrancePoint") == null)
        {
            error = "El diseño necesita una entrada operativa.";
            return false;
        }

        if (state.premisesProfile == BistroBuilderStartingPremisesProfile.Empty)
        {
            BistroBuilderEditDocument layout = editDocumentService != null
                ? editDocumentService.GetCommittedSnapshot() : null;
            if (!HasFunctionalZone(layout, "dining") || !HasFunctionalZone(layout, "kitchen") ||
                !HasFunctionalZone(layout, "bathroom"))
            {
                error = "El local vacío necesita al menos un Salón, una Cocina y un Baño antes de validar.";
                return false;
            }
        }

        int seats = 0;
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
            if (table != null && table.Capacity > 0) seats += table.Capacity;
        if (seats < minimumDiningSeats)
        {
            error = "Añade al menos " + minimumDiningSeats + " plazas de mesa antes de continuar.";
            return false;
        }

        RestaurantPlacementValidationSummary placement =
            placementValidationService.ValidateAllRegisteredPlacements(false);
        if (!placement.IsValid)
        {
            int invalid = Mathf.Max(0, placement.TotalCount - placement.ValidCount);
            error = "Corrige la distribución: " + invalid +
                " elemento(s) tienen conflictos de colocación o espacio.";
            return false;
        }
        return true;
    }

    public bool TryCompleteInitialDesign(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        EnsureState();
        if (!TryValidateInitialDesign(out error)) return false;
        if (saveGameService.IsBusy)
        {
            error = "Espera a que termine el guardado actual antes de confirmar el diseño.";
            return false;
        }
        if (!HasActiveStaffRole("waiter") || !HasActiveStaffRole("cook"))
        {
            error = "La preparación inicial necesita los roles activos de camarero y cocinero.";
            return false;
        }

        if (!recruitmentService.EnsureMarketReady(out error) ||
            !EnsureInitialEmployee("waiter", "Alex", "Sala", 8200, out error) ||
            !EnsureInitialEmployee("cook", "Sam", "Cocina", 9000, out error) ||
            !scheduleService.TryResetForLegacyLoad(out error) ||
            !scheduleService.TryAutoFillMinimumWaiters(
                1, BistroBuilderMealServiceAvailability.Lunch, out error))
            return false;

        if (!TryRunOpeningPreflight(out BistroBuilderOpeningPreflightReport report, out error))
            return false;

        if (editModeService.IsEditModeActive &&
            !editModeService.TryExitEditMode(false, out RestaurantEditModeFailureReason exitReason))
        {
            error = exitReason == RestaurantEditModeFailureReason.PlacementOperationActive
                ? "Confirma o cancela el objeto que estás colocando antes de finalizar el diseño."
                : "No pudo cerrarse el modo edición: " + exitReason + ".";
            return false;
        }

        state.lastBriefing = BistroBuilderNewGameEngine.BuildBriefing(
            state.restaurantName, state.premisesProfile, report,
            initialOpeningHour, initialClosingHour);
        state.phase = BistroBuilderNewGamePhase.Briefing;
        state.revision++;
        StateChanged?.Invoke();

        if (!TryRequestInitialSave(out string saveError))
        {
            state.phase = BistroBuilderNewGamePhase.InitialSetup;
            state.revision++;
            StateChanged?.Invoke();
            editModeService.TryEnterEditMode(out _, out _);
            error = "No pudo guardarse el diseño confirmado: " + saveError;
            return false;
        }
        return true;
    }

    private bool TryPrepareEmptyPremises(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (placeableRegistry == null || placeableLifecycleService == null)
        {
            error = "No está disponible el sistema de mobiliario para preparar el local vacío.";
            return false;
        }
        var placed = new List<RestaurantPlaceableObject>(placeableRegistry.RegisteredPlaceables);
        for (int i = 0; i < placed.Count; i++)
        {
            RestaurantPlaceableObject item = placed[i];
            if (item == null || !item.gameObject.activeSelf) continue;
            if (!placeableLifecycleService.TryDeactivateInstance(item, out _, out RestaurantPlaceableLifecycleResult deactivate))
            {
                error = "No pudo vaciarse el local: " + deactivate.Message;
                return false;
            }
            if (!placeableLifecycleService.TryPermanentlyDestroyInstance(item, out RestaurantPlaceableLifecycleResult destroy))
            {
                error = "No pudo completarse el vaciado del local: " + destroy.Message;
                return false;
            }
        }

        // 367H instalaba una barra de demostración que no pertenece al catálogo de
        // placeables. En un local Vacío también debe retirarse: conservarla hacía que
        // barra y taburetes sobrevivieran aunque el mobiliario se hubiese eliminado.
        BistroBuilder367HInstalledFixture[] fixtures =
            UnityEngine.Object.FindObjectsByType<BistroBuilder367HInstalledFixture>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < fixtures.Length; i++)
        {
            BistroBuilder367HInstalledFixture fixture = fixtures[i];
            if (fixture == null || fixture.GetComponent<RestaurantPlaceableObject>() != null) continue;
            fixture.gameObject.SetActive(false);
        }
        Physics.SyncTransforms();
        return true;
    }

    private static bool HasFunctionalZone(BistroBuilderEditDocument document, string zoneDefinitionId)
    {
        if (document == null || document.zones == null) return false;
        for (int i = 0; i < document.zones.Count; i++)
        {
            BistroBuilderFunctionalZoneRecord zone = document.zones[i];
            if (zone != null && string.Equals(zone.zoneDefinitionId, zoneDefinitionId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public BistroBuilderNewGameStateSnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(BistroBuilderNewGameStateSnapshot snapshot, out string error)
    {
        if (!BistroBuilderNewGameEngine.TryValidateSnapshot(snapshot, out error)) return false;
        state = snapshot.DeepClone();
        StateChanged?.Invoke();
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = new BistroBuilderNewGameStateSnapshot { initialSaveSlot = EffectiveSaveSlot };
        lastPreflight = new BistroBuilderOpeningPreflightReport();
        error = string.Empty;
        StateChanged?.Invoke();
        return true;
    }

    private bool EnsureInitialEmployee(
        string roleId, string firstName, string lastName, long salary, out string error)
    {
        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, false);
        for (int i = 0; i < employeeBuffer.Count; i++)
            if (employeeBuffer[i] != null &&
                employeeBuffer[i].availability == BistroBuilderEmployeeAvailability.Available &&
                string.Equals(employeeBuffer[i].roleId, roleId, StringComparison.Ordinal))
            {
                error = string.Empty;
                return true;
            }

        if (recruitmentService.EnsureMarketReady(out error))
        {
            var candidates = new List<BistroBuilderStaffCandidateRecord>();
            recruitmentService.CopyCandidates(candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && string.Equals(candidates[i].roleId, roleId, StringComparison.Ordinal) &&
                    recruitmentService.TryHireCandidate(candidates[i].candidateId, out _, out error))
                    return true;
            }
        }

        var request = new BistroBuilderEmployeeCreateRequest
        {
            firstName = firstName,
            lastName = lastName,
            roleId = roleId,
            salaryCentsPerService = salary,
            hiredDayIndex = 1,
            initialExperiencePoints = 100,
            initialSkills = new BistroBuilderEmployeeSkillSet
            {
                speed = 55, attentiveness = 55, organization = 55, hospitality = 55
            },
            availability = BistroBuilderEmployeeAvailability.Available,
            responsibilities = new BistroBuilderEmployeeResponsibilitySettings()
        };
        return staffService.TryCreateEmployee(request, out _, out error);
    }

    private bool HasActiveStaffRole(string roleId)
    {
        return staffService != null &&
               staffService.TryGetRoleDefinition(roleId, out BistroBuilderStaffRoleDefinition role) &&
               role != null && role.active;
    }
    private void AddObjectCheck(
        BistroBuilderOpeningPreflightReport report, string id, string label,
        string exactName, bool blocker, string success, string failure)
    {
        bool ok = GameObject.Find(exactName) != null;
        BistroBuilderNewGameEngine.AddCheck(report, id, label,
            ok ? BistroBuilderOpeningCheckLevel.Passed :
            (blocker ? BistroBuilderOpeningCheckLevel.Blocker : BistroBuilderOpeningCheckLevel.Warning),
            ok ? success : failure);
    }

    private static bool FindSceneObjectContaining(params string[] terms)
    {
        GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string lower = all[i].name.ToLowerInvariant();
            for (int j = 0; j < terms.Length; j++)
                if (!string.IsNullOrEmpty(terms[j]) && lower.Contains(terms[j])) return true;
        }
        return false;
    }

    private void EnsureState()
    {
        if (state == null) state = new BistroBuilderNewGameStateSnapshot { initialSaveSlot = EffectiveSaveSlot };
    }

    private void CacheDependencies()
    {
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (gameClock == null) TryGetComponent(out gameClock);
        if (serviceStateService == null) TryGetComponent(out serviceStateService);
        if (inventoryService == null) TryGetComponent(out inventoryService);
        if (menuService == null) TryGetComponent(out menuService);
        if (staffService == null) TryGetComponent(out staffService);
        if (recruitmentService == null) TryGetComponent(out recruitmentService);
        if (scheduleService == null) TryGetComponent(out scheduleService);
        if (financeService == null) TryGetComponent(out financeService);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (customerHistoryService == null) TryGetComponent(out customerHistoryService);
        if (endOfDayService == null) TryGetComponent(out endOfDayService);
        if (advancedKitchenService == null) TryGetComponent(out advancedKitchenService);
        if (tableRegistry == null) TryGetComponent(out tableRegistry);
        if (placementValidationService == null) TryGetComponent(out placementValidationService);
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (editModeService == null) TryGetComponent(out editModeService);
        if (editDocumentService == null) editDocumentService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        if (placeableRegistry == null) placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (placeableLifecycleService == null) placeableLifecycleService = FindFirstObjectByType<RestaurantPlaceableLifecycleService>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
