using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BBPLFSRankedCandidate
{
    [SerializeField] private BBPLFSLayoutCandidate candidate;
    [SerializeField] private float finalScore;
    [SerializeField] private float authorityAdjustment;

    public BBPLFSLayoutCandidate Candidate => candidate;
    public float FinalScore => finalScore;
    public float AuthorityAdjustment => authorityAdjustment;

    public BBPLFSRankedCandidate(BBPLFSLayoutCandidate candidate, float authorityAdjustment)
    {
        this.candidate = candidate;
        this.authorityAdjustment = authorityAdjustment;
        finalScore = candidate != null ? candidate.Score + authorityAdjustment : float.NegativeInfinity;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Auto Furnish Service")]
public sealed class BBPLFSAutoFurnishService : MonoBehaviour
{
    [Header("Services")]
    [SerializeField] private BBPLFSPremisesCaptureService premisesCaptureService;
    [SerializeField] private BBPLFSAssetLayoutCatalog assetCatalog;
    [SerializeField] private BBPLFSFurnishingSetCatalog furnishingSetCatalog;
    [SerializeField] private BBPLFSAdvancedLayoutGenerator generator;
    [SerializeField] private BBPLFSCandidateValidationService candidateValidationService;
    [SerializeField] private BBPLFSPreviewService previewService;
    [SerializeField] private BBPLFSMaterializationService materializationService;

    [Header("Active request")]
    [SerializeField] private RestaurantArea selectedArea;
    [SerializeField] private BBPLFSSpaceFunction spaceFunction = BBPLFSSpaceFunction.Dining;
    [SerializeField] private BBPLFSGoalProfile goalProfile = BBPLFSGoalProfile.Balanced;
    [SerializeField, Min(1)] private int targetCapacity = 20;
    [SerializeField] private string[] styleTags = Array.Empty<string>();

    private readonly List<BBPLFSRankedCandidate> candidates = new();
    private readonly List<string> rejectionDiagnostics = new();
    private int previewedIndex = -1;
    private BBPLFSPremisesModel premises;

    public RestaurantArea SelectedArea => selectedArea;
    public IReadOnlyList<BBPLFSRankedCandidate> Candidates => candidates;
    public IReadOnlyList<string> RejectionDiagnostics => rejectionDiagnostics;
    public int PreviewedIndex => previewedIndex;
    public string PremisesRevision => premises != null ? premises.Revision : string.Empty;

    public bool Generate(out string message)
    {
        candidates.Clear();
        rejectionDiagnostics.Clear();
        previewedIndex = -1;
        previewService?.ClearPreview();
        if (!ResolveRuntimeDependencies(out message)) return false;

        premises = premisesCaptureService.CaptureAll();
        if (!premises.TryGetSpace(selectedArea, out BBPLFSPremisesSpaceSnapshot space) &&
            !premisesCaptureService.TryCaptureArea(selectedArea, out space))
        { message = "No se ha podido analizar el área seleccionada."; return false; }

        BBPLFSDesignScope scope = new(BBPLFSDesignScopeKind.Room, selectedArea);
        BBPLFSLayoutBrief brief = new(scope, spaceFunction, goalProfile, targetCapacity, styleTags, true);
        List<BBPLFSLayoutCandidate> generated = generator.GenerateCandidates(space, brief, assetCatalog, furnishingSetCatalog);
        for (int i = 0; i < generated.Count; i++)
        {
            BBPLFSLayoutCandidate candidate = generated[i];
            BBPLFSCandidateEvaluation evaluation = candidateValidationService.Evaluate(candidate, selectedArea, assetCatalog);
            if (!evaluation.IsValid)
            {
                rejectionDiagnostics.Add(candidate.CandidateId + ": " + evaluation.ReasonCode + " — " + evaluation.Message);
                continue;
            }
            candidates.Add(new BBPLFSRankedCandidate(candidate, evaluation.ScoreAdjustment));
        }
        candidates.Sort((a, b) =>
        {
            int score = b.FinalScore.CompareTo(a.FinalScore);
            return score != 0 ? score : string.CompareOrdinal(a.Candidate.CandidateId, b.Candidate.CandidateId);
        });
        if (candidates.Count == 0)
        {
            message = rejectionDiagnostics.Count > 0
                ? "No hay propuestas válidas. " + rejectionDiagnostics[0]
                : "No se ha encontrado una distribución compatible con el alcance y los assets disponibles.";
            return false;
        }
        message = "BBPLFS ha generado y validado " + candidates.Count + " propuesta(s). Mejor: " +
            candidates[0].Candidate.Capacity + " plazas, score " + candidates[0].FinalScore.ToString("0.000") + ".";
        return true;
    }

    public bool Preview(int index, out string message)
    {
        if (index < 0 || index >= candidates.Count || previewService == null)
        { message = "La propuesta o el servicio de preview no están disponibles."; return false; }
        previewService.ShowCandidate(candidates[index].Candidate);
        previewedIndex = index;
        message = "Preview " + candidates[index].Candidate.CandidateId + " — score " + candidates[index].FinalScore.ToString("0.000") + ".";
        return true;
    }

    public bool Accept(out string message)
    {
        if (previewedIndex < 0 || previewedIndex >= candidates.Count)
        { message = "No hay una propuesta previsualizada."; return false; }
        if (materializationService == null)
        { message = "El servicio de materialización no está disponible."; return false; }
        BBPLFSLayoutCandidate candidate = candidates[previewedIndex].Candidate;
        BBPLFSCandidateEvaluation finalEvaluation = candidateValidationService.Evaluate(candidate, selectedArea, assetCatalog);
        if (!finalEvaluation.IsValid)
        { message = "La escena ha cambiado y la propuesta ya no es válida: " + finalEvaluation.Message; return false; }
        if (!materializationService.TryMaterialize(candidate, selectedArea, out message)) return false;
        previewService?.ClearPreview();
        previewedIndex = -1;
        premises = premisesCaptureService.CaptureAll();
        return true;
    }

    public void CancelPreview()
    {
        previewService?.ClearPreview();
        previewedIndex = -1;
    }

    public bool CompleteCurrentRoom(out string message) => Generate(out message);

    private bool ResolveRuntimeDependencies(out string message)
    {
        if (premisesCaptureService == null) premisesCaptureService = FindFirstObjectByType<BBPLFSPremisesCaptureService>();
        if (assetCatalog == null) assetCatalog = FindFirstObjectByType<BBPLFSAssetLayoutCatalog>();
        if (furnishingSetCatalog == null) furnishingSetCatalog = FindFirstObjectByType<BBPLFSFurnishingSetCatalog>();
        if (generator == null) generator = FindFirstObjectByType<BBPLFSAdvancedLayoutGenerator>();
        if (candidateValidationService == null) candidateValidationService = FindFirstObjectByType<BBPLFSCandidateValidationService>();
        if (previewService == null) previewService = FindFirstObjectByType<BBPLFSPreviewService>();
        if (materializationService == null) materializationService = FindFirstObjectByType<BBPLFSMaterializationService>();
        if (selectedArea == null)
        { message = "Selecciona un RestaurantArea para Auto Furnish."; return false; }
        if (premisesCaptureService == null || assetCatalog == null || furnishingSetCatalog == null || generator == null || candidateValidationService == null)
        { message = "Faltan servicios BBPLFS V1."; return false; }
        message = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public void EditorConfigure(BBPLFSPremisesCaptureService premisesCapture,
        BBPLFSAssetLayoutCatalog assets, BBPLFSFurnishingSetCatalog sets,
        BBPLFSAdvancedLayoutGenerator layoutGenerator, BBPLFSCandidateValidationService validation,
        BBPLFSPreviewService preview, BBPLFSMaterializationService materialization, RestaurantArea area)
    {
        premisesCaptureService = premisesCapture;
        assetCatalog = assets;
        furnishingSetCatalog = sets;
        generator = layoutGenerator;
        candidateValidationService = validation;
        previewService = preview;
        materializationService = materialization;
        selectedArea = area;
    }
#endif
}
