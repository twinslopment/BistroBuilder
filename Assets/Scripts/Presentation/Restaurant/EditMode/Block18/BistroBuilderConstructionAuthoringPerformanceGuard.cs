using UnityEngine;

/// <summary>
/// Mantiene un frame pacing estable en el playtest de Construction Authoring.
/// Evita consumir un núcleo completo cuando VSync está desactivado.
/// </summary>
public static class BistroBuilderConstructionAuthoringPerformanceGuard
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigurePlaytestFramePacing()
    {
        if (QualitySettings.vSyncCount != 0) return;
        if (Application.targetFrameRate <= 0 || Application.targetFrameRate > TargetFrameRate)
            Application.targetFrameRate = TargetFrameRate;
    }
}
