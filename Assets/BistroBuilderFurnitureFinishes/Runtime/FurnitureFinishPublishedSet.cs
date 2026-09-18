using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [CreateAssetMenu(
        fileName = "FurnitureFinishPublishedSet",
        menuName = "BistroBuilder/Furniture Finishes/Published Set")]
    public sealed class FurnitureFinishPublishedSet : ScriptableObject
    {
        [SerializeField] private string furnitureId = string.Empty;
        [SerializeField] private GameObject sourceAsset;
        [SerializeField] private string defaultVariantId = string.Empty;
        [SerializeField] private PublishedVariant[] variants = Array.Empty<PublishedVariant>();

        public string FurnitureId => furnitureId;
        public GameObject SourceAsset => sourceAsset;
        public string DefaultVariantId => defaultVariantId;
        public IReadOnlyList<PublishedVariant> Variants => variants;

        public bool TryGetVariant(string variantId, out PublishedVariant variant)
        {
            foreach (var entry in variants ?? Array.Empty<PublishedVariant>())
            {
                if (entry != null
                    && string.Equals(entry.Id, variantId, StringComparison.Ordinal))
                {
                    variant = entry;
                    return true;
                }
            }

            variant = null;
            return false;
        }

        public PublishedVariant GetDefaultVariant()
        {
            if (TryGetVariant(defaultVariantId, out var variant))
                return variant;
            return variants != null && variants.Length > 0 ? variants[0] : null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            GameObject source,
            string defaultId,
            PublishedVariant[] entries)
        {
            furnitureId = id ?? string.Empty;
            sourceAsset = source;
            defaultVariantId = defaultId ?? string.Empty;
            variants = entries ?? Array.Empty<PublishedVariant>();
        }
#endif

        [Serializable]
        public sealed class PublishedBinding
        {
            [SerializeField] private string rendererPath = string.Empty;
            [SerializeField] private int materialIndex;
            [SerializeField] private Material material;

            public string RendererPath => rendererPath;
            public int MaterialIndex => materialIndex;
            public Material Material => material;

            public PublishedBinding(string path, int index, Material value)
            {
                rendererPath = path ?? string.Empty;
                materialIndex = Mathf.Max(0, index);
                material = value;
            }
        }

        [Serializable]
        public sealed class PublishedVariant
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private Texture2D thumbnail;
            [SerializeField] private PublishedBinding[] bindings = Array.Empty<PublishedBinding>();

            public string Id => id;
            public string DisplayName => displayName;
            public Texture2D Thumbnail => thumbnail;
            public IReadOnlyList<PublishedBinding> Bindings => bindings;

            public PublishedVariant(
                string variantId,
                string visibleName,
                Texture2D variantThumbnail,
                PublishedBinding[] entries)
            {
                id = variantId ?? string.Empty;
                displayName = visibleName ?? string.Empty;
                thumbnail = variantThumbnail;
                bindings = entries ?? Array.Empty<PublishedBinding>();
            }
        }
    }
}