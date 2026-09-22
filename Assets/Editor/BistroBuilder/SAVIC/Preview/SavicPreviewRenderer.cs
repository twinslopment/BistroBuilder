using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicPreviewGenerationResult
    {
        internal SavicPreviewGenerationResult(
            bool succeeded,
            string largePreviewAssetPath,
            string catalogPreviewAssetPath,
            Sprite largePreview,
            Sprite catalogPreview,
            string message)
        {
            Succeeded = succeeded;
            LargePreviewAssetPath = largePreviewAssetPath ?? string.Empty;
            CatalogPreviewAssetPath = catalogPreviewAssetPath ?? string.Empty;
            LargePreview = largePreview;
            CatalogPreview = catalogPreview;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string LargePreviewAssetPath { get; }
        internal string CatalogPreviewAssetPath { get; }
        internal Sprite LargePreview { get; }
        internal Sprite CatalogPreview { get; }
        internal string Message { get; }
    }

    internal static class SavicPreviewRenderer
    {
        internal const string Version = "1.0.0";

        private const int LargeWidth = 768;
        private const int LargeHeight = 576;
        private const int CatalogWidth = 384;
        private const int CatalogHeight = 384;

        private const float FieldOfView = 32f;
        private const float LargeMargin = 0.115f;
        private const float CatalogMargin = 0.075f;
        private const int MaximumFramingIterations = 16;

        private static readonly Color BackgroundColor =
            new Color(0.93f, 0.925f, 0.91f, 1f);

        private static readonly Color FloorColor =
            new Color(0.82f, 0.815f, 0.80f, 1f);

        internal static string BuildInputFingerprint(
            string prefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(prefabAssetPath) ||
                AssetDatabase.LoadMainAssetAtPath(prefabAssetPath) == null)
            {
                throw new InvalidOperationException(
                    "Cannot fingerprint missing preview prefab: " +
                    prefabAssetPath);
            }

            string dependencyHash =
                AssetDatabase
                    .GetAssetDependencyHash(prefabAssetPath)
                    .ToString();

            string canonical =
                string.Join(
                    "|",
                    new[]
                    {
                        "savic.preview",
                        Version,
                        prefabAssetPath,
                        dependencyHash,
                        LargeWidth.ToString(),
                        LargeHeight.ToString(),
                        CatalogWidth.ToString(),
                        CatalogHeight.ToString(),
                        FieldOfView.ToString(
                            "R",
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                        LargeMargin.ToString(
                            "R",
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                        CatalogMargin.ToString(
                            "R",
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                        "single-sample",
                        "table-camera-v1",
                        "floor-v1",
                        "lighting-v1"
                    });

            return SavicHashService.ComputeSha256Text(
                canonical);
        }

        internal static SavicPreviewGenerationResult ReuseAndAssign(
            RestaurantPlaceableItemDefinition item,
            string largePreviewAssetPath,
            string catalogPreviewAssetPath)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            Sprite largeSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    largePreviewAssetPath);

            Sprite catalogSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    catalogPreviewAssetPath);

            if (largeSprite == null ||
                catalogSprite == null)
            {
                return Failure(
                    largePreviewAssetPath,
                    catalogPreviewAssetPath,
                    "Previously generated preview sprites are missing.");
            }

            ApplyPreviewReferences(
                item,
                largeSprite,
                catalogSprite,
                largePreviewAssetPath,
                catalogPreviewAssetPath);

            StampPreviewAsset(largeSprite);
            StampPreviewAsset(catalogSprite);

            return new SavicPreviewGenerationResult(
                true,
                largePreviewAssetPath,
                catalogPreviewAssetPath,
                largeSprite,
                catalogSprite,
                "Reused unchanged validated SAVIC previews.");
        }

        internal static SavicPreviewGenerationResult GenerateAndAssign(
            GameObject prefabAsset,
            RestaurantPlaceableItemDefinition item,
            string contentFolderAssetPath)
        {
            if (prefabAsset == null)
                throw new ArgumentNullException(nameof(prefabAsset));
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(contentFolderAssetPath))
                throw new ArgumentException(
                    "Content folder is required.",
                    nameof(contentFolderAssetPath));

            string largePath =
                contentFolderAssetPath.TrimEnd('/') +
                "/Preview_Large.png";

            string catalogPath =
                contentFolderAssetPath.TrimEnd('/') +
                "/Preview_Catalog.png";

            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            // NewPreviewScene intentionally receives a private culling mask.
            // Manual Camera.Render uses Game-view culling semantics, so the
            // temporary scene must explicitly opt into the default render mask.
            EditorSceneManager.SetSceneCullingMask(
                previewScene,
                SceneCullingMasks.DefaultSceneCullingMask);

            GameObject instance = null;
            GameObject cameraObject = null;
            GameObject keyLightObject = null;
            GameObject fillLightObject = null;
            GameObject floorObject = null;
            Material floorMaterial = null;

            try
            {
                instance =
                    PrefabUtility.InstantiatePrefab(
                        prefabAsset,
                        previewScene) as GameObject;

                if (instance == null)
                {
                    return Failure(
                        largePath,
                        catalogPath,
                        "Preview prefab could not be instantiated.");
                }

                DisableBehaviourExecution(instance);

                if (!TryCalculateRendererBounds(
                        instance,
                        out Bounds bounds,
                        out int rendererCount))
                {
                    return Failure(
                        largePath,
                        catalogPath,
                        "Preview source has no enabled renderer bounds.");
                }

                cameraObject =
                    new GameObject(
                        "SAVIC Preview Camera",
                        typeof(Camera));

                SceneManager.MoveGameObjectToScene(
                    cameraObject,
                    previewScene);

                Camera camera =
                    cameraObject.GetComponent<Camera>();

                ConfigureCamera(camera);

                keyLightObject =
                    CreateDirectionalLight(
                        previewScene,
                        "SAVIC Key Light",
                        new Vector3(48f, -32f, 0f),
                        1.15f,
                        true);

                fillLightObject =
                    CreateDirectionalLight(
                        previewScene,
                        "SAVIC Fill Light",
                        new Vector3(28f, 142f, 0f),
                        0.38f,
                        false);

                floorMaterial =
                    CreateFloorMaterial();

                floorObject =
                    CreateFloor(
                        previewScene,
                        bounds,
                        floorMaterial);

                RenderPreviewToPng(
                    camera,
                    bounds,
                    LargeWidth,
                    LargeHeight,
                    LargeMargin,
                    largePath);

                RenderPreviewToPng(
                    camera,
                    bounds,
                    CatalogWidth,
                    CatalogHeight,
                    CatalogMargin,
                    catalogPath);

                Sprite largeSprite =
                    ImportPreviewAsSprite(
                        largePath,
                        LargeWidth);

                Sprite catalogSprite =
                    ImportPreviewAsSprite(
                        catalogPath,
                        CatalogWidth);

                if (largeSprite == null ||
                    catalogSprite == null)
                {
                    return Failure(
                        largePath,
                        catalogPath,
                        "Generated preview PNGs could not be imported as sprites.");
                }

                ApplyPreviewReferences(
                    item,
                    largeSprite,
                    catalogSprite,
                    largePath,
                    catalogPath);

                StampPreviewAsset(largeSprite);
                StampPreviewAsset(catalogSprite);

                return new SavicPreviewGenerationResult(
                    true,
                    largePath,
                    catalogPath,
                    largeSprite,
                    catalogSprite,
                    "Generated and assigned two validated SAVIC previews from " +
                    rendererCount +
                    " renderer(s).");
            }
            catch (Exception exception)
            {
                return Failure(
                    largePath,
                    catalogPath,
                    exception.Message);
            }
            finally
            {
                if (floorMaterial != null)
                    Object.DestroyImmediate(floorMaterial);

                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(
                        previewScene);
                }
            }
        }
        private static void ConfigureCamera(Camera camera)
        {
            camera.enabled = false;
            camera.clearFlags =
                CameraClearFlags.SolidColor;
            camera.backgroundColor =
                BackgroundColor;
            camera.fieldOfView =
                FieldOfView;
            camera.nearClipPlane =
                0.01f;
            camera.farClipPlane =
                100f;
            camera.allowHDR = true;
            // Explicit single-sample target: URP can emit invalid resolve
            // passes for manual preview-camera renders into bindMS targets.
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.cameraType =
                CameraType.Preview;
        }

        private static GameObject CreateDirectionalLight(
            Scene scene,
            string name,
            Vector3 eulerAngles,
            float intensity,
            bool castShadows)
        {
            GameObject lightObject =
                new GameObject(
                    name,
                    typeof(Light));

            SceneManager.MoveGameObjectToScene(
                lightObject,
                scene);

            Light light =
                lightObject.GetComponent<Light>();

            light.type =
                LightType.Directional;
            light.intensity =
                intensity;
            light.color =
                new Color(1f, 0.965f, 0.92f, 1f);
            light.shadows =
                castShadows
                    ? LightShadows.Soft
                    : LightShadows.None;

            lightObject.transform.rotation =
                Quaternion.Euler(eulerAngles);

            return lightObject;
        }

        private static Material CreateFloorMaterial()
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No supported Lit shader is available for SAVIC previews.");
            }

            Material material =
                new Material(shader)
                {
                    name = "SAVIC Preview Floor",
                    hideFlags = HideFlags.HideAndDontSave
                };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    FloorColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    FloorColor);
            }

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.08f);

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);

            return material;
        }

        private static GameObject CreateFloor(
            Scene scene,
            Bounds bounds,
            Material material)
        {
            GameObject floor =
                GameObject.CreatePrimitive(
                    PrimitiveType.Plane);

            floor.name =
                "SAVIC Preview Floor";

            SceneManager.MoveGameObjectToScene(
                floor,
                scene);

            Collider collider =
                floor.GetComponent<Collider>();

            if (collider != null)
                Object.DestroyImmediate(collider);

            MeshRenderer renderer =
                floor.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    material;
                renderer.receiveShadows =
                    true;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering
                        .ShadowCastingMode.Off;
            }

            float floorDiameter =
                Mathf.Max(
                    4f,
                    Mathf.Max(
                        bounds.size.x,
                        bounds.size.z) * 4f);

            floor.transform.position =
                new Vector3(
                    bounds.center.x,
                    bounds.min.y - 0.004f,
                    bounds.center.z);

            floor.transform.localScale =
                new Vector3(
                    floorDiameter / 10f,
                    1f,
                    floorDiameter / 10f);

            return floor;
        }

        private static void DisableBehaviourExecution(
            GameObject root)
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren
                    <MonoBehaviour>(true);

            for (int index = 0;
                 index < behaviours.Length;
                 index++)
            {
                MonoBehaviour behaviour =
                    behaviours[index];

                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        private static bool TryCalculateRendererBounds(
            GameObject root,
            out Bounds bounds,
            out int rendererCount)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren
                    <Renderer>(true);

            bounds =
                new Bounds();
            rendererCount = 0;

            bool initialized = false;

            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer =
                    renderers[index];

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds candidate =
                    renderer.bounds;

                if (!IsFinite(candidate.center) ||
                    !IsFinite(candidate.size) ||
                    candidate.size.sqrMagnitude <=
                        0.0000001f)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = candidate;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(
                        candidate);
                }

                rendererCount++;
            }

            return initialized &&
                   rendererCount > 0;
        }
        private static void RenderPreviewToPng(
            Camera camera,
            Bounds bounds,
            int width,
            int height,
            float margin,
            string assetPath)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(width));

            camera.aspect =
                width / (float)height;

            ConfigureCameraPose(
                camera,
                bounds,
                margin);

            if (!ProjectedBoundsAreSafe(
                    camera,
                    bounds,
                    margin))
            {
                throw new InvalidOperationException(
                    "SAVIC could not frame the preview safely.");
            }

            RenderTexture renderTexture =
                new RenderTexture(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    name =
                        "SAVIC Preview " +
                        width +
                        "x" +
                        height,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    hideFlags = HideFlags.HideAndDontSave
                };

            Texture2D image = null;
            RenderTexture previous =
                RenderTexture.active;

            try
            {
                renderTexture.Create();
                camera.targetTexture =
                    renderTexture;

                camera.Render();

                RenderTexture.active =
                    renderTexture;

                image =
                    new Texture2D(
                        width,
                        height,
                        TextureFormat.RGBA32,
                        false,
                        false)
                    {
                        hideFlags =
                            HideFlags.HideAndDontSave
                    };

                image.ReadPixels(
                    new Rect(
                        0f,
                        0f,
                        width,
                        height),
                    0,
                    0,
                    false);

                image.Apply(
                    false,
                    false);

                ValidateRenderedImage(
                    image,
                    assetPath);

                byte[] png =
                    image.EncodeToPNG();

                if (png == null ||
                    png.Length < 2048)
                {
                    throw new InvalidOperationException(
                        "Rendered preview PNG is unexpectedly small.");
                }

                WriteBytesIfChanged(
                    assetPath,
                    png);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active =
                    previous;

                if (image != null)
                    Object.DestroyImmediate(image);

                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureCameraPose(
            Camera camera,
            Bounds bounds,
            float margin)
        {
            Vector3 target =
                new Vector3(
                    bounds.center.x,
                    bounds.min.y +
                    bounds.size.y * 0.46f,
                    bounds.center.z);

            Vector3 cameraFromTarget =
                new Vector3(
                    1.25f,
                    0.86f,
                    -1.35f).normalized;

            float radius =
                Mathf.Max(
                    0.25f,
                    bounds.extents.magnitude);

            float tangent =
                Mathf.Tan(
                    camera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad);

            float distance =
                Mathf.Max(
                    0.6f,
                    radius /
                    Mathf.Max(
                        0.05f,
                        tangent));

            camera.transform.rotation =
                Quaternion.LookRotation(
                    -cameraFromTarget,
                    Vector3.up);

            for (int iteration = 0;
                 iteration < MaximumFramingIterations;
                 iteration++)
            {
                camera.transform.position =
                    target +
                    cameraFromTarget *
                    distance;

                if (ProjectedBoundsAreSafe(
                        camera,
                        bounds,
                        margin))
                {
                    break;
                }

                distance *= 1.10f;
            }

            camera.farClipPlane =
                Mathf.Max(
                    20f,
                    distance +
                    radius * 4f);
        }

        private static bool ProjectedBoundsAreSafe(
            Camera camera,
            Bounds bounds,
            float margin)
        {
            Vector3 min =
                bounds.min;
            Vector3 max =
                bounds.max;

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            float minimum =
                Mathf.Clamp(
                    margin,
                    0.025f,
                    0.30f);

            float maximum =
                1f - minimum;

            for (int index = 0;
                 index < corners.Length;
                 index++)
            {
                Vector3 viewport =
                    camera.WorldToViewportPoint(
                        corners[index]);

                if (viewport.z <=
                        camera.nearClipPlane ||
                    viewport.x < minimum ||
                    viewport.x > maximum ||
                    viewport.y < minimum ||
                    viewport.y > maximum)
                {
                    return false;
                }
            }

            return true;
        }
        private static void ValidateRenderedImage(
            Texture2D image,
            string assetPath)
        {
            Color32[] pixels =
                image.GetPixels32();

            if (pixels == null ||
                pixels.Length == 0)
            {
                throw new InvalidOperationException(
                    "Rendered preview contains no pixels: " +
                    assetPath);
            }

            int stride =
                Mathf.Max(
                    1,
                    pixels.Length / 12000);

            double luminanceSum = 0d;
            double luminanceSquaredSum = 0d;
            int sampleCount = 0;

            for (int index = 0;
                 index < pixels.Length;
                 index += stride)
            {
                Color32 pixel =
                    pixels[index];

                double luminance =
                    pixel.r * 0.2126d +
                    pixel.g * 0.7152d +
                    pixel.b * 0.0722d;

                luminanceSum +=
                    luminance;

                luminanceSquaredSum +=
                    luminance *
                    luminance;

                sampleCount++;
            }

            if (sampleCount <= 0)
            {
                throw new InvalidOperationException(
                    "Rendered preview sampling failed.");
            }

            double mean =
                luminanceSum /
                sampleCount;

            double variance =
                luminanceSquaredSum /
                sampleCount -
                mean *
                mean;

            if (double.IsNaN(variance) ||
                double.IsInfinity(variance) ||
                variance < 10d)
            {
                throw new InvalidOperationException(
                    "Rendered preview appears blank or visually degenerate.");
            }
        }

        private static Sprite ImportPreviewAsSprite(
            string assetPath,
            int maximumSize)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(
                    assetPath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Preview texture importer is unavailable: " +
                    assetPath);
            }

            importer.textureType =
                TextureImporterType.Sprite;
            importer.spriteImportMode =
                SpriteImportMode.Single;
            importer.mipmapEnabled =
                false;
            importer.wrapMode =
                TextureWrapMode.Clamp;
            importer.filterMode =
                FilterMode.Bilinear;
            importer.sRGBTexture =
                true;
            importer.alphaIsTransparency =
                false;
            importer.npotScale =
                TextureImporterNPOTScale.None;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.maxTextureSize =
                Mathf.NextPowerOfTwo(
                    Mathf.Clamp(
                        maximumSize,
                        32,
                        2048));
            importer.spritePixelsPerUnit =
                100f;

            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath
                <Sprite>(assetPath);
        }

        private static void ApplyPreviewReferences(
            RestaurantPlaceableItemDefinition item,
            Sprite largePreview,
            Sprite catalogPreview,
            string largePath,
            string catalogPath)
        {
            SerializedObject serialized =
                new SerializedObject(item);

            SerializedProperty inspector =
                RequireProperty(
                    serialized,
                    "inspectorPreview");

            SerializedProperty catalog =
                RequireProperty(
                    serialized,
                    "catalogIcon");

            if (CanReplaceManagedPreview(
                    inspector.objectReferenceValue as Sprite,
                    largePath))
            {
                inspector.objectReferenceValue =
                    largePreview;
            }

            if (CanReplaceManagedPreview(
                    catalog.objectReferenceValue as Sprite,
                    catalogPath))
            {
                catalog.objectReferenceValue =
                    catalogPreview;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static bool CanReplaceManagedPreview(
            Sprite current,
            string expectedPath)
        {
            if (current == null)
                return true;

            string currentPath =
                AssetDatabase.GetAssetPath(
                    current);

            if (string.Equals(
                    currentPath,
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(
                    currentPath))
            {
                return false;
            }

            string[] labels =
                AssetDatabase.GetLabels(
                    current);

            bool managed =
                Array.IndexOf(
                    labels,
                    "SAVIC.Managed") >= 0 &&
                Array.IndexOf(
                    labels,
                    "SAVIC.Preview") >= 0;

            return managed &&
                   currentPath.StartsWith(
                       "Assets/Generated/BistroBuilder/SAVIC/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void StampPreviewAsset(
            Sprite sprite)
        {
            if (sprite == null)
                return;

            HashSet<string> labels =
                new HashSet<string>(
                    AssetDatabase.GetLabels(sprite),
                    StringComparer.Ordinal)
                {
                    "SAVIC.Managed",
                    "SAVIC.Preview"
                };

            string[] values =
                new string[labels.Count];

            labels.CopyTo(values);
            Array.Sort(
                values,
                StringComparer.Ordinal);

            AssetDatabase.SetLabels(
                sprite,
                values);
        }
        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name +
                    " no longer exposes serialized property '" +
                    propertyName +
                    "'. SAVIC preview integration requires migration.");
            }

            return property;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static void WriteBytesIfChanged(
            string assetPath,
            byte[] bytes)
        {
            string absolutePath =
                ToAbsoluteProjectPath(
                    assetPath);

            string directory =
                Path.GetDirectoryName(
                    absolutePath);

            if (string.IsNullOrWhiteSpace(
                    directory))
            {
                throw new InvalidOperationException(
                    "Preview output directory could not be resolved.");
            }

            Directory.CreateDirectory(
                directory);

            if (File.Exists(
                    absolutePath))
            {
                byte[] existing =
                    File.ReadAllBytes(
                        absolutePath);

                if (ByteArraysEqual(
                        existing,
                        bytes))
                {
                    return;
                }
            }

            string temporaryPath =
                absolutePath +
                ".tmp." +
                Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllBytes(
                    temporaryPath,
                    bytes);

                if (File.Exists(
                        absolutePath))
                {
                    File.Copy(
                        temporaryPath,
                        absolutePath,
                        true);

                    File.Delete(
                        temporaryPath);
                }
                else
                {
                    File.Move(
                        temporaryPath,
                        absolutePath);
                }
            }
            finally
            {
                if (File.Exists(
                        temporaryPath))
                {
                    File.Delete(
                        temporaryPath);
                }
            }
        }

        private static bool ByteArraysEqual(
            byte[] first,
            byte[] second)
        {
            if (ReferenceEquals(first, second))
                return true;

            if (first == null ||
                second == null ||
                first.Length != second.Length)
            {
                return false;
            }

            for (int index = 0;
                 index < first.Length;
                 index++)
            {
                if (first[index] !=
                    second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(
                    assetPath) ||
                !assetPath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Preview path must be a project Assets path.");
            }

            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static SavicPreviewGenerationResult Failure(
            string largePath,
            string catalogPath,
            string message)
        {
            return new SavicPreviewGenerationResult(
                false,
                largePath,
                catalogPath,
                null,
                null,
                message);
        }
    }
}
