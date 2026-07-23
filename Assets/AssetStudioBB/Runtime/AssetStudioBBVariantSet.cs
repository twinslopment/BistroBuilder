using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.AssetStudioBB
{
    /// <summary>
    /// Resultado estable de Asset Studio BB. Conserva la familia, las medidas
    /// y los prefabs visuales de cada acabado sin depender del Editor.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AssetStudioBBVariantSet",
        menuName = "BistroBuilder/Asset Studio BB/Variant Set")]
    public sealed class AssetStudioBBVariantSet : ScriptableObject
    {
        [SerializeField] private string assetId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string family = string.Empty;
        [SerializeField] private string subtype = string.Empty;
        [SerializeField] private Vector3 dimensionsMeters = Vector3.one;
        [SerializeField] private string defaultVariantId = string.Empty;
        [SerializeField] private VariantEntry[] variants = Array.Empty<VariantEntry>();

        public string AssetId => assetId;
        public string DisplayName => displayName;
        public string Category => category;
        public string Family => family;
        public string Subtype => subtype;
        public Vector3 DimensionsMeters => dimensionsMeters;
        public string DefaultVariantId => defaultVariantId;
        public IReadOnlyList<VariantEntry> Variants => variants;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string visibleName,
            string assetCategory,
            string assetFamily,
            string assetSubtype,
            Vector3 dimensions,
            string defaultId,
            VariantEntry[] entries)
        {
            assetId = id ?? string.Empty;
            displayName = visibleName ?? string.Empty;
            category = assetCategory ?? string.Empty;
            family = assetFamily ?? string.Empty;
            subtype = assetSubtype ?? string.Empty;
            dimensionsMeters = dimensions;
            defaultVariantId = defaultId ?? string.Empty;
            variants = entries ?? Array.Empty<VariantEntry>();
        }
#endif

        [Serializable]
        public sealed class VariantEntry
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private float priceMultiplier = 1f;
            [SerializeField] private GameObject visualPrefab;

            public string Id => id;
            public string DisplayName => displayName;
            public float PriceMultiplier => priceMultiplier;
            public GameObject VisualPrefab => visualPrefab;

            public VariantEntry(
                string variantId,
                string visibleName,
                float multiplier,
                GameObject prefab)
            {
                id = variantId ?? string.Empty;
                displayName = visibleName ?? string.Empty;
                priceMultiplier = Mathf.Max(0.01f, multiplier);
                visualPrefab = prefab;
            }
        }
    }
}
