using System.Collections.Generic;
using UnityEngine;

/// <summary>Catálogo canónico de perfiles conductuales de clientes.</summary>
[CreateAssetMenu(
    fileName = "BB_AdvancedCustomer_Profile_Catalog",
    menuName = "Bistro Builder/Customers/Advanced Profile Catalog")]
public sealed class BistroBuilderAdvancedCustomerProfileCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderAdvancedCustomerArchetypeDefinition> archetypes =
        new List<BistroBuilderAdvancedCustomerArchetypeDefinition>();

    public IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> Archetypes =>
        archetypes;
    public int Count => archetypes != null ? archetypes.Count : 0;

    public bool ValidateConfiguration(out string error) =>
        BistroBuilderAdvancedCustomerProfileEngine.TryValidateCatalog(archetypes, out error);

#if UNITY_EDITOR
    public void EditorReplaceAll(
        IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> definitions)
    {
        archetypes.Clear();
        if (definitions == null) return;
        for (int i = 0; i < definitions.Count; i++)
            archetypes.Add(definitions[i]?.DeepClone());
    }
#endif
}
