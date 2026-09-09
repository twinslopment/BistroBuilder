using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador de seating: TableAssignmentSystem decide qué mesa conviene;
/// Interaction & Reservation es la única autoridad del derecho lógico grupo→mesa.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSeatingReservationCoordinator : MonoBehaviour
{
    private const string InteractionId = "table.assign";
    private const string GroupPrefix = "group:";
    private const string TablePrefix = "table:";

    [SerializeField] private BistroBuilderInteractionService interactionService;
    [SerializeField] private RestaurantTableRegistry tableRegistry;

    private readonly Dictionary<string, int> acquisitionAttempts =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (interactionService == null)
        {
            error = "Seating Reservation necesita BistroBuilderInteractionService.";
            return false;
        }
        if (tableRegistry == null)
        {
            error = "Seating Reservation necesita RestaurantTableRegistry.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryAcquireTableRight(
        CustomerGroup group,
        RestaurantTable table,
        int priority,
        out BistroBuilderInteractionGrantHandle handle,
        out string error)
    {
        handle = default;
        error = string.Empty;
        if (!ValidateConfiguration(out error) || group == null || table == null)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "Grupo y mesa son obligatorios.";
            return false;
        }

        if (TryGetTableRight(group, out RestaurantTable current, out handle))
        {
            if (ReferenceEquals(current, table)) return true;
            error = "El grupo ya posee otra mesa lógicamente.";
            return false;
        }

        if (IsTableClaimedByOther(table, group))
        {
            error = "La mesa ya pertenece lógicamente a otro grupo.";
            return false;
        }
        string pairKey = group.GroupId + "|" + table.TableId;
        acquisitionAttempts.TryGetValue(pairKey, out int attempt);
        attempt++;
        acquisitionAttempts[pairKey] = attempt;
        string requestId = "seating.assign:" + group.GroupId.ToString("D6") + ":" +
                           table.TableId.ToString("D6") + ":" + attempt.ToString("D4");
        interactionService.SubmitAcquisition(new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = BistroBuilderInteractionGrantKind.Assignment,
            holderKind = BistroBuilderInteractionHolderKind.Group,
            holderId = GroupId(group.GroupId),
            interactionId = InteractionId,
            taskPriorityClass = priority,
            persistCanonical = true,
            candidates = new List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    resourceId = TableId(table.TableId),
                    suitability = 0,
                    travelCostHint = 0
                }
            }
        });
        interactionService.ResolveArbitrationEpoch();
        if (!interactionService.TryGetDecision(requestId, out var decision) ||
            decision.outcome != BistroBuilderInteractionRequestOutcome.Granted ||
            !interactionService.TryGetGrant(decision.handle, out _))
        {
            error = decision != null
                ? "Interaction rechazó mesa: " + decision.reason + "."
                : "Interaction no publicó decisión para la mesa.";
            return false;
        }
        handle = decision.handle;
        return true;
    }
    public bool TryGetTableRight(
        CustomerGroup group,
        out RestaurantTable table,
        out BistroBuilderInteractionGrantHandle handle)
    {
        table = null;
        handle = default;
        if (group == null || interactionService == null || tableRegistry == null)
            return false;
        IReadOnlyList<BistroBuilderInteractionGrantRecord> grants =
            interactionService.GetGrantsForHolder(GroupId(group.GroupId));
        for (int i = 0; i < grants.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = grants[i];
            if (!IsTableAssignment(grant) ||
                !TryParseTableId(grant.resourceId, out int tableId) ||
                !tableRegistry.TryGetTableById(tableId, out table) || table == null)
                continue;
            handle = grant.Handle;
            return true;
        }
        table = null;
        return false;
    }

    public bool IsTableClaimedByOther(RestaurantTable table, CustomerGroup requester)
    {
        if (table == null || interactionService == null) return false;
        string requesterId = requester != null ? GroupId(requester.GroupId) : string.Empty;
        IReadOnlyList<BistroBuilderInteractionGrantRecord> grants =
            interactionService.GetGrantsForResource(TableId(table.TableId));
        for (int i = 0; i < grants.Count; i++)
        {
            BistroBuilderInteractionGrantRecord grant = grants[i];
            if (IsTableAssignment(grant) &&
                !string.Equals(grant.holderId, requesterId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
    public bool ReleaseGroupRight(CustomerGroup group)
    {
        if (group == null || interactionService == null) return false;
        IReadOnlyList<BistroBuilderInteractionGrantRecord> grants =
            interactionService.GetGrantsForHolder(GroupId(group.GroupId));
        bool released = false;
        for (int i = 0; i < grants.Count; i++)
            if (IsTableAssignment(grants[i]))
                released |= interactionService.ReleaseGrant(grants[i].Handle);
        return released;
    }

    public int ReleaseTableRight(RestaurantTable table)
    {
        if (table == null || interactionService == null) return 0;
        IReadOnlyList<BistroBuilderInteractionGrantRecord> grants =
            interactionService.GetGrantsForResource(TableId(table.TableId));
        int released = 0;
        for (int i = 0; i < grants.Count; i++)
            if (IsTableAssignment(grants[i]) &&
                interactionService.InvalidateGrant(
                    grants[i].Handle,
                    BistroBuilderInteractionReasonCode.TargetInvalidated))
                released++;
        return released;
    }

    public bool TryEnsureAssignedTableRight(
        CustomerGroup group,
        out string error)
    {
        error = string.Empty;
        if (group == null || group.AssignedTable == null) return true;
        if (TryGetTableRight(group, out RestaurantTable current, out _))
        {
            if (ReferenceEquals(current, group.AssignedTable)) return true;
            error = "El gameplay y el Assignment lógico apuntan a mesas distintas.";
            return false;
        }
        return TryAcquireTableRight(
            group, group.AssignedTable, int.MaxValue / 4, out _, out error);
    }
    private void CacheDependencies()
    {
        if (interactionService == null)
            interactionService = FindFirstObjectByType<BistroBuilderInteractionService>();
        if (tableRegistry == null)
            tableRegistry = FindFirstObjectByType<RestaurantTableRegistry>();
    }

    private static bool IsTableAssignment(BistroBuilderInteractionGrantRecord grant)
    {
        return grant != null &&
               grant.kind == BistroBuilderInteractionGrantKind.Assignment &&
               string.Equals(grant.interactionId, InteractionId, StringComparison.Ordinal) &&
               grant.resourceId.StartsWith(TablePrefix, StringComparison.Ordinal);
    }

    private static string GroupId(int groupId)
    {
        return GroupPrefix + Math.Max(1, groupId).ToString("D6");
    }

    private static string TableId(int tableId)
    {
        return TablePrefix + Math.Max(1, tableId).ToString("D6");
    }

    private static bool TryParseTableId(string resourceId, out int tableId)
    {
        tableId = 0;
        return !string.IsNullOrWhiteSpace(resourceId) &&
               resourceId.StartsWith(TablePrefix, StringComparison.Ordinal) &&
               int.TryParse(resourceId.Substring(TablePrefix.Length), out tableId) &&
               tableId > 0;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderInteractionService service,
        RestaurantTableRegistry registry)
    {
        interactionService = service;
        tableRegistry = registry;
    }
#endif
}
