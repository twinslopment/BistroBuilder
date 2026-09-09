using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Adaptador de finance.runtime para la plataforma universal de guardado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Finance Save Provider")]
public sealed class BistroBuilderFinanceSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider
{
    public const string StableSectionId = BistroBuilderFinanceSnapshot.CurrentSchemaId;
    public const int StableSectionVersion = BistroBuilderFinanceSnapshot.CurrentSchemaVersion;

    private BistroBuilderFinanceService financeService;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 300;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderFinanceSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (financeService == null)
        {
            error = "Falta BistroBuilderFinanceService junto al proveedor de guardado.";
            return false;
        }

        return financeService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderFinanceSnapshot snapshot = financeService.CreateSnapshot();
        if (snapshot == null || !BistroBuilderFinanceEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(string.IsNullOrWhiteSpace(error)
                ? "No se pudo capturar finance.runtime."
                : error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        BistroBuilderFinanceSnapshot snapshot = state as BistroBuilderFinanceSnapshot;
        if (snapshot == null)
        {
            error = "finance.runtime no tiene el tipo esperado.";
            return false;
        }

        return BistroBuilderFinanceEngine.TryValidateSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
        }
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !financeService.TryRestoreSnapshot((BistroBuilderFinanceSnapshot)state, out error))
        {
            context.Fail(error);
            yield break;
        }

        sectionAppliedThisLoad = true;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || sectionAppliedThisLoad)
        {
            return;
        }

        if (!financeService.TryInitializeFresh(out string error))
        {
            context.Fail("No se pudo inicializar Finanzas para una partida anterior a 3A. " + error);
        }
    }

    private void CacheDependencies()
    {
        if (financeService == null)
        {
            TryGetComponent(out financeService);
        }
    }
}
