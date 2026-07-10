using UnityEngine;

/// <summary>
/// Define una categoría reutilizable de zona del restaurante.
///
/// Cada tipo de zona se crea como un asset configurable:
/// comedor, cocina, entrada, terraza, barra, almacén, baños, etc.
///
/// Añadir un nuevo tipo de zona no requiere modificar código.
/// </summary>
[CreateAssetMenu(
    fileName = "AreaDefinition_",
    menuName = "Bistro Builder/Restaurant/Area Definition"
)]
public sealed class RestaurantAreaDefinition : ScriptableObject
{
    [Header("Identificación")]

    [Tooltip(
        "Identificador estable del tipo de área. " +
        "Ejemplos: dining, kitchen, entrance."
    )]
    [SerializeField]
    private string areaTypeId;

    [Tooltip(
        "Nombre visible del tipo de área."
    )]
    [SerializeField]
    private string displayName;

    [TextArea(2, 5)]
    [SerializeField]
    private string description;

    public string AreaTypeId => areaTypeId;
    public string DisplayName => displayName;
    public string Description => description;

#if UNITY_EDITOR
    private void OnValidate()
    {
        areaTypeId = NormalizeIdentifier(areaTypeId);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }
    }

    /// <summary>
    /// Normaliza identificadores únicamente desde el editor.
    /// No genera trabajo adicional durante la partida.
    /// </summary>
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