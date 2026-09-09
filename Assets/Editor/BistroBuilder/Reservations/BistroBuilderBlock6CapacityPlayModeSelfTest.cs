using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate Play Mode de la ampliación del Bloque 6.
/// Valida las colocaciones con los registros y obstáculos runtime reales,
/// incluido mobiliario, barra fija y reglas especializadas.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderBlock6CapacityPlayModeSelfTest
{
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.Reservations.6X.CapacityPlay.Stage";
    private const string SuccessKey =
        "BB.Reservations.6X.CapacityPlay.Success";
    private const string ReportPath =
        "Block6CapacityPlayModeReport.txt";

    static BistroBuilderBlock6CapacityPlayModeSelfTest()
    {        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem(
        "Tools/Bistro Builder/Reservations/6X - Capacity PlayMode gate",
        false,
        602)]
    private static void RunFromMenu()
    {
        Begin(false);
    }

    public static void RunFromCommandLine()
    {
        Begin(true);
    }

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El gate de capacidad ya está en Play Mode.");        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(
            MainScenePath,
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        string stage = SessionState.GetString(
            StageKey,
            string.Empty);
        if (string.IsNullOrWhiteSpace(stage))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetString(
                StageKey,
                stage.EndsWith("cli", StringComparison.Ordinal)
                    ? "validate_cli"
                    : "validate_menu");
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(
                SuccessKey,
                false);
            bool commandLine = stage.Contains("cli");
            SessionState.EraseString(StageKey);
            if (commandLine)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying)
            return;

        string stage = SessionState.GetString(
            StageKey,
            string.Empty);
        if (!stage.StartsWith(
                "validate_",
                StringComparison.Ordinal))
            return;

        if (Time.frameCount < 3)
            return;

        ValidateRuntime(stage.EndsWith("cli", StringComparison.Ordinal));
    }
    private static void ValidateRuntime(bool commandLine)
    {
        RestaurantPlacementValidationService validation =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementValidationService>();
        GameObject root = GameObject.Find(
            BistroBuilderBlock6CapacityInstaller.ExpansionRootName);

        var report = new StringBuilder();
        report.AppendLine(
            "=== BISTRO BUILDER — 6X / CAPACITY PLAY MODE ===");

        if (validation == null || root == null)
        {
            report.AppendLine(
                "[FAIL] Faltan la autoridad de Placement o la ampliación.");
            Finish(false, report.ToString(), commandLine);
            return;
        }

        RestaurantAreaMember[] members =
            root.GetComponentsInChildren<RestaurantAreaMember>(true);
        int checkedCount = 0;
        int failedCount = 0;

        for (int index = 0; index < members.Length; index++)
        {
            RestaurantAreaMember member = members[index];            if (member == null ||
                member.GetComponent<RestaurantPlacementFootprint>() == null)
                continue;

            checkedCount++;
            RestaurantPlacementValidationResult result =
                validation.ValidateCurrentPlacement(member);
            if (result.IsValid)
            {
                report.AppendLine("[OK] " + member.name + " placement válido.");
                continue;
            }

            failedCount++;
            report.Append("[FAIL] ")
                .Append(member.name)
                .Append(" -> ")
                .Append(result.Status);

            if (result.ConflictingFootprint != null)
                report.Append(" / footprint=")
                    .Append(result.ConflictingFootprint.name);
            if (result.ConflictingObstacle != null)
                report.Append(" / obstacle=")
                    .Append(result.ConflictingObstacle.name);
            if (!string.IsNullOrWhiteSpace(result.TechnicalMessage))
                report.Append(" / detail=").Append(result.TechnicalMessage);
            if (result.RelatedObject != null)
                report.Append(" / related=").Append(result.RelatedObject.name);
            report.AppendLine();
        }

        report.Append("Resultado: ")            .Append(checkedCount - failedCount)
            .Append(" OK / ")
            .Append(failedCount)
            .Append(" fallos.")
            .AppendLine();

        Finish(
            failedCount == 0 && checkedCount > 0,
            report.ToString(),
            commandLine);
    }

    private static void Finish(
        bool success,
        string report,
        bool commandLine)
    {
        if (!EditorApplication.isPlaying)
            return;

        File.WriteAllText(
            Path.GetFullPath(ReportPath),
            report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}