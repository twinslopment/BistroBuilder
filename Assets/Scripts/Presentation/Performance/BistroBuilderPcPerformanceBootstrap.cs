using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// PC-only runtime quality adapter for integrated/low-spec Windows GPUs.
/// It clones the active URP asset in memory, so the canonical PC asset is
/// never modified. Dedicated GPUs keep the normal PC profile.
/// </summary>
public static class BistroBuilderPcPerformanceBootstrap
{
    private const int IntegratedTargetFps = 30;
    private const float IntegratedRenderScale = 0.75f;
    private const float IntegratedShadowDistance = 28f;
    private const int IntegratedShadowCascades = 1;
    private const int IntegratedAdditionalLightsPerObject = 2;

    private static UniversalRenderPipelineAsset runtimePipeline;
    private static bool active;

    public static bool IsActive => active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        if (!IsWindowsPc() || HasCommandLineSwitch("-bb-full-pc"))
            return;

        bool forced = HasCommandLineSwitch("-bb-low-spec-pc");
        if (!forced && !LooksLikeIntegratedIntelGpu())
            return;

        UniversalRenderPipelineAsset source =
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (source == null)
        {
            Debug.LogWarning(
                "[BB PERF] PC Integrated Playtest not applied: active pipeline is not URP.");
            return;
        }

        runtimePipeline = UnityEngine.Object.Instantiate(source);
        runtimePipeline.name = source.name + "_RuntimePcIntegrated";
        runtimePipeline.hideFlags = HideFlags.DontSave;

        runtimePipeline.renderScale =
            Mathf.Min(source.renderScale, IntegratedRenderScale);
        runtimePipeline.shadowDistance =
            Mathf.Min(source.shadowDistance, IntegratedShadowDistance);
        runtimePipeline.shadowCascadeCount =
            Mathf.Min(source.shadowCascadeCount, IntegratedShadowCascades);
        runtimePipeline.maxAdditionalLightsCount =
            Mathf.Min(
                source.maxAdditionalLightsCount,
                IntegratedAdditionalLightsPerObject);
        runtimePipeline.msaaSampleCount = 1;

        // Keep HDR, depth and opaque texture compatibility intact.
        // The current HUD glass path explicitly requests the opaque texture.
        QualitySettings.renderPipeline = runtimePipeline;

        QualitySettings.vSyncCount = 0;
        QualitySettings.shadowDistance =
            Mathf.Min(QualitySettings.shadowDistance, IntegratedShadowDistance);
        // URP uses the cloned asset's one-cascade setting. Do not force the
        // legacy QualitySettings cascade enum to an unsupported value.
        QualitySettings.pixelLightCount =
            Mathf.Min(QualitySettings.pixelLightCount, 2);
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        QualitySettings.lodBias =
            Mathf.Min(QualitySettings.lodBias, 1.25f);

        active = true;
        ApplyFramePacingAndSceneLights();
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Debug.Log(
            "[BB PERF] PC Integrated Playtest ACTIVE | GPU=" +
            SystemInfo.graphicsDeviceName +
            " | renderScale=" + runtimePipeline.renderScale.ToString("0.00") +
            " | shadowDistance=" + runtimePipeline.shadowDistance.ToString("0") +
            "m | cascades=" + runtimePipeline.shadowCascadeCount +
            " | additionalLights/object=" + runtimePipeline.maxAdditionalLightsCount +
            " | target=" + IntegratedTargetFps + " FPS");
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!active) return;
        ApplyFramePacingAndSceneLights();
    }

    private static void ApplyFramePacingAndSceneLights()
    {
        QualitySettings.vSyncCount = 0;
        if (Application.targetFrameRate <= 0 ||
            Application.targetFrameRate > IntegratedTargetFps)
        {
            Application.targetFrameRate = IntegratedTargetFps;
        }

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        Light mainDirectional = null;
        float mainDirectionalIntensity = float.MinValue;

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || !light.enabled) continue;

            if (light.type == LightType.Directional &&
                light.intensity > mainDirectionalIntensity)
            {
                mainDirectional = light;
                mainDirectionalIntensity = light.intensity;
            }
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || !light.enabled) continue;

            if (light == mainDirectional)
            {
                light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
                continue;
            }

            // Additional real-time shadows are one of the most expensive GPU
            // costs on integrated graphics. Lighting remains active.
            light.shadows = LightShadows.None;
        }
    }

    private static bool LooksLikeIntegratedIntelGpu()
    {
        string name = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();
        string vendor = (SystemInfo.graphicsDeviceVendor ?? string.Empty).ToLowerInvariant();

        return vendor.Contains("intel") ||
               name.Contains("intel") ||
               name.Contains("iris") ||
               name.Contains("uhd graphics");
    }

    private static bool IsWindowsPc()
    {
        return Application.platform == RuntimePlatform.WindowsEditor ||
               Application.platform == RuntimePlatform.WindowsPlayer;
    }

    private static bool HasCommandLineSwitch(string value)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(
                    args[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
