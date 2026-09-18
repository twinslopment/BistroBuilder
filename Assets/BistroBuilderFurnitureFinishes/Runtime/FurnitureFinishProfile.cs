using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [CreateAssetMenu(
        fileName = "FurnitureFinishProfile",
        menuName = "BistroBuilder/Furniture Finishes/Furniture Profile")]
    public sealed class FurnitureFinishProfile : ScriptableObject
    {
        [SerializeField] private string furnitureId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private GameObject sourceAsset;
        [SerializeField] private string defaultVariantId = string.Empty;
        [SerializeField] private ZoneDefinition[] zones = Array.Empty<ZoneDefinition>();
        [SerializeField] private VariantDefinition[] variants = Array.Empty<VariantDefinition>();

        public string FurnitureId => furnitureId;
        public string DisplayName => displayName;
        public GameObject SourceAsset => sourceAsset;
        public string DefaultVariantId => defaultVariantId;
        public IReadOnlyList<ZoneDefinition> Zones => zones;
        public IReadOnlyList<VariantDefinition> Variants => variants;

        public ZoneDefinition FindZone(string zoneId)
        {
            foreach (var zone in zones ?? Array.Empty<ZoneDefinition>())
            {
                if (zone != null && string.Equals(zone.Id, zoneId, StringComparison.Ordinal))
                    return zone;
            }
            return null;
        }

        public VariantDefinition FindVariant(string variantId)
        {
            foreach (var variant in variants ?? Array.Empty<VariantDefinition>())
            {
                if (variant != null && string.Equals(variant.Id, variantId, StringComparison.Ordinal))
                    return variant;
            }
            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string visibleName,
            GameObject source,
            ZoneDefinition[] zoneDefinitions,
            VariantDefinition[] variantDefinitions,
            string defaultId)
        {
            furnitureId = id ?? string.Empty;
            displayName = visibleName ?? string.Empty;
            sourceAsset = source;
            zones = zoneDefinitions ?? Array.Empty<ZoneDefinition>();
            variants = variantDefinitions ?? Array.Empty<VariantDefinition>();
            defaultVariantId = defaultId ?? string.Empty;
        }

        public void EditorSetVariants(VariantDefinition[] entries, string defaultId)
        {
            variants = entries ?? Array.Empty<VariantDefinition>();
            defaultVariantId = defaultId ?? string.Empty;
        }
#endif

        [Serializable]
        public sealed class SlotBinding
        {
            [SerializeField] private string rendererPath = string.Empty;
            [SerializeField] private int materialIndex;

            public string RendererPath => rendererPath;
            public int MaterialIndex => materialIndex;

            public SlotBinding(string path, int index)
            {
                rendererPath = path ?? string.Empty;
                materialIndex = Mathf.Max(0, index);
            }
        }

        [Serializable]
        public sealed class ZoneDefinition
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private FurnitureSurfaceFamily family = FurnitureSurfaceFamily.Unknown;
            [SerializeField] private FurnitureSurfaceFamily[] compatibleFamilies =
                Array.Empty<FurnitureSurfaceFamily>();
            [SerializeField] private bool classificationConfirmed;
            [SerializeField] private FurnitureFinishChannel requiredChannels = FurnitureFinishChannel.None;
            [SerializeField] private SlotBinding[] slots = Array.Empty<SlotBinding>();

            public string Id => id;
            public string DisplayName => displayName;
            public FurnitureSurfaceFamily Family => family;
            public IReadOnlyList<FurnitureSurfaceFamily> CompatibleFamilies => compatibleFamilies;
            public bool ClassificationConfirmed => classificationConfirmed;
            public FurnitureFinishChannel RequiredChannels => requiredChannels;
            public IReadOnlyList<SlotBinding> Slots => slots;

            public ZoneDefinition(
                string zoneId,
                string visibleName,
                FurnitureSurfaceFamily surfaceFamily,
                bool confirmed,
                FurnitureFinishChannel channels,
                SlotBinding[] slotBindings)
            {
                id = zoneId ?? string.Empty;
                displayName = visibleName ?? string.Empty;
                family = surfaceFamily;
                compatibleFamilies = surfaceFamily == FurnitureSurfaceFamily.Unknown
                    ? Array.Empty<FurnitureSurfaceFamily>()
                    : new[] { surfaceFamily };
                classificationConfirmed = confirmed;
                requiredChannels = channels;
                slots = slotBindings ?? Array.Empty<SlotBinding>();
            }

            public ZoneDefinition(
                string zoneId,
                string visibleName,
                FurnitureSurfaceFamily primaryFamily,
                FurnitureSurfaceFamily[] allowedFamilies,
                bool confirmed,
                FurnitureFinishChannel channels,
                SlotBinding[] slotBindings)
            {
                id = zoneId ?? string.Empty;
                displayName = visibleName ?? string.Empty;
                family = primaryFamily;
                compatibleFamilies = allowedFamilies ?? Array.Empty<FurnitureSurfaceFamily>();
                classificationConfirmed = confirmed;
                requiredChannels = channels;
                slots = slotBindings ?? Array.Empty<SlotBinding>();
            }

            public bool Allows(FurnitureSurfaceFamily candidate)
            {
                if (candidate == FurnitureSurfaceFamily.Unknown)
                    return false;
                if (compatibleFamilies != null && compatibleFamilies.Length > 0)
                {
                    foreach (var allowed in compatibleFamilies)
                    {
                        if (allowed == candidate)
                            return true;
                    }
                    return false;
                }
                return family != FurnitureSurfaceFamily.Unknown && family == candidate;
            }
        }

        [Serializable]
        public sealed class ZoneFinishBinding
        {
            [SerializeField] private string zoneId = string.Empty;
            [SerializeField] private FurnitureFinishDefinition finish;

            public string ZoneId => zoneId;
            public FurnitureFinishDefinition Finish => finish;

            public ZoneFinishBinding(string id, FurnitureFinishDefinition definition)
            {
                zoneId = id ?? string.Empty;
                finish = definition;
            }
        }

        [Serializable]
        public sealed class VariantDefinition
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private ZoneFinishBinding[] bindings = Array.Empty<ZoneFinishBinding>();

            public string Id => id;
            public string DisplayName => displayName;
            public IReadOnlyList<ZoneFinishBinding> Bindings => bindings;

            public VariantDefinition(string variantId, string visibleName, ZoneFinishBinding[] zoneBindings)
            {
                id = variantId ?? string.Empty;
                displayName = visibleName ?? string.Empty;
                bindings = zoneBindings ?? Array.Empty<ZoneFinishBinding>();
            }

            public FurnitureFinishDefinition FindFinish(string zoneId)
            {
                foreach (var binding in bindings ?? Array.Empty<ZoneFinishBinding>())
                {
                    if (binding != null && string.Equals(binding.ZoneId, zoneId, StringComparison.Ordinal))
                        return binding.Finish;
                }
                return null;
            }
        }
    }
}