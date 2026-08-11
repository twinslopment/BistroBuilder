using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Capa de autoría visual/comercial de ingredientes canónicos.
///
/// Mantiene imágenes y formatos comerciales reutilizables por SupplierId/SKU
/// futuros, pero no reemplaza IngredientCatalog ni Inventory como autoridad
/// culinaria/física.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderIngredientAuthoringDatabase",
    menuName = "Bistro Builder/Inventario/Base visual y comercial de ingredientes"
)]
public sealed class BistroBuilderIngredientAuthoringDatabase : ScriptableObject
{
    public const string CurrentSchemaId = "ingredient.authoring";
    public const int CurrentSchemaVersion = 2;

    [SerializeField]
    private string schemaId = CurrentSchemaId;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private int contentRevision = 1;

    [SerializeField]
    private List<BistroBuilderIngredientAuthoringRecord> ingredients =
        new List<BistroBuilderIngredientAuthoringRecord>();

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public int ContentRevision => contentRevision;
    public IReadOnlyList<BistroBuilderIngredientAuthoringRecord> Ingredients => ingredients.AsReadOnly();

    public bool TryGetIngredient(string ingredientId, out BistroBuilderIngredientAuthoringRecord ingredient)
    {
        ingredient = null;
        string normalized = BistroBuilderIngredientAuthoringRecord.NormalizeIngredientId(ingredientId);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord candidate = ingredients[index];
            if (candidate != null && string.Equals(candidate.IngredientId, normalized, StringComparison.Ordinal))
            {
                ingredient = candidate;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public List<BistroBuilderIngredientAuthoringRecord> EditorIngredients => ingredients;

    public void EditorEnsureSchema()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        contentRevision = Mathf.Max(1, contentRevision);
    }

    public void EditorTouchRevision()
    {
        contentRevision = Mathf.Max(1, contentRevision + 1);
    }
#endif
}
