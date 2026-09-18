using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DishImageCatalog",
    menuName = "Bistro Builder/Menu/Dish Image Catalog",
    order = 130
)]
public sealed class BistroBuilderDishImageCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string dishId = string.Empty;
        [SerializeField] private Sprite image;
        [SerializeField] private string sourceFileName = string.Empty;

        public string DishId => dishId;
        public Sprite Image => image;
        public string SourceFileName => sourceFileName;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, Sprite> byId;

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGetImage(string dishId, out Sprite image)
    {
        EnsureCache();
        if (string.IsNullOrWhiteSpace(dishId))
        {
            image = null;
            return false;
        }

        return byId.TryGetValue(dishId.Trim(), out image) && image != null;
    }

    public Sprite GetImageOrNull(string dishId)
    {
        return TryGetImage(dishId, out Sprite image) ? image : null;
    }

    public static BistroBuilderDishImageCatalog LoadDefault()
    {
        return Resources.Load<BistroBuilderDishImageCatalog>(
            "BistroBuilder/Menu/DishImageCatalog"
        );
    }

    private void EnsureCache()
    {
        if (byId != null)
        {
            return;
        }

        byId = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.DishId) ||
                entry.Image == null)
            {
                continue;
            }

            byId[entry.DishId.Trim()] = entry.Image;
        }
    }

    private void OnValidate()
    {
        byId = null;
    }
}
