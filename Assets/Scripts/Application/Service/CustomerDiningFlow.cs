using UnityEngine;

/// <summary>
/// Adaptador de compatibilidad del prefab de grupos.
///
/// Antes de 367E este componente ejecutaba un único temporizador para todo el
/// grupo. Esa responsabilidad pertenece ahora a
/// BistroBuilderCustomerDiningService, que trabaja por CustomerId y pase.
///
/// Se conserva el componente para no romper prefabs ni GUID existentes, pero
/// no mantiene corrutinas, no cambia estados y no constituye autoridad.
/// </summary>
[DisallowMultipleComponent]
public sealed class CustomerDiningFlow : MonoBehaviour
{
    [Header("Referencia legacy conservada")]

    [SerializeField]
    private CustomerGroup customerGroup;

    public CustomerGroup CustomerGroup => customerGroup;

    private void Awake()
    {
        if (customerGroup == null)
        {
            customerGroup = GetComponent<CustomerGroup>();
        }
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (customerGroup == null)
        {
            error = "CustomerDiningFlow necesita CustomerGroup.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
