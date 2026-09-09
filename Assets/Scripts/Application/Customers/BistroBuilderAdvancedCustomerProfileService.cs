using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proyecta perfiles individuales sobre los CustomerGroup existentes.
/// No crea clientes, no asigna mesas y no sustituye al flujo de servicio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Profile Service")]
public sealed class BistroBuilderAdvancedCustomerProfileService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderAdvancedCustomerProfileCatalog profileCatalog;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    private readonly Dictionary<int, BistroBuilderAdvancedCustomerGroupProfile>
        profilesByGroupId = new Dictionary<int, BistroBuilderAdvancedCustomerGroupProfile>();
    private readonly Dictionary<int, CustomerGroup> groupReferences =
        new Dictionary<int, CustomerGroup>();
    private readonly Dictionary<int, string> acquisitionFingerprints =
        new Dictionary<int, string>();
    private readonly List<int> staleGroupIds = new List<int>(16);

    public event Action ProfilesChanged;

    public BistroBuilderAdvancedCustomerProfileCatalog ProfileCatalog => profileCatalog;
    public BistroBuilderGeneralGameStateService GeneralGameStateService => generalGameStateService;
    public TableAssignmentSystem TableAssignmentSystem => tableAssignmentSystem;
    public int ActiveProfileCount => profilesByGroupId.Count;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        SynchronizeGroups();
    }

    private void Update()
    {
        if (!Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        SynchronizeGroups();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (profileCatalog == null || generalGameStateService == null ||
            tableAssignmentSystem == null)
        {
            error = "10A necesita catálogo, estado general y TableAssignmentSystem.";
            return false;
        }
        if (!profileCatalog.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryGetGroupProfile(
        int groupId,
        out BistroBuilderAdvancedCustomerGroupProfile profile)
    {
        profile = null;
        if (groupId < 1) return false;
        SynchronizeGroups();
        if (!profilesByGroupId.TryGetValue(groupId, out var stored) || stored == null)
            return false;
        profile = stored.DeepClone();
        return true;
    }

    public bool TryGetMemberProfile(
        int groupId,
        int memberIndex,
        out BistroBuilderAdvancedCustomerMemberProfile profile)
    {
        profile = null;
        if (!TryGetGroupProfile(groupId, out var groupProfile) ||
            memberIndex < 1 || memberIndex > groupProfile.members.Count)
            return false;
        profile = groupProfile.members[memberIndex - 1].DeepClone();
        return true;
    }

    public bool TryRefreshGroupProfile(CustomerGroup group, out string error)
    {
        error = string.Empty;
        if (group == null || group.GroupId < 1 || group.GroupSize < 1)
        {
            error = "El grupo que se intenta perfilar es inválido.";
            return false;
        }

        if (!ValidateConfiguration(out error)) return false;

        BistroBuilderCustomerAcquisitionTag tag =
            group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        BistroBuilderCustomerAcquisitionProfile acquisition = tag != null
            ? tag.CreateSnapshot()
            : BistroBuilderCustomerAcquisitionProfile.CreateBaseline();

        if (!BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
                group.GroupId,
                group.GroupSize,
                generalGameStateService.DayIndex,
                acquisition,
                profileCatalog.Archetypes,
                out BistroBuilderAdvancedCustomerGroupProfile profile,
                out error))
            return false;

        profilesByGroupId[group.GroupId] = profile;
        groupReferences[group.GroupId] = group;
        acquisitionFingerprints[group.GroupId] = BuildFingerprint(acquisition);
        ProfilesChanged?.Invoke();
        return true;
    }

    private void SynchronizeGroups()
    {
        CacheDependencies();
        if (profileCatalog == null || generalGameStateService == null ||
            tableAssignmentSystem == null)
            return;

        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || group.GroupId < 1) continue;

            BistroBuilderCustomerAcquisitionTag tag =
                group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
            BistroBuilderCustomerAcquisitionProfile acquisition = tag != null
                ? tag.CreateSnapshot()
                : BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
            string fingerprint = BuildFingerprint(acquisition);

            bool needsRefresh =
                !profilesByGroupId.ContainsKey(group.GroupId) ||
                !groupReferences.TryGetValue(group.GroupId, out CustomerGroup known) ||
                !ReferenceEquals(known, group) ||
                !acquisitionFingerprints.TryGetValue(group.GroupId, out string knownFingerprint) ||
                !string.Equals(knownFingerprint, fingerprint, StringComparison.Ordinal);
            if (needsRefresh)
                TryRefreshGroupProfile(group, out _);
        }

        staleGroupIds.Clear();
        foreach (int groupId in profilesByGroupId.Keys)
            if (!ContainsGroup(groups, groupId)) staleGroupIds.Add(groupId);
        for (int i = 0; i < staleGroupIds.Count; i++)
            RemoveGroup(staleGroupIds[i]);
    }

    private void RemoveGroup(int groupId)
    {
        bool removed = profilesByGroupId.Remove(groupId);
        groupReferences.Remove(groupId);
        acquisitionFingerprints.Remove(groupId);
        if (removed) ProfilesChanged?.Invoke();
    }

    private static bool ContainsGroup(
        IReadOnlyList<CustomerGroup> groups,
        int groupId)
    {
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId == groupId)
                return true;
        return false;
    }

    private static string BuildFingerprint(
        BistroBuilderCustomerAcquisitionProfile acquisition)
    {
        if (acquisition == null) return "baseline";
        return BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(acquisition.segmentId) + "|" +
               (acquisition.returningVisit ? "1" : "0") + "|" +
               BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(
                   acquisition.guestRelationsReferenceId) + "|" +
               BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(
                   acquisition.sourceReferenceId);
    }

    private void CacheDependencies()
    {
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (tableAssignmentSystem == null)
            tableAssignmentSystem = FindFirstObjectByType<TableAssignmentSystem>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
