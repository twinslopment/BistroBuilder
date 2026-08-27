using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla jugable 5E de horarios y turnos. Presentation pura.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Schedule Player Screen")]
public sealed class BistroBuilderStaffSchedulePlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffSchedulePlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform employeeContent;
    [SerializeField] private BistroBuilderStaffScheduleEmployeeRowView employeeRowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousDayButton;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button lunchButton;
    [SerializeField] private Button dinnerButton;
    [SerializeField] private Button autoFillButton;
    [SerializeField] private Button copyPreviousButton;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text coverageText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text emptyStateText;

    private int dayIndex = 1;
    private BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    private bool bound;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public int SelectedDayIndex => dayIndex;
    public BistroBuilderMealServiceAvailability SelectedMealService => mealService;

    private void Awake()
    {
        CacheDependencies();
        Bind();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
        if (facade != null) facade.ViewInvalidated += HandleInvalidated;
    }

    private void OnDisable()
    {
        if (facade != null) facade.ViewInvalidated -= HandleInvalidated;
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || employeeContent == null ||
            employeeRowPrefab == null || closeButton == null || previousDayButton == null ||
            nextDayButton == null || lunchButton == null || dinnerButton == null ||
            autoFillButton == null || copyPreviousButton == null || headerText == null ||
            coverageText == null || feedbackText == null || emptyStateText == null)
        {
            error = "La pantalla 5E tiene referencias incompletas.";
            return false;
        }
        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (!ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }
        dayIndex = facade.CurrentDayIndex;
        mealService = BistroBuilderMealServiceAvailability.Lunch;
        panelRoot.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        ShowFeedback(string.Empty);
    }

    public void Refresh()
    {
        if (!IsVisible || facade == null) return;
        if (!facade.TryBuildSnapshot(dayIndex, mealService,
                out BistroBuilderStaffSchedulePlayerSnapshot snapshot,
                out string error))
        {
            ShowFeedback(error);
            return;
        }

        ClearRows();
        bool hasEmployees = snapshot.employees.Count > 0;
        emptyStateText.gameObject.SetActive(!hasEmployees);
        emptyStateText.text = hasEmployees
            ? string.Empty
            : "No hay camareros en plantilla. Contrata Personal para empezar a planificar turnos.";
        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            BistroBuilderStaffScheduleEmployeeRowView row = Instantiate(
                employeeRowPrefab, employeeContent);
            row.gameObject.SetActive(true);
            row.Bind(snapshot.employees[index], HandleToggleEmployee);
        }

        headerText.text = "Día " + dayIndex + " · " +
            (mealService == BistroBuilderMealServiceAvailability.Lunch ? "Comida" : "Cena");
        BistroBuilderStaffScheduleCoverage coverage = snapshot.coverage;
        coverageText.text = coverage != null
            ? "Cobertura: " + coverage.scheduledWaiters + "/" +
              coverage.minimumRecommendedWaiters + " camareros · Coste previsto: " +
              (coverage.projectedSalaryCents / 100m).ToString("0.00") + " € · " +
              (coverage.isSufficient ? "SUFICIENTE" : "INSUFICIENTE")
            : string.Empty;

        coverageText.color = coverage == null
            ? new Color(0.78f, 0.78f, 0.75f, 1f)
            : coverage.isSufficient
                ? new Color(0.62f, 0.82f, 0.66f, 1f)
                : new Color(0.95f, 0.72f, 0.38f, 1f);
        UpdateMealButtonState();
        previousDayButton.interactable = dayIndex > facade.CurrentDayIndex;
        nextDayButton.interactable =
            dayIndex < facade.CurrentDayIndex + facade.PlanningHorizonDays - 1;
        copyPreviousButton.interactable = dayIndex > facade.CurrentDayIndex;
        ShowFeedback(string.Empty);
    }

    private void HandleToggleEmployee(string employeeId)
    {
        if (!facade.TryToggleEmployee(employeeId, dayIndex, mealService, out string error))
        {
            ShowFeedback(error);
            return;
        }
        Refresh();
    }

    private void HandlePreviousDay()
    {
        dayIndex = Mathf.Max(facade.CurrentDayIndex, dayIndex - 1);
        Refresh();
    }

    private void HandleNextDay()
    {
        int last = facade.CurrentDayIndex + facade.PlanningHorizonDays - 1;
        dayIndex = Mathf.Min(last, dayIndex + 1);
        Refresh();
    }

    private void HandleLunch()
    {
        mealService = BistroBuilderMealServiceAvailability.Lunch;
        Refresh();
    }

    private void HandleDinner()
    {
        mealService = BistroBuilderMealServiceAvailability.Dinner;
        Refresh();
    }

    private void HandleAutoFill()
    {
        if (!facade.TryAutoFill(dayIndex, mealService, out string error))
        {
            ShowFeedback(error);
            return;
        }
        Refresh();
    }

    private void HandleCopyPrevious()
    {
        if (!facade.TryCopyPreviousDay(dayIndex, mealService, out string error))
        {
            ShowFeedback(error);
            return;
        }
        Refresh();
    }

    private void UpdateMealButtonState()
    {
        SetButtonState(lunchButton, mealService == BistroBuilderMealServiceAvailability.Lunch);
        SetButtonState(dinnerButton, mealService == BistroBuilderMealServiceAvailability.Dinner);
    }

    private static void SetButtonState(Button button, bool active)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = active
            ? new Color(0.22f, 0.34f, 0.27f, 1f)
            : new Color(0.16f, 0.19f, 0.21f, 1f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        label.color = active
            ? new Color(0.88f, 0.93f, 0.80f, 1f)
            : new Color(0.92f, 0.92f, 0.90f, 1f);
    }

    private void Bind()
    {
        if (bound) return;
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (previousDayButton != null) previousDayButton.onClick.AddListener(HandlePreviousDay);
        if (nextDayButton != null) nextDayButton.onClick.AddListener(HandleNextDay);
        if (lunchButton != null) lunchButton.onClick.AddListener(HandleLunch);
        if (dinnerButton != null) dinnerButton.onClick.AddListener(HandleDinner);
        if (autoFillButton != null) autoFillButton.onClick.AddListener(HandleAutoFill);
        if (copyPreviousButton != null) copyPreviousButton.onClick.AddListener(HandleCopyPrevious);
        bound = true;
    }

    private void Unbind()
    {
        if (!bound) return;
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
        if (previousDayButton != null) previousDayButton.onClick.RemoveListener(HandlePreviousDay);
        if (nextDayButton != null) nextDayButton.onClick.RemoveListener(HandleNextDay);
        if (lunchButton != null) lunchButton.onClick.RemoveListener(HandleLunch);
        if (dinnerButton != null) dinnerButton.onClick.RemoveListener(HandleDinner);
        if (autoFillButton != null) autoFillButton.onClick.RemoveListener(HandleAutoFill);
        if (copyPreviousButton != null) copyPreviousButton.onClick.RemoveListener(HandleCopyPrevious);
        bound = false;
    }

    private void ClearRows()
    {
        if (employeeContent == null) return;
        for (int index = employeeContent.childCount - 1; index >= 0; index--)
            Destroy(employeeContent.GetChild(index).gameObject);
    }

    private void HandleInvalidated() => Refresh();

    private void ShowFeedback(string message)
    {
        if (feedbackText != null) feedbackText.text = message ?? string.Empty;
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
