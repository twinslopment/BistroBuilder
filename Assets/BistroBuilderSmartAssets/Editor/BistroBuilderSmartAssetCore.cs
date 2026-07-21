using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    [Serializable]
    internal sealed class BistroBuilderSmartAssetManifest
    {
        public string schemaVersion = string.Empty;
        public string assetId = string.Empty;
        public string displayName = string.Empty;
        public string category = string.Empty;
        public string assetType = string.Empty;
        public SourceData source = new SourceData();
        public DimensionData dimensions = new DimensionData();
        public FunctionalData functionalMeasurements = new FunctionalData();
        public OrientationData orientation = new OrientationData();
        public GeometryData geometry = new GeometryData();
        public VariantData[] variants = Array.Empty<VariantData>();

        [Serializable]
        internal sealed class SourceData
        {
            public string mode = string.Empty;
            public string prompt = string.Empty;
            public string referenceImage = string.Empty;
            public string notes = string.Empty;
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
            public float toleranceMm = 1f;
        }

        [Serializable]
        internal sealed class FunctionalData
        {
            public float seatHeightM;
            public float worktopHeightM;
            public float doorClearanceM;
        }

        [Serializable]
        internal sealed class OrientationData
        {
            public string blenderFront = "-Y";
            public string unityFront = "+Z";
            public string groundAxis = "Z";
            public string origin = "FOOTPRINT_CENTER_GROUND";
        }

        [Serializable]
        internal sealed class GeometryData
        {
            public int meshCount;
            public int triangleCount;
            public string[] materialNames = Array.Empty<string>();
            public bool uv0;
            public string topologyHash = string.Empty;
        }

        [Serializable]
        internal sealed class VariantData
        {
            public string id = string.Empty;
            public string displayName = string.Empty;

            // El manifiesto de Blender serializa el color como [r, g, b, a].
            // JsonUtility sí puede leer arrays de float, pero no convertirlos
            // automáticamente a UnityEngine.Color.
            public float[] baseColor = { 1f, 1f, 1f, 1f };

            public float metallic;
            public float roughness = 0.5f;
            public float priceMultiplier = 1f;

            public Color GetBaseColor()
            {
                if (baseColor == null || baseColor.Length < 3)
                {
                    return Color.white;
                }

                var alpha = baseColor.Length >= 4 ? baseColor[3] : 1f;
                return new Color(
                    Mathf.Clamp01(baseColor[0]),
                    Mathf.Clamp01(baseColor[1]),
                    Mathf.Clamp01(baseColor[2]),
                    Mathf.Clamp01(alpha));
            }
        }

        public static bool TryLoad(
            string manifestPath,
            out BistroBuilderSmartAssetManifest manifest)
        {
            manifest = null;

            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
            if (text == null)
            {
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<BistroBuilderSmartAssetManifest>(text.text);
                return manifest != null
                    && !string.IsNullOrWhiteSpace(manifest.assetId);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Smart Assets] No se pudo leer '{manifestPath}': {exception.Message}");
                return false;
            }
        }
    }

    internal static class BistroBuilderSmartAssetPaths
    {
        private const string Marker = "/Art/Blender/Placeables/";

        public static bool IsManagedModel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Replace('\\', '/');
            return normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf(
                    Marker,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ManifestPath(string modelPath)
        {
            var folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            return $"{folder}/{Path.GetFileNameWithoutExtension(modelPath)}.bbasset.json";
        }

        public static string ContainerPath(string modelPath)
        {
            var models = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            return Path.GetDirectoryName(models)?.Replace('\\', '/') ?? "Assets";
        }

        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');

            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];

            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }

    internal enum SmartAssetSeverity
    {
        Information,
        Warning,
        Error
    }

    internal readonly struct SmartAssetMessage
    {
        public SmartAssetSeverity Severity { get; }
        public string Text { get; }

        public SmartAssetMessage(
            SmartAssetSeverity severity,
            string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    internal static class BistroBuilderSmartAssetValidator
    {
        public static IReadOnlyList<SmartAssetMessage> Validate(
            GameObject model,
            BistroBuilderSmartAssetManifest manifest)
        {
            var result = new List<SmartAssetMessage>();

            if (model == null)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    "No hay un FBX válido seleccionado."));
                return result;
            }

            if (manifest == null)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    "Falta el manifiesto .bbasset.json."));
                return result;
            }

            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var colliders = model.GetComponentsInChildren<Collider>(true);
            var animators = model.GetComponentsInChildren<Animator>(true);

            result.Add(filters.Length == manifest.geometry.meshCount
                ? new SmartAssetMessage(
                    SmartAssetSeverity.Information,
                    $"Mallas correctas: {filters.Length}.")
                : new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    $"Mallas: {filters.Length}; manifiesto: {manifest.geometry.meshCount}."));

            if (renderers.Length != filters.Length)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Warning,
                    $"MeshRenderers: {renderers.Length}; MeshFilters: {filters.Length}."));
            }

            if (colliders.Length > 0)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    "El asset visual contiene colliders; debe generarlos la fábrica universal."));
            }

            if (animators.Length > 0)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    "El asset estático contiene Animator."));
            }

            if (!TryBounds(model.transform, filters, out var bounds))
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    "No se pudieron calcular los bounds."));
                return result;
            }

            var tolerance = Mathf.Max(
                0.00001f,
                manifest.dimensions.toleranceMm / 1000f);

            Dimension(
                result,
                "Anchura",
                bounds.size.x,
                manifest.dimensions.targetWidthM,
                tolerance);

            Dimension(
                result,
                "Profundidad",
                bounds.size.z,
                manifest.dimensions.targetDepthM,
                tolerance);

            Dimension(
                result,
                "Altura",
                bounds.size.y,
                manifest.dimensions.targetHeightM,
                tolerance);

            result.Add(Mathf.Abs(bounds.min.y) <= tolerance
                ? new SmartAssetMessage(
                    SmartAssetSeverity.Information,
                    "La base local está en Y=0.")
                : new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    $"La base local está en Y={bounds.min.y:F6}."));

            if ((model.transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                result.Add(new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    $"Escala raíz incorrecta: {model.transform.localScale}."));
            }

            return result;
        }

        public static bool HasErrors(
            IReadOnlyList<SmartAssetMessage> messages)
        {
            foreach (var message in messages)
            {
                if (message.Severity == SmartAssetSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Dimension(
            List<SmartAssetMessage> result,
            string label,
            float actual,
            float expected,
            float tolerance)
        {
            var difference = Mathf.Abs(actual - expected);

            result.Add(difference <= tolerance
                ? new SmartAssetMessage(
                    SmartAssetSeverity.Information,
                    $"{label}: {actual:F4} m.")
                : new SmartAssetMessage(
                    SmartAssetSeverity.Error,
                    $"{label}: {actual:F4} m; objetivo {expected:F4} m; " +
                    $"diferencia {difference * 1000f:F2} mm."));
        }

        private static bool TryBounds(
            Transform root,
            IReadOnlyList<MeshFilter> filters,
            out Bounds bounds)
        {
            bounds = default;
            var initialized = false;

            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var localToRoot =
                    root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                var source = filter.sharedMesh.bounds;

                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var corner = source.center + Vector3.Scale(
                                source.extents,
                                new Vector3(x, y, z));

                            var point = localToRoot.MultiplyPoint3x4(corner);

                            if (!initialized)
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                bounds.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return initialized;
        }
    }
}
