using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BistroBuilderOrderLineReadyEvent
{
    public KitchenSystem Kitchen { get; }
    public RestaurantOrder Order { get; }
    public string OrderLineId { get; }
    public string DishId { get; }

    public BistroBuilderOrderLineReadyEvent(
        KitchenSystem kitchen,
        RestaurantOrder order,
        string orderLineId,
        string dishId)
    {
        Kitchen = kitchen;
        Order = order;
        OrderLineId = BistroBuilderOrderIdUtility.Normalize(orderLineId);
        DishId = BistroBuilderOrderIdUtility.Normalize(dishId);
    }
}

[Serializable]
public sealed class BistroBuilderKitchenLineWorkSaveData
{
    public string canonicalOrderId = string.Empty;
    public string orderLineId = string.Empty;
    public string dishId = string.Empty;
    public int legacyOrderId;
    public long sequence;
    public float totalDurationSeconds;
    public float remainingDurationSeconds;
    public bool wasActive;

    // Extensión compatible del Bloque 12. En saves anteriores advanced=false.
    public bool advanced;
    public string stationId = string.Empty;
    public int stationSlotIndex = -1;
    public int stageIndex;
    public int stageCount = 1;
    public int priority;
    public string cookEmployeeId = string.Empty;
    public int qualityBasisPoints = 7000;
    public int incidentKind;

    public BistroBuilderKitchenLineWorkSaveData Clone()
    {
        return (BistroBuilderKitchenLineWorkSaveData)MemberwiseClone();
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        canonicalOrderId = BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);
        orderLineId = BistroBuilderOrderIdUtility.Normalize(orderLineId);
        dishId = BistroBuilderOrderIdUtility.Normalize(dishId);
        if (!BistroBuilderOrderIdUtility.IsValid(canonicalOrderId) ||
            !BistroBuilderOrderIdUtility.IsValid(orderLineId) ||
            !BistroBuilderOrderIdUtility.IsValid(dishId) || legacyOrderId < 1 || sequence < 0)
        {
            error = "El trabajo de cocina contiene identidades o secuencia inválidas.";
            return false;
        }
        if (!Finite(totalDurationSeconds) || totalDurationSeconds <= 0f ||
            !Finite(remainingDurationSeconds) || remainingDurationSeconds < 0f ||
            remainingDurationSeconds > totalDurationSeconds + 0.001f)
        {
            error = "Los tiempos del trabajo de cocina son inválidos.";
            return false;
        }
        if (advanced)
        {
            stationId = BistroBuilderStaffStableIdUtility.Normalize(stationId);
            cookEmployeeId = BistroBuilderEmployeeIdUtility.Normalize(cookEmployeeId);
            if (!BistroBuilderStaffStableIdUtility.IsValid(stationId) || stationSlotIndex < -1 ||
                stageIndex < 0 || stageCount < 1 || stageIndex >= stageCount ||
                priority < 0 || priority > 100 || qualityBasisPoints < 0 || qualityBasisPoints > 10000 ||
                incidentKind < 0 || incidentKind > 3 ||
                (!string.IsNullOrEmpty(cookEmployeeId) && !BistroBuilderEmployeeIdUtility.IsValid(cookEmployeeId)))
            {
                error = "Los datos avanzados del trabajo de cocina son inválidos.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

[Serializable]
public sealed class BistroBuilderKitchenStationRuntimeSaveData
{
    public string stationId = string.Empty;
    public float blockedSeconds;
    public int lastIncidentKind;

    public BistroBuilderKitchenStationRuntimeSaveData Clone() =>
        (BistroBuilderKitchenStationRuntimeSaveData)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderKitchenLineQualitySaveData
{
    public string orderLineId = string.Empty;
    public int qualityBasisPoints = 7000;

    public BistroBuilderKitchenLineQualitySaveData Clone() =>
        (BistroBuilderKitchenLineQualitySaveData)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderKitchenRuntimeSnapshot
{
    // Se conserva v1 porque los campos 12 son opcionales y JsonUtility los
    // inicializa con valores compatibles al cargar un save anterior.
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public string kitchenId = string.Empty;
    public long nextSequence;
    public List<BistroBuilderKitchenLineWorkSaveData> workItems =
        new List<BistroBuilderKitchenLineWorkSaveData>();

    public bool advancedEnabled;
    public int advancedIntakeMode;
    public List<BistroBuilderKitchenStationRuntimeSaveData> stationStates =
        new List<BistroBuilderKitchenStationRuntimeSaveData>();
    public List<BistroBuilderKitchenLineQualitySaveData> completedQualities =
        new List<BistroBuilderKitchenLineQualitySaveData>();

    public BistroBuilderKitchenRuntimeSnapshot Clone()
    {
        var clone = new BistroBuilderKitchenRuntimeSnapshot
        {
            version = version,
            kitchenId = kitchenId,
            nextSequence = nextSequence,
            advancedEnabled = advancedEnabled,
            advancedIntakeMode = advancedIntakeMode
        };
        if (workItems != null)
            for (int i = 0; i < workItems.Count; i++) clone.workItems.Add(workItems[i]?.Clone());
        if (stationStates != null)
            for (int i = 0; i < stationStates.Count; i++) clone.stationStates.Add(stationStates[i]?.Clone());
        if (completedQualities != null)
            for (int i = 0; i < completedQualities.Count; i++) clone.completedQualities.Add(completedQualities[i]?.Clone());
        return clone;
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        kitchenId = BistroBuilderOrderIdUtility.Normalize(kitchenId);
        if (version != CurrentVersion || !BistroBuilderOrderIdUtility.IsValid(kitchenId) || nextSequence < 0)
        {
            error = "La cabecera del snapshot de cocina es inválida.";
            return false;
        }
        if (workItems == null)
        {
            error = "La colección de trabajos de cocina es nula.";
            return false;
        }
        if (advancedEnabled && !Enum.IsDefined(
                typeof(BistroBuilderKitchenIntakeMode), advancedIntakeMode))
        {
            error = "El modo de entrada avanzado no es válido.";
            return false;
        }

        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<long>();
        var activeSlots = new HashSet<string>(StringComparer.Ordinal);
        int activeCount = 0;
        long maximumSequence = -1;
        long legacyActiveSequence = -1;
        for (int i = 0; i < workItems.Count; i++)
        {
            BistroBuilderKitchenLineWorkSaveData item = workItems[i];
            if (item == null || !item.TryValidate(out error) ||
                !lineIds.Add(item.orderLineId) || !sequences.Add(item.sequence))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "El snapshot contiene trabajos duplicados."
                    : error;
                return false;
            }
            maximumSequence = Math.Max(maximumSequence, item.sequence);
            if (item.wasActive)
            {
                activeCount++;
                legacyActiveSequence = item.sequence;
                if (advancedEnabled)
                {
                    string key = item.stationId + "#" + item.stationSlotIndex;
                    if (item.stationSlotIndex < 0 || !activeSlots.Add(key))
                    {
                        error = "Dos trabajos avanzados ocupan el mismo hueco de estación.";
                        return false;
                    }
                }
            }
        }
        if (!advancedEnabled && activeCount > 1)
        {
            error = "Una cocina legacy no puede tener más de una línea activa.";
            return false;
        }
        if (workItems.Count > 0 && nextSequence <= maximumSequence)
        {
            error = "La siguiente secuencia de cocina debe ser posterior a los trabajos capturados.";
            return false;
        }
        if (!advancedEnabled && activeCount == 1)
        {
            long min = long.MaxValue;
            for (int i = 0; i < workItems.Count; i++) min = Math.Min(min, workItems[i].sequence);
            if (legacyActiveSequence != min)
            {
                error = "La línea activa legacy debe ser el trabajo más antiguo.";
                return false;
            }
        }

        stationStates ??= new List<BistroBuilderKitchenStationRuntimeSaveData>();
        completedQualities ??= new List<BistroBuilderKitchenLineQualitySaveData>();
        var stationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < stationStates.Count; i++)
        {
            var station = stationStates[i];
            if (station == null) { error = "Estado de estación nulo."; return false; }
            station.stationId = BistroBuilderStaffStableIdUtility.Normalize(station.stationId);
            if (!BistroBuilderStaffStableIdUtility.IsValid(station.stationId) ||
                !stationIds.Add(station.stationId) || !FiniteNonNegative(station.blockedSeconds) ||
                station.lastIncidentKind < 0 || station.lastIncidentKind > 3)
            {
                error = "Estado persistente de estación inválido.";
                return false;
            }
        }
        var qualityIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < completedQualities.Count; i++)
        {
            var quality = completedQualities[i];
            if (quality == null) { error = "Calidad persistente nula."; return false; }
            quality.orderLineId = BistroBuilderOrderIdUtility.Normalize(quality.orderLineId);
            if (!BistroBuilderOrderIdUtility.IsValid(quality.orderLineId) ||
                !qualityIds.Add(quality.orderLineId) || quality.qualityBasisPoints < 0 ||
                quality.qualityBasisPoints > 10000)
            {
                error = "Calidad persistente de cocina inválida.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}