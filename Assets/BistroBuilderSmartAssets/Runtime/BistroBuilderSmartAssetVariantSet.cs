using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.SmartAssets
{
    /// <summary>
    /// Define una familia de acabados visuales que comparten exactamente
    /// la misma geometría, medidas, huella y collider de juego.
    ///
    /// Este objeto no duplica el FBX: cada entrada referencia un material
    /// y un prefab visual construido sobre el mismo modelo fuente.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SmartAssetVariantSet",
        menuName = "BistroBuilder/Configurador de Assets/Conjunto de variantes")]
    public sealed class BistroBuilderSmartAssetVariantSet : ScriptableObject
    {
        [SerializeField] private string assetId = string.Empty;
        [SerializeField] private string defaultVariantId = string.Empty;
        [SerializeField] private GameObject sourceModel;
        [SerializeField] private VariantEntry[] variants = Array.Empty<VariantEntry>();

        public string AssetId => assetId;
        public string DefaultVariantId => defaultVariantId;
        public GameObject SourceModel => sourceModel;
        public IReadOnlyList<VariantEntry> Variants => variants;

        public bool TryGetVariant(
            string variantId,
            out VariantEntry variant)
        {
            if (!string.IsNullOrWhiteSpace(variantId))
            {
                foreach (var entry in variants)
                {
                    if (entry != null
                        && string.Equals(
                            entry.Id,
                            variantId,
                            StringComparison.Ordinal))
                    {
                        variant = entry;
                        return true;
                    }
                }
            }

            variant = null;
            return false;
        }

        public VariantEntry GetDefaultVariant()
        {
            if (TryGetVariant(defaultVariantId, out var selected))
            {
                return selected;
            }

            return variants != null && variants.Length > 0
                ? variants[0]
                : null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newAssetId,
            string newDefaultVariantId,
            GameObject newSourceModel,
            VariantEntry[] newVariants)
        {
            assetId = newAssetId ?? string.Empty;
            defaultVariantId = newDefaultVariantId ?? string.Empty;
            sourceModel = newSourceModel;
            variants = newVariants ?? Array.Empty<VariantEntry>();
        }
#endif

        [Serializable]
        public sealed class VariantEntry
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private Color baseColor = Color.white;
            [SerializeField] private Material material;
            [SerializeField] private GameObject visualPrefab;
            [SerializeField] private float priceMultiplier = 1f;

            public string Id => id;
            public string DisplayName => displayName;
            public Color BaseColor => baseColor;
            public Material Material => material;
            public GameObject VisualPrefab => visualPrefab;
            public float PriceMultiplier => priceMultiplier;

            public VariantEntry(
                string id,
                string displayName,
                Color baseColor,
                Material material,
                GameObject visualPrefab,
                float priceMultiplier)
            {
                this.id = id ?? string.Empty;
                this.displayName = displayName ?? string.Empty;
                this.baseColor = baseColor;
                this.material = material;
                this.visualPrefab = visualPrefab;
                this.priceMultiplier = Mathf.Max(0.01f, priceMultiplier);
            }
        }
    }
}
