using System;
using System.Collections.Generic;

public enum BistroBuilderCustomerExperienceReason
{
    None = 0,
    TableWait = 1,
    WaiterWait = 2,
    FoodWait = 3,
    BillWait = 4,
    PriceTooHigh = 5,
    GoodValue = 6,
    FoodQualityLow = 7,
    FoodQualityHigh = 8,
    ServicePoor = 9,
    ServiceExcellent = 10,
    AmbiencePoor = 11,
    AmbienceExcellent = 12
}

/// <summary>Percepción individual calculada desde hechos objetivos de la visita.</summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerIndividualExperience
{
    public string customerId = string.Empty;
    public int memberIndex;
    public int serviceScoreBasisPoints;
    public int waitingScoreBasisPoints;
    public int foodQualityScoreBasisPoints;
    public int valueForMoneyScoreBasisPoints;
    public int ambienceScoreBasisPoints;
    public int overallSatisfactionBasisPoints;
    public List<BistroBuilderCustomerExperienceReason> reasons =
        new List<BistroBuilderCustomerExperienceReason>();

    public BistroBuilderAdvancedCustomerIndividualExperience DeepClone()
    {
        return new BistroBuilderAdvancedCustomerIndividualExperience
        {
            customerId = customerId,
            memberIndex = memberIndex,
            serviceScoreBasisPoints = serviceScoreBasisPoints,
            waitingScoreBasisPoints = waitingScoreBasisPoints,
            foodQualityScoreBasisPoints = foodQualityScoreBasisPoints,
            valueForMoneyScoreBasisPoints = valueForMoneyScoreBasisPoints,
            ambienceScoreBasisPoints = ambienceScoreBasisPoints,
            overallSatisfactionBasisPoints = overallSatisfactionBasisPoints,
            reasons = reasons != null
                ? new List<BistroBuilderCustomerExperienceReason>(reasons)
                : new List<BistroBuilderCustomerExperienceReason>()
        };
    }
}

/// <summary>
/// Resultado consultivo 10B. El agregado se publica después por la autoridad
/// existente de Reputation; este objeto no posee reputación ni persistencia.
/// </summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerExperienceResult
{
    public int groupId;
    public int aggregateServiceBasisPoints;
    public int aggregateWaitingBasisPoints;
    public int aggregateFoodQualityBasisPoints;
    public int aggregateValueBasisPoints;
    public int aggregateAmbienceBasisPoints;
    public int aggregateSatisfactionBasisPoints;
    public List<BistroBuilderAdvancedCustomerIndividualExperience> individuals =
        new List<BistroBuilderAdvancedCustomerIndividualExperience>();

    public BistroBuilderAdvancedCustomerExperienceResult DeepClone()
    {
        var clone = new BistroBuilderAdvancedCustomerExperienceResult
        {
            groupId = groupId,
            aggregateServiceBasisPoints = aggregateServiceBasisPoints,
            aggregateWaitingBasisPoints = aggregateWaitingBasisPoints,
            aggregateFoodQualityBasisPoints = aggregateFoodQualityBasisPoints,
            aggregateValueBasisPoints = aggregateValueBasisPoints,
            aggregateAmbienceBasisPoints = aggregateAmbienceBasisPoints,
            aggregateSatisfactionBasisPoints = aggregateSatisfactionBasisPoints
        };
        if (individuals != null)
            for (int i = 0; i < individuals.Count; i++)
                clone.individuals.Add(individuals[i]?.DeepClone());
        return clone;
    }
}
