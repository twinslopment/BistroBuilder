using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BBMotionRecipeCatalog", menuName = "Bistro Builder/Animation V1/Motion Recipe Catalog")]
public sealed class BistroBuilderMotionRecipeCatalog : ScriptableObject
{
    [SerializeField] private List<BistroBuilderMotionRecipe> recipes = new List<BistroBuilderMotionRecipe>();
    public IReadOnlyList<BistroBuilderMotionRecipe> Recipes => recipes;

    public bool TryResolve(string requestedMotionId, out BistroBuilderMotionRecipe recipe)
    {
        recipe = null;
        string current = BistroBuilderMotionProfile.NormalizeId(requestedMotionId);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (!visited.Add(current)) return false;
            BistroBuilderMotionRecipe direct = FindDirect(current);
            if (direct == null) return false;
            if (direct.ValidateConfiguration(out _)) { recipe = direct; return true; }
            current = direct.FallbackMotionId;
        }
        return false;
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < recipes.Count; i++)
        {
            BistroBuilderMotionRecipe recipe = recipes[i];
            if (recipe == null || !recipe.ValidateConfiguration(out error)) return false;
            if (!ids.Add(recipe.MotionId)) { error = name + " contains duplicate MotionId: " + recipe.MotionId; return false; }
        }
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(List<BistroBuilderMotionRecipe> configuredRecipes)
    {
        recipes = configuredRecipes ?? new List<BistroBuilderMotionRecipe>();
    }
#endif
    private BistroBuilderMotionRecipe FindDirect(string id)
    {
        for (int i = 0; i < recipes.Count; i++)
        {
            BistroBuilderMotionRecipe recipe = recipes[i];
            if (recipe != null && recipe.MotionId == id) return recipe;
        }
        return null;
    }
}
