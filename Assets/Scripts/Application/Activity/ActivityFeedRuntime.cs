using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public interface IActivityLocalizationResolver
{
    string Resolve(string key, string fallback);
}

public sealed class ActivityFallbackLocalizationResolver : IActivityLocalizationResolver
{
    public string Resolve(string key, string fallback) => fallback ?? string.Empty;
}

public sealed class ActivityFormattedText
{
    public string Title { get; }
    public string Body { get; }

    public ActivityFormattedText(string title, string body)
    {
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
    }
}

public sealed class ActivityTemplateFormatter
{
    private IActivityLocalizationResolver localizationResolver;

    public ActivityTemplateFormatter(IActivityLocalizationResolver localizationResolver = null)
    {
        this.localizationResolver =
            localizationResolver ?? new ActivityFallbackLocalizationResolver();
    }

    public void SetLocalizationResolver(IActivityLocalizationResolver resolver)
    {
        localizationResolver = resolver ?? new ActivityFallbackLocalizationResolver();
    }

    public ActivityFormattedText Format(ActivityDisplayEntry entry)
    {
        if (entry == null || entry.Definition == null || entry.Primary == null)
            return new ActivityFormattedText(string.Empty, string.Empty);

        string title = !string.IsNullOrWhiteSpace(entry.TitleOverride)
            ? entry.TitleOverride
            : localizationResolver.Resolve(
                entry.Definition.TitleKey,
                entry.Definition.FallbackTitle);

        string body = !string.IsNullOrWhiteSpace(entry.BodyOverride)
            ? entry.BodyOverride
            : localizationResolver.Resolve(
                entry.Definition.BodyKey,
                entry.Definition.FallbackBody);

        ActivityEventPayload payload = entry.Primary.payload;
        return new ActivityFormattedText(
            ReplaceTokens(title, payload),
            ReplaceTokens(body, payload));
    }

    public static string ReplaceTokens(string template, ActivityEventPayload payload)
    {
        if (string.IsNullOrEmpty(template) || payload == null)
            return template ?? string.Empty;

        string result = template;
        for (int i = 0; i < payload.parameters.Count; i++)
        {
            ActivityTemplateParameter parameter = payload.parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.key))
                continue;
            result = result.Replace(
                "{" + parameter.key + "}",
                parameter.value ?? string.Empty);
        }

        return result;
    }
}

public sealed class ActivityFeedAggregator
{
    private readonly HashSet<long> consumed = new HashSet<long>();
    private readonly List<ActivityEventInstance> candidates =
        new List<ActivityEventInstance>(128);

    public void Build(
        IReadOnlyList<ActivityEventInstance> source,
        ActivityFilter filter,
        int currentDayIndex,
        List<ActivityDisplayEntry> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        candidates.Clear();
        consumed.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ActivityEventInstance item = source[i];
            if (item == null ||
                !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition) ||
                !MatchesFilter(item, definition, filter, currentDayIndex))
                continue;

            candidates.Add(item);
        }

        candidates.Sort(CompareInstances);

        for (int i = 0; i < candidates.Count; i++)
        {
            ActivityEventInstance item = candidates[i];
            if (item == null || consumed.Contains(item.sequence) ||
                !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition))
                continue;

            ActivityDisplayEntry entry = BuildEntry(item, definition);
            consumed.Add(item.sequence);

            if (definition.Aggregation == ActivityAggregationRule.WaitingTables60Seconds)
                AggregateWaitingTables(item, entry);
            else if (definition.Aggregation == ActivityAggregationRule.StaffShortageSimultaneous)
                AggregateStaffShortages(item, entry);
            else if (definition.Aggregation == ActivityAggregationRule.UpsellByZone)
                AggregateUpsell(item, entry);

            destination.Add(entry);
        }
    }

    private void AggregateWaitingTables(ActivityEventInstance seed, ActivityDisplayEntry entry)
    {
        const double windowMinutes = 1d;
        for (int i = 0; i < candidates.Count; i++)
        {
            ActivityEventInstance candidate = candidates[i];
            if (candidate == null || consumed.Contains(candidate.sequence) ||
                !string.Equals(candidate.eventId, seed.eventId, StringComparison.Ordinal) ||
                Math.Abs(candidate.AbsoluteGameMinute - seed.AbsoluteGameMinute) > windowMinutes)
                continue;

            consumed.Add(candidate.sequence);
            AddTarget(entry, candidate.target);
            entry.AggregatedCount++;
        }

        if (entry.AggregatedCount > 1)
        {
            entry.TitleOverride = "Espera elevada";
            entry.BodyOverride = entry.AggregatedCount + " mesas necesitan atención";
        }
    }

    private void AggregateStaffShortages(ActivityEventInstance seed, ActivityDisplayEntry entry)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            ActivityEventInstance candidate = candidates[i];
            if (candidate == null || consumed.Contains(candidate.sequence) ||
                candidate.resolved ||
                !string.Equals(candidate.eventId, seed.eventId, StringComparison.Ordinal))
                continue;

            consumed.Add(candidate.sequence);
            AddTarget(entry, candidate.target);
            entry.AggregatedCount++;
        }

        if (entry.AggregatedCount > 1)
        {
            entry.TitleOverride = "Falta de personal";
            entry.BodyOverride = entry.AggregatedCount + " zonas afectadas";
        }
    }

    private void AggregateUpsell(ActivityEventInstance seed, ActivityDisplayEntry entry)
    {
        string zone = string.Empty;
        seed.payload?.TryGet("zone", out zone);

        for (int i = 0; i < candidates.Count; i++)
        {
            ActivityEventInstance candidate = candidates[i];
            if (candidate == null || consumed.Contains(candidate.sequence) ||
                candidate.resolved)
                continue;

            if (!ActivityEventCatalog.TryGet(candidate.eventId, out ActivityEventDefinition definition) ||
                definition.Aggregation != ActivityAggregationRule.UpsellByZone)
                continue;

            string candidateZone = string.Empty;
            candidate.payload?.TryGet("zone", out candidateZone);
            if (!string.Equals(zone ?? string.Empty, candidateZone ?? string.Empty, StringComparison.Ordinal))
                continue;

            consumed.Add(candidate.sequence);
            AddTarget(entry, candidate.target);
            entry.AggregatedCount++;
        }

        if (entry.AggregatedCount > 1)
        {
            entry.TitleOverride = "Oportunidades de venta";
            entry.BodyOverride = entry.AggregatedCount +
                (string.IsNullOrWhiteSpace(zone) ? " mesas" : " mesas · " + zone);
        }
    }

    private static ActivityDisplayEntry BuildEntry(
        ActivityEventInstance item,
        ActivityEventDefinition definition)
    {
        var entry = new ActivityDisplayEntry
        {
            Definition = definition,
            Primary = item,
            AggregatedCount = 1
        };
        AddTarget(entry, item.target);
        return entry;
    }

    private static void AddTarget(ActivityDisplayEntry entry, ActivityTargetRef target)
    {
        if (entry == null || target == null || target.IsEmpty)
            return;

        for (int i = 0; i < entry.Targets.Count; i++)
        {
            ActivityTargetRef existing = entry.Targets[i];
            if (existing != null &&
                existing.targetType == target.targetType &&
                string.Equals(existing.targetId, target.targetId, StringComparison.Ordinal))
                return;
        }

        entry.Targets.Add(target.DeepClone());
    }

    private static bool MatchesFilter(
        ActivityEventInstance item,
        ActivityEventDefinition definition,
        ActivityFilter filter,
        int currentDayIndex)
    {
        switch (filter)
        {
            case ActivityFilter.Incidents:
                return definition.Category == ActivityCategory.Incident;
            case ActivityFilter.Opportunities:
                return definition.Category == ActivityCategory.Opportunity;
            case ActivityFilter.Reservations:
                return definition.Category == ActivityCategory.Reservation;
            default:
                return item.dayIndex == Math.Max(1, currentDayIndex);
        }
    }

    private static int CompareInstances(ActivityEventInstance left, ActivityEventInstance right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int leftPriority = ResolvePriority(left);
        int rightPriority = ResolvePriority(right);
        int byPriority = rightPriority.CompareTo(leftPriority);
        if (byPriority != 0)
            return byPriority;

        int byTime = right.AbsoluteGameMinute.CompareTo(left.AbsoluteGameMinute);
        if (byTime != 0)
            return byTime;

        return right.sequence.CompareTo(left.sequence);
    }

    private static int ResolvePriority(ActivityEventInstance item)
    {
        if (!ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition))
            return 0;

        if (!item.resolved && definition.Severity == ActivitySeverity.Critical)
            return 600;

        switch (definition.Severity)
        {
            case ActivitySeverity.Critical: return 500;
            case ActivitySeverity.Attention: return 400;
            case ActivitySeverity.Opportunity: return 300;
            case ActivitySeverity.Reservation: return 250;
            case ActivitySeverity.Positive: return 150;
            default: return 100;
        }
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Activity/Activity Feed Service")]
public sealed class ActivityFeedService : MonoBehaviour, IActivityEventPublisher
{
    public const string RuntimeRevision = "ACTIVITY-RUNTIME-V1.0";

    [SerializeField, Min(16)]
    private int maxHistoryEvents = 128;

    private readonly List<ActivityEventInstance> events =
        new List<ActivityEventInstance>(128);

    private readonly Dictionary<string, ActivityEventInstance> latestByDedupKey =
        new Dictionary<string, ActivityEventInstance>(StringComparer.Ordinal);

    private readonly Dictionary<string, ActivityEventInstance> activeStateByKey =
        new Dictionary<string, ActivityEventInstance>(StringComparer.Ordinal);

    private GameClock gameClock;
    private BistroBuilderGeneralGameStateService generalGameState;
    private long nextSequence = 1;

    public event Action Changed;

    public IReadOnlyList<ActivityEventInstance> Events => events;
    public int Count => events.Count;
    public int CurrentDayIndex
    {
        get
        {
            ResolveClock();
            return generalGameState != null ? Math.Max(1, generalGameState.DayIndex) : 1;
        }
    }

    private void Awake()
    {
        ResolveClock();
        if (!ActivityEventCatalog.Validate(out string error))
        {
            Debug.LogError("Catálogo ACTIVIDAD inválido: " + error, this);
            enabled = false;
        }
    }

    private void Start()
    {
        BistroBuilderActivityPersistenceInstaller.TryInstall(this);
    }

    public ActivityPublishResult Publish(
        ActivityEventId eventId,
        ActivityEventPayload payload = null,
        ActivityTargetRef target = null)
    {
        return PublishAt(eventId, payload, target, CurrentDayIndex, CurrentMinuteOfDay());
    }

    public ActivityPublishResult PublishAt(
        ActivityEventId eventId,
        ActivityEventPayload payload,
        ActivityTargetRef target,
        int dayIndex,
        double minuteOfDay)
    {
        if (!ActivityEventCatalog.TryGet(eventId, out ActivityEventDefinition definition))
        {
            Debug.LogWarning("ACTIVIDAD ignoró EventId desconocido: " + eventId.Value, this);
            return new ActivityPublishResult(false, false, null);
        }

        ActivityTargetRef normalizedTarget = NormalizeTarget(target, definition.TargetType);
        if (!ValidateTargetShape(definition, normalizedTarget))
        {
            Debug.LogWarning(
                "ACTIVIDAD ignoró " + eventId.Value +
                " por TargetRef incompatible con " + definition.TargetType + ".", this);
            return new ActivityPublishResult(false, false, null);
        }

        int normalizedDay = Math.Max(1, dayIndex);
        double normalizedMinute = Math.Max(0d, Math.Min(1439.999d, minuteOfDay));
        double absolute = (normalizedDay - 1) * 1440d + normalizedMinute;
        string dedupKey = BuildDedupKey(eventId.Value, normalizedTarget);

        if (latestByDedupKey.TryGetValue(dedupKey, out ActivityEventInstance latest) &&
            latest != null &&
            absolute - latest.AbsoluteGameMinute >= 0d &&
            absolute - latest.AbsoluteGameMinute <= definition.DedupWindowMinutes)
        {
            return new ActivityPublishResult(false, true, latest);
        }

        var instance = new ActivityEventInstance
        {
            sequence = nextSequence++,
            eventId = eventId.Value,
            target = normalizedTarget,
            payload = payload != null ? payload.DeepClone() : new ActivityEventPayload(),
            dayIndex = normalizedDay,
            minuteOfDay = normalizedMinute
        };

        events.Add(instance);
        latestByDedupKey[dedupKey] = instance;
        PruneHistory();
        Changed?.Invoke();
        return new ActivityPublishResult(true, false, instance);
    }

    public ActivityPublishResult PublishState(
        ActivityEventId eventId,
        string stateFamily,
        ActivityTargetRef target = null,
        ActivityEventPayload payload = null)
    {
        if (!ActivityEventCatalog.TryGet(eventId, out ActivityEventDefinition definition) ||
            !definition.IsStateEvent)
        {
            return Publish(eventId, payload, target);
        }

        ActivityTargetRef normalizedTarget = NormalizeTarget(target, definition.TargetType);
        string stateKey = BuildStateKey(stateFamily, normalizedTarget);

        if (activeStateByKey.TryGetValue(stateKey, out ActivityEventInstance current) &&
            current != null && !current.resolved &&
            string.Equals(current.eventId, eventId.Value, StringComparison.Ordinal))
        {
            return new ActivityPublishResult(false, true, current);
        }

        if (current != null && !current.resolved)
            ResolveInternal(current, false);

        // Una transición real (incluida salir y volver a entrar) debe poder
        // emitirse aunque ocurra dentro de la ventana de deduplicación.
        latestByDedupKey.Remove(
            BuildDedupKey(eventId.Value, normalizedTarget));

        ActivityPublishResult result = PublishAt(
            eventId,
            payload,
            normalizedTarget,
            CurrentDayIndex,
            CurrentMinuteOfDay());

        if (result.Event != null)
        {
            result.Event.stateFamily =
                (stateFamily ?? string.Empty).Trim().ToLowerInvariant();
            activeStateByKey[stateKey] = result.Event;
        }

        if (current != null || result.Accepted)
            Changed?.Invoke();

        return result;
    }

    public bool ClearState(string stateFamily, ActivityTargetRef target)
    {
        string key = BuildStateKey(stateFamily, target);
        if (!activeStateByKey.TryGetValue(key, out ActivityEventInstance current))
            return false;

        activeStateByKey.Remove(key);
        latestByDedupKey.Remove(
            BuildDedupKey(current.eventId, current.target));
        bool changed = ResolveInternal(current, false);
        if (changed)
            Changed?.Invoke();
        return changed;
    }

    public int ResolveTarget(ActivityTargetType targetType, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return 0;

        int changed = 0;
        for (int i = 0; i < events.Count; i++)
        {
            ActivityEventInstance item = events[i];
            if (item == null || item.resolved || item.target == null ||
                item.target.targetType != targetType ||
                !string.Equals(item.target.targetId, targetId, StringComparison.Ordinal))
                continue;

            if (ResolveInternal(item, false))
                changed++;
        }

        if (changed > 0)
            Changed?.Invoke();
        return changed;
    }

    public bool Resolve(ActivityEventId eventId, string targetId)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            ActivityEventInstance item = events[i];
            if (item == null || item.resolved ||
                !string.Equals(item.eventId, eventId.Value, StringComparison.Ordinal) ||
                (targetId != null && (item.target == null ||
                 !string.Equals(item.target.targetId, targetId, StringComparison.Ordinal))))
                continue;

            bool changed = ResolveInternal(item, false);
            if (changed)
                Changed?.Invoke();
            return changed;
        }

        return false;
    }

    public void ClearForNewService()
    {
        events.Clear();
        latestByDedupKey.Clear();
        activeStateByKey.Clear();
        nextSequence = 1;
        Changed?.Invoke();
    }

    public ActivityFeedSaveSnapshot CaptureSnapshot(int recentLimit = 48)
    {
        var snapshot = new ActivityFeedSaveSnapshot
        {
            schemaVersion = 1,
            nextSequence = Math.Max(1L, nextSequence)
        };

        var included = new HashSet<long>();
        for (int i = events.Count - 1; i >= 0 && snapshot.events.Count < Math.Max(8, recentLimit); i--)
        {
            ActivityEventInstance item = events[i];
            if (item == null || !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition))
                continue;

            if (definition.Lifetime == ActivityLifetime.StickyUntilResolved && !item.resolved)
                continue;

            snapshot.events.Add(item.DeepClone());
            included.Add(item.sequence);
        }

        for (int i = 0; i < events.Count; i++)
        {
            ActivityEventInstance item = events[i];
            if (item == null || included.Contains(item.sequence) ||
                !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition) ||
                definition.Lifetime != ActivityLifetime.StickyUntilResolved ||
                item.resolved)
                continue;

            snapshot.events.Add(item.DeepClone());
            included.Add(item.sequence);
        }

        snapshot.events.Sort((a, b) => a.sequence.CompareTo(b.sequence));
        return snapshot;
    }

    public bool RestoreSnapshot(
        ActivityFeedSaveSnapshot snapshot,
        Func<ActivityTargetRef, bool> targetValidator,
        out string error)
    {
        error = string.Empty;
        if (snapshot == null || snapshot.schemaVersion != 1)
        {
            error = "Snapshot de ACTIVIDAD ausente o con versión no compatible.";
            return false;
        }

        var restored = new List<ActivityEventInstance>(snapshot.events != null
            ? snapshot.events.Count
            : 0);
        var sequences = new HashSet<long>();

        if (snapshot.events != null)
        {
            for (int i = 0; i < snapshot.events.Count; i++)
            {
                ActivityEventInstance item = snapshot.events[i];
                if (item == null ||
                    item.sequence <= 0 ||
                    !sequences.Add(item.sequence) ||
                    !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition))
                    continue;

                ActivityTargetRef target = NormalizeTarget(item.target, definition.TargetType);
                if (!ValidateTargetShape(definition, target))
                    continue;

                if (targetValidator != null && !targetValidator(target))
                    continue;

                ActivityEventInstance clone = item.DeepClone();
                clone.target = target;
                restored.Add(clone);
            }
        }

        restored.Sort((a, b) => a.sequence.CompareTo(b.sequence));

        events.Clear();
        latestByDedupKey.Clear();
        activeStateByKey.Clear();
        nextSequence = Math.Max(1L, snapshot.nextSequence);

        for (int i = 0; i < restored.Count; i++)
        {
            ActivityEventInstance item = restored[i];
            events.Add(item);
            nextSequence = Math.Max(nextSequence, item.sequence + 1);
        }

        RebuildIndexes();
        Changed?.Invoke();
        return true;
    }

    public int DiscardInvalidTargets(Func<ActivityTargetRef, bool> validator)
    {
        if (validator == null)
            return 0;

        int removed = 0;
        for (int i = events.Count - 1; i >= 0; i--)
        {
            ActivityEventInstance item = events[i];
            if (item == null || item.target == null || item.target.IsEmpty)
                continue;
            if (validator(item.target))
                continue;

            events.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            RebuildIndexes();
            Changed?.Invoke();
        }

        return removed;
    }

    private bool ResolveInternal(ActivityEventInstance item, bool notify)
    {
        if (item == null || item.resolved)
            return false;

        item.resolved = true;
        item.resolvedDayIndex = CurrentDayIndex;
        item.resolvedMinuteOfDay = CurrentMinuteOfDay();
        if (notify)
            Changed?.Invoke();
        return true;
    }

    private void PruneHistory()
    {
        int limit = Math.Max(16, maxHistoryEvents);
        if (events.Count <= limit)
            return;

        for (int i = 0; i < events.Count && events.Count > limit;)
        {
            ActivityEventInstance item = events[i];
            bool sticky = item != null && !item.resolved &&
                ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition) &&
                definition.Lifetime == ActivityLifetime.StickyUntilResolved;

            if (sticky)
            {
                i++;
                continue;
            }

            events.RemoveAt(i);
        }

        RebuildIndexes();
    }

    private void RebuildIndexes()
    {
        latestByDedupKey.Clear();
        activeStateByKey.Clear();

        for (int i = 0; i < events.Count; i++)
        {
            ActivityEventInstance item = events[i];
            if (item == null)
                continue;
            latestByDedupKey[BuildDedupKey(item.eventId, item.target)] = item;

            if (!item.resolved && !string.IsNullOrWhiteSpace(item.stateFamily))
            {
                activeStateByKey[
                    BuildStateKey(item.stateFamily, item.target)
                ] = item;
            }
        }
    }

    private static ActivityTargetRef NormalizeTarget(
        ActivityTargetRef target,
        ActivityTargetType expected)
    {
        if (expected == ActivityTargetType.None)
            return new ActivityTargetRef();

        if (target == null)
            return new ActivityTargetRef(expected, string.Empty);

        return new ActivityTargetRef(expected, target.targetId);
    }

    private static bool ValidateTargetShape(
        ActivityEventDefinition definition,
        ActivityTargetRef target)
    {
        if (definition == null)
            return false;

        if (definition.TargetType == ActivityTargetType.None)
            return true;

        return target != null &&
               target.targetType == definition.TargetType &&
               !string.IsNullOrWhiteSpace(target.targetId);
    }

    private static string BuildDedupKey(string eventId, ActivityTargetRef target)
    {
        return (eventId ?? string.Empty) + "|" +
               (target != null ? target.targetType.ToString() : ActivityTargetType.None.ToString()) + "|" +
               (target != null ? target.targetId ?? string.Empty : string.Empty);
    }

    private static string BuildStateKey(string family, ActivityTargetRef target)
    {
        return (family ?? string.Empty).Trim().ToLowerInvariant() + "|" +
               (target != null ? target.targetType.ToString() : ActivityTargetType.None.ToString()) + "|" +
               (target != null ? target.targetId ?? string.Empty : string.Empty);
    }

    private double CurrentMinuteOfDay()
    {
        ResolveClock();
        return gameClock != null ? gameClock.Hour * 60d + gameClock.Minute : 0d;
    }

    private void ResolveClock()
    {
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (generalGameState == null)
            generalGameState = FindFirstObjectByType<BistroBuilderGeneralGameStateService>(FindObjectsInactive.Include);
    }
}
