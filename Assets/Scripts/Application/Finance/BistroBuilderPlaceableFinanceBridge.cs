using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Integra colocables con finance.runtime. Compra y retirada mantienen pistas
/// históricas independientes para que Undo/Redo compense exactamente el acto
/// que se está deshaciendo, incluso cuando ambos se intercalan.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Placeable Finance Bridge")]
public sealed class BistroBuilderPlaceableFinanceBridge :
    MonoBehaviour,
    IRestaurantPlaceableEconomyGate,
    IRestaurantEditHistoryOperationParticipant
{
    public const string SourceSystemId = "placeable_economy";
    private const string CreateTrack = "create";
    private const string DeleteTrack = "delete";

    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderDiscretionaryFinanceService discretionaryFinanceService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private RestaurantPlaceableCreationService creationService;
    [SerializeField] private RestaurantPlaceableDeletionService deletionService;
    [SerializeField] private RestaurantPlacementHistoryService historyService;
    [SerializeField] private BistroBuilderMoneyPopupService moneyPopupService;

    private readonly Dictionary<string, FinancialPlan> pendingCreations =
        new Dictionary<string, FinancialPlan>(StringComparer.Ordinal);
    private readonly Dictionary<string, FinancialPlan> pendingDeletions =
        new Dictionary<string, FinancialPlan>(StringComparer.Ordinal);
    private readonly Dictionary<string, FinancialPlan> committedWorldPlans =
        new Dictionary<string, FinancialPlan>(StringComparer.Ordinal);
    private readonly Dictionary<string, PopupPayload> pendingWorldPopups =
        new Dictionary<string, PopupPayload>(StringComparer.Ordinal);
    private readonly Dictionary<IRestaurantEditHistoryCommand, HistoryPlan> pendingHistory =
        new Dictionary<IRestaurantEditHistoryCommand, HistoryPlan>();
    private readonly Dictionary<IRestaurantEditHistoryCommand, PopupPayload> pendingHistoryPopups =
        new Dictionary<IRestaurantEditHistoryCommand, PopupPayload>();

    private bool isBound;
    public bool IsBound => isBound;

    private void OnEnable()
    {
        Bind();
        SubscribePresentationEvents();
    }

    private void OnDisable()
    {
        UnsubscribePresentationEvents();
        Unbind();
        ClearPendingState();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (financeService == null || discretionaryFinanceService == null ||
            generalGameStateService == null || gameClock == null ||
            creationService == null || deletionService == null ||
            historyService == null || moneyPopupService == null)
        {
            error = "3F necesita Finanzas, gasto discrecional, reloj, estado general, creación, eliminación, historial y popup monetario.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !discretionaryFinanceService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !moneyPopupService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetDeletionPreview(
        RestaurantPlaceableObject placeable,
        out BistroBuilderPlaceableDisposalPreview preview,
        out string error)
    {
        preview = default;
        if (!TryValidatePlaceable(placeable, out error))
        {
            return false;
        }

        return BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
            placeable.ItemDefinition,
            ResolveAcquisitionCostCents(placeable),
            out preview,
            out error);
    }

    public bool TryAuthorizeCreation(RestaurantPlaceableObject placeable, out string error)
    {
        if (!TryValidatePlaceable(placeable, out error))
        {
            return false;
        }

        FinancialPlan plan = BuildPurchasePlan(
            placeable,
            BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCents(placeable.ItemDefinition));
        if (!TryAuthorizePlan(plan, out error))
        {
            return false;
        }

        pendingCreations[placeable.InstanceId] = plan;
        return true;
    }

    public bool TryCommitCreation(RestaurantPlaceableObject placeable, out string error)
    {
        return TryCommitWorldPlan(placeable, pendingCreations, out error);
    }

    public bool TryRollbackCreation(RestaurantPlaceableObject placeable, out string error)
    {
        return TryRollbackCommittedWorldPlan(placeable, out error);
    }

    public bool TryAuthorizeDeletion(RestaurantPlaceableObject placeable, out string error)
    {
        if (!TryGetDeletionPreview(placeable, out var preview, out error))
        {
            return false;
        }

        FinancialPlan plan = BuildDisposalPlan(placeable, preview);
        if (!TryAuthorizePlan(plan, out error))
        {
            return false;
        }

        pendingDeletions[placeable.InstanceId] = plan;
        return true;
    }

    public bool TryCommitDeletion(RestaurantPlaceableObject placeable, out string error)
    {
        return TryCommitWorldPlan(placeable, pendingDeletions, out error);
    }

    public bool TryRollbackDeletion(RestaurantPlaceableObject placeable, out string error)
    {
        return TryRollbackCommittedWorldPlan(placeable, out error);
    }

    public bool TryAuthorizeHistoryOperation(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error)
    {
        error = string.Empty;
        if (!TryResolveFinancialPlaceable(command, out RestaurantPlaceableObject placeable))
        {
            return true;
        }

        string track = ResolveTrack(command.CommandType);
        if (!TryBuildInverseOfLatestTrackGroup(
                placeable,
                track,
                direction == RestaurantEditHistoryDirection.Undo
                    ? "Deshacer " + command.Description
                    : "Rehacer " + command.Description,
                out FinancialPlan plan,
                out error) ||
            !TryAuthorizePlan(plan, out error))
        {
            return false;
        }

        pendingHistory[command] = new HistoryPlan(direction, plan, placeable);
        return true;
    }

    public bool TryCommitHistoryOperation(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error)
    {
        error = string.Empty;
        if (!TryResolveFinancialPlaceable(command, out _))
        {
            return true;
        }

        if (!pendingHistory.TryGetValue(command, out HistoryPlan historyPlan) ||
            historyPlan.Direction != direction)
        {
            error = "La operación histórica no conserva una autorización financiera válida.";
            return false;
        }

        pendingHistory.Remove(command);
        if (!TryPostPlan(historyPlan.Plan, out error))
        {
            return false;
        }

        long net = historyPlan.Plan.NetCashCents;
        if (net != 0L)
        {
            pendingHistoryPopups[command] = new PopupPayload(
                net,
                ResolveWorldPosition(historyPlan.Placeable));
        }
        return true;
    }

    private void Bind()
    {
        if (isBound || creationService == null || deletionService == null || historyService == null)
        {
            return;
        }

        if (!creationService.TryBindEconomyGate(this, out string creationError))
        {
            Debug.LogError("3F no pudo enlazar creación. " + creationError, this);
            return;
        }
        if (!deletionService.TryBindEconomyGate(this, out string deletionError))
        {
            creationService.UnbindEconomyGate(this);
            Debug.LogError("3F no pudo enlazar eliminación. " + deletionError, this);
            return;
        }
        if (!historyService.TryBindOperationParticipant(this, out string historyError))
        {
            deletionService.UnbindEconomyGate(this);
            creationService.UnbindEconomyGate(this);
            Debug.LogError("3F no pudo enlazar historial. " + historyError, this);
            return;
        }
        isBound = true;
    }

    private void Unbind()
    {
        historyService?.UnbindOperationParticipant(this);
        deletionService?.UnbindEconomyGate(this);
        creationService?.UnbindEconomyGate(this);
        isBound = false;
    }

    private void SubscribePresentationEvents()
    {
        if (creationService != null)
        {
            creationService.CreationCommitted -= HandleWorldOperationCommitted;
            creationService.CreationCommitted += HandleWorldOperationCommitted;
        }
        if (deletionService != null)
        {
            deletionService.PlaceableDeleted -= HandleWorldOperationCommitted;
            deletionService.PlaceableDeleted += HandleWorldOperationCommitted;
        }
        if (historyService != null)
        {
            historyService.CommandUndone -= HandleHistoryOperationCompleted;
            historyService.CommandUndone += HandleHistoryOperationCompleted;
            historyService.CommandRedone -= HandleHistoryOperationCompleted;
            historyService.CommandRedone += HandleHistoryOperationCompleted;
        }
    }

    private void UnsubscribePresentationEvents()
    {
        if (creationService != null)
        {
            creationService.CreationCommitted -= HandleWorldOperationCommitted;
        }
        if (deletionService != null)
        {
            deletionService.PlaceableDeleted -= HandleWorldOperationCommitted;
        }
        if (historyService != null)
        {
            historyService.CommandUndone -= HandleHistoryOperationCompleted;
            historyService.CommandRedone -= HandleHistoryOperationCompleted;
        }
    }

    private void HandleWorldOperationCommitted(RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
        {
            return;
        }

        committedWorldPlans.Remove(placeable.InstanceId);
        if (pendingWorldPopups.TryGetValue(placeable.InstanceId, out PopupPayload popup))
        {
            pendingWorldPopups.Remove(placeable.InstanceId);
            ShowPopup(popup);
        }
    }

    private void HandleHistoryOperationCompleted(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryCommandResult result)
    {
        if (command != null &&
            pendingHistoryPopups.TryGetValue(command, out PopupPayload popup))
        {
            pendingHistoryPopups.Remove(command);
            ShowPopup(popup);
        }
    }

    private bool TryCommitWorldPlan(
        RestaurantPlaceableObject placeable,
        Dictionary<string, FinancialPlan> pending,
        out string error)
    {
        if (!TryValidatePlaceable(placeable, out error))
        {
            return false;
        }

        string instanceId = placeable.InstanceId;
        if (!pending.TryGetValue(instanceId, out FinancialPlan plan))
        {
            error = "La operación no conserva una autorización financiera válida.";
            return false;
        }

        pending.Remove(instanceId);
        if (!TryPostPlan(plan, out error))
        {
            return false;
        }

        committedWorldPlans[instanceId] = plan;
        long net = plan.NetCashCents;
        if (net != 0L)
        {
            pendingWorldPopups[instanceId] =
                new PopupPayload(net, ResolveWorldPosition(placeable));
        }
        return true;
    }

    private bool TryRollbackCommittedWorldPlan(
        RestaurantPlaceableObject placeable,
        out string error)
    {
        if (!TryValidatePlaceable(placeable, out error))
        {
            return false;
        }

        string instanceId = placeable.InstanceId;
        pendingWorldPopups.Remove(instanceId);
        if (!committedWorldPlans.TryGetValue(instanceId, out FinancialPlan committed))
        {
            error = "No existe una operación financiera recién confirmada para revertir.";
            return false;
        }

        committedWorldPlans.Remove(instanceId);
        if (committed.Requests.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        FinancialPlan rollback = BuildInversePlan(
            placeable,
            committed,
            committed.GroupOrdinal + 1,
            "Reversión de operación no confirmada");
        return TryPostPlan(rollback, out error);
    }

    private FinancialPlan BuildPurchasePlan(RestaurantPlaceableObject placeable, long purchaseCents)
    {
        int group = ResolveNextGroupOrdinal(placeable.InstanceId, CreateTrack);
        var requests = new List<BistroBuilderFinanceTransactionRequest>(1);
        if (purchaseCents > 0L)
        {
            requests.Add(BuildRequest(
                placeable, CreateTrack, group, "purchase",
                BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCategory(placeable.ItemDefinition),
                BistroBuilderFinanceTransactionKind.Debit,
                purchaseCents,
                "Compra de " + placeable.DisplayName + "."));
        }
        return new FinancialPlan(CreateTrack, group, requests);
    }

    private FinancialPlan BuildDisposalPlan(
        RestaurantPlaceableObject placeable,
        BistroBuilderPlaceableDisposalPreview preview)
    {
        int group = ResolveNextGroupOrdinal(placeable.InstanceId, DeleteTrack);
        var requests = new List<BistroBuilderFinanceTransactionRequest>(2);
        if (preview.ResaleCents > 0L)
        {
            requests.Add(BuildRequest(
                placeable, DeleteTrack, group, "resale", "income.asset_resale",
                BistroBuilderFinanceTransactionKind.Credit,
                preview.ResaleCents,
                "Venta de " + placeable.DisplayName + "."));
        }
        if (preview.RemovalCostCents > 0L)
        {
            bool demolition = preview.Mode == RestaurantPlaceableDisposalMode.Demolition;
            requests.Add(BuildRequest(
                placeable, DeleteTrack, group, "removal",
                demolition ? "expense.demolition" : "expense.asset_removal",
                BistroBuilderFinanceTransactionKind.Debit,
                preview.RemovalCostCents,
                (demolition ? "Demolición de " : "Retirada de ") +
                placeable.DisplayName + "."));
        }
        return new FinancialPlan(DeleteTrack, group, requests);
    }

    private bool TryBuildInverseOfLatestTrackGroup(
        RestaurantPlaceableObject placeable,
        string track,
        string descriptionPrefix,
        out FinancialPlan inverse,
        out string error)
    {
        error = string.Empty;
        if (!TryGetLatestGroup(
                placeable.InstanceId,
                track,
                out int latestGroup,
                out List<BistroBuilderFinanceTransactionRecord> latestRecords))
        {
            inverse = new FinancialPlan(
                track,
                1,
                new List<BistroBuilderFinanceTransactionRequest>());
            return true;
        }

        var sourceRequests =
            new List<BistroBuilderFinanceTransactionRequest>(latestRecords.Count);
        for (int index = 0; index < latestRecords.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = latestRecords[index];
            sourceRequests.Add(new BistroBuilderFinanceTransactionRequest
            {
                categoryId = record.categoryId,
                kind = record.kind,
                amountCents = record.amountCents
            });
        }

        inverse = BuildInversePlan(
            placeable,
            new FinancialPlan(track, latestGroup, sourceRequests),
            latestGroup + 1,
            descriptionPrefix);
        return true;
    }

    private FinancialPlan BuildInversePlan(
        RestaurantPlaceableObject placeable,
        FinancialPlan source,
        int targetGroup,
        string descriptionPrefix)
    {
        var requests = new List<BistroBuilderFinanceTransactionRequest>(source.Requests.Count);
        for (int index = 0; index < source.Requests.Count; index++)
        {
            BistroBuilderFinanceTransactionRequest sourceRequest = source.Requests[index];
            requests.Add(BuildRequest(
                placeable,
                source.Track,
                targetGroup,
                "reverse" + (index + 1).ToString("D2"),
                sourceRequest.categoryId,
                sourceRequest.kind == BistroBuilderFinanceTransactionKind.Credit
                    ? BistroBuilderFinanceTransactionKind.Debit
                    : BistroBuilderFinanceTransactionKind.Credit,
                sourceRequest.amountCents,
                descriptionPrefix + ": " + placeable.DisplayName + "."));
        }
        return new FinancialPlan(source.Track, targetGroup, requests);
    }

    private bool TryAuthorizePlan(FinancialPlan plan, out string error)
    {
        if (!plan.TryCalculateCashEffects(
                out long creditCents,
                out long debitCents,
                out error))
        {
            return false;
        }
        return discretionaryFinanceService.TryAuthorizeNetCashEffect(
            creditCents,
            debitCents,
            out error);
    }

    private bool TryPostPlan(FinancialPlan plan, out string error)
    {
        if (plan.Requests.Count == 0)
        {
            error = string.Empty;
            return true;
        }
        return financeService.TryPostTransactions(plan.Requests, out _, out error);
    }

    private BistroBuilderFinanceTransactionRequest BuildRequest(
        RestaurantPlaceableObject placeable,
        string track,
        int group,
        string leg,
        string category,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents,
        string description)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = BuildOperationId(placeable.InstanceId, track, group, leg),
            sourceSystemId = SourceSystemId,
            sourceReferenceId = placeable.InstanceId,
            categoryId = category,
            kind = kind,
            amountCents = amountCents,
            dayIndex = generalGameStateService.DayIndex,
            minuteOfDay = gameClock.Hour * 60 + gameClock.Minute,
            description = description
        };
    }

    private long ResolveAcquisitionCostCents(RestaurantPlaceableObject placeable)
    {
        BistroBuilderFinanceSnapshot snapshot = financeService.CreateSnapshot();
        if (snapshot != null && snapshot.transactions != null)
        {
            for (int index = 0; index < snapshot.transactions.Count; index++)
            {
                BistroBuilderFinanceTransactionRecord record = snapshot.transactions[index];
                if (record != null &&
                    record.kind == BistroBuilderFinanceTransactionKind.Debit &&
                    string.Equals(record.sourceSystemId, SourceSystemId, StringComparison.Ordinal) &&
                    string.Equals(record.sourceReferenceId, placeable.InstanceId, StringComparison.Ordinal) &&
                    record.categoryId != null &&
                    record.categoryId.StartsWith("investment.", StringComparison.Ordinal) &&
                    OperationBelongsToTrack(record.operationId, CreateTrack))
                {
                    return record.amountCents;
                }
            }
        }
        return BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCents(placeable.ItemDefinition);
    }

    private int ResolveNextGroupOrdinal(string instanceId, string track)
    {
        return TryGetLatestGroup(instanceId, track, out int latest, out _)
            ? checked(latest + 1)
            : 1;
    }

    private bool TryGetLatestGroup(
        string instanceId,
        string track,
        out int latestGroup,
        out List<BistroBuilderFinanceTransactionRecord> records)
    {
        latestGroup = 0;
        records = new List<BistroBuilderFinanceTransactionRecord>();
        BistroBuilderFinanceSnapshot snapshot = financeService.CreateSnapshot();
        if (snapshot == null || snapshot.transactions == null)
        {
            return false;
        }

        for (int index = 0; index < snapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = snapshot.transactions[index];
            if (record == null ||
                !string.Equals(record.sourceSystemId, SourceSystemId, StringComparison.Ordinal) ||
                !string.Equals(record.sourceReferenceId, instanceId, StringComparison.Ordinal) ||
                !OperationBelongsToTrack(record.operationId, track) ||
                !TryParseGroupOrdinal(record.operationId, out int group))
            {
                continue;
            }

            if (group > latestGroup)
            {
                latestGroup = group;
                records.Clear();
                records.Add(record);
            }
            else if (group == latestGroup)
            {
                records.Add(record);
            }
        }

        records.Sort((left, right) => left.sequence.CompareTo(right.sequence));
        return latestGroup > 0;
    }

    private static string ResolveTrack(RestaurantEditHistoryCommandType commandType)
    {
        return commandType == RestaurantEditHistoryCommandType.Create
            ? CreateTrack
            : DeleteTrack;
    }

    private static bool TryResolveFinancialPlaceable(
        IRestaurantEditHistoryCommand command,
        out RestaurantPlaceableObject placeable)
    {
        placeable = null;
        if (command == null ||
            (command.CommandType != RestaurantEditHistoryCommandType.Create &&
             command.CommandType != RestaurantEditHistoryCommandType.Delete))
        {
            return false;
        }

        if (command.PrimaryTarget is RestaurantPlaceableObject direct)
        {
            placeable = direct;
            return direct != null;
        }
        if (command.PrimaryTarget is Component component)
        {
            return component.TryGetComponent(out placeable);
        }
        if (command.PrimaryTarget is GameObject gameObject)
        {
            return gameObject.TryGetComponent(out placeable);
        }
        return false;
    }

    private static bool TryValidatePlaceable(RestaurantPlaceableObject placeable, out string error)
    {
        if (placeable == null || placeable.ItemDefinition == null ||
            string.IsNullOrWhiteSpace(placeable.InstanceId))
        {
            error = "El colocable no tiene identidad o definición económica estable.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static string BuildOperationId(
        string instanceId,
        string track,
        int group,
        string leg)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(instanceId ?? string.Empty));
            var builder = new StringBuilder(64);
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }
            return "placeable_fin_" + builder +
                   "_" + track +
                   "_g" + group.ToString("D6") +
                   "_" + (leg ?? "leg").Trim().ToLowerInvariant();
        }
    }

    private static bool OperationBelongsToTrack(string operationId, string track)
    {
        return !string.IsNullOrWhiteSpace(operationId) &&
               operationId.IndexOf("_" + track + "_g", StringComparison.Ordinal) >= 0;
    }

    private static bool TryParseGroupOrdinal(string operationId, out int group)
    {
        group = 0;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }
        int marker = operationId.LastIndexOf("_g", StringComparison.Ordinal);
        return marker >= 0 && marker + 8 <= operationId.Length &&
               int.TryParse(operationId.Substring(marker + 2, 6), out group) && group > 0;
    }

    private static Vector3 ResolveWorldPosition(RestaurantPlaceableObject placeable)
    {
        if (placeable == null)
        {
            return Vector3.zero;
        }

        Renderer[] renderers = placeable.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
        }

        Collider[] colliders = placeable.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int index = 1; index < colliders.Length; index++)
            {
                bounds.Encapsulate(colliders[index].bounds);
            }
            return new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
        }

        return placeable.transform.position + Vector3.up * 0.75f;
    }

    private void ShowPopup(PopupPayload popup)
    {
        if (moneyPopupService != null && popup.SignedCents != 0L)
        {
            moneyPopupService.Show(popup.SignedCents, popup.WorldPosition);
        }
    }

    private void ClearPendingState()
    {
        pendingCreations.Clear();
        pendingDeletions.Clear();
        committedWorldPlans.Clear();
        pendingWorldPopups.Clear();
        pendingHistory.Clear();
        pendingHistoryPopups.Clear();
    }

    private readonly struct HistoryPlan
    {
        public RestaurantEditHistoryDirection Direction { get; }
        public FinancialPlan Plan { get; }
        public RestaurantPlaceableObject Placeable { get; }
        public HistoryPlan(
            RestaurantEditHistoryDirection direction,
            FinancialPlan plan,
            RestaurantPlaceableObject placeable)
        {
            Direction = direction;
            Plan = plan;
            Placeable = placeable;
        }
    }

    private readonly struct PopupPayload
    {
        public long SignedCents { get; }
        public Vector3 WorldPosition { get; }
        public PopupPayload(long signedCents, Vector3 worldPosition)
        {
            SignedCents = signedCents;
            WorldPosition = worldPosition;
        }
    }

    private readonly struct FinancialPlan
    {
        public string Track { get; }
        public int GroupOrdinal { get; }
        public IReadOnlyList<BistroBuilderFinanceTransactionRequest> Requests { get; }
        public long NetCashCents
        {
            get
            {
                long net = 0L;
                for (int index = 0; index < Requests.Count; index++)
                {
                    BistroBuilderFinanceTransactionRequest request = Requests[index];
                    net = request.kind == BistroBuilderFinanceTransactionKind.Credit
                        ? checked(net + request.amountCents)
                        : checked(net - request.amountCents);
                }
                return net;
            }
        }

        public FinancialPlan(
            string track,
            int groupOrdinal,
            IReadOnlyList<BistroBuilderFinanceTransactionRequest> requests)
        {
            Track = track ?? string.Empty;
            GroupOrdinal = groupOrdinal;
            Requests = requests ?? Array.Empty<BistroBuilderFinanceTransactionRequest>();
        }

        public bool TryCalculateCashEffects(
            out long creditCents,
            out long debitCents,
            out string error)
        {
            creditCents = 0L;
            debitCents = 0L;
            try
            {
                for (int index = 0; index < Requests.Count; index++)
                {
                    BistroBuilderFinanceTransactionRequest request = Requests[index];
                    if (request.kind == BistroBuilderFinanceTransactionKind.Credit)
                    {
                        creditCents = checked(creditCents + request.amountCents);
                    }
                    else
                    {
                        debitCents = checked(debitCents + request.amountCents);
                    }
                }
            }
            catch (OverflowException)
            {
                error = "El plan financiero del colocable queda fuera de rango.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
