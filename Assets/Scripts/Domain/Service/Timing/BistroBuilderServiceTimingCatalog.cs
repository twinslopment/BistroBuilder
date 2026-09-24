using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BB_ServiceTimingCatalog",
    menuName = "Bistro Builder/Service/Service Timing Catalog"
)]
public sealed class BistroBuilderServiceTimingCatalog : ScriptableObject
{
    public const string ResourcesPath =
        "BistroBuilder/Service/BB_ServiceTimingCatalog";

    [SerializeField]
    private List<BistroBuilderServiceTimingProfile> profiles =
        new List<BistroBuilderServiceTimingProfile>();

    [SerializeField]
    private BistroBuilderFoodTimingPolicy foodTimingPolicy =
        new BistroBuilderFoodTimingPolicy();

    [SerializeField, Range(0, 10000)]
    private int recoverableServiceIncidentPenaltyBasisPoints = 1000;

    [SerializeField, Range(0, 10000)]
    private int recoverableServiceIncidentApologyRecoveryBasisPoints = 500;

    public IReadOnlyList<BistroBuilderServiceTimingProfile> Profiles => profiles;
    public BistroBuilderFoodTimingPolicy FoodTimingPolicy => foodTimingPolicy;
    public int RecoverableServiceIncidentPenaltyBasisPoints =>
        recoverableServiceIncidentPenaltyBasisPoints;
    public int RecoverableServiceIncidentApologyRecoveryBasisPoints =>
        recoverableServiceIncidentApologyRecoveryBasisPoints;

    public bool TryGetProfile(
        BistroBuilderServiceTimingPhase phase,
        out BistroBuilderServiceTimingProfile profile)
    {
        profile = null;

        if (profiles == null)
            return false;

        for (int index = 0; index < profiles.Count; index++)
        {
            BistroBuilderServiceTimingProfile candidate = profiles[index];
            if (candidate != null && candidate.Phase == phase)
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryEvaluate(
        BistroBuilderServiceTimingPhase phase,
        float elapsedSeconds,
        out BistroBuilderServiceTimingState state)
    {
        return TryEvaluate(
            phase,
            elapsedSeconds,
            0f,
            out state
        );
    }

    public bool TryEvaluate(
        BistroBuilderServiceTimingPhase phase,
        float elapsedSeconds,
        float expectedFoodSeconds,
        out BistroBuilderServiceTimingState state)
    {
        state = BistroBuilderServiceTimingState.Normal;

        if (phase == BistroBuilderServiceTimingPhase.FoodDelivery)
        {
            if (foodTimingPolicy == null)
                return false;

            state = foodTimingPolicy.Evaluate(
                expectedFoodSeconds,
                elapsedSeconds
            );
            return true;
        }

        if (!TryGetProfile(phase, out BistroBuilderServiceTimingProfile profile))
            return false;

        state = profile.Evaluate(elapsedSeconds);
        return true;
    }

    public bool TryGetRecoveryTuning(
        BistroBuilderServiceTimingPhase phase,
        out int explanationMitigationBasisPoints,
        out int apologyMitigationBasisPoints)
    {
        explanationMitigationBasisPoints = 0;
        apologyMitigationBasisPoints = 0;

        if (phase == BistroBuilderServiceTimingPhase.FoodDelivery)
        {
            if (foodTimingPolicy == null)
                return false;

            explanationMitigationBasisPoints =
                foodTimingPolicy.ExplanationPenaltyMitigationBasisPoints;
            apologyMitigationBasisPoints =
                foodTimingPolicy.ApologyPenaltyMitigationBasisPoints;
            return true;
        }

        if (!TryGetProfile(
                phase,
                out BistroBuilderServiceTimingProfile profile
            ) ||
            profile == null)
        {
            return false;
        }

        explanationMitigationBasisPoints =
            profile.ExplanationPenaltyMitigationBasisPoints;
        apologyMitigationBasisPoints =
            profile.ApologyPenaltyMitigationBasisPoints;
        return true;
    }

    public bool Validate(out string error)
    {
        if (recoverableServiceIncidentPenaltyBasisPoints < 0 ||
            recoverableServiceIncidentPenaltyBasisPoints > 10000 ||
            recoverableServiceIncidentApologyRecoveryBasisPoints < 0 ||
            recoverableServiceIncidentApologyRecoveryBasisPoints >
                recoverableServiceIncidentPenaltyBasisPoints)
        {
            error =
                "El tuning de incidencias debe ser válido y la recuperación por disculpa no puede superar su penalización.";
            return false;
        }

        if (foodTimingPolicy == null)
        {
            error = "FoodDelivery: falta la política dinámica.";
            return false;
        }

        if (!foodTimingPolicy.Validate(out string foodTimingError))
        {
            error = "FoodDelivery: " + foodTimingError;
            return false;
        }

        if (profiles == null || profiles.Count == 0)
        {
            error = "ServiceTimingCatalog no contiene perfiles.";
            return false;
        }

        var phases = new HashSet<BistroBuilderServiceTimingPhase>();

        for (int index = 0; index < profiles.Count; index++)
        {
            BistroBuilderServiceTimingProfile profile = profiles[index];

            if (profile == null)
            {
                error = "ServiceTimingCatalog contiene un perfil nulo.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BistroBuilderServiceTimingPhase), profile.Phase))
            {
                error = "ServiceTimingCatalog contiene una fase desconocida.";
                return false;
            }

            if (!phases.Add(profile.Phase))
            {
                error =
                    "ServiceTimingCatalog contiene una fase duplicada: " +
                    profile.Phase + ".";
                return false;
            }

            if (!profile.Validate(out error))
            {
                error = profile.Phase + ": " + error;
                return false;
            }
        }

        if (!phases.Contains(BistroBuilderServiceTimingPhase.BillDelivery))
        {
            error = "ServiceTimingCatalog no define BillDelivery.";
            return false;
        }

        if (!phases.Contains(BistroBuilderServiceTimingPhase.TakeOrder))
        {
            error = "ServiceTimingCatalog no define TakeOrder.";
            return false;
        }

        if (phases.Contains(BistroBuilderServiceTimingPhase.FoodDelivery))
        {
            error =
                "FoodDelivery usa una política dinámica y no debe duplicarse como perfil fijo.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
