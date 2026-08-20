using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 4E — Persistencia del mercado de contratación 4B.
///
/// Guarda exclusivamente candidatos y metadatos de refresco. Los candidatos
/// no son Employee y esta sección nunca toca plantilla, Waiter ni economía.
/// Las partidas anteriores a 4E generan un mercado nuevo determinista para el
/// día cargado en lugar de heredar candidatos de la partida previa.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Staff Recruitment Save Provider")]
public sealed class BistroBuilderStaffRecruitmentSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId =
        BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaVersion;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderStaffService staffService;

    [SerializeField]
    private BistroBuilderStaffRecruitmentService recruitmentService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 430;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderStaffRecruitmentSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 9075;
    public int ApplyOrder => 425;
    public int FinalizeOrder => 10600;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || staffService == null ||
            recruitmentService == null)
        {
            error =
                "4E staff.recruitment necesita SaveGame, Staff y " +
                "StaffRecruitmentService.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !recruitmentService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !recruitmentService.EnsureMarketReady(out error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderStaffRecruitmentSnapshot snapshot =
            recruitmentService.CreateMarketSnapshot();
        if (!ValidateSnapshot(snapshot, false, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderStaffRecruitmentSnapshot snapshot))
        {
            error = "staff.recruitment no tiene el tipo esperado.";
            return false;
        }

        CacheDependencies();
        return ValidateSnapshot(snapshot, false, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        // Evita contaminación cruzada si la partida cargada no contiene la
        // sección. El mercado se regenerará en FinalizeLoad para su DayIndex.
        BistroBuilderStaffRecruitmentSnapshot empty =
            BistroBuilderStaffRecruitmentEngine.CreateEmptySnapshot();
        if (!recruitmentService.TryRestoreMarketSnapshot(empty, out error))
        {
            context.Fail(error);
        }
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

        if (!recruitmentService.TryRestoreMarketSnapshot(
                (BistroBuilderStaffRecruitmentSnapshot)state,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        context.SharedData.Set(
            "save.loaded_section." + StableSectionId,
            true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
        {
            return;
        }

        bool sectionWasLoaded = context.SharedData.TryGet(
            "save.loaded_section." + StableSectionId,
            out bool loaded) && loaded;
        if (sectionWasLoaded)
        {
            return;
        }

        if (!recruitmentService.EnsureMarketReady(out string error))
        {
            context.Fail(
                "No pudo generarse el mercado de Personal para una partida " +
                "anterior a 4E. " + error);
        }
    }

    private bool ValidateSnapshot(
        BistroBuilderStaffRecruitmentSnapshot snapshot,
        bool allowNeverGenerated,
        out string error)
    {
        if (recruitmentService == null || staffService == null)
        {
            error = "Faltan dependencias para validar staff.recruitment.";
            return false;
        }

        return BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
            snapshot,
            recruitmentService.RecruitmentProfile,
            staffService.RoleCatalog,
            allowNeverGenerated,
            out error);
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (staffService == null) TryGetComponent(out staffService);
        if (recruitmentService == null)
            TryGetComponent(out recruitmentService);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
