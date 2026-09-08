using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 10F. Proyecta reacciones individuales durante el servicio desde el runtime
/// canónico de espera. No cronometra, no mueve clientes y no cambia su estado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Behavior Service")]
public sealed class BistroBuilderAdvancedCustomerBehaviorService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderCustomerExperienceTrackingService trackingService;
    [SerializeField]
    private BistroBuilderAdvancedCustomerProfileService profileService;
    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;
    [SerializeField, Min(0.05f)]
    private float refreshIntervalSeconds = 0.25f;

    private readonly Dictionary<int, BistroBuilderAdvancedCustomerGroupBehavior>
        behaviors = new Dictionary<int, BistroBuilderAdvancedCustomerGroupBehavior>();
    private readonly Dictionary<int, string> signatures = new Dictionary<int, string>();
    private readonly List<int> staleIds = new List<int>(16);
    private float nextRefreshTime;

    public event Action<int, BistroBuilderAdvancedCustomerGroupBehavior> BehaviorChanged;
    public int TrackedBehaviorCount => behaviors.Count;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        nextRefreshTime = 0f;
    }

    private void Update()
    {
        if (!Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + Math.Max(0.05f, refreshIntervalSeconds);
        RefreshAll();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (trackingService == null || profileService == null ||
            tableAssignmentSystem == null)
        {
            error = "10F necesita Experience Tracking, perfiles y TableAssignment canónicos.";
            return false;
        }
        if (!trackingService.ValidateConfiguration(out error) ||
            !profileService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryGetBehavior(
        int groupId,
        out BistroBuilderAdvancedCustomerGroupBehavior behavior)
    {
        behavior = null;
        if (groupId < 1) return false;
        RefreshGroup(groupId, ResolveGroup(groupId));
        if (!behaviors.TryGetValue(groupId, out var stored) || stored == null)
            return false;
        behavior = stored.DeepClone();
        return true;
    }

    public bool TryEvaluateNow(
        CustomerGroup group,
        out BistroBuilderAdvancedCustomerGroupBehavior behavior,
        out string error)
    {
        behavior = null;
        error = string.Empty;
        if (group == null || group.GroupId < 1)
        {
            error = "El grupo de 10F es inválido.";
            return false;
        }
        if (!trackingService.TryGetRuntimeVisit(group.GroupId, out var visit) ||
            !profileService.TryGetGroupProfile(group.GroupId, out var profile))
        {
            error = "10F todavía no dispone de visita/perfil para el grupo.";
            return false;
        }
        return BistroBuilderAdvancedCustomerBehaviorEngine.TryEvaluate(
            visit, profile, group.CurrentState, out behavior, out error);
    }

    private void RefreshAll()
    {
        CacheDependencies();
        if (trackingService == null || profileService == null ||
            tableAssignmentSystem == null)
            return;

        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null) RefreshGroup(groups[i].GroupId, groups[i]);

        staleIds.Clear();
        foreach (int groupId in behaviors.Keys)
            if (!ContainsGroup(groups, groupId)) staleIds.Add(groupId);
        for (int i = 0; i < staleIds.Count; i++)
        {
            behaviors.Remove(staleIds[i]);
            signatures.Remove(staleIds[i]);
        }
    }

    private void RefreshGroup(int groupId, CustomerGroup group)
    {
        if (groupId < 1 || group == null ||
            !TryEvaluateNow(group, out var current, out _))
            return;

        string signature = BuildSignature(current);
        bool changed = !signatures.TryGetValue(groupId, out string previous) ||
            !string.Equals(previous, signature, StringComparison.Ordinal);
        behaviors[groupId] = current.DeepClone();
        signatures[groupId] = signature;
        if (changed) BehaviorChanged?.Invoke(groupId, current.DeepClone());
    }

    private CustomerGroup ResolveGroup(int groupId)
    {
        if (tableAssignmentSystem == null) CacheDependencies();
        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem != null
            ? tableAssignmentSystem.RegisteredGroups : null;
        if (groups == null) return null;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId == groupId)
                return groups[i];
        return null;
    }

    private static bool ContainsGroup(
        IReadOnlyList<CustomerGroup> groups, int groupId)
    {
        if (groups == null) return false;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId == groupId) return true;
        return false;
    }

    private static string BuildSignature(BistroBuilderAdvancedCustomerGroupBehavior value)
    {
        if (value == null) return string.Empty;
        return ((int)value.serviceState) + "|" + ((int)value.dominantMood) + "|" +
            ((int)value.dominantReason) + "|" + (value.maximumPressureBasisPoints / 500) +
            "|" + value.impatientMemberCount;
    }

    private void CacheDependencies()
    {
        if (trackingService == null) TryGetComponent(out trackingService);
        if (profileService == null) TryGetComponent(out profileService);
        if (tableAssignmentSystem == null)
            tableAssignmentSystem = FindFirstObjectByType<TableAssignmentSystem>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
