using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BBInteractionFamily",
    menuName = "Bistro Builder/Animation/Interaction Family Profile")]
public sealed class BistroBuilderInteractionFamilyProfile : ScriptableObject
{
    [SerializeField] private string familyProfileId = "interaction.standard";
    [SerializeField] private BistroBuilderInteractionFamily family = BistroBuilderInteractionFamily.Workstation;
    [SerializeField] private List<BistroBuilderInteractionOperation> supportedOperations = new List<BistroBuilderInteractionOperation>();
    [SerializeField] private BistroBuilderInteractionAdaptationBudget adaptationBudget = new BistroBuilderInteractionAdaptationBudget();
    [SerializeField] private string primaryMotionId = string.Empty;
    [SerializeField] private string fallbackMotionId = string.Empty;
    [SerializeField] private bool requiresCommitConfirmation;

    public string FamilyProfileId => BistroBuilderMotionProfile.NormalizeId(familyProfileId);
    public BistroBuilderInteractionFamily Family => family;
    public IReadOnlyList<BistroBuilderInteractionOperation> SupportedOperations => supportedOperations;
    public BistroBuilderInteractionAdaptationBudget AdaptationBudget => adaptationBudget;
    public string PrimaryMotionId => BistroBuilderMotionProfile.NormalizeId(primaryMotionId);
    public string FallbackMotionId => BistroBuilderMotionProfile.NormalizeId(fallbackMotionId);
    public bool RequiresCommitConfirmation => requiresCommitConfirmation;

    public bool Supports(BistroBuilderInteractionOperation operation) => supportedOperations != null && supportedOperations.Contains(operation);

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(FamilyProfileId))
        {
            error = name + " necesita FamilyProfileId.";
            return false;
        }
        if (family == BistroBuilderInteractionFamily.None)
        {
            error = name + " necesita familia semántica.";
            return false;
        }
        if (adaptationBudget == null || !adaptationBudget.Validate(out error)) return false;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredId,
        BistroBuilderInteractionFamily configuredFamily,
        List<BistroBuilderInteractionOperation> operations,
        string configuredPrimaryMotionId,
        string configuredFallbackMotionId,
        bool commitConfirmation)
    {
        familyProfileId = configuredId;
        family = configuredFamily;
        supportedOperations = operations ?? new List<BistroBuilderInteractionOperation>();
        primaryMotionId = configuredPrimaryMotionId ?? string.Empty;
        fallbackMotionId = configuredFallbackMotionId ?? string.Empty;
        requiresCommitConfirmation = commitConfirmation;
    }
#endif
}
