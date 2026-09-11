using System;
using System.Collections.Generic;

public enum BistroBuilderNewGamePhase
{
    StartMenu = 0,
    InitialSetup = 1,
    Briefing = 2,
    ReadyToOpen = 3,
    FirstService = 4,
    NormalPlay = 5
}

public enum BistroBuilderStartingPremisesProfile
{
    Compact = 0,
    Balanced = 1,
    Spacious = 2,
    Empty = 3
}

public enum BistroBuilderOpeningCheckLevel
{
    Passed = 0,
    Warning = 1,
    Blocker = 2
}

[Serializable]
public sealed class BistroBuilderOpeningCheck
{
    public string checkId = string.Empty;
    public string label = string.Empty;
    public BistroBuilderOpeningCheckLevel level;
    public string message = string.Empty;

    public BistroBuilderOpeningCheck DeepClone() =>
        (BistroBuilderOpeningCheck)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderOpeningPreflightReport
{
    public List<BistroBuilderOpeningCheck> checks = new List<BistroBuilderOpeningCheck>();
    public int passedCount;
    public int warningCount;
    public int blockerCount;

    public bool CanOpen => blockerCount == 0;

    public BistroBuilderOpeningPreflightReport DeepClone()
    {
        var clone = new BistroBuilderOpeningPreflightReport
        {
            passedCount = passedCount,
            warningCount = warningCount,
            blockerCount = blockerCount
        };
        if (checks != null)
            for (int i = 0; i < checks.Count; i++)
                clone.checks.Add(checks[i]?.DeepClone());
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderNewGameStateSnapshot
{
    public const string CurrentSchemaId = "new_game.opening.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public BistroBuilderNewGamePhase phase = BistroBuilderNewGamePhase.StartMenu;
    public bool setupCompleted;
    public bool briefingAcknowledged;
    public bool firstOpeningCompleted;
    public bool firstServiceStarted;
    public bool transitionedToNormalPlay;
    public bool initialSaveRequested;
    public int initialSaveSlot;
    public string restaurantName = string.Empty;
    public BistroBuilderStartingPremisesProfile premisesProfile =
        BistroBuilderStartingPremisesProfile.Balanced;
    public int initialOpeningHour = 12;
    public int initialClosingHour = 15;
    public string lastBriefing = string.Empty;

    public BistroBuilderNewGameStateSnapshot DeepClone() =>
        (BistroBuilderNewGameStateSnapshot)MemberwiseClone();
}

public static class BistroBuilderNewGameEngine
{
    public static bool TryValidateSnapshot(
        BistroBuilderNewGameStateSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId, BistroBuilderNewGameStateSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderNewGameStateSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L ||
            !Enum.IsDefined(typeof(BistroBuilderNewGamePhase), snapshot.phase) ||
            !Enum.IsDefined(typeof(BistroBuilderStartingPremisesProfile), snapshot.premisesProfile) ||
            snapshot.initialSaveSlot < 0 || snapshot.initialSaveSlot > 999 ||
            snapshot.initialOpeningHour < 0 || snapshot.initialOpeningHour > 23 ||
            snapshot.initialClosingHour < 0 || snapshot.initialClosingHour > 23 ||
            snapshot.initialOpeningHour == snapshot.initialClosingHour ||
            snapshot.restaurantName == null || snapshot.restaurantName.Length > 80 ||
            snapshot.lastBriefing == null || snapshot.lastBriefing.Length > 4000)
        {
            error = "El estado de nueva partida/apertura inicial es invalido.";
            return false;
        }

        if (snapshot.setupCompleted && string.IsNullOrWhiteSpace(snapshot.restaurantName))
        {
            error = "Una nueva partida configurada necesita identidad de restaurante.";
            return false;
        }
        if (snapshot.firstOpeningCompleted && !snapshot.setupCompleted)
        {
            error = "La primera apertura no puede existir sin configuracion inicial.";
            return false;
        }
        if (snapshot.firstServiceStarted && !snapshot.firstOpeningCompleted)
        {
            error = "El primer servicio no puede existir sin primera apertura.";
            return false;
        }
        if (snapshot.transitionedToNormalPlay && !snapshot.firstServiceStarted)
        {
            error = "El juego normal no puede comenzar antes del primer servicio.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static void AddCheck(
        BistroBuilderOpeningPreflightReport report,
        string id,
        string label,
        BistroBuilderOpeningCheckLevel level,
        string message)
    {
        if (report == null) return;
        report.checks.Add(new BistroBuilderOpeningCheck
        {
            checkId = id ?? string.Empty,
            label = label ?? string.Empty,
            level = level,
            message = message ?? string.Empty
        });
        if (level == BistroBuilderOpeningCheckLevel.Blocker) report.blockerCount++;
        else if (level == BistroBuilderOpeningCheckLevel.Warning) report.warningCount++;
        else report.passedCount++;
    }

    public static string BuildBriefing(
        string restaurantName,
        BistroBuilderStartingPremisesProfile premises,
        BistroBuilderOpeningPreflightReport report,
        int openingHour,
        int closingHour)
    {
        string name = string.IsNullOrWhiteSpace(restaurantName)
            ? "Tu restaurante"
            : restaurantName.Trim();
        string readiness = report != null && report.CanOpen
            ? "Todo lo imprescindible esta listo para abrir."
            : "Aun hay requisitos imprescindibles que resolver antes de abrir.";
        int warnings = report != null ? report.warningCount : 0;
        return name + " esta preparado con el local " + premises + ".\n" +
               readiness + "\nHorario inicial: " + openingHour.ToString("00") +
               ":00 - " + closingHour.ToString("00") + ":00.\n" +
               (warnings > 0
                   ? "Hay " + warnings + " aviso(s) no bloqueante(s) para revisar cuando quieras."
                   : "No hay riesgos no bloqueantes detectados.");
    }
}
