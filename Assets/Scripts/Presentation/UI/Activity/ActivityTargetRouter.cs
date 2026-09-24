using System;
using System.Collections.Generic;
using UnityEngine;

public interface IActivityTargetResolver
{
    ActivityTargetType TargetType { get; }
    bool CanResolve(ActivityTargetRef target);
    bool TryResolve(ActivityTargetRef target);
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Activity/Activity Target Router")]
public sealed class ActivityTargetRouter : MonoBehaviour
{
    private readonly List<IActivityTargetResolver> customResolvers =
        new List<IActivityTargetResolver>(8);

    private readonly Dictionary<long, int> nextGroupedTargetBySequence =
        new Dictionary<long, int>();

    private RestaurantTableRegistry tableRegistry;
    private TableAssignmentSystem tableAssignments;
    private BistroBuilderTableSelectionController tableSelection;
    private BistroBuilderInventoryPlanningService inventoryPlanning;

    public event Action<ActivityTargetRef> RouteSucceeded;
    public event Action<ActivityTargetRef> RouteFailed;

    private void Awake() => ResolveDependencies();

    public void RegisterResolver(IActivityTargetResolver resolver)
    {
        if (resolver == null || customResolvers.Contains(resolver))
            return;
        customResolvers.Add(resolver);
    }

    public void UnregisterResolver(IActivityTargetResolver resolver)
    {
        if (resolver != null)
            customResolvers.Remove(resolver);
    }

    public bool TryRoute(ActivityDisplayEntry entry)
    {
        if (entry == null)
            return false;

        if (entry.Targets.Count == 0)
            return TryRoute(entry.Primary != null ? entry.Primary.target : null);

        long key = entry.Primary != null ? entry.Primary.sequence : 0L;
        int start = nextGroupedTargetBySequence.TryGetValue(key, out int current)
            ? current
            : 0;

        for (int attempt = 0; attempt < entry.Targets.Count; attempt++)
        {
            int index = (start + attempt) % entry.Targets.Count;
            ActivityTargetRef target = entry.Targets[index];
            if (!TryRoute(target))
                continue;

            nextGroupedTargetBySequence[key] =
                (index + 1) % Math.Max(1, entry.Targets.Count);
            return true;
        }

        return false;
    }

    public bool TryRoute(ActivityTargetRef target)
    {
        ResolveDependencies();

        if (target == null || target.IsEmpty)
        {
            RouteFailed?.Invoke(target);
            return false;
        }

        bool success;
        switch (target.targetType)
        {
            case ActivityTargetType.Table:
                success = TrySelectTable(target.targetId);
                break;
            case ActivityTargetType.Group:
                success = TrySelectGroupTable(target.targetId);
                break;
            default:
                success = TryCustomResolvers(target);
                break;
        }

        if (success)
            RouteSucceeded?.Invoke(target);
        else
            RouteFailed?.Invoke(target);

        return success;
    }

    /// <summary>
    /// Validador conservador para rehidratación. Solo descarta referencias
    /// que el runtime actual puede demostrar que son inválidas.
    /// </summary>
    public bool IsPersistenceTargetValid(ActivityTargetRef target)
    {
        ResolveDependencies();

        if (target == null || target.IsEmpty)
            return false;

        switch (target.targetType)
        {
            case ActivityTargetType.Table:
                return TryFindTable(target.targetId, out _);

            case ActivityTargetType.Group:
                return TryFindGroup(target.targetId, out _);

            case ActivityTargetType.Ingredient:
                return inventoryPlanning == null ||
                       inventoryPlanning.TryGetPlanningSnapshot(
                           target.targetId,
                           out _);

            default:
                // Management targets can live outside the active scene.
                // Non-empty stable IDs remain valid until their owner
                // registers a concrete resolver.
                return true;
        }
    }

    public bool CanRoute(ActivityTargetRef target)
    {
        ResolveDependencies();
        if (target == null || target.IsEmpty)
            return false;

        if (target.targetType == ActivityTargetType.Table)
            return TryFindTable(target.targetId, out _);

        if (target.targetType == ActivityTargetType.Group)
        {
            if (!TryFindGroup(target.targetId, out CustomerGroup group))
                return false;
            return group.AssignedTable != null;
        }

        for (int i = 0; i < customResolvers.Count; i++)
        {
            IActivityTargetResolver resolver = customResolvers[i];
            if (resolver != null &&
                resolver.TargetType == target.targetType &&
                resolver.CanResolve(target))
                return true;
        }

        return false;
    }

    private bool TrySelectTable(string id)
    {
        if (!TryFindTable(id, out RestaurantTable table) ||
            tableSelection == null)
            return false;

        return tableSelection.TrySelectForTest(table, true);
    }

    private bool TrySelectGroupTable(string id)
    {
        if (!TryFindGroup(id, out CustomerGroup group) ||
            group == null ||
            group.AssignedTable == null ||
            tableSelection == null)
            return false;

        return tableSelection.TrySelectForTest(group.AssignedTable, true);
    }

    private bool TryCustomResolvers(ActivityTargetRef target)
    {
        for (int i = 0; i < customResolvers.Count; i++)
        {
            IActivityTargetResolver resolver = customResolvers[i];
            if (resolver == null ||
                resolver.TargetType != target.targetType ||
                !resolver.CanResolve(target))
                continue;

            if (resolver.TryResolve(target))
                return true;
        }

        return false;
    }

    private bool TryFindTable(string id, out RestaurantTable table)
    {
        table = null;
        if (!int.TryParse(id, out int tableId) || tableId <= 0)
            return false;

        ResolveDependencies();
        return tableRegistry != null &&
               tableRegistry.TryGetTableById(tableId, out table) &&
               table != null;
    }

    private bool TryFindGroup(string id, out CustomerGroup group)
    {
        group = null;
        if (!int.TryParse(id, out int groupId) || groupId <= 0)
            return false;

        ResolveDependencies();
        if (tableAssignments == null)
            return false;

        IReadOnlyList<CustomerGroup> groups = tableAssignments.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup candidate = groups[i];
            if (candidate != null && candidate.GroupId == groupId)
            {
                group = candidate;
                return true;
            }
        }

        return false;
    }

    private void ResolveDependencies()
    {
        if (tableRegistry == null)
            tableRegistry = FindFirstObjectByType<RestaurantTableRegistry>(
                FindObjectsInactive.Include);
        if (tableAssignments == null)
            tableAssignments = FindFirstObjectByType<TableAssignmentSystem>(
                FindObjectsInactive.Include);
        if (tableSelection == null)
            tableSelection = FindFirstObjectByType<BistroBuilderTableSelectionController>(
                FindObjectsInactive.Include);
        if (inventoryPlanning == null)
            inventoryPlanning = FindFirstObjectByType<BistroBuilderInventoryPlanningService>(
                FindObjectsInactive.Include);
    }
}
