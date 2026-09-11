using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drives Play Mode at a bounded real-time cadence when Unity runs headless.
/// Custom Bistro Builder self-tests enter Play Mode through ExecuteMethod; in
/// batch mode Unity does not reliably advance the runtime player loop by itself.
/// Centralising the pump here keeps production services and individual tests
/// free of batch-only coroutine/frame-driving workarounds.
/// </summary>
[InitializeOnLoad]
internal static class BistroBuilderBatchPlayModePump
{
    internal const string DisabledSessionKey = "BB.BatchPlayModePump.Disabled";
    private const double StepIntervalSeconds = 1d / 50d;

    private static double nextStepAt;
    private static bool stepping;
    private static bool ownsPause;

    static BistroBuilderBatchPlayModePump()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!Application.isBatchMode) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            nextStepAt = EditorApplication.timeSinceStartup;
            stepping = false;
            EditorApplication.QueuePlayerLoopUpdate();
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            if (ownsPause && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
            ownsPause = false;
            stepping = false;
            nextStepAt = 0d;
        }
    }

    private static void Tick()
    {
        if (!Application.isBatchMode || !EditorApplication.isPlaying ||
            SessionState.GetBool(DisabledSessionKey, false))
            return;

        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (stepping || now < nextStepAt) return;

        if (!EditorApplication.isPaused)
        {
            EditorApplication.isPaused = true;
            ownsPause = true;
        }

        stepping = true;
        try
        {
            EditorApplication.Step();
        }
        finally
        {
            stepping = false;
            nextStepAt = EditorApplication.timeSinceStartup + StepIntervalSeconds;
        }
    }
}
