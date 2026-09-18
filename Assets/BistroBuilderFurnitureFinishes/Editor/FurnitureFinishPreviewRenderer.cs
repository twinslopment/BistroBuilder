using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal sealed class FurnitureFinishPreviewRenderer : IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject instance;
        private FurnitureFinishProfile profile;
        private string variantId = string.Empty;
        private Vector2 orbit = new Vector2(135f, 20f);
        private float distance = 2.5f;
        private Bounds bounds;
        private bool hasBounds;

        public void Set(FurnitureFinishProfile value, string selectedVariantId)
        {
            if (profile == value && string.Equals(variantId, selectedVariantId, StringComparison.Ordinal))
                return;

            profile = value;
            variantId = selectedVariantId ?? string.Empty;
            Rebuild();
        }

        public void Draw(Rect rect)
        {
            if (profile == null || profile.SourceAsset == null)
            {
                EditorGUI.HelpBox(rect, "Selecciona un perfil para previsualizar.", MessageType.Info);
                return;
            }

            EnsurePreview();
            HandleInput(rect);
            if (instance == null || !hasBounds)
            {
                EditorGUI.HelpBox(rect, "No se pudo construir la preview 3D.", MessageType.Warning);
                return;
            }

            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            var center = bounds.center;
            var radius = Mathf.Max(0.15f, bounds.extents.magnitude);
            var cameraDistance = Mathf.Max(radius * distance, 0.5f);
            preview.camera.transform.position = center - rotation * Vector3.forward * cameraDistance;
            preview.camera.transform.rotation = rotation;
            preview.camera.nearClipPlane = Mathf.Max(0.01f, cameraDistance - radius * 2.5f);
            preview.camera.farClipPlane = cameraDistance + radius * 4f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.16f, 0.17f, 0.18f, 1f);

            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.Render();
            var texture = preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 20f),
                string.IsNullOrWhiteSpace(variantId) ? "Original" : variantId,
                EditorStyles.whiteMiniLabel);
        }

        public void ResetCamera()
        {
            orbit = new Vector2(135f, 20f);
            distance = 2.5f;
        }

        public void Refresh()
        {
            Rebuild();
        }

        public void Dispose()
        {
            DestroyInstance();
            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }
        }

        private void EnsurePreview()
        {
            EnsureUtility();
            if (instance == null)
                Rebuild();
        }

        private void EnsureUtility()
        {
            if (preview != null)
                return;

            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 30f;
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        private void Rebuild()
        {
            DestroyInstance();
            if (profile == null || profile.SourceAsset == null)
                return;

            EnsureUtility();
            instance = UnityEngine.Object.Instantiate(profile.SourceAsset);
            instance.name = profile.SourceAsset.name + "_BBFFVAS_PREVIEW";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ApplyVariant(instance);
            preview.AddSingleGO(instance);
            CalculateBounds(instance);
        }

        private void ApplyVariant(GameObject root)
        {
            var variant = profile.FindVariant(variantId);
            if (variant == null)
                return;

            foreach (var zone in profile.Zones)
            {
                if (zone == null)
                    continue;
                var finish = variant.FindFinish(zone.Id);
                if (finish == null || finish.Material == null)
                    continue;

                foreach (var slot in zone.Slots)
                {
                    var renderer = FurnitureFinishAssetUtility.FindRenderer(root, slot.RendererPath);
                    if (renderer == null)
                        continue;
                    var materials = renderer.sharedMaterials;
                    if (slot.MaterialIndex < 0 || slot.MaterialIndex >= materials.Length)
                        continue;
                    materials[slot.MaterialIndex] = finish.Material;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            hasBounds = renderers.Length > 0;
            if (!hasBounds)
                return;
            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
        }

        private void HandleInput(Rect rect)
        {
            var current = Event.current;
            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.MouseDrag && current.button == 0)
            {
                orbit.x += current.delta.x * 0.6f;
                orbit.y = Mathf.Clamp(orbit.y - current.delta.y * 0.5f, -80f, 80f);
                current.Use();
            }
            else if (current.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + current.delta.y * 0.12f, 1.2f, 8f);
                current.Use();
            }
        }

        private void DestroyInstance()
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                instance = null;
            }
            hasBounds = false;
        }
    }
}