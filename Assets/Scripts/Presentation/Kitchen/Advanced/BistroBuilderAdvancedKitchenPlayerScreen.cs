using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista operacional compacta de cocina. Resume carga y estaciones, permite
/// limitar entradas y priorizar una única preparación en espera.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedKitchenPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedKitchenPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button reducedButton;
    [SerializeField] private Button pausedButton;
    [SerializeField] private Button previousStationButton;
    [SerializeField] private Button nextStationButton;
    [SerializeField] private Button previousTaskButton;
    [SerializeField] private Button nextTaskButton;
    [SerializeField] private Button prioritizeButton;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text stationListText;
    [SerializeField] private TMP_Text taskDetailText;
    [SerializeField] private TMP_Text feedbackText;

    private BistroBuilderAdvancedKitchenSnapshot snapshot;
    private int stationIndex;
    private int taskIndex;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        WireButtons();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (facade != null)
        {
            facade.Changed -= Refresh;
            facade.Changed += Refresh;
        }
    }

    private void OnDisable()
    {
        if (facade != null) facade.Changed -= Refresh;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (facade == null || panelRoot == null || closeButton == null ||
            normalButton == null || reducedButton == null || pausedButton == null ||
            previousStationButton == null || nextStationButton == null ||
            previousTaskButton == null || nextTaskButton == null ||
            prioritizeButton == null || summaryText == null ||
            stationListText == null || taskDetailText == null || feedbackText == null)
        {
            error = "La pantalla de Cocina 12 no tiene todos sus controles enlazados.";
            return false;
        }
        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        stationIndex = 0;
        taskIndex = 0;
        feedbackText.text = string.Empty;
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (!IsOpen || facade == null) return;
        if (!facade.TryBuildSnapshot(out snapshot, out string error))
        {
            feedbackText.text = error;
            return;
        }
        stationIndex = Mathf.Clamp(
            stationIndex, 0, Mathf.Max(0, snapshot.stations.Count - 1));
        BistroBuilderKitchenStationSnapshot station = SelectedStation();
        taskIndex = Mathf.Clamp(
            taskIndex, 0, Mathf.Max(0, station != null ? station.tasks.Count - 1 : 0));
        Render();
    }

    private void Render()
    {
        summaryText.text = "COCINA — " + LoadLabel(snapshot.loadState).ToUpperInvariant() +
            "   ·   " + snapshot.activeCount + " preparando / " +
            snapshot.queuedCount + " esperando   ·   Capacidad " +
            snapshot.totalCapacity + "   ·   Calidad " +
            QualityLabel(snapshot.averageRecentQualityBasisPoints) +
            "\nEntrada: " + IntakeLabel(snapshot.intakeMode);

        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < snapshot.stations.Count; i++)
        {
            BistroBuilderKitchenStationSnapshot row = snapshot.stations[i];
            builder.Append(i == stationIndex ? "> " : "   ");
            builder.Append(row.displayName).Append("  ·  ")
                .Append(LoadLabel(row.loadState)).Append("  ·  ")
                .Append(row.activeCount).Append('/').Append(row.capacity)
                .Append(" activos  ·  ").Append(row.queuedCount).Append(" espera");
            if (row.blockedSeconds > 0.01f)
                builder.Append("  ! bloqueada ").Append(row.blockedSeconds.ToString("0.0")).Append(" s");
            builder.AppendLine();
        }
        stationListText.text = builder.ToString();
        RenderTask();
    }

    private void RenderTask()
    {
        BistroBuilderKitchenStationSnapshot station = SelectedStation();
        BistroBuilderKitchenTaskSnapshot task = SelectedTask();
        if (station == null)
        {
            taskDetailText.text = "No hay estaciones disponibles.";
            prioritizeButton.interactable = false;
            return;
        }
        if (task == null)
        {
            taskDetailText.text = "<b>" + station.displayName + "</b>\nSin preparaciones pendientes.";
            prioritizeButton.interactable = false;
            return;
        }

        string state = task.active
            ? "Preparando · quedan " + Mathf.Max(0f, task.remainingSeconds).ToString("0.0") + " s"
            : "Esperando estación";
        taskDetailText.text = "<b>" + station.displayName + "</b>\n" +
            task.dishId + "  ·  etapa " + (task.stageIndex + 1) + "/" + task.stageCount +
            "\n" + state + "\nCocinero: " + task.cookDisplayName +
            "\nPrioridad: " + PriorityLabel(task.priority) +
            "\nCalidad estimada: " + QualityLabel(task.qualityBasisPoints) +
            (task.incident == BistroBuilderKitchenIncidentKind.None
                ? string.Empty : "\n! " + IncidentLabel(task.incident));
        prioritizeButton.interactable = !task.active &&
            task.priority != BistroBuilderKitchenPriorityKind.PlayerPriority;
    }

    private void WireButtons()
    {
        closeButton?.onClick.AddListener(Hide);
        normalButton?.onClick.AddListener(() => SetMode(BistroBuilderKitchenIntakeMode.Normal));
        reducedButton?.onClick.AddListener(() => SetMode(BistroBuilderKitchenIntakeMode.Reduced));
        pausedButton?.onClick.AddListener(() => SetMode(BistroBuilderKitchenIntakeMode.Paused));
        previousStationButton?.onClick.AddListener(() => MoveStation(-1));
        nextStationButton?.onClick.AddListener(() => MoveStation(1));
        previousTaskButton?.onClick.AddListener(() => MoveTask(-1));
        nextTaskButton?.onClick.AddListener(() => MoveTask(1));
        prioritizeButton?.onClick.AddListener(PrioritizeSelected);
    }

    private void SetMode(BistroBuilderKitchenIntakeMode mode)
    {
        bool ok = facade.SetIntakeMode(mode, out string error);
        feedbackText.text = ok
            ? "✓ Entrada de cocina: " + IntakeLabel(mode)
            : "✕ " + error;
        if (ok) Refresh();
    }

    private void PrioritizeSelected()
    {
        BistroBuilderKitchenTaskSnapshot task = SelectedTask();
        if (task == null) return;
        bool ok = facade.Prioritize(task.lineId, out string error);
        feedbackText.text = ok ? "✓ Preparación priorizada." : "✕ " + error;
        if (ok) Refresh();
    }
    private void MoveStation(int delta)
    {
        if (snapshot == null || snapshot.stations.Count == 0) return;
        stationIndex = (stationIndex + delta + snapshot.stations.Count) % snapshot.stations.Count;
        taskIndex = 0;
        feedbackText.text = string.Empty;
        Render();
    }

    private void MoveTask(int delta)
    {
        BistroBuilderKitchenStationSnapshot station = SelectedStation();
        if (station == null || station.tasks.Count == 0) return;
        taskIndex = (taskIndex + delta + station.tasks.Count) % station.tasks.Count;
        feedbackText.text = string.Empty;
        RenderTask();
    }

    private BistroBuilderKitchenStationSnapshot SelectedStation()
    {
        return snapshot != null && snapshot.stations.Count > 0
            ? snapshot.stations[Mathf.Clamp(stationIndex, 0, snapshot.stations.Count - 1)]
            : null;
    }

    private BistroBuilderKitchenTaskSnapshot SelectedTask()
    {
        BistroBuilderKitchenStationSnapshot station = SelectedStation();
        return station != null && station.tasks.Count > 0
            ? station.tasks[Mathf.Clamp(taskIndex, 0, station.tasks.Count - 1)]
            : null;
    }

    private static string LoadLabel(BistroBuilderKitchenLoadState state)
    {
        return state switch
        {
            BistroBuilderKitchenLoadState.Fluid => "Fluida",
            BistroBuilderKitchenLoadState.Loaded => "Cargada",
            BistroBuilderKitchenLoadState.Saturated => "Saturada",
            BistroBuilderKitchenLoadState.Blocked => "Bloqueada",
            _ => state.ToString()
        };
    }

    private static string IntakeLabel(BistroBuilderKitchenIntakeMode mode)
    {
        return mode switch
        {
            BistroBuilderKitchenIntakeMode.Normal => "Normal",
            BistroBuilderKitchenIntakeMode.Reduced => "Ritmo reducido",
            BistroBuilderKitchenIntakeMode.Paused => "Pausada",
            _ => mode.ToString()
        };
    }

    private static string PriorityLabel(BistroBuilderKitchenPriorityKind priority)
    {
        return priority switch
        {
            BistroBuilderKitchenPriorityKind.PlayerPriority => "Prioridad del jugador",
            BistroBuilderKitchenPriorityKind.IncidentReplacement => "Reposición urgente",
            BistroBuilderKitchenPriorityKind.WaitingRecovery => "Recuperar espera",
            BistroBuilderKitchenPriorityKind.CourseSync => "Sincronizar pase",
            _ => "Normal"
        };
    }

    private static string IncidentLabel(BistroBuilderKitchenIncidentKind incident)
    {
        return incident switch
        {
            BistroBuilderKitchenIncidentKind.EquipmentFailure => "Avería de equipo",
            BistroBuilderKitchenIncidentKind.Slowdown => "Ralentización",
            BistroBuilderKitchenIncidentKind.QualityRisk => "Riesgo de calidad",
            _ => string.Empty
        };
    }

    private static string QualityLabel(int basisPoints)
    {
        int value = Mathf.Clamp(basisPoints, 0, 10000);
        if (value >= 8500) return "Excelente";
        if (value >= 7200) return "Muy buena";
        if (value >= 6000) return "Correcta";
        if (value >= 4500) return "Irregular";
        return "Deficiente";
    }
}
