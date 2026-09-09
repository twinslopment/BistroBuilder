using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Navigation/Edit Mode Validation Provider")]
public sealed class BistroBuilderEditNavigationValidationProvider :
    MonoBehaviour,
    IBistroBuilderEditValidationProvider
{
    [SerializeField, Min(0.1f)] private float minimumPassageWidth = 0.75f;
    [SerializeField, Min(0f)] private float floorOpeningTolerance = 0.15f;
    [SerializeField, Min(0.5f)] private float minimumPassageHeight = 1.8f;
    [SerializeField] private BistroBuilderEditDocumentRuntimeService editDocumentService;

    public string SourceSystem => "Navigation";
    public bool IsRegistered { get; private set; }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        if (IsRegistered && editDocumentService != null)
            editDocumentService.UnregisterValidationProvider(this);
        IsRegistered = false;
    }

    public bool Register()
    {
        if (editDocumentService == null)
            editDocumentService = FindFirstObjectByType<
                BistroBuilderEditDocumentRuntimeService>();
        if (editDocumentService == null)
        {
            IsRegistered = false;
            return false;
        }
        IsRegistered = editDocumentService.RegisterValidationProvider(this);
        return IsRegistered;
    }

    public void Validate(
        BistroBuilderEditDocument draft,
        long draftRevision,
        List<BistroBuilderEditDiagnostic> diagnostics)
    {
        if (draft == null || diagnostics == null) return;
        for (int i = 0; i < draft.openings.Count; i++)
        {
            BistroBuilderOpeningRecord opening = draft.openings[i];
            if (opening == null ||
                opening.bottomElevation > floorOpeningTolerance ||
                opening.height < minimumPassageHeight ||
                opening.width >= minimumPassageWidth)
                continue;

            diagnostics.Add(new BistroBuilderEditDiagnostic
            {
                sourceSystem = SourceSystem,
                code = "NAV_PASSAGE_TOO_NARROW",
                severity = BistroBuilderEditDiagnosticSeverity.Blocking,
                targetId = opening.openingId,
                message = "El paso queda por debajo de la anchura mínima de circulación.",
                draftRevision = draftRevision
            });
        }
    }
}
