using System;
using System.Collections.Generic;

/// <summary>
/// Parámetros de balance de la demanda orgánica. Son datos serializables y
/// editables; el motor puro no depende de Unity ni de objetos de escena.
/// </summary>
[Serializable]
public sealed class BistroBuilderDynamicDemandSettings
{
    public int minimumBaseGroups = 2;
    public int maximumBaseGroups = 60;
    public float neutralBaseGroups = 5f;
    public float referenceCapacitySeats = 12f;
    public float averagePartySize = 2.2f;
    public float serviceSeatTurns = 1.65f;
    public float reservationCapacityProtection = 0.70f;
    public int progressionBasisPointsPerLevel = 700;
    public int maximumProgressionBonusBasisPoints = 7000;
    public float baseArrivalSpacingSeconds = 5.5f;
    public float minimumArrivalSpacingSeconds = 1.5f;
    public float maximumArrivalSpacingSeconds = 12f;

    public BistroBuilderDynamicDemandSettings DeepClone() =>
        (BistroBuilderDynamicDemandSettings)MemberwiseClone();

    public bool TryValidate(out string error)
    {
        if (minimumBaseGroups < 1 || maximumBaseGroups < minimumBaseGroups ||
            maximumBaseGroups > 100 || neutralBaseGroups <= 0f ||
            referenceCapacitySeats <= 0f || averagePartySize <= 0f ||
            serviceSeatTurns <= 0f || reservationCapacityProtection < 0f ||
            reservationCapacityProtection > 1f || progressionBasisPointsPerLevel < 0 ||
            maximumProgressionBonusBasisPoints < 0 || baseArrivalSpacingSeconds <= 0f ||
            minimumArrivalSpacingSeconds <= 0f ||
            maximumArrivalSpacingSeconds < minimumArrivalSpacingSeconds)
        {
            error = "La configuración de demanda dinámica contiene valores inválidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Entrada factual del motor de demanda para un servicio concreto.
/// </summary>
[Serializable]
public sealed class BistroBuilderDynamicDemandContext
{
    public int progressionLevel = 1;
    public int tableCount;
    public int tableSeatCapacity;
    public int barSeatCapacity;
    public int globalReputationBasisPoints = 5000;
    public int recentSatisfactionBasisPoints = 5000;
    public DayOfWeek dayOfWeek = DayOfWeek.Monday;
    public int minuteOfDay = 720;
    public int availableDishCount;
    public int reservationGroupCount;
    public int reservedPartySize;

    public int TotalSeatCapacity => Math.Max(0, tableSeatCapacity) +
                                    Math.Max(0, barSeatCapacity);
}

/// <summary>
/// Resultado trazable de la demanda base antes de Marketing.
/// </summary>
[Serializable]
public sealed class BistroBuilderDynamicDemandProjection
{
    public int baseWalkInGroups;
    public int unconstrainedGroups;
    public int capacityCeilingGroups;
    public int effectiveAvailableSeats;
    public int progressionMultiplierBasisPoints;
    public int capacityMultiplierBasisPoints;
    public int reputationMultiplierBasisPoints;
    public int satisfactionMultiplierBasisPoints;
    public int calendarMultiplierBasisPoints;
    public int dayPartMultiplierBasisPoints;
    public int menuMultiplierBasisPoints;
    public int reservationGroupCount;
    public int reservedPartySize;
    public string dayPartId = string.Empty;
    public List<float> arrivalDelaySeconds = new List<float>();

    public BistroBuilderDynamicDemandProjection DeepClone()
    {
        return new BistroBuilderDynamicDemandProjection
        {
            baseWalkInGroups = baseWalkInGroups,
            unconstrainedGroups = unconstrainedGroups,
            capacityCeilingGroups = capacityCeilingGroups,
            effectiveAvailableSeats = effectiveAvailableSeats,
            progressionMultiplierBasisPoints = progressionMultiplierBasisPoints,
            capacityMultiplierBasisPoints = capacityMultiplierBasisPoints,
            reputationMultiplierBasisPoints = reputationMultiplierBasisPoints,
            satisfactionMultiplierBasisPoints = satisfactionMultiplierBasisPoints,
            calendarMultiplierBasisPoints = calendarMultiplierBasisPoints,
            dayPartMultiplierBasisPoints = dayPartMultiplierBasisPoints,
            menuMultiplierBasisPoints = menuMultiplierBasisPoints,
            reservationGroupCount = reservationGroupCount,
            reservedPartySize = reservedPartySize,
            dayPartId = dayPartId,
            arrivalDelaySeconds = new List<float>(arrivalDelaySeconds ?? new List<float>())
        };
    }
}
