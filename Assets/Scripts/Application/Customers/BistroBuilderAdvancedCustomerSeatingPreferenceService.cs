using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 10D. Proyecta preferencias avanzadas sobre el TableAssignmentSystem canónico.
/// Solo reserva una mesa preferente; nunca asigna ni ocupa mesas directamente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Seating Preference Service")]
public sealed class BistroBuilderAdvancedCustomerSeatingPreferenceService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderAdvancedCustomerProfileService profileService;
    [SerializeField]
    private BistroBuilderAdvancedCustomerHistoryService historyService;
    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;
    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    private int lastRegisteredGroupCount = -1;
    private bool evaluationRequested = true;

    public event Action<int, int> PreferredTableReserved;

    public BistroBuilderAdvancedCustomerProfileService ProfileService => profileService;
    public BistroBuilderAdvancedCustomerHistoryService HistoryService => historyService;
    public TableAssignmentSystem TableAssignmentSystem => tableAssignmentSystem;
    public RestaurantTableRegistry TableRegistry => tableRegistry;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        evaluationRequested = true;
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (!Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        CacheDependencies();
        int count = tableAssignmentSystem != null
            ? tableAssignmentSystem.RegisteredGroups.Count : 0;
        if (count != lastRegisteredGroupCount)
        {
            lastRegisteredGroupCount = count;
            evaluationRequested = true;
        }
        if (!evaluationRequested) return;
        evaluationRequested = false;
        EvaluateRegisteredGroups();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (profileService == null || historyService == null ||
            tableAssignmentSystem == null || tableRegistry == null)
        {
            error = "10D necesita perfiles, historial, TableAssignment y TableRegistry.";
            return false;
        }
        if (!profileService.ValidateConfiguration(out error) ||
            !historyService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryBuildPreference(
        CustomerGroup group,
        out BistroBuilderAdvancedCustomerServicePreference preference,
        out string error)
    {
        preference = null;
        if (group == null || !profileService.TryGetGroupProfile(
                group.GroupId, out BistroBuilderAdvancedCustomerGroupProfile profile))
        {
            error = "No existe perfil avanzado para el grupo.";
            return false;
        }

        BistroBuilderAdvancedCustomerHistoryRecord history = null;
        if (profile.returningVisit &&
            !string.IsNullOrWhiteSpace(profile.returningReferenceId))
            historyService.TryGetCustomer(profile.returningReferenceId, out history);

        return BistroBuilderAdvancedCustomerServicePreferenceEngine.TryBuildPreference(
            profile, history, out preference, out error);
    }

    public bool TryReserveBestPreferredTable(CustomerGroup group, out string error)
    {
        error = string.Empty;
        if (!ValidateConfiguration(out error) || group == null || group.HasAssignedTable ||
            group.RequestedServiceMode == BistroBuilderServiceMode.BarService)
            return false;
        if (tableAssignmentSystem.TryGetPreferredTable(group, out _)) return true;

        if (!TryBuildPreference(group, out var preference, out error))
            return false;
        bool meaningful = preference.isVip ||
            preference.specialNeeds != BistroBuilderCustomerSpecialNeed.None ||
            preference.preferredZoneTagIds.Count > 0;
        if (!meaningful) return true;

        RestaurantTable best = null;
        BistroBuilderAdvancedCustomerTableEvaluation bestEvaluation = null;
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            if (table == null || !table.CanSeatGroup(group.GroupSize)) continue;
            if (!TryBuildDescriptor(table, out var descriptor, out _)) continue;
            BistroBuilderAdvancedCustomerTableEvaluation evaluation =
                BistroBuilderAdvancedCustomerServicePreferenceEngine.EvaluateTable(
                    preference, descriptor, group.GroupSize);
            if (IsBetterCandidate(group, table, evaluation, best, bestEvaluation))
            {
                best = table;
                bestEvaluation = evaluation;
            }
        }

        if (best == null) return true;
        if (bestEvaluation != null && bestEvaluation.unmetSpecialNeeds > 0 &&
            bestEvaluation.matchedPreferences == 0 && !preference.isVip)
            return true;

        if (!tableAssignmentSystem.TryReservePreferredTable(group, best, out error))
            return false;
        PreferredTableReserved?.Invoke(group.GroupId, best.TableId);
        return true;
    }

    private void EvaluateRegisteredGroups()
    {
        if (!ValidateConfiguration(out _)) return;
        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || group.HasAssignedTable) continue;
            TryReserveBestPreferredTable(group, out _);
        }
    }

    private static bool TryBuildDescriptor(
        RestaurantTable table,
        out BistroBuilderAdvancedCustomerTableDescriptor descriptor,
        out string error)
    {
        descriptor = null;
        if (table == null)
        {
            error = "La mesa candidata es nula.";
            return false;
        }
        if (table.TryGetComponent(out BistroBuilderAdvancedCustomerTableProfile profile))
            return profile.TryBuildDescriptor(table, out descriptor, out error);

        descriptor = new BistroBuilderAdvancedCustomerTableDescriptor
        {
            tableId = table.TableId,
            capacity = table.Capacity,
            available = table.IsAvailable
        };
        if (table.TryGetComponent(out RestaurantAreaMember member) &&
            member.AssignedArea != null)
        {
            AddTag(descriptor.semanticTags, member.AssignedArea.AreaId);
            if (member.AssignedArea.Definition != null)
                AddTag(descriptor.semanticTags, member.AssignedArea.Definition.AreaTypeId);
        }
        error = string.Empty;
        return true;
    }

    private static bool IsBetterCandidate(
        CustomerGroup group,
        RestaurantTable candidate,
        BistroBuilderAdvancedCustomerTableEvaluation evaluation,
        RestaurantTable current,
        BistroBuilderAdvancedCustomerTableEvaluation currentEvaluation)
    {
        if (candidate == null || evaluation == null) return false;
        if (current == null || currentEvaluation == null) return true;
        if (evaluation.unmetSpecialNeeds != currentEvaluation.unmetSpecialNeeds)
            return evaluation.unmetSpecialNeeds < currentEvaluation.unmetSpecialNeeds;
        if (evaluation.preferenceScore != currentEvaluation.preferenceScore)
            return evaluation.preferenceScore > currentEvaluation.preferenceScore;

        int candidateWaste = candidate.Capacity - group.GroupSize;
        int currentWaste = current.Capacity - group.GroupSize;
        if (candidateWaste != currentWaste) return candidateWaste < currentWaste;

        float candidateDistance = (group.transform.position -
            ResolveApproach(candidate)).sqrMagnitude;
        float currentDistance = (group.transform.position -
            ResolveApproach(current)).sqrMagnitude;
        if (!Mathf.Approximately(candidateDistance, currentDistance))
            return candidateDistance < currentDistance;
        return candidate.TableId < current.TableId;
    }

    private static Vector3 ResolveApproach(RestaurantTable table) =>
        table.CustomerApproachPoint != null
            ? table.CustomerApproachPoint.position
            : table.transform.position;

    private static void AddTag(List<string> target, string value)
    {
        string id = BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(value);
        if (!BistroBuilderAdvancedCustomerProfileEngine.IsSafeId(id)) return;
        for (int i = 0; i < target.Count; i++)
            if (string.Equals(target[i], id, StringComparison.Ordinal)) return;
        target.Add(id);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (profileService != null) profileService.ProfilesChanged += RequestEvaluation;
        if (tableAssignmentSystem != null)
            tableAssignmentSystem.CustomerGroupRegistered += HandleGroupRegistered;
        if (historyService != null) historyService.HistoryChanged += HandleHistoryChanged;
        if (tableRegistry != null)
        {
            tableRegistry.TableRegistered += HandleTableChanged;
            tableRegistry.TableUnregistered += HandleTableChanged;
        }
    }

    private void Unsubscribe()
    {
        if (profileService != null) profileService.ProfilesChanged -= RequestEvaluation;
        if (tableAssignmentSystem != null)
            tableAssignmentSystem.CustomerGroupRegistered -= HandleGroupRegistered;
        if (historyService != null) historyService.HistoryChanged -= HandleHistoryChanged;
        if (tableRegistry != null)
        {
            tableRegistry.TableRegistered -= HandleTableChanged;
            tableRegistry.TableUnregistered -= HandleTableChanged;
        }
    }

    private void HandleGroupRegistered(CustomerGroup group)
    {
        if (group == null) return;
        profileService?.TryRefreshGroupProfile(group, out _);
        TryReserveBestPreferredTable(group, out _);
    }

    private void RequestEvaluation() => evaluationRequested = true;
    private void HandleHistoryChanged(long _) => evaluationRequested = true;
    private void HandleTableChanged(RestaurantTable _) => evaluationRequested = true;

    private void CacheDependencies()
    {
        if (profileService == null) TryGetComponent(out profileService);
        if (historyService == null) TryGetComponent(out historyService);
        if (tableAssignmentSystem == null) TryGetComponent(out tableAssignmentSystem);
        if (tableRegistry == null) TryGetComponent(out tableRegistry);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
