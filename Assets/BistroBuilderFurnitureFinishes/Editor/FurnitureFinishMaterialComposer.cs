using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishMaterialComposer
    {
        private const string GeneratedRoot = "Assets/Generated/FurnitureFinishes/AutoDrafts";

        public static FurnitureFinishDefinition Compose(
            FurnitureFinishProfile profile,
            string variantId,
            string zoneId,
            Material source,
            FurnitureFinishDefinition donor,
            FurnitureFinishChannel missingChannels)
        {
            if (profile == null || source == null || donor == null || donor.Material == null)
                throw new InvalidOperationException("No se puede componer el acabado automático.");

            var folder = $"{GeneratedRoot}/{profile.FurnitureId}";
            FurnitureFinishAssetUtility.EnsureFolder(folder);
            var stem = FurnitureFinishAssetUtility.StableId($"{variantId}_{zoneId}", "auto_finish");
            var materialPath = $"{folder}/{stem}.mat";
            var finishPath = $"{folder}/{stem}_Finish.asset";

            var composed = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (composed == null)
            {
                composed = new Material(source)
                {
                    name = stem + "_MAT",
                    enableInstancing = source.enableInstancing
                };
                AssetDatabase.CreateAsset(composed, materialPath);
            }
            else
            {
                composed.shader = source.shader;
                composed.CopyPropertiesFromMaterial(source);
                composed.enableInstancing = source.enableInstancing;
            }

            foreach (FurnitureFinishChannel channel in new[]
            {
                FurnitureFinishChannel.BaseColorMap,
                FurnitureFinishChannel.NormalMap,
                FurnitureFinishChannel.MaskMap,
                FurnitureFinishChannel.OcclusionMap
            })
            {
                if ((missingChannels & channel) == 0)
                    continue;
                CopyMissingTexture(composed, donor.Material, channel);
            }
            EditorUtility.SetDirty(composed);

            var definition = AssetDatabase.LoadAssetAtPath<FurnitureFinishDefinition>(finishPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FurnitureFinishDefinition>();
                AssetDatabase.CreateAsset(definition, finishPath);
            }
            definition.EditorConfigure(
                "auto_" + stem,
                $"Auto · {profile.DisplayName} · {zoneId}",
                donor.Family,
                composed,
                FurnitureFinishAssetUtility.DetectProvidedChannels(composed),
                new[] { "auto_generated", "missing_only" });
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static void CopyMissingTexture(
            Material target,
            Material donor,
            FurnitureFinishChannel channel)
        {
            if (FurnitureFinishAssetUtility.GetTexture(target, channel) != null)
                return;

            var donorTexture = FurnitureFinishAssetUtility.GetTexture(donor, channel);
            if (donorTexture == null)
                return;

            foreach (var property in FurnitureFinishAssetUtility.Properties(channel))
            {
                if (!target.HasProperty(property))
                    continue;
                target.SetTexture(property, donorTexture);

                foreach (var donorProperty in FurnitureFinishAssetUtility.Properties(channel))
                {
                    if (!donor.HasProperty(donorProperty))
                        continue;
                    target.SetTextureScale(property, donor.GetTextureScale(donorProperty));
                    target.SetTextureOffset(property, donor.GetTextureOffset(donorProperty));
                    break;
                }
                break;
            }

            if (channel == FurnitureFinishChannel.NormalMap)
                target.EnableKeyword("_NORMALMAP");
            if (channel == FurnitureFinishChannel.OcclusionMap)
                target.EnableKeyword("_OCCLUSIONMAP");
        }
    }
}