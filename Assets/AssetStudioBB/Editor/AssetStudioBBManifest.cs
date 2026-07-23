using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.AssetStudioBB.Editor
{
    [Serializable]
    internal sealed class AssetStudioBBManifest
    {
        public string schemaVersion = string.Empty;
        public string pipeline = string.Empty;
        public string generatorVersion = string.Empty;
        public string assetId = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string category = string.Empty;
        public string family = string.Empty;
        public string subtype = string.Empty;
        public string quality = string.Empty;
        public OrientationData orientation = new OrientationData();
        public DimensionData dimensions = new DimensionData();
        public GeometryData geometry = new GeometryData();
        public ModelData models = new ModelData();
        public string preview = string.Empty;
        public PresetData[] presets = Array.Empty<PresetData>();
        public RoleAssignment[] baseRoles = Array.Empty<RoleAssignment>();
        public VariantData[] variants = Array.Empty<VariantData>();
        public SourceData source = new SourceData();

        [Serializable]
        internal sealed class OrientationData
        {
            public string blenderFront = "-Y";
            public string unityFront = "+Z";
            public string groundAxis = "Z";
            public string origin = "FOOTPRINT_CENTER_GROUND";
        }

        [Serializable]
        internal sealed class DimensionData
        {
            public float targetWidthM;
            public float targetDepthM;
            public float targetHeightM;
            public float actualWidthM;
            public float actualDepthM;
            public float actualHeightM;
            public float seatHeightM;
            public float worktopHeightM;
            public float toleranceMm = 2f;
        }

        [Serializable]
        internal sealed class GeometryData
        {
            public int meshCount;
            public int triangleCountSource;
            public string[] materialRoles = Array.Empty<string>();
        }

        [Serializable]
        internal sealed class ModelData
        {
            public string lod0 = string.Empty;
            public string lod1 = string.Empty;
            public string lod2 = string.Empty;
        }

        [Serializable]
        internal sealed class TextureData
        {
            public string baseColor = string.Empty;
            public string normal = string.Empty;
            public string mask = string.Empty;
            public string ao = string.Empty;
        }

        [Serializable]
        internal sealed class PresetData
        {
            public string id = string.Empty;
            public string label = string.Empty;
            public string category = string.Empty;
            public string surfaceType = "opaque";
            public float textureScale = 1f;
            public float normalScale = 1f;
            public float metallic;
            public float roughness = 0.5f;
            public float alpha = 1f;
            public float transmission;
            public float ior = 1.45f;
            public TextureData textures = new TextureData();
        }

        [Serializable]
        internal sealed class RoleAssignment
        {
            public string role = string.Empty;
            public string preset = string.Empty;
            public float[] tint = { 1f, 1f, 1f, 1f };

            public Color TintColor()
            {
                if (tint == null || tint.Length < 3)
                    return Color.white;
                return new Color(
                    Mathf.Clamp01(tint[0]),
                    Mathf.Clamp01(tint[1]),
                    Mathf.Clamp01(tint[2]),
                    tint.Length >= 4 ? Mathf.Clamp01(tint[3]) : 1f);
            }
        }

        [Serializable]
        internal sealed class VariantData
        {
            public string id = string.Empty;
            public string displayName = string.Empty;
            public float priceMultiplier = 1f;
            public RoleAssignment[] roles = Array.Empty<RoleAssignment>();
        }

        [Serializable]
        internal sealed class SourceData
        {
            public string blendFile = string.Empty;
            public string referenceImage = string.Empty;
            public string notes = string.Empty;
        }

        public PresetData FindPreset(string id)
        {
            if (presets == null)
                return null;
            foreach (var preset in presets)
            {
                if (preset != null && string.Equals(preset.id, id, StringComparison.Ordinal))
                    return preset;
            }
            return null;
        }

        public static bool TryLoad(string manifestPath, out AssetStudioBBManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(manifestPath))
                return false;
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
            if (textAsset == null)
                return false;
            try
            {
                manifest = JsonUtility.FromJson<AssetStudioBBManifest>(textAsset.text);
                return manifest != null
                    && string.Equals(manifest.schemaVersion, "3.0", StringComparison.Ordinal)
                    && string.Equals(manifest.pipeline, "AssetStudioBB.Local", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(manifest.assetId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Asset Studio BB] Manifiesto inválido: {exception.Message}");
                return false;
            }
        }
    }
}
