using System.Collections.Generic;
using UnityEngine;

/// <summary>Catálogo canónico de hitos de progresión de negocio.</summary>
[CreateAssetMenu(
    fileName = "BB_Progression_Milestone_Catalog",
    menuName = "Bistro Builder/Progression/Milestone Catalog")]
public sealed class BistroBuilderProgressionMilestoneCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderProgressionMilestoneDefinition> milestones =
        new List<BistroBuilderProgressionMilestoneDefinition>();

    public IReadOnlyList<BistroBuilderProgressionMilestoneDefinition> Milestones => milestones;

    public bool ValidateConfiguration(out string error)
    {
        return BistroBuilderProgressionMilestoneEngine.TryValidateCatalog(milestones, out error);
    }

#if UNITY_EDITOR
    public void EditorReplaceAll(
        IReadOnlyList<BistroBuilderProgressionMilestoneDefinition> definitions)
    {
        milestones.Clear();
        if (definitions == null) return;
        for (int i = 0; i < definitions.Count; i++)
            milestones.Add(definitions[i]?.DeepClone());
    }
#endif
}
