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

#if UNITY_EDITOR
    public void EditorUpsert(
        string dishId,
        Sprite image,
        string sourceFileName)
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
            throw new ArgumentException("Dish image requires a valid DishId.", nameof(dishId));

        if (image == null)
            throw new ArgumentNullException(nameof(image));

        if (entries == null)
            entries = new List<Entry>();

        for (int index = 0; index < entries.Count; index++)
        {
            Entry current = entries[index];
            if (current != null &&
                string.Equals(
                    current.DishId,
                    normalized,
                    StringComparison.Ordinal))
            {
                UnityEditor.SerializedObject serialized =
                    new UnityEditor.SerializedObject(this);
                UnityEditor.SerializedProperty list =
                    serialized.FindProperty("entries");
                UnityEditor.SerializedProperty element =
                    list.GetArrayElementAtIndex(index);

                element.FindPropertyRelative("dishId").stringValue =
                    normalized;
                element.FindPropertyRelative("image").objectReferenceValue =
                    image;
                element.FindPropertyRelative("sourceFileName").stringValue =
                    sourceFileName ?? string.Empty;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                byId = null;
                return;
            }
        }

        UnityEditor.SerializedObject target =
            new UnityEditor.SerializedObject(this);
        UnityEditor.SerializedProperty targetEntries =
            target.FindProperty("entries");

        int newIndex = targetEntries.arraySize;
        targetEntries.InsertArrayElementAtIndex(newIndex);

        UnityEditor.SerializedProperty newEntry =
            targetEntries.GetArrayElementAtIndex(newIndex);

        newEntry.FindPropertyRelative("dishId").stringValue =
            normalized;
        newEntry.FindPropertyRelative("image").objectReferenceValue =
            image;
        newEntry.FindPropertyRelative("sourceFileName").stringValue =
            sourceFileName ?? string.Empty;

        target.ApplyModifiedPropertiesWithoutUndo();
        byId = null;
    }
#endif

    private void OnValidate()
    {
        byId = null;
    }
}
