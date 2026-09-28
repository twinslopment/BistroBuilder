using System;
using System.Collections;
using UnityEngine;

/// <summary>Uses monitor pixels for borderless fullscreen instead of stretching a saved window size.</summary>
public sealed class BistroBuilderDisplaySettings : MonoBehaviour
{
    private const string FullscreenPreference = "BB.Options.FullScreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Application.isEditor || Application.isBatchMode) return;
        var host = new GameObject("BB_DisplaySettings", typeof(BistroBuilderDisplaySettings));
        DontDestroyOnLoad(host);
    }

    private IEnumerator Start()
    {
        // Wait until Unity knows the display hosting the player window.
        yield return null;
        string[] args = Environment.GetCommandLineArgs();
        bool fullscreen = PlayerPrefs.GetInt(FullscreenPreference, Screen.fullScreen ? 1 : 0) == 1;
        int mode = Array.FindIndex(args, value => string.Equals(value, "-screen-fullscreen", StringComparison.OrdinalIgnoreCase));
        if (mode >= 0 && mode + 1 < args.Length) fullscreen = args[mode + 1] != "0";
        bool explicitSize = Array.Exists(args, value =>
            string.Equals(value, "-screen-width", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "-screen-height", StringComparison.OrdinalIgnoreCase));

        // Explicit command-line sizes remain available for windowed play and resolution QA.
        if (fullscreen && !explicitSize) SetFullscreen(true);
        else Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        yield return new WaitForSecondsRealtime(.5f);
        Debug.Log($"BB_DISPLAY_RESOLVED|{Screen.width}x{Screen.height}|MODE={Screen.fullScreenMode}|MONITOR={Screen.mainWindowDisplayInfo.width}x{Screen.mainWindowDisplayInfo.height}");
        Destroy(gameObject);
    }

    public static void SetFullscreen(bool fullscreen)
    {
        if (Application.isEditor) return;
        if (!fullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            return;
        }

        var display = Screen.mainWindowDisplayInfo;
        int width = display.width > 0 ? display.width : Display.main.systemWidth;
        int height = display.height > 0 ? display.height : Display.main.systemHeight;
        if (width <= 0 || height <= 0) return;
        Debug.Log($"BB_DISPLAY_NATIVE|FROM={Screen.width}x{Screen.height}|TO={width}x{height}");
        Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
    }
}