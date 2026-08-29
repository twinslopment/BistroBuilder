using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 8B-8D. Mide la experiencia real de cada grupo y la cierra contra el cobro
/// financiero canónico. No es autoridad de servicio, comandas ni Finanzas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reputation/Customer Experience Tracking Service")]
public sealed class BistroBuilderCustomerExperienceTrackingService : MonoBehaviour
{
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private TableAssignmentSystem tableAssignmentSystem;
    [SerializeField] private OrderSystem orderSystem;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderDishCatalogService dishCatalogService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderUpgradeEffectsService upgradeEffectsService;

    private readonly Dictionary<int, BistroBuilderReputationVisitRuntimeRecord> visitsByGroup =
        new Dictionary<int, BistroBuilderReputationVisitRuntimeRecord>();
    private readonly Dictionary<int, CustomerGroup> groupsById =
        new Dictionary<int, CustomerGroup>();
    private readonly Dictionary<string, int> groupByOrderId =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<int> staleGroupIds = new List<int>(16);

    public event Action ExperienceRuntimeChanged;
    public int ActiveVisitCount => visitsByGroup.Count;
    public int LastRecordedSatisfactionBasisPoints { get; private set; }
    public string LastRecordedExperienceId { get; private set; } = string.Empty;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        SynchronizeGroups();
    }

    private void OnDisable()
    {
        Unsubscribe();
        foreach (CustomerGroup group in groupsById.Values)
            if (group != null) group.StateChanged -= HandleGroupStateChanged;
        groupsById.Clear();
    }

    private void Update()
    {
        if (!Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;

        SynchronizeGroups();
        float delta = Time.deltaTime;
        if (delta <= 0f) return;

        foreach (KeyValuePair<int, CustomerGroup> pair in groupsById)
        {
            if (pair.Value == null ||
                !visitsByGroup.TryGetValue(pair.Key, out var visit))
                continue;
            AccumulateWait(visit, pair.Value.CurrentState, delta);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (reputationService == null || tableAssignmentSystem == null ||
            orderSystem == null || canonicalOrderService == null ||
            financeService == null || dishCatalogService == null ||
            generalGameStateService == null ||
            upgradeEffectsService == null)
        {
            error = "Experience Tracking necesita Reputación, clientes, comandas, Finanzas, catálogo y calendario.";
            return false;
        }
        if (!reputationService.ValidateConfiguration(out error) ||
            !orderSystem.ValidateConfiguration(out error) ||
            !canonicalOrderService.ValidateConfiguration(out error) ||
            !financeService.ValidateConfiguration(out error) ||
            !dishCatalogService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !upgradeEffectsService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public BistroBuilderReputationRuntimeSnapshot CreateRuntimeSnapshot()
    {
        var snapshot = new BistroBuilderReputationRuntimeSnapshot();
        foreach (BistroBuilderReputationVisitRuntimeRecord visit in visitsByGroup.Values)
            snapshot.visits.Add(visit.DeepClone());
        snapshot.visits.Sort((a, b) => a.groupId.CompareTo(b.groupId));
        return snapshot;
    }

    public bool TryRestoreRuntimeSnapshot(
        BistroBuilderReputationRuntimeSnapshot snapshot,
        out string error)
    {
        if (!TryValidateRuntimeSnapshot(snapshot, out error))
            return false;

        visitsByGroup.Clear();
        groupByOrderId.Clear();
        for (int i = 0; i < snapshot.visits.Count; i++)
        {
            BistroBuilderReputationVisitRuntimeRecord visit = snapshot.visits[i].DeepClone();
            visitsByGroup.Add(visit.groupId, visit);
            if (!string.IsNullOrWhiteSpace(visit.canonicalOrderId))
                groupByOrderId[visit.canonicalOrderId] = visit.groupId;
        }
        SynchronizeGroups();
        ExperienceRuntimeChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    public bool TryResetRuntimeForLegacyLoad(out string error)
    {
        visitsByGroup.Clear();
        groupByOrderId.Clear();
        ExperienceRuntimeChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    public static bool TryValidateRuntimeSnapshot(
        BistroBuilderReputationRuntimeSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId,
                BistroBuilderReputationRuntimeSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderReputationRuntimeSnapshot.CurrentSchemaVersion ||
            snapshot.visits == null || snapshot.visits.Count > 128)
        {
            error = "reputation.runtime contiene una cabecera inválida.";
            return false;
        }

        var groups = new HashSet<int>();
        var orders = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.visits.Count; i++)
        {
            var visit = snapshot.visits[i];
            if (!BistroBuilderCustomerExperienceEvaluator.TryValidateRuntimeVisit(
                    visit, out error) || !groups.Add(visit.groupId))
                return false;
            if (!string.IsNullOrWhiteSpace(visit.canonicalOrderId) &&
                (!BistroBuilderOrderIdUtility.IsValid(visit.canonicalOrderId) ||
                 !orders.Add(visit.canonicalOrderId)))
            {
                error = "reputation.runtime contiene una comanda inválida o duplicada.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private void SynchronizeGroups()
    {
        if (tableAssignmentSystem == null) return;
        IReadOnlyList<CustomerGroup> registered = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < registered.Count; i++)
            RegisterGroup(registered[i]);

        staleGroupIds.Clear();
        foreach (KeyValuePair<int, CustomerGroup> pair in groupsById)
            if (pair.Value == null || !ContainsReference(registered, pair.Value))
                staleGroupIds.Add(pair.Key);
        for (int i = 0; i < staleGroupIds.Count; i++)
        {
            int id = staleGroupIds[i];
            if (groupsById.TryGetValue(id, out CustomerGroup group) && group != null)
                group.StateChanged -= HandleGroupStateChanged;
            groupsById.Remove(id);
        }
    }

    private void RegisterGroup(CustomerGroup group)
    {
        if (group == null || group.GroupId < 1) return;
        if (!groupsById.ContainsKey(group.GroupId))
        {
            groupsById.Add(group.GroupId, group);
            group.StateChanged -= HandleGroupStateChanged;
            group.StateChanged += HandleGroupStateChanged;
        }
        if (!visitsByGroup.ContainsKey(group.GroupId))
            visitsByGroup.Add(group.GroupId, CreateVisit(group));
    }

    private BistroBuilderReputationVisitRuntimeRecord CreateVisit(CustomerGroup group)
    {
        BistroBuilderCustomerAcquisitionTag tag =
            group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        return new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = group.GroupId,
            segmentId = tag != null && !string.IsNullOrWhiteSpace(tag.SegmentId)
                ? tag.SegmentId : "general",
            partySize = group.GroupSize,
            discoverySource = ResolveDiscoverySource(tag),
            foodQualityPotentialBasisPoints = Mathf.Clamp(7000 +
                upgradeEffectsService.FoodQualityPotentialBonusBasisPoints, 0, 10000),
            ambienceScoreBasisPoints = Mathf.Clamp(5000 +
                upgradeEffectsService.AmbienceBonusBasisPoints, 0, 10000)
        };
    }

    private void HandleGroupStateChanged(CustomerGroup group, CustomerGroupState state)
    {
        if (group == null) return;
        RegisterGroup(group);
        if (state == CustomerGroupState.Finished &&
            visitsByGroup.TryGetValue(group.GroupId, out var visit) &&
            string.IsNullOrWhiteSpace(visit.canonicalOrderId))
        {
            FinalizeVisit(visit, out _);
        }
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        if (order?.CustomerGroup == null || !order.HasCanonicalOrder) return;
        RegisterGroup(order.CustomerGroup);
        if (!visitsByGroup.TryGetValue(order.CustomerGroup.GroupId, out var visit))
            return;

        if (!string.IsNullOrWhiteSpace(visit.canonicalOrderId))
            groupByOrderId.Remove(visit.canonicalOrderId);
        visit.canonicalOrderId = order.CanonicalOrderId;
        groupByOrderId[order.CanonicalOrderId] = visit.groupId;
        PopulateOrderReference(order.CanonicalOrderId, visit);
        ExperienceRuntimeChanged?.Invoke();
    }

    private void HandleOrderCompleted(RestaurantOrder order)
    {
        if (order == null || !order.HasCanonicalOrder ||
            !groupByOrderId.TryGetValue(order.CanonicalOrderId, out int groupId) ||
            !visitsByGroup.TryGetValue(groupId, out var visit))
            return;

        visit.orderCompleted = true;
        if (visit.financeCaptured)
        {
            FinalizeVisit(visit, out _);
            return;
        }

        if (canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId, out BistroBuilderCanonicalOrder canonical) &&
            canonical != null && canonical.CalculateTotalPriceCents() == 0)
        {
            visit.financeCaptured = true;
            visit.paidAmountCents = 0L;
            FinalizeVisit(visit, out _);
        }
    }

    private void HandleFinanceTransaction(BistroBuilderFinanceTransactionRecord transaction)
    {
        if (transaction == null || transaction.kind != BistroBuilderFinanceTransactionKind.Credit ||
            !string.Equals(transaction.sourceSystemId,
                BistroBuilderSalesRevenuePolicy.SourceSystemId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(transaction.sourceReferenceId) ||
            !groupByOrderId.TryGetValue(transaction.sourceReferenceId, out int groupId) ||
            !visitsByGroup.TryGetValue(groupId, out var visit))
            return;

        visit.financeCaptured = true;
        visit.paidAmountCents = transaction.amountCents;
        if (visit.orderCompleted)
            FinalizeVisit(visit, out _);
    }

    private bool FinalizeVisit(
        BistroBuilderReputationVisitRuntimeRecord visit,
        out string error)
    {
        error = string.Empty;
        if (visit == null) return true;
        if (!BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit, generalGameStateService.DayIndex,
                out BistroBuilderCustomerExperienceRecord experience,
                out error))
            return false;
        if (!reputationService.TryRecordExperience(
                experience, out _, out error))
            return false;

        LastRecordedSatisfactionBasisPoints = experience.overallSatisfactionBasisPoints;
        LastRecordedExperienceId = experience.experienceId;
        visitsByGroup.Remove(visit.groupId);
        if (!string.IsNullOrWhiteSpace(visit.canonicalOrderId))
            groupByOrderId.Remove(visit.canonicalOrderId);
        ExperienceRuntimeChanged?.Invoke();
        return true;
    }

    private void PopulateOrderReference(
        string orderId,
        BistroBuilderReputationVisitRuntimeRecord visit)
    {
        if (!canonicalOrderService.TryGetOrderSnapshot(
                orderId, out BistroBuilderCanonicalOrder order) || order == null)
            return;

        long reference = 0L;
        float expected = 0f;
        long quality = 0L;
        int qualityCount = 0;
        for (int i = 0; i < order.Lines.Count; i++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[i];
            if (line == null || line.State == BistroBuilderCanonicalOrderLineState.Cancelled)
                continue;

            if (dishCatalogService.TryGetDefinition(
                    line.DishId, out BistroBuilderDishDefinition dish) && dish != null)
            {
                reference += dish.BasePriceCents;
                expected = Mathf.Max(expected, dish.BasePreparationSeconds);
                int potential = 6500 + (dish.Complexity - 1) * 250;
                if (line.WasSignatureDishAtOrder) potential += 500;
                quality += Mathf.Clamp(potential, 5000, 9500);
                qualityCount++;
            }
            else
            {
                reference += line.PriceCentsAtOrder;
            }
        }

        visit.referenceAmountCents = Math.Max(0L, reference);
        visit.expectedFoodSeconds = Mathf.Max(4f, expected);
        if (qualityCount > 0)
            visit.foodQualityPotentialBasisPoints =
                Mathf.Clamp((int)Math.Round(quality / (double)qualityCount), 0, 10000);
    }

    private static void AccumulateWait(
        BistroBuilderReputationVisitRuntimeRecord visit,
        CustomerGroupState state,
        float delta)
    {
        switch (state)
        {
            case CustomerGroupState.WaitingForTable:
                visit.tableWaitSeconds += delta;
                break;
            case CustomerGroupState.WaitingForWaiter:
            case CustomerGroupState.WaitingForBarOrder:
                visit.waiterWaitSeconds += delta;
                break;
            case CustomerGroupState.WaitingForFood:
            case CustomerGroupState.WaitingForBarItems:
                visit.foodWaitSeconds += delta;
                break;
            case CustomerGroupState.WaitingForBill:
                visit.billWaitSeconds += delta;
                break;
        }
    }

    private static BistroBuilderRestaurantDiscoverySource ResolveDiscoverySource(
        BistroBuilderCustomerAcquisitionTag tag)
    {
        if (tag == null) return BistroBuilderRestaurantDiscoverySource.Organic;
        if (tag.ReturningVisit) return BistroBuilderRestaurantDiscoverySource.ReturningGuest;
        string source = (tag.DiscoverySourceId ?? string.Empty).Trim().ToLowerInvariant();
        if (source == "marketing" || tag.MarketingInfluenced)
            return BistroBuilderRestaurantDiscoverySource.Marketing;
        if (source == "word_of_mouth")
            return BistroBuilderRestaurantDiscoverySource.WordOfMouth;
        if (source == "reservation")
            return BistroBuilderRestaurantDiscoverySource.Reservation;
        return BistroBuilderRestaurantDiscoverySource.Organic;
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (orderSystem != null)
        {
            orderSystem.OrderCreated += HandleOrderCreated;
            orderSystem.OrderCompleted += HandleOrderCompleted;
        }
        if (financeService != null)
            financeService.TransactionPosted += HandleFinanceTransaction;
    }

    private void Unsubscribe()
    {
        if (orderSystem != null)
        {
            orderSystem.OrderCreated -= HandleOrderCreated;
            orderSystem.OrderCompleted -= HandleOrderCompleted;
        }
        if (financeService != null)
            financeService.TransactionPosted -= HandleFinanceTransaction;
    }

    private void CacheDependencies()
    {
        if (reputationService == null) TryGetComponent(out reputationService);
        if (tableAssignmentSystem == null) TryGetComponent(out tableAssignmentSystem);
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (canonicalOrderService == null) TryGetComponent(out canonicalOrderService);
        if (financeService == null) TryGetComponent(out financeService);
        if (dishCatalogService == null) TryGetComponent(out dishCatalogService);
        if (generalGameStateService == null ||
            upgradeEffectsService == null) TryGetComponent(out generalGameStateService);
        if (upgradeEffectsService == null) TryGetComponent(out upgradeEffectsService);
    }

    private static bool ContainsReference(
        IReadOnlyList<CustomerGroup> groups,
        CustomerGroup target)
    {
        for (int i = 0; i < groups.Count; i++)
            if (ReferenceEquals(groups[i], target)) return true;
        return false;
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
