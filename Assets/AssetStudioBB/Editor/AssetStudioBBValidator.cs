using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.AssetStudioBB.Editor
{
    internal enum AssetStudioBBSeverity
    {
        Information,
        Warning,
        Error,
    }

    internal readonly struct AssetStudioBBValidationMessage
    {
        public AssetStudioBBSeverity Severity { get; }
        public string Text { get; }

        public AssetStudioBBValidationMessage(AssetStudioBBSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    internal static class AssetStudioBBValidator
    {
        public static IReadOnlyList<AssetStudioBBValidationMessage> Validate(
            string manifestPath,
            AssetStudioBBManifest manifest)
        {
            var messages = new List<AssetStudioBBValidationMessage>();
            if (manifest == null)
            {
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, "No hay un manifiesto válido."));
                return messages;
            }

            if (!string.Equals(manifest.orientation.unityFront, "+Z", StringComparison.Ordinal))
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, "El frente Unity debe ser +Z."));
            if (!string.Equals(manifest.orientation.origin, "FOOTPRINT_CENTER_GROUND", StringComparison.Ordinal))
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, "El origen no cumple el contrato de huella."));

            var lod0Path = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, manifest.models.lod0);
            var lod0 = AssetDatabase.LoadAssetAtPath<GameObject>(lod0Path);
            if (lod0 == null)
            {
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, $"No existe LOD0: {lod0Path}"));
                return messages;
            }

            ValidateModel(messages, lod0, manifest);
            ValidateOptionalModel(messages, manifestPath, manifest.models.lod1, "LOD1");
            ValidateOptionalModel(messages, manifestPath, manifest.models.lod2, "LOD2");
            ValidatePresets(messages, manifestPath, manifest);

            var variants = manifest.variants ?? Array.Empty<AssetStudioBBManifest.VariantData>();
            if (variants.Length == 0)
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Warning, "No hay variantes; se generará un acabado predeterminado si el manifiesto lo contiene."));
            else
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Information, $"Variantes preparadas: {variants.Length}."));

            return messages;
        }

        public static bool HasErrors(IReadOnlyList<AssetStudioBBValidationMessage> messages)
        {
            foreach (var message in messages)
            {
                if (message.Severity == AssetStudioBBSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void ValidateOptionalModel(
            ICollection<AssetStudioBBValidationMessage> messages,
            string manifestPath,
            string relativePath,
            string label)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;
            var path = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, relativePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, $"No existe {label}: {path}"));
        }

        private static void ValidateModel(
            ICollection<AssetStudioBBValidationMessage> messages,
            GameObject model,
            AssetStudioBBManifest manifest)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, "LOD0 no contiene renderers."));
                return;
            }
            if (model.GetComponentsInChildren<Collider>(true).Length > 0)
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, "El modelo visual contiene colliders."));

            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    var name = material != null ? material.name : string.Empty;
                    foreach (var role in manifest.geometry.materialRoles ?? Array.Empty<string>())
                    {
                        if (name.StartsWith($"ASBB_role_{role}", StringComparison.Ordinal))
                            roles.Add(role);
                    }
                }
            }
            if (!roles.Contains("surface"))
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Warning, "No se detectó el rol surface en LOD0."));

            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(model) as GameObject ?? UnityEngine.Object.Instantiate(model);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var instanceRenderers = instance.GetComponentsInChildren<Renderer>(true);
                if (instanceRenderers.Length == 0)
                    return;
                var bounds = instanceRenderers[0].bounds;
                for (var index = 1; index < instanceRenderers.Length; index++)
                    bounds.Encapsulate(instanceRenderers[index].bounds);
                var actual = bounds.size;
                var tolerance = Mathf.Max(0.0001f, manifest.dimensions.toleranceMm / 1000f);
                CompareDimension(messages, "anchura", actual.x, manifest.dimensions.targetWidthM, tolerance);
                CompareDimension(messages, "altura", actual.y, manifest.dimensions.targetHeightM, tolerance);
                CompareDimension(messages, "profundidad", actual.z, manifest.dimensions.targetDepthM, tolerance);
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void CompareDimension(
            ICollection<AssetStudioBBValidationMessage> messages,
            string label,
            float actual,
            float target,
            float tolerance)
        {
            var delta = Mathf.Abs(actual - target);
            var severity = delta <= tolerance ? AssetStudioBBSeverity.Information : AssetStudioBBSeverity.Warning;
            messages.Add(new AssetStudioBBValidationMessage(
                severity,
                $"{label}: {actual:F4} m; objetivo {target:F4} m; diferencia {delta * 1000f:F1} mm."));
        }

        private static void ValidatePresets(
            ICollection<AssetStudioBBValidationMessage> messages,
            string manifestPath,
            AssetStudioBBManifest manifest)
        {
            foreach (var preset in manifest.presets ?? Array.Empty<AssetStudioBBManifest.PresetData>())
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.id))
                    continue;
                ValidateTexture(messages, manifestPath, preset.id, "Base Color", preset.textures.baseColor, false);
                ValidateTexture(messages, manifestPath, preset.id, "Normal", preset.textures.normal, false);
                ValidateTexture(messages, manifestPath, preset.id, "Mask", preset.textures.mask, false);
                ValidateTexture(messages, manifestPath, preset.id, "AO", preset.textures.ao, false);
            }
        }

        private static void ValidateTexture(
            ICollection<AssetStudioBBValidationMessage> messages,
            string manifestPath,
            string preset,
            string label,
            string relative,
            bool optional)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                if (!optional)
                    messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Warning, $"{preset}: falta {label}."));
                return;
            }
            var path = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, relative);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                messages.Add(new AssetStudioBBValidationMessage(AssetStudioBBSeverity.Error, $"{preset}: no se encuentra {label} en {path}."));
        }
    }
}
