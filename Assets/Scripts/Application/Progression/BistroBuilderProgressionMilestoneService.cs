using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proyecta rendimiento canónico y avanza el nivel global al cumplir hitos.
/// No persiste estado propio: stage/level siguen perteneciendo a GeneralGameState.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Progression/Progression Milestone Service")]
public sealed class BistroBuilderProgressionMilestoneService : MonoBehaviour
{
    [SerializeField] private BistroBuilderProgressionMilestoneCatalog milestoneCatalog;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderUpgradeService upgradeService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderFinancialResultsService financialResultsService;

    private readonly List<BistroBuilderDayFinancialResult> dayResults =
        new List<BistroBuilderDayFinancialResult>(64);
    private bool evaluating;

    public event Action<string, int> MilestoneReached;
    public event Action ProgressionEvaluationChanged;

    public BistroBuilderProgressionMilestoneCatalog MilestoneCatalog => milestoneCatalog;

    private void Awake() => TryAdvanceProgression(out _);

    private void OnEnable()
    {
        if (upgradeService != null) upgradeService.UpgradePurchased += HandleUpgradePurchased;
        if (reputationService != null) reputationService.ReputationChanged += HandleReputationChanged;
        if (financialResultsService != null) financialResultsService.ResultsChanged += HandleResultsChanged;
        if (generalGameStateService != null) generalGameStateService.ProgressionChanged += HandleProgressionChanged;
    }

    private void OnDisable()
    {
        if (upgradeService != null) upgradeService.UpgradePurchased -= HandleUpgradePurchased;
        if (reputationService != null) reputationService.ReputationChanged -= HandleReputationChanged;
        if (financialResultsService != null) financialResultsService.ResultsChanged -= HandleResultsChanged;
        if (generalGameStateService != null) generalGameStateService.ProgressionChanged -= HandleProgressionChanged;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (milestoneCatalog == null || generalGameStateService == null ||
            upgradeService == null || reputationService == null || financialResultsService == null)
        {
            error = "9C necesita catálogo, estado general, mejoras, Reputación y resultados financieros.";
            return false;
        }
        if (!milestoneCatalog.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !upgradeService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !financialResultsService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryGetCurrentEvaluation(
        out BistroBuilderProgressionMilestoneEvaluation evaluation,
        out string error)
    {
        evaluation = null;
        if (!ValidateConfiguration(out error) || !TryBuildContext(out var context, out error))
            return false;
        evaluation = BistroBuilderProgressionMilestoneEngine.EvaluateNext(
            milestoneCatalog.Milestones, context);
        error = string.Empty;
        return true;
    }

    public bool TryAdvanceProgression(out string error)
    {
        error = string.Empty;
        if (evaluating) return true;
        if (!ValidateConfiguration(out error)) return false;
        evaluating = true;
        try
        {
            while (TryBuildContext(out var context, out error))
            {
                BistroBuilderProgressionMilestoneEvaluation evaluation =
                    BistroBuilderProgressionMilestoneEngine.EvaluateNext(
                        milestoneCatalog.Milestones, context);
                if (evaluation.milestone == null || !evaluation.completed) break;

                BistroBuilderProgressionMilestoneDefinition target = evaluation.milestone;
                if (!generalGameStateService.TrySetProgression(
                        target.stageId, target.targetLevel))
                {
                    error = "No se pudo publicar el hito " + target.milestoneId + ".";
                    return false;
                }
                MilestoneReached?.Invoke(target.milestoneId, target.targetLevel);
            }
            ProgressionEvaluationChanged?.Invoke();
            return string.IsNullOrWhiteSpace(error);
        }
        finally { evaluating = false; }
    }

    private bool TryBuildContext(
        out BistroBuilderProgressionMilestoneContext context,
        out string error)
    {
        context = new BistroBuilderProgressionMilestoneContext
        {
            currentLevel = generalGameStateService.ProgressionLevel,
            reputationBasisPoints = reputationService.GlobalScoreBasisPoints,
            purchasedUpgradeCount = upgradeService.PurchasedCount
        };

        dayResults.Clear();
        int endDay = Math.Max(1, generalGameStateService.DayIndex);
        if (!financialResultsService.TryGetDayResults(1, endDay, dayResults, out error))
            return false;

        long revenue = 0L;
        int profitable = 0;
        for (int i = 0; i < dayResults.Count; i++)
        {
            BistroBuilderDayFinancialResult day = dayResults[i];
            if (day == null) continue;
            try { revenue = checked(revenue + Math.Max(0L, day.revenueCents)); }
            catch (OverflowException)
            {
                error = "Los ingresos acumulados exceden el rango soportado.";
                return false;
            }
            if (day.HasOperatingResultActivity && day.operatingResultCents > 0L)
                profitable++;
        }
        context.cumulativeRevenueCents = revenue;
        context.profitableDays = profitable;
        error = string.Empty;
        return true;
    }

    private void HandleUpgradePurchased(string upgradeId) => TryAdvanceProgression(out _);
    private void HandleReputationChanged(long revision) => TryAdvanceProgression(out _);
    private void HandleResultsChanged() => TryAdvanceProgression(out _);
    private void HandleProgressionChanged() => ProgressionEvaluationChanged?.Invoke();
}
