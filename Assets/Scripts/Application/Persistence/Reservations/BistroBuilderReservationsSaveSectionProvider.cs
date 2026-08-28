using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6D — Persistencia de Reservas dentro del SaveGame universal.
/// Guarda planificación y enlaces runtime por ReservationId/GroupId.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Reservations Save Provider")]
public sealed class BistroBuilderReservationsSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "reservations.state";
    public const int StableSectionVersion = BistroBuilderReservationsSaveData.CurrentVersion;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderReservationServiceIntegration serviceIntegration;

    private readonly List<BistroBuilderReservationRuntimeBindingSaveRecord>
        bindingBuffer = new List<BistroBuilderReservationRuntimeBindingSaveRecord>();
    private BistroBuilderReservationsSaveData pendingData;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 425;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderReservationsSaveData);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 9050;
    public int ApplyOrder => 425;
    public int FinalizeOrder => 11200;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || reservationService == null ||
            serviceIntegration == null)
        {
            error = "6D necesita SaveGame, ReservationService e integración 6C.";
            return false;
        }

        if (!reservationService.ValidateConfiguration(out error) ||
            !serviceIntegration.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        bindingBuffer.Clear();
        serviceIntegration.CopyActiveRuntimeBindings(bindingBuffer);
        var data = new BistroBuilderReservationsSaveData
        {
            version = StableSectionVersion,
            state = reservationService.CreateSnapshot(),
            activeBindings = new List<BistroBuilderReservationRuntimeBindingSaveRecord>()
        };

        for (int index = 0; index < bindingBuffer.Count; index++)
        {
            BistroBuilderReservationRuntimeBindingSaveRecord binding = bindingBuffer[index];
            if (binding != null)
                data.activeBindings.Add(binding.DeepClone());
        }

        if (!TryValidateSaveData(data, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderReservationsSaveData data))
        {
            error = "reservations.state no tiene el tipo esperado.";
            return false;
        }

        return TryValidateSaveData(data, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        pendingData = null;
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        serviceIntegration.PrepareForRuntimeLoad();
        if (!reservationService.TryResetForLegacyLoad(out error))
            context.Fail(error);
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error))
        {
            context.Fail(error);
            yield break;
        }

        pendingData = ((BistroBuilderReservationsSaveData)state).DeepClone();
        if (!reservationService.TryRestoreSnapshot(pendingData.state, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
            return;

        if (pendingData == null)
        {
            // Save antiguo sin reservations.state: Prepare dejó estado vacío.
            return;
        }

        if (!serviceIntegration.TryRestoreRuntimeBindings(
                pendingData.activeBindings,
                out string error))
        {
            context.Fail(error);
            return;
        }

        pendingData = null;
    }

    public static bool TryValidateSaveData(
        BistroBuilderReservationsSaveData data,
        out string error)
    {
        error = string.Empty;
        if (data == null || data.version != StableSectionVersion ||
            data.state == null || data.activeBindings == null)
        {
            error = "reservations.state contiene una cabecera inválida.";
            return false;
        }

        if (!BistroBuilderReservationEngine.TryValidateSnapshot(data.state, out error))
            return false;

        var reservationIds = new HashSet<string>(StringComparer.Ordinal);
        var groupIds = new HashSet<int>();
        for (int index = 0; index < data.activeBindings.Count; index++)
        {
            BistroBuilderReservationRuntimeBindingSaveRecord binding =
                data.activeBindings[index];
            string reservationId = binding != null
                ? BistroBuilderReservationEngine.NormalizeId(binding.reservationId)
                : string.Empty;

            if (binding == null || reservationId.Length == 0 || binding.groupId < 1 ||
                !reservationIds.Add(reservationId) || !groupIds.Add(binding.groupId))
            {
                error = "reservations.state contiene enlaces runtime inválidos o duplicados.";
                return false;
            }

            if (!BistroBuilderReservationEngine.TryFind(
                    data.state,
                    reservationId,
                    out BistroBuilderReservationRecord reservation) ||
                reservation == null || reservation.IsTerminal ||
                reservation.status == BistroBuilderReservationStatus.Booked ||
                reservation.tableId < 1)
            {
                error = "Un enlace runtime no corresponde a una reserva activa materializada.";
                return false;
            }
        }

        return true;
    }

    private void CacheDependencies()
    {
        if (saveGameService == null)
            TryGetComponent(out saveGameService);
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (serviceIntegration == null)
            TryGetComponent(out serviceIntegration);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
