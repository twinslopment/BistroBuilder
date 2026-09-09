using UnityEngine;

/// <summary>
/// 10E. Expone recomendación e intención de retorno derivadas del historial 10C.
/// Implementa solo prioridad de cohorte: Marketing/Reputación conservan la cantidad.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Advocacy Service")]
public sealed class BistroBuilderAdvancedCustomerAdvocacyService : MonoBehaviour,
    IBistroBuilderReturnCohortPriorityProvider
{
    [SerializeField]
    private BistroBuilderAdvancedCustomerHistoryService historyService;
    [SerializeField]
    private BistroBuilderCustomerExperienceTrackingService trackingService;

    public BistroBuilderAdvancedCustomerHistoryService HistoryService => historyService;
    public BistroBuilderCustomerExperienceTrackingService TrackingService => trackingService;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (historyService == null || trackingService == null)
        {
            error = "10E necesita historial avanzado y Experience Tracking canónicos.";
            return false;
        }
        if (!historyService.ValidateConfiguration(out error) ||
            !trackingService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public int GetReturnPriority(string cohortId)
    {
        if (historyService == null) CacheDependencies();
        if (historyService == null ||
            !historyService.TryGetCustomer(cohortId, out var history))
            return 0;
        BistroBuilderAdvancedCustomerAdvocacyResult result =
            BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateHistory(history);
        return result != null ? result.returnPriority : 0;
    }

    public bool TryGetCohortAdvocacy(
        string cohortId,
        out BistroBuilderAdvancedCustomerAdvocacyResult result,
        out string error)
    {
        result = null;
        if (!ValidateConfiguration(out error)) return false;
        if (!historyService.TryGetCustomer(cohortId, out var history))
        {
            error = "No existe historial avanzado para la cohorte.";
            return false;
        }
        result = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateHistory(history);
        if (result == null)
        {
            error = "No pudo derivarse recomendación para la cohorte.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryEvaluateLatestIndividuals(
        BistroBuilderCustomerLoyaltyTier loyaltyTier,
        out BistroBuilderAdvancedCustomerAdvocacyResult result,
        out string error)
    {
        result = null;
        if (!ValidateConfiguration(out error)) return false;
        BistroBuilderAdvancedCustomerExperienceResult latest =
            trackingService.LastAdvancedExperience;
        if (latest == null || latest.individuals == null || latest.individuals.Count == 0)
        {
            error = "No existe todavía una experiencia individual completada.";
            return false;
        }

        result = new BistroBuilderAdvancedCustomerAdvocacyResult();
        long recommend = 0L; long returnIntent = 0L;
        for (int i = 0; i < latest.individuals.Count; i++)
        {
            var advocacy = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateIndividual(
                latest.individuals[i], loyaltyTier);
            if (advocacy == null) continue;
            result.individuals.Add(advocacy);
            recommend += advocacy.recommendationBasisPoints;
            returnIntent += advocacy.returnIntentBasisPoints;
        }
        if (result.individuals.Count == 0)
        {
            error = "La experiencia no produjo intenciones individuales válidas.";
            return false;
        }
        result.recommendationBasisPoints =
            Mathf.RoundToInt(recommend / (float)result.individuals.Count);
        result.returnIntentBasisPoints =
            Mathf.RoundToInt(returnIntent / (float)result.individuals.Count);
        error = string.Empty;
        return true;
    }

    private void CacheDependencies()
    {
        if (historyService == null) TryGetComponent(out historyService);
        if (trackingService == null) TryGetComponent(out trackingService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
