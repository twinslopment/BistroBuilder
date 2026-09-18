using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [CreateAssetMenu(
        fileName = "FurnitureFinishRegistry",
        menuName = "BistroBuilder/Furniture Finishes/Published Registry")]
    public sealed class FurnitureFinishRegistry : ScriptableObject
    {
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(string furnitureId, out FurnitureFinishPublishedSet publishedSet)
        {
            foreach (var entry in entries ?? Array.Empty<Entry>())
            {
                if (entry != null
                    && string.Equals(entry.FurnitureId, furnitureId, StringComparison.Ordinal))
                {
                    publishedSet = entry.PublishedSet;
                    return publishedSet != null;
                }
            }

            publishedSet = null;
            return false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Entry[] value)
        {
            entries = value ?? Array.Empty<Entry>();
        }
#endif

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string furnitureId = string.Empty;
            [SerializeField] private FurnitureFinishPublishedSet publishedSet;

            public string FurnitureId => furnitureId;
            public FurnitureFinishPublishedSet PublishedSet => publishedSet;

            public Entry(string id, FurnitureFinishPublishedSet set)
            {
                furnitureId = id ?? string.Empty;
                publishedSet = set;
            }
        }
    }
}