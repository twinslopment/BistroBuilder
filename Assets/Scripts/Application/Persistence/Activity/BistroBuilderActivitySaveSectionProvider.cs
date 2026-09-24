using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderActivitySaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public ActivityFeedSaveSnapshot feed = new ActivityFeedSaveSnapshot();
}

/// <summary>
/// ACTIVIDAD persiste únicamente su proyección visible/Sticky.
/// No restaura ni ejecuta efectos de gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Activity Feed Save Provider")]
public sealed class BistroBuilderActivitySaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "ui.activity";
    public const int StableSectionVersion = 1;

    [SerializeField] private ActivityFeedService feed;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 950;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderActivitySaveData);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 9000;
    public int ApplyOrder => 12000;
    public int FinalizeOrder => 13000;

    private void Awake() => CacheDependencies();

    public void Bind(ActivityFeedService value)
    {
        feed = value;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        CacheDependencies();
        if (feed == null)
        {
            context.Fail("ACTIVIDAD no encuentra ActivityFeedService.");
            yield break;
        }

        var data = new BistroBuilderActivitySaveData
        {
            feed = feed.CaptureSnapshot()
        };

        if (!ValidateState(data, out string error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        error = string.Empty;
        BistroBuilderActivitySaveData data =
            state as BistroBuilderActivitySaveData;

        if (data == null ||
            data.schemaVersion != BistroBuilderActivitySaveData.CurrentSchemaVersion ||
            data.feed == null ||
            data.feed.schemaVersion != 1)
        {
            error = "ui.activity contiene un snapshot no compatible.";
            return false;
        }

        if (data.feed.events == null)
        {
            error = "ui.activity no contiene colección de eventos.";
            return false;
        }

        for (int i = 0; i < data.feed.events.Count; i++)
        {
            ActivityEventInstance item = data.feed.events[i];
            if (item == null || item.sequence <= 0 ||
                !ActivityEventCatalog.TryGet(item.eventId, out ActivityEventDefinition definition))
            {
                error = "ui.activity contiene un EventId o secuencia inválidos.";
                return false;
            }

            if (definition.TargetType != ActivityTargetType.None &&
                (item.target == null ||
                 item.target.targetType != definition.TargetType ||
                 string.IsNullOrWhiteSpace(item.target.targetId)))
            {
                error = "ui.activity contiene TargetRef incompatible en " +
                        item.eventId + ".";
                return false;
            }
        }

        return true;
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;
        CacheDependencies();
        if (feed == null)
            context.Fail("ACTIVIDAD no está instalada antes de cargar ui.activity.");
        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error))
        {
            context.Fail(error);
            yield break;
        }

        CacheDependencies();
        if (feed == null)
        {
            context.Fail("No existe ActivityFeedService para aplicar ui.activity.");
            yield break;
        }

        BistroBuilderActivitySaveData data =
            (BistroBuilderActivitySaveData)state;

        // La rehidratación no publica gameplay ni recompensas.
        if (!feed.RestoreSnapshot(data.feed, null, out error))
        {
            context.Fail(error);
            yield break;
        }

        sectionAppliedThisLoad = true;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || !sectionAppliedThisLoad || feed == null)
            return;

        ActivityTargetRouter router =
            FindFirstObjectByType<ActivityTargetRouter>(FindObjectsInactive.Include);
        if (router != null)
            feed.DiscardInvalidTargets(router.IsPersistenceTargetValid);
    }

    private void CacheDependencies()
    {
        if (feed == null)
            feed = FindFirstObjectByType<ActivityFeedService>(
                FindObjectsInactive.Include);
    }
}

public static class BistroBuilderActivityPersistenceInstaller
{
    public static bool TryInstall(ActivityFeedService feed)
    {
        if (feed == null)
            return false;

        BistroBuilderSaveGameService saveGame =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>(
                FindObjectsInactive.Include);

        if (saveGame == null || saveGame.IsBusy)
            return false;

        BistroBuilderActivitySaveSectionProvider provider =
            saveGame.GetComponent<BistroBuilderActivitySaveSectionProvider>();

        if (provider == null)
            provider = saveGame.gameObject.AddComponent<
                BistroBuilderActivitySaveSectionProvider>();

        provider.Bind(feed);
        saveGame.RefreshExtensions();
        return saveGame.HasProvider(
            BistroBuilderActivitySaveSectionProvider.StableSectionId);
    }
}
