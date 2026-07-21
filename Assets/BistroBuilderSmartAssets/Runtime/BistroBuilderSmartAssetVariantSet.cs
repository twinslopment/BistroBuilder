using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.SmartAssets
{
    /// <summary>
    /// Conjunto de variantes visuales que reutilizan el mismo modelo FBX.
    /// </summary>
    [CreateAssetMenu(fileName = "SmartAssetVariantSet", menuName = "BistroBuilder/Smart Assets/Conjunto de variantes")]
    public sealed class BistroBuilderSmartAssetVariantSet : ScriptableObject
    {
        [SerializeField] private string assetId = string.Empty;
        [SerializeField] private GameObject sourceModel;
        [SerializeField] private VariantEntry[] variants = Array.Empty<VariantEntry>();

        public string AssetId => assetId;
        public GameObject SourceModel => sourceModel;
        public IReadOnlyList<VariantEntry> Variants => variants;

#if UNITY_EDITOR
        public void EditorConfigure(string newAssetId, GameObject newSourceModel, VariantEntry[] newVariants)
        {
            assetId = newAssetId;
            sourceModel = newSourceModel;
            variants = newVariants ?? Array.Empty<VariantEntry>();
        }
#endif

        [Serializable]
        public sealed class VariantEntry
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private Material material;
            [SerializeField] private GameObject visualPrefab;
            [SerializeField] private float priceMultiplier = 1f;

            public string Id => id;
            public string DisplayName => displayName;
            public Material Material => material;
            public GameObject VisualPrefab => visualPrefab;
            public float PriceMultiplier => priceMultiplier;

            public VariantEntry(string id, string displayName, Material material, GameObject visualPrefab, float priceMultiplier)
            {
                this.id = id;
                this.displayName = displayName;
                this.material = material;
                this.visualPrefab = visualPrefab;
                this.priceMultiplier = priceMultiplier;
            }
        }
    }
}
