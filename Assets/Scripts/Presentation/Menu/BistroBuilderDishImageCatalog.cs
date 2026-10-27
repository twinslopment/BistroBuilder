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
    private Dictionary<string, Sprite> byCanonicalPhotoKey;
    private HashSet<string> ambiguousPhotoKeys;

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGetImage(string dishId, out Sprite image)
    {
        return TryGetImage(dishId, null, out image);
    }

    /// <summary>
    /// Always prefer an exact stable DishId. Only then use canonical photo
    /// keys derived from the DishId and visible name, so a player-authored
    /// dish can reuse a staged image without changing any gameplay identity.
    /// Ambiguous keys never select a random photograph.
    /// </summary>
    public bool TryGetImage(
        string dishId, string displayName, out Sprite image)
    {
        EnsureCache();

        if (!string.IsNullOrWhiteSpace(dishId) &&
            byId.TryGetValue(dishId.Trim(), out image) && image != null)
            return true;

        if (TryGetCanonicalImage(dishId, out image))
            return true;

        return TryGetCanonicalImage(displayName, out image);
    }

    public Sprite GetImageOrNull(string dishId)
    {
        return GetImageOrNull(dishId, null);
    }

    public Sprite GetImageOrNull(string dishId, string displayName)
    {
        return TryGetImage(dishId, displayName, out Sprite image)
            ? image : null;
    }

    public static BistroBuilderDishImageCatalog LoadDefault()
    {
        return Resources.Load<BistroBuilderDishImageCatalog>(
            "BistroBuilder/Menu/DishImageCatalog"
        );
    }

    private bool TryGetCanonicalImage(string keySource, out Sprite image)
    {
        image = null;
        string key = BistroBuilderDishImageKeyUtility.CanonicalKey(keySource);
        return key.Length > 0 && !ambiguousPhotoKeys.Contains(key) &&
               byCanonicalPhotoKey.TryGetValue(key, out image) &&
               image != null;
    }

    private void EnsureCache()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        byCanonicalPhotoKey =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        ambiguousPhotoKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.DishId) ||
                entry.Image == null)
                continue;

            byId[entry.DishId.Trim()] = entry.Image;

            string key =
                BistroBuilderDishImageKeyUtility.CanonicalKey(entry.DishId);
            if (key.Length == 0 || ambiguousPhotoKeys.Contains(key))
                continue;

            if (byCanonicalPhotoKey.TryGetValue(key, out Sprite existing))
            {
                if (existing != entry.Image)
                {
                    byCanonicalPhotoKey.Remove(key);
                    ambiguousPhotoKeys.Add(key);
                }
            }
            else
                byCanonicalPhotoKey.Add(key, entry.Image);
        }
    }

    private void OnValidate()
    {
        byId = null;
        byCanonicalPhotoKey = null;
        ambiguousPhotoKeys = null;
    }
}
