using UnityEngine;

/// <summary>Identifica qué miembro lógico representa un visual clicable 10G.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedCustomerMemberHitTarget : MonoBehaviour
{
    [SerializeField] private CustomerGroup customerGroup;
    [SerializeField, Min(1)] private int memberIndex = 1;

    public CustomerGroup CustomerGroup => customerGroup;
    public int MemberIndex => memberIndex;

    public void Bind(CustomerGroup group, int index)
    {
        customerGroup = group;
        memberIndex = Mathf.Max(1, index);
    }

    public bool ValidateConfiguration(out string error)
    {
        if (customerGroup == null || customerGroup.GroupId < 1 ||
            memberIndex < 1 || memberIndex > customerGroup.GroupSize)
        {
            error = "El visual individual 10G no está enlazado a un miembro válido.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}
