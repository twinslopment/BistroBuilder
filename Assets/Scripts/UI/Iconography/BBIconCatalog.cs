using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.UI.Iconography
{
    [CreateAssetMenu(fileName = "BBIconCatalog", menuName = "Bistro Builder/UI/Iconography/Icon Catalog")]
    public sealed class BBIconCatalog : ScriptableObject
    {
        public const string DefaultResourcesPath = "BistroBuilder/UI/BBIconCatalog";

        [SerializeField] private List<BBIconDefinition> entries = new List<BBIconDefinition>();

        private Dictionary<BBIconId, BBIconDefinition> lookup;

        public IReadOnlyList<BBIconDefinition> Entries => entries;

        public static BBIconCatalog LoadDefault()
        {
            return Resources.Load<BBIconCatalog>(DefaultResourcesPath);
        }

        public bool TryGet(BBIconId id, out BBIconDefinition definition)
        {
            EnsureLookup();
            return lookup.TryGetValue(id, out definition);
        }

        public Sprite GetSprite(BBIconId id)
        {
            return TryGet(id, out var definition) ? definition.sprite : null;
        }

        public BBIconSemanticRole GetSemanticRole(BBIconId id)
        {
            return TryGet(id, out var definition) ? definition.semanticRole : BBIconSemanticRole.Neutral;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(IEnumerable<BBIconDefinition> newEntries)
        {
            entries.Clear();
            if (newEntries != null)
                entries.AddRange(newEntries);

            lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnEnable()
        {
            lookup = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
                return;

            lookup = new Dictionary<BBIconId, BBIconDefinition>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!lookup.ContainsKey(entry.id))
                    lookup.Add(entry.id, entry);
            }
        }
    }
}
