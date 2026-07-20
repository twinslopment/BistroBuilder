using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera miniaturas de catálogo a partir del prefab real de cada
/// artículo colocable.
///
/// Principios de seguridad:
/// - No modifica el prefab.
/// - Renderiza en una Preview Scene aislada.
/// - Conserva iconos manuales salvo petición explícita.
/// - Escribe en una carpeta generada y determinista.
/// - Si falla, restaura el PNG anterior o elimina el archivo parcial.
/// - Asigna el Sprite únicamente después de importarlo y validarlo.
/// </summary>
public static class BistroBuilderCatalogThumbnailService
{
    public const string GeneratedIconFolder =
        "Assets/Generated/BistroBuilder/CatalogIcons";

    public const int DefaultThumbnailSize = 256;

    private const float CameraFieldOfView = 30f;
    private const float MinimumRenderRadius = 0.1f;

    /// <summary>
    /// Perfil interno de render. Se prueban varios perfiles y se
    /// conserva automáticamente el resultado de mayor calidad.
    /// </summary>
    private readonly struct ThumbnailRenderProfile
    {
        public string Name
        {
            get;
        }

        public float CameraPadding
        {
            get;
        }

        public float AmbientIntensity
        {
            get;
        }

        public float KeyIntensity
        {
            get;
        }

        public float FillIntensity
        {
            get;
        }

        public float Exposure
        {
            get;
        }

        public float ShadowLift
        {
            get;
        }

        public Vector3 ViewDirection
        {
            get;
        }

        public ThumbnailRenderProfile(
            string name,
            float cameraPadding,
            float ambientIntensity,
            float keyIntensity,
            float fillIntensity,
            float exposure,
            float shadowLift,
            Vector3 viewDirection
        )
        {
            Name = name;
            CameraPadding = cameraPadding;
            AmbientIntensity = ambientIntensity;
            KeyIntensity = keyIntensity;
            FillIntensity = fillIntensity;
            Exposure = exposure;
            ShadowLift = shadowLift;
            ViewDirection = viewDirection;
        }
    }

    private static readonly ThumbnailRenderProfile[]
        RenderProfiles =
        {
            new ThumbnailRenderProfile(
                "Studio",
                1.15f,
                0.72f,
                1.85f,
                0.95f,
                1.16f,
                0.045f,
                new Vector3(1.05f, 0.72f, -1.05f)
            ),
            new ThumbnailRenderProfile(
                "Bright",
                1.18f,
                0.92f,
                2.25f,
                1.25f,
                1.28f,
                0.075f,
                new Vector3(1.0f, 0.8f, -1.0f)
            ),
            new ThumbnailRenderProfile(
                "Wide",
                1.34f,
                0.82f,
                2.0f,
                1.05f,
                1.2f,
                0.055f,
                new Vector3(1.12f, 0.68f, -1.0f)
            )
        };

    /// <summary>
    /// Resultado de una generación individual.
    /// </summary>
    public readonly struct ThumbnailResult
    {
        public bool Succeeded
        {
            get;
        }

        public bool Changed
        {
            get;
        }

        public string AssetPath
        {
            get;
        }

        public string Message
        {
            get;
        }

        public ThumbnailResult(
            bool succeeded,
            bool changed,
            string assetPath,
            string message
        )
        {
            Succeeded = succeeded;
            Changed = changed;
            AssetPath = assetPath ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Resultado agregado para generación por lotes.
    /// </summary>
    public sealed class ThumbnailBatchResult
    {
        public int GeneratedCount;
        public int PreservedCount;
        public int FailedCount;

        public readonly List<string> Messages =
            new List<string>();

        public string BuildSummary()
        {
            return
                "Generadas: " + GeneratedCount + "\n" +
                "Conservadas: " + PreservedCount + "\n" +
                "Errores: " + FailedCount;
        }
    }

    /// <summary>
    /// Indica si un icono pertenece a la carpeta administrada por
    /// esta herramienta.
    /// </summary>
    public static bool IsGeneratedIcon(
        Sprite sprite
    )
    {
        if (sprite == null)
        {
            return false;
        }

        string path =
            NormalizeUnityAssetPath(
                AssetDatabase.GetAssetPath(sprite)
            );

        return path.StartsWith(
            GeneratedIconFolder + "/",
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Genera y asigna una miniatura para una definición.
    /// Los iconos manuales se preservan por defecto.
    /// </summary>
    public static ThumbnailResult GenerateAndAssign(
        RestaurantPlaceableItemDefinition definition,
        bool overwriteManualIcon = false,
        bool forceRegenerateGeneratedIcon = true,
        int size = DefaultThumbnailSize,
        bool saveAssets = true
    )
    {
        if (definition == null)
        {
            return new ThumbnailResult(
                false,
                false,
                string.Empty,
                "La definición del artículo es nula."
            );
        }

        if (definition.Prefab == null)
        {
            return new ThumbnailResult(
                false,
                false,
                string.Empty,
                definition.DisplayName +
                " no tiene prefab para renderizar."
            );
        }

        Sprite previousIcon =
            definition.CatalogIcon;

        bool previousIconIsGenerated =
            IsGeneratedIcon(previousIcon);

        if (previousIcon != null &&
            !previousIconIsGenerated &&
            !overwriteManualIcon)
        {
            return new ThumbnailResult(
                true,
                false,
                AssetDatabase.GetAssetPath(previousIcon),
                definition.DisplayName +
                ": se conserva el icono manual."
            );
        }

        if (previousIcon != null &&
            previousIconIsGenerated &&
            !forceRegenerateGeneratedIcon)
        {
            return new ThumbnailResult(
                true,
                false,
                AssetDatabase.GetAssetPath(previousIcon),
                definition.DisplayName +
                ": la miniatura generada ya existe."
            );
        }

        int resolvedSize =
            Mathf.Clamp(size, 64, 1024);

        string iconPath =
            BuildIconAssetPath(definition);

        byte[] previousFileBytes = null;
        bool hadPreviousFile = false;

        try
        {
            EnsureUnityAssetFolderExists(
                GeneratedIconFolder
            );

            string absoluteIconPath =
                ConvertUnityAssetPathToAbsolutePath(
                    iconPath
                );

            hadPreviousFile =
                File.Exists(absoluteIconPath);

            if (hadPreviousFile)
            {
                previousFileBytes =
                    File.ReadAllBytes(absoluteIconPath);
            }

            if (!TryRenderPrefabToPng(
                    definition.Prefab.gameObject,
                    resolvedSize,
                    out byte[] pngBytes,
                    out string renderError
                ))
            {
                return new ThumbnailResult(
                    false,
                    false,
                    iconPath,
                    definition.DisplayName +
                    ": " +
                    renderError
                );
            }

            string absoluteFolder =
                GetAbsoluteDirectoryName(
                    absoluteIconPath
                );

            if (!Directory.Exists(absoluteFolder))
            {
                Directory.CreateDirectory(
                    absoluteFolder
                );
            }

            File.WriteAllBytes(
                absoluteIconPath,
                pngBytes
            );

            AssetDatabase.ImportAsset(
                iconPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate
            );

            if (!ConfigureTextureAsCatalogSprite(
                    iconPath,
                    resolvedSize,
                    out string importerError
                ))
            {
                throw new InvalidOperationException(
                    importerError
                );
            }

            Sprite generatedSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    iconPath
                );

            if (generatedSprite == null)
            {
                throw new InvalidOperationException(
                    "Unity importó el PNG, pero no creó un Sprite."
                );
            }

            SerializedObject serializedDefinition =
                new SerializedObject(definition);

            SerializedProperty iconProperty =
                serializedDefinition.FindProperty(
                    "catalogIcon"
                );

            if (iconProperty == null)
            {
                throw new InvalidOperationException(
                    "RestaurantPlaceableItemDefinition no contiene " +
                    "la propiedad catalogIcon."
                );
            }

            iconProperty.objectReferenceValue =
                generatedSprite;

            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(definition);

            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
            }

            BistroBuilderThumbnailQualityReport finalQuality =
                BistroBuilderCatalogThumbnailQualityService
                    .AnalyzeSprite(
                        generatedSprite
                    );

            return new ThumbnailResult(
                true,
                true,
                iconPath,
                definition.DisplayName +
                ": miniatura generada en " +
                iconPath +
                ". " +
                finalQuality.BuildCompactSummary()
            );
        }
        catch (Exception exception)
        {
            RestoreIconFileAfterFailure(
                iconPath,
                hadPreviousFile,
                previousFileBytes
            );

            Debug.LogException(exception);

            return new ThumbnailResult(
                false,
                false,
                iconPath,
                definition.DisplayName +
                ": no se pudo generar la miniatura. " +
                exception.Message
            );
        }
    }

    /// <summary>
    /// Genera miniaturas para una colección de definiciones.
    /// </summary>
    public static ThumbnailBatchResult GenerateBatch(
        IEnumerable<RestaurantPlaceableItemDefinition> definitions,
        bool onlyMissing,
        bool overwriteManualIcons,
        int size = DefaultThumbnailSize
    )
    {
        ThumbnailBatchResult result =
            new ThumbnailBatchResult();

        if (definitions == null)
        {
            result.FailedCount = 1;
            result.Messages.Add(
                "No se recibió ninguna colección de artículos."
            );

            return result;
        }

        List<RestaurantPlaceableItemDefinition> ordered =
            definitions
                .Where(definition => definition != null)
                .Distinct()
                .OrderBy(
                    definition => definition.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase
                )
                .ToList();

        for (int index = 0;
             index < ordered.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                ordered[index];

            if (onlyMissing &&
                definition.CatalogIcon != null)
            {
                result.PreservedCount++;
                result.Messages.Add(
                    definition.DisplayName +
                    ": ya tiene icono."
                );

                continue;
            }

            ThumbnailResult itemResult =
                GenerateAndAssign(
                    definition,
                    overwriteManualIcons,
                    !onlyMissing,
                    size,
                    false
                );

            result.Messages.Add(
                itemResult.Message
            );

            if (!itemResult.Succeeded)
            {
                result.FailedCount++;
            }
            else if (itemResult.Changed)
            {
                result.GeneratedCount++;
            }
            else
            {
                result.PreservedCount++;
            }
        }

        AssetDatabase.SaveAssets();

        return result;
    }

    /// <summary>
    /// Carga todas las definiciones del proyecto de forma estable.
    /// </summary>
    public static List<RestaurantPlaceableItemDefinition>
        LoadAllDefinitions()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableItemDefinition"
            );

        List<RestaurantPlaceableItemDefinition> definitions =
            new List<RestaurantPlaceableItemDefinition>(
                guids.Length
            );

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]
                );

            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            CompareDefinitions
        );

        return definitions;
    }

    private static bool TryRenderPrefabToPng(
        GameObject prefabAsset,
        int size,
        out byte[] pngBytes,
        out string errorMessage
    )
    {
        pngBytes = null;
        errorMessage = string.Empty;

        if (prefabAsset == null)
        {
            errorMessage =
                "el prefab de origen es nulo.";

            return false;
        }

        byte[] bestBytes = null;

        BistroBuilderThumbnailQualityReport bestQuality =
            null;

        string bestProfileName =
            string.Empty;

        List<string> attemptErrors =
            new List<string>();

        for (int index = 0;
             index < RenderProfiles.Length;
             index++)
        {
            ThumbnailRenderProfile profile =
                RenderProfiles[index];

            if (!TryRenderSingleProfile(
                    prefabAsset,
                    size,
                    profile,
                    out byte[] candidateBytes,
                    out string candidateError
                ))
            {
                attemptErrors.Add(
                    profile.Name +
                    ": " +
                    candidateError
                );

                continue;
            }

            BistroBuilderThumbnailQualityReport candidateQuality =
                BistroBuilderCatalogThumbnailQualityService
                    .AnalyzePngBytes(
                        candidateBytes,
                        profile.Name
                    );

            if (bestQuality == null ||
                candidateQuality.Score >
                bestQuality.Score)
            {
                bestBytes =
                    candidateBytes;

                bestQuality =
                    candidateQuality;

                bestProfileName =
                    profile.Name;
            }

            if (candidateQuality.Status ==
                BistroBuilderThumbnailQualityStatus.Good)
            {
                break;
            }
        }

        if (bestBytes == null ||
            bestQuality == null)
        {
            errorMessage =
                attemptErrors.Count > 0
                    ? string.Join(" | ", attemptErrors)
                    : "ningún perfil produjo una imagen válida.";

            return false;
        }

        pngBytes =
            bestBytes;

        errorMessage =
            "Perfil " +
            bestProfileName +
            "; calidad " +
            bestQuality.Score.ToString("0.00") +
            ".";

        return true;
    }

    /// <summary>
    /// Renderiza un único perfil. Todo el estado de PreviewRenderUtility
    /// se crea y destruye dentro del mismo intento para impedir fugas
    /// entre perfiles.
    /// </summary>
    private static bool TryRenderSingleProfile(
        GameObject prefabAsset,
        int size,
        ThumbnailRenderProfile profile,
        out byte[] pngBytes,
        out string errorMessage
    )
    {
        pngBytes = null;
        errorMessage = string.Empty;

        PreviewRenderUtility previewUtility = null;
        GameObject previewRoot = null;
        Texture2D readableTexture = null;
        RenderTexture previousActive = null;
        bool previewWasOpened = false;

        try
        {
            previewUtility =
                new PreviewRenderUtility();

            previewRoot =
                previewUtility.InstantiatePrefabInScene(
                    prefabAsset
                );

            if (previewRoot == null)
            {
                errorMessage =
                    "Unity no pudo instanciar el prefab en la " +
                    "escena de previsualización.";

                return false;
            }

            previewRoot.name =
                "ThumbnailPreview_" +
                prefabAsset.name;

            ResetRootTransformForPreview(
                previewRoot.transform
            );

            if (!TryCalculateRendererBounds(
                    previewRoot,
                    out Bounds bounds
                ))
            {
                errorMessage =
                    "el prefab no contiene ningún Renderer activo.";

                return false;
            }

            ConfigurePreviewUtility(
                previewUtility,
                bounds,
                profile
            );

            Rect previewRect =
                new Rect(
                    0f,
                    0f,
                    size,
                    size
                );

            previewUtility.BeginPreview(
                previewRect,
                GUIStyle.none
            );

            previewWasOpened = true;

            // true permite que Unity utilice el Scriptable Render
            // Pipeline activo, incluido URP.
            previewUtility.Render(
                true,
                true
            );

            Texture renderedTexture =
                previewUtility.EndPreview();

            previewWasOpened = false;

            RenderTexture renderTexture =
                renderedTexture as RenderTexture;

            if (renderTexture == null)
            {
                errorMessage =
                    "PreviewRenderUtility no devolvió un " +
                    "RenderTexture válido.";

                return false;
            }

            previousActive =
                RenderTexture.active;

            RenderTexture.active =
                renderTexture;

            readableTexture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false,
                    false
                );

            readableTexture.ReadPixels(
                new Rect(0f, 0f, size, size),
                0,
                0,
                false
            );

            readableTexture.Apply(
                false,
                false
            );

            ApplyCatalogToneMapping(
                readableTexture,
                profile
            );

            pngBytes =
                readableTexture.EncodeToPNG();

            if (pngBytes == null ||
                pngBytes.Length == 0)
            {
                errorMessage =
                    "Unity no produjo datos PNG.";

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            errorMessage =
                "error de renderizado: " +
                exception.Message;

            return false;
        }
        finally
        {
            RenderTexture.active =
                previousActive;

            if (readableTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    readableTexture
                );
            }

            if (previewUtility != null)
            {
                if (previewWasOpened)
                {
                    try
                    {
                        previewUtility.EndPreview();
                    }
                    catch (Exception endPreviewException)
                    {
                        Debug.LogException(
                            endPreviewException
                        );
                    }
                }

                previewUtility.Cleanup();
            }
        }
    }

    /// <summary>
    /// Ajuste suave de exposición para iconos. Solo afecta a la
    /// textura temporal; no toca materiales ni prefabs.
    /// </summary>
    private static void ApplyCatalogToneMapping(
        Texture2D texture,
        ThumbnailRenderProfile profile
    )
    {
        if (texture == null)
        {
            return;
        }

        Color32[] pixels =
            texture.GetPixels32();

        for (int index = 0;
             index < pixels.Length;
             index++)
        {
            Color32 pixel =
                pixels[index];

            if (pixel.a <= 4)
            {
                continue;
            }

            float red =
                ToneMapChannel(
                    pixel.r / 255f,
                    profile
                );

            float green =
                ToneMapChannel(
                    pixel.g / 255f,
                    profile
                );

            float blue =
                ToneMapChannel(
                    pixel.b / 255f,
                    profile
                );

            pixels[index] =
                new Color(
                    red,
                    green,
                    blue,
                    pixel.a / 255f
                );
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static float ToneMapChannel(
        float value,
        ThumbnailRenderProfile profile
    )
    {
        float lifted =
            Mathf.Clamp01(
                (
                    value +
                    profile.ShadowLift
                ) *
                profile.Exposure
            );

        return Mathf.Pow(
            lifted,
            0.92f
        );
    }

    /// <summary>
    /// Normaliza únicamente la instancia temporal utilizada por
    /// PreviewRenderUtility. El prefab persistente no se modifica.
    /// </summary>
    private static void ResetRootTransformForPreview(
        Transform root
    )
    {
        if (root == null)
        {
            return;
        }

        root.localPosition =
            Vector3.zero;

        root.localRotation =
            Quaternion.identity;

        root.localScale =
            Vector3.one;

        if (!root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Calcula los límites combinados de los renderers que realmente
    /// están visibles en la instancia de previsualización.
    ///
    /// Se descartan límites con valores NaN o infinitos para impedir
    /// que una malla defectuosa coloque la cámara en una posición no
    /// válida.
    /// </summary>
    private static bool TryCalculateRendererBounds(
        GameObject previewRoot,
        out Bounds combinedBounds
    )
    {
        combinedBounds =
            default(Bounds);

        if (previewRoot == null)
        {
            return false;
        }

        Renderer[] renderers =
            previewRoot.GetComponentsInChildren<Renderer>(
                true
            );

        bool hasValidBounds =
            false;

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

            Bounds rendererBounds =
                renderer.bounds;

            if (!IsFiniteVector3(rendererBounds.center) ||
                !IsFiniteVector3(rendererBounds.extents))
            {
                continue;
            }

            if (!hasValidBounds)
            {
                combinedBounds =
                    rendererBounds;

                hasValidBounds =
                    true;

                continue;
            }

            combinedBounds.Encapsulate(
                rendererBounds
            );
        }

        return hasValidBounds;
    }

    /// <summary>
    /// Comprueba valores numéricos sin depender de APIs de .NET que
    /// puedan variar entre perfiles de compatibilidad de Unity.
    /// </summary>
    private static bool IsFiniteVector3(
        Vector3 value
    )
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.z);
    }

    private static void ConfigurePreviewUtility(
        PreviewRenderUtility previewUtility,
        Bounds bounds,
        ThumbnailRenderProfile profile
    )
    {
        Camera camera =
            previewUtility.camera;

        camera.clearFlags =
            CameraClearFlags.SolidColor;

        camera.backgroundColor =
            new Color(
                0.055f,
                0.06f,
                0.065f,
                0f
            );

        camera.orthographic = false;
        camera.fieldOfView = CameraFieldOfView;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = false;
        camera.nearClipPlane = 0.01f;

        float radius =
            Mathf.Max(
                MinimumRenderRadius,
                bounds.extents.magnitude
            );

        float halfFieldOfViewRadians =
            CameraFieldOfView *
            0.5f *
            Mathf.Deg2Rad;

        float distance =
            radius /
            Mathf.Sin(halfFieldOfViewRadians) *
            profile.CameraPadding;

        Vector3 viewDirection =
            profile.ViewDirection.sqrMagnitude > 0.0001f
                ? profile.ViewDirection.normalized
                : new Vector3(
                    1.05f,
                    0.72f,
                    -1.05f
                ).normalized;

        camera.transform.position =
            bounds.center -
            viewDirection *
            distance;

        camera.transform.rotation =
            Quaternion.LookRotation(
                bounds.center -
                camera.transform.position,
                Vector3.up
            );

        camera.farClipPlane =
            Mathf.Max(
                100f,
                distance + radius * 4f
            );

        previewUtility.ambientColor =
            new Color(
                profile.AmbientIntensity,
                profile.AmbientIntensity,
                profile.AmbientIntensity * 1.02f,
                1f
            );

        Light[] lights =
            previewUtility.lights;

        if (lights != null &&
            lights.Length > 0 &&
            lights[0] != null)
        {
            lights[0].intensity =
                profile.KeyIntensity;

            lights[0].color =
                new Color(
                    1f,
                    0.96f,
                    0.9f,
                    1f
                );

            lights[0].shadows =
                LightShadows.Soft;

            lights[0].transform.rotation =
                Quaternion.Euler(
                    34f,
                    -34f,
                    0f
                );
        }

        if (lights != null &&
            lights.Length > 1 &&
            lights[1] != null)
        {
            lights[1].intensity =
                profile.FillIntensity;

            lights[1].color =
                new Color(
                    0.78f,
                    0.86f,
                    1f,
                    1f
                );

            lights[1].shadows =
                LightShadows.None;

            lights[1].transform.rotation =
                Quaternion.Euler(
                    318f,
                    145f,
                    0f
                );
        }
    }

    private static bool ConfigureTextureAsCatalogSprite(
        string iconPath,
        int maximumSize,
        out string errorMessage
    )
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(iconPath)
            as TextureImporter;

        if (importer == null)
        {
            errorMessage =
                "Unity no creó un TextureImporter para " +
                iconPath +
                ".";

            return false;
        }

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.alphaSource =
            TextureImporterAlphaSource.FromInput;

        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = maximumSize;
        importer.textureCompression =
            TextureImporterCompression.Compressed;
        importer.compressionQuality = 75;
        importer.spritePixelsPerUnit = 100f;

        importer.SaveAndReimport();

        errorMessage = string.Empty;
        return true;
    }

    private static string BuildIconAssetPath(
        RestaurantPlaceableItemDefinition definition
    )
    {
        string stableName =
            SanitizeFileName(
                definition.ItemId
            );

        if (string.IsNullOrWhiteSpace(stableName))
        {
            stableName =
                SanitizeFileName(
                    definition.name
                );
        }

        if (string.IsNullOrWhiteSpace(stableName))
        {
            stableName = "placeable_item";
        }

        return
            GeneratedIconFolder +
            "/" +
            stableName +
            ".png";
    }

    private static string SanitizeFileName(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder =
            new StringBuilder();

        bool previousSeparator = false;

        for (int index = 0;
             index < value.Length;
             index++)
        {
            char character = value[index];

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(
                    char.ToLowerInvariant(character)
                );

                previousSeparator = false;
            }
            else if (!previousSeparator)
            {
                builder.Append('_');
                previousSeparator = true;
            }
        }

        return builder
            .ToString()
            .Trim('_');
    }

    private static void RestoreIconFileAfterFailure(
        string iconPath,
        bool hadPreviousFile,
        byte[] previousFileBytes
    )
    {
        try
        {
            string absolutePath =
                ConvertUnityAssetPathToAbsolutePath(
                    iconPath
                );

            if (hadPreviousFile &&
                previousFileBytes != null)
            {
                File.WriteAllBytes(
                    absolutePath,
                    previousFileBytes
                );

                AssetDatabase.ImportAsset(
                    iconPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate
                );
            }
            else
            {
                AssetDatabase.DeleteAsset(
                    iconPath
                );
            }
        }
        catch (Exception restoreException)
        {
            Debug.LogException(
                restoreException
            );
        }
    }

    private static void EnsureUnityAssetFolderExists(
        string folderPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(folderPath);

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        ValidateUnityAssetPath(
            normalized,
            true
        );

        string[] segments =
            normalized.Split('/');

        string current = "Assets";

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string next =
                current +
                "/" +
                segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                string guid =
                    AssetDatabase.CreateFolder(
                        current,
                        segments[index]
                    );

                if (string.IsNullOrWhiteSpace(guid) ||
                    !AssetDatabase.IsValidFolder(next))
                {
                    throw new InvalidOperationException(
                        "Unity no pudo crear la carpeta " +
                        next +
                        "."
                    );
                }
            }

            current = next;
        }
    }

    private static string ConvertUnityAssetPathToAbsolutePath(
        string assetPath
    )
    {
        string normalized =
            NormalizeUnityAssetPath(assetPath);

        ValidateUnityAssetPath(
            normalized,
            false
        );

        string relativeToAssets =
            normalized.Substring(
                "Assets".Length
            );

        string platformRelative =
            relativeToAssets.Replace(
                '/',
                Path.DirectorySeparatorChar
            );

        return
            Application.dataPath +
            platformRelative;
    }

    private static string GetAbsoluteDirectoryName(
        string absoluteFilePath
    )
    {
        int lastSlash =
            Math.Max(
                absoluteFilePath.LastIndexOf('/'),
                absoluteFilePath.LastIndexOf('\\')
            );

        if (lastSlash <= 0)
        {
            throw new InvalidOperationException(
                "No se pudo resolver la carpeta física de " +
                absoluteFilePath +
                "."
            );
        }

        return absoluteFilePath.Substring(
            0,
            lastSlash
        );
    }

    private static string NormalizeUnityAssetPath(
        string path
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized =
            path.Trim()
                .Replace('\\', '/');

        while (normalized.Contains("//"))
        {
            normalized =
                normalized.Replace("//", "/");
        }

        return normalized.TrimEnd('/');
    }

    private static void ValidateUnityAssetPath(
        string path,
        bool folderExpected
    )
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith(
                "Assets",
                StringComparison.Ordinal
            ) ||
            (path.Length > "Assets".Length &&
             path["Assets".Length] != '/'))
        {
            throw new ArgumentException(
                "La ruta debe comenzar por Assets/: " +
                path
            );
        }

        if (path.Contains("../") ||
            path.Contains("/..") ||
            path.Contains(":"))
        {
            throw new ArgumentException(
                "La ruta contiene segmentos no permitidos: " +
                path
            );
        }

        if (!folderExpected &&
            !path.EndsWith(
                ".png",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new ArgumentException(
                "La miniatura debe utilizar extensión .png: " +
                path
            );
        }
    }

    private static int CompareDefinitions(
        RestaurantPlaceableItemDefinition first,
        RestaurantPlaceableItemDefinition second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int categoryComparison =
            first.Category.CompareTo(
                second.Category
            );

        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );
    }
}
