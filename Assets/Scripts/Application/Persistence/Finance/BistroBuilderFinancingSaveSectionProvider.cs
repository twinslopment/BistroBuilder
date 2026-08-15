using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia de obligaciones financieras 3I. La caja permanece en 3A;
/// esta sección guarda únicamente préstamos, cuotas y estados de deuda.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Financing Save Provider")]
public sealed class BistroBuilderFinancingSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = BistroBuilderFinancingSnapshot.CurrentSchemaId;
    public const int StableSectionVersion = BistroBuilderFinancingSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderFinancingService financingService;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 340;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderFinancingSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 340;
    public int ApplyOrder => 340;
    public int FinalizeOrder => 11300;

    private void Awake()
    {
        CacheDependency();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependency();
        if (financingService == null)
        {
            error = "Falta BistroBuilderFinancingService junto al proveedor 3I.";
            return false;
        }
        return financingService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderFinancingSnapshot snapshot = financingService.CreateSnapshot();
        if (!BistroBuilderFinancingEngine.TryValidateSnapshot(snapshot, out error) ||
            !financingService.TryValidateLedgerConsistency(out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        return BistroBuilderFinancingEngine.TryValidateSnapshot(
            state as BistroBuilderFinancingSnapshot,
            out error);
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
            !financingService.TryRestoreSnapshot(
                (BistroBuilderFinancingSnapshot)state,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        sectionAppliedThisLoad = true;
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
        {
            return;
        }

        string error;
        if (!sectionAppliedThisLoad)
        {
            if (!financingService.TryInitializeFresh(out error))
            {
                context.Fail(
                    "No se pudo inicializar 3I para una partida anterior. " + error);
            }
            return;
        }

        if (!financingService.TryValidateLedgerConsistency(out error))
        {
            context.Fail("3I no coincide con el ledger financiero cargado. " + error);
        }
    }

    private void CacheDependency()
    {
        if (financingService == null)
        {
            TryGetComponent(out financingService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependency();
    }

    private void OnValidate()
    {
        CacheDependency();
    }
#endif
}
