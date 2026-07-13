using UnityEngine;

/// <summary>
/// Define una capacidad funcional que puede ofrecer un área.
///
/// Ejemplos:
/// - Permitir llegada de clientes.
/// - Permitir espera.
/// - Permitir colocar mesas.
/// - Permitir producción de comida.
/// - Permitir recogida de comandas.
///
/// Las capacidades se crean como assets configurables.
/// Añadir una capacidad futura no requiere modificar código.
/// </summary>
[CreateAssetMenu(
    fileName = "AreaCapability_",
    menuName =
        "Bistro Builder/Restaurant/Area Capability Definition"
)]
public sealed class RestaurantAreaCapabilityDefinition :
    ScriptableObject
{
    [Header("Identificación")]

    [Tooltip(
        "Identificador estable de la capacidad. " +
        "Ejemplos: customer_seating, food_production."
    )]
    [SerializeField]
    private string capabilityId;

    [Tooltip("Nombre visible de la capacidad.")]
    [SerializeField]
    private string displayName;

    [TextArea(2, 5)]
    [SerializeField]
    private string description;

    public string CapabilityId => capabilityId;
    public string DisplayName => displayName;
    public string Description => description;

#if UNITY_EDITOR
    private void OnValidate()
    {
        capabilityId =
            NormalizeIdentifier(capabilityId);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }
    }

    private static string NormalizeIdentifier(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_");
    }
#endif
}