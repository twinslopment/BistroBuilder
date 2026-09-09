using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia independiente de 3D. Inventario conserva cantidades/lotes;
/// esta sección conserva únicamente su valoración, costes consumidos y bajas
/// económicas no monetarias.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Product Cost Save Provider")]
public sealed class BistroBuilderProductCostSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId =
        BistroBuilderProductCostSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderProductCostSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderProductCostService productCostService;

    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 320;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderProductCostSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 320;
    public int ApplyOrder => 320;
    public int FinalizeOrder => 11100;

    private void Awake()
    {
        CacheDependency();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependency();
        if (productCostService == null)
        {
            error = "Falta BistroBuilderProductCostService junto al proveedor 3D.";
            return false;
        }
        return productCostService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !productCostService.TrySynchronizeWithInventory(out error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderProductCostSnapshot snapshot =
            productCostService.CreateSnapshot();
        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(snapshot);
        if (!BistroBuilderProductCostEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        BistroBuilderProductCostSnapshot snapshot =
            state as BistroBuilderProductCostSnapshot;
        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(snapshot);
        return BistroBuilderProductCostEngine.TryValidateSnapshot(
            snapshot,
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
        BistroBuilderProductCostSnapshot snapshot =
            state as BistroBuilderProductCostSnapshot;
        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(snapshot);
        if (!ValidateState(snapshot, out string error) ||
            !productCostService.TryRestoreSnapshot(snapshot, out error))
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
            if (!productCostService.TryInitializeFresh(out error))
            {
                context.Fail(
                    "No se pudo inicializar 3D para una partida anterior. " +
                    error);
                return;
            }
        }
        else if (!productCostService.TrySynchronizeWithInventory(out error))
        {
            context.Fail("3D no pudo reconciliar lotes tras Load. " + error);
            return;
        }

        if (!productCostService.TryRebuildActiveReservationCache(out error))
        {
            context.Fail(
                "3D no pudo reconstruir reservas activas tras Load. " + error);
        }
    }

    private void CacheDependency()
    {
        if (productCostService == null)
        {
            TryGetComponent(out productCostService);
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
