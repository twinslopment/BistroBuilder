using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Validation Hub")]
public sealed class BBPLFSValidationHub : MonoBehaviour
{
    [Tooltip("Adapters to BBSIS, Navigation or other authoritative validators.")]
    [SerializeField] private MonoBehaviour[] validatorSources = Array.Empty<MonoBehaviour>();

    private readonly List<IBBPLFSLayoutValidator> validators = new();

    private void Awake()
    {
        RebuildValidators();
    }

    public void RebuildValidators()
    {
        validators.Clear();
        if (validatorSources == null)
        {
            return;
        }

        for (int index = 0; index < validatorSources.Length; index++)
        {
            if (validatorSources[index] is IBBPLFSLayoutValidator validator)
            {
                validators.Add(validator);
            }
        }

        validators.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
    public BBPLFSValidationReport Validate(
        BBPLFSLayoutCandidate candidate,
        RestaurantArea area)
    {
        if (candidate == null || area == null)
        {
            return new BBPLFSValidationReport(false, "invalid_input", "Candidate or area is unavailable.");
        }

        IReadOnlyList<BBPLFSLayoutPlacement> placements = candidate.Placements;
        for (int index = 0; index < placements.Count; index++)
        {
            if (!area.ContainsPosition(placements[index].WorldPosition))
            {
                return new BBPLFSValidationReport(
                    false,
                    "outside_scope",
                    "A generated placement lies outside the selected area.");
            }
        }

        for (int index = 0; index < validators.Count; index++)
        {
            BBPLFSValidationReport report = validators[index].Validate(candidate, area);
            if (!report.IsValid)
            {
                return report;
            }
        }

        return BBPLFSValidationReport.Valid();
    }

#if UNITY_EDITOR
    public void EditorSetValidatorSources(MonoBehaviour[] sources)
    {
        validatorSources = sources ?? Array.Empty<MonoBehaviour>();
        RebuildValidators();
    }
#endif
}