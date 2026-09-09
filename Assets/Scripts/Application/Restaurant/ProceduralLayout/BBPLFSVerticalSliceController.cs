using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Vertical Slice Controller")]
public sealed class BBPLFSVerticalSliceController : MonoBehaviour
{
    [Header("BBPLFS")]
    [SerializeField] private BBPLFSPremisesCaptureService premisesCaptureService;
    [SerializeField] private BBPLFSAssetLayoutCatalog assetLayoutCatalog;
    [SerializeField] private BBPLFSLayoutGenerator layoutGenerator;
    [SerializeField] private BBPLFSValidationHub validationHub;
    [SerializeField] private BBPLFSPreviewService previewService;
    [SerializeField] private BBPLFSMaterializationService materializationService;

    [Header("Current test scope")]
    [SerializeField] private RestaurantArea selectedArea;
    [SerializeField, Min(1)] private int targetCapacity = 20;
    [SerializeField] private BBPLFSGoalProfile goalProfile = BBPLFSGoalProfile.Balanced;

    private BBPLFSPremisesModel currentPremises;
    private readonly List<BBPLFSLayoutCandidate> candidates = new();
    private int previewedCandidateIndex = -1;

    public RestaurantArea SelectedArea => selectedArea;
    public IReadOnlyList<BBPLFSLayoutCandidate> Candidates => candidates;
    public int PreviewedCandidateIndex => previewedCandidateIndex;
    public string PremisesRevision => currentPremises != null ? currentPremises.Revision : string.Empty;

    public event Action CandidatesChanged;

    public bool AnalyzeSelectedArea(out string message)
    {
        if (premisesCaptureService == null || selectedArea == null)
        {
            message = "Select a RestaurantArea and configure Premises Capture Service.";
            return false;
        }

        currentPremises = premisesCaptureService.CaptureAll();
        if (!currentPremises.TryGetSpace(selectedArea, out BBPLFSPremisesSpaceSnapshot space))
        {
            if (!premisesCaptureService.TryCaptureArea(selectedArea, out space))
            {
                message = "The selected area has no usable boundary colliders.";
                return false;
            }
        }

        message = $"{space.SpaceId}: {space.FloorAreaSquareMeters:0.0} m², premises {currentPremises.Revision}.";
        return true;
    }
    public bool GenerateDiningRoom(out string message)
    {
        candidates.Clear();
        previewedCandidateIndex = -1;
        previewService?.ClearPreview();

        if (!AnalyzeSelectedArea(out message))
        {
            CandidatesChanged?.Invoke();
            return false;
        }

        if (!currentPremises.TryGetSpace(selectedArea, out BBPLFSPremisesSpaceSnapshot space) &&
            !premisesCaptureService.TryCaptureArea(selectedArea, out space))
        {
            message = "Selected room could not be captured.";
            CandidatesChanged?.Invoke();
            return false;
        }

        if (assetLayoutCatalog == null || layoutGenerator == null)
        {
            message = "BBPLFS layout dependencies are unavailable.";
            CandidatesChanged?.Invoke();
            return false;
        }

        if (!assetLayoutCatalog.TryGetFirst(BBPLFSLayoutRole.DiningTable, out BBPLFSAssetLayoutProfile tableProfile) ||
            !assetLayoutCatalog.TryGetFirst(BBPLFSLayoutRole.DiningSeat, out BBPLFSAssetLayoutProfile chairProfile))
        {
            message = "A dining table profile and a dining seat profile are required.";
            CandidatesChanged?.Invoke();
            return false;
        }

        BBPLFSDesignScope scope = new(BBPLFSDesignScopeKind.Room, selectedArea);
        BBPLFSLayoutBrief brief = new(
            scope,
            BBPLFSSpaceFunction.Dining,
            goalProfile,
            targetCapacity,
            preserveExisting: true);

        List<BBPLFSLayoutCandidate> generated = layoutGenerator.GenerateDiningCandidates(
            space,
            brief,
            tableProfile,
            chairProfile);
        for (int index = 0; index < generated.Count; index++)
        {
            BBPLFSLayoutCandidate candidate = generated[index];
            BBPLFSValidationReport report = validationHub != null
                ? validationHub.Validate(candidate, selectedArea)
                : BBPLFSValidationReport.Valid();

            if (report.IsValid)
            {
                candidates.Add(candidate);
            }
        }

        CandidatesChanged?.Invoke();

        if (candidates.Count == 0)
        {
            message = "No valid dining layout was generated for the selected scope.";
            return false;
        }

        message = $"Generated {candidates.Count} candidate(s). Best capacity: {candidates[0].Capacity}.";
        return true;
    }

    public bool PreviewCandidate(int index, out string message)
    {
        if (index < 0 || index >= candidates.Count || previewService == null)
        {
            message = "Candidate index or preview service is invalid.";
            return false;
        }

        previewService.ShowCandidate(candidates[index]);
        previewedCandidateIndex = index;
        message = $"Previewing {candidates[index].CandidateId}, capacity {candidates[index].Capacity}.";
        return true;
    }

    public bool AcceptPreviewedCandidate(out string message)
    {
        if (previewedCandidateIndex < 0 || previewedCandidateIndex >= candidates.Count)
        {
            message = "No BBPLFS candidate is currently previewed.";
            return false;
        }

        if (materializationService == null)
        {
            message = "Materialization service is unavailable.";
            return false;
        }
        BBPLFSLayoutCandidate candidate = candidates[previewedCandidateIndex];
        if (!materializationService.TryMaterialize(candidate, selectedArea, out message))
        {
            return false;
        }

        previewService?.ClearPreview();
        previewedCandidateIndex = -1;
        return true;
    }

    public void CancelPreview()
    {
        previewService?.ClearPreview();
        previewedCandidateIndex = -1;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        BBPLFSPremisesCaptureService premises,
        BBPLFSAssetLayoutCatalog catalog,
        BBPLFSLayoutGenerator generator,
        BBPLFSValidationHub validation,
        BBPLFSPreviewService preview,
        BBPLFSMaterializationService materialization,
        RestaurantArea area)
    {
        premisesCaptureService = premises;
        assetLayoutCatalog = catalog;
        layoutGenerator = generator;
        validationHub = validation;
        previewService = preview;
        materializationService = materialization;
        selectedArea = area;
    }
#endif
}