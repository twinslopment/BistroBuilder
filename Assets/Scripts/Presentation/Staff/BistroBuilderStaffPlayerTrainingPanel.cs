using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class BistroBuilderStaffPlayerTrainingButtonBinding
{
    public string trainingId = string.Empty;
    public string displayName = string.Empty;
    public int skillGain;
    public long financialCostCents;
    public Button button;
    public TMP_Text label;
}

/// <summary>
/// 4F — Presentación jugable de formación de empleados.
///
/// No conoce DevelopmentService, Finanzas, Save ni WaiterTaskCoordinator.
/// Las opciones visibles se copian del perfil canónico durante la instalación,
/// pero cada ejecución se delega exclusivamente a StaffPlayerFacade.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Player Training Panel")]
public sealed class BistroBuilderStaffPlayerTrainingPanel : MonoBehaviour
{
    private sealed class BoundAction
    {
        public Button button;
        public UnityAction action;
    }

    [SerializeField] private BistroBuilderStaffPlayerFacade facade;
    [SerializeField] private BistroBuilderStaffPlayerScreen screen;
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text employeeText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private List<BistroBuilderStaffPlayerTrainingButtonBinding> bindings =
        new List<BistroBuilderStaffPlayerTrainingButtonBinding>();

    private readonly List<BoundAction> boundActions = new List<BoundAction>();

    public bool IsVisible => modalRoot != null && modalRoot.activeSelf;
    public int TrainingOptionCount => bindings != null ? bindings.Count : 0;

    private void Awake()
    {
        CacheDependencies();
        Bind();
        SetVisible(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
        RenderOptions();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || screen == null || openButton == null ||
            modalRoot == null || closeButton == null || employeeText == null ||
            feedbackText == null || bindings == null || bindings.Count == 0)
        {
            error = "La UI de formación 4F tiene referencias incompletas.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < bindings.Count; index++)
        {
            BistroBuilderStaffPlayerTrainingButtonBinding binding = bindings[index];
            string id = binding != null
                ? BistroBuilderStaffStableIdUtility.Normalize(binding.trainingId)
                : string.Empty;
            if (binding == null ||
                !BistroBuilderStaffStableIdUtility.IsValid(id) ||
                !ids.Add(id) ||
                string.IsNullOrWhiteSpace(binding.displayName) ||
                binding.skillGain < 1 || binding.financialCostCents < 0L ||
                binding.button == null || binding.label == null)
            {
                error = "La UI de formación 4F contiene una opción inválida o duplicada.";
                return false;
            }
        }

        if (!facade.ValidateConfiguration(out error) ||
            !screen.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Show()
    {
        if (!ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }

        string employeeId = screen.SelectedEmployeeId;
        if (!BistroBuilderEmployeeIdUtility.IsValid(employeeId))
        {
            ShowFeedback("Selecciona primero un empleado de la plantilla.");
            return;
        }

        employeeText.text = "Empleado: " + employeeId;
        ShowFeedback(string.Empty);
        RenderOptions();
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        ShowFeedback(string.Empty);
    }

    private void Train(string trainingId)
    {
        string employeeId = screen != null ? screen.SelectedEmployeeId : string.Empty;
        if (!BistroBuilderEmployeeIdUtility.IsValid(employeeId))
        {
            ShowFeedback("El empleado seleccionado ya no es válido.");
            return;
        }

        if (!facade.TryTrainEmployee(
                employeeId,
                trainingId,
                out _,
                out BistroBuilderEmployeeTrainingResult result,
                out string error))
        {
            ShowFeedback(error);
            return;
        }

        string resultText = result != null
            ? "Formación completada: +" + result.skillGained + " habilidad."
            : "Formación completada.";
        ShowFeedback(resultText);
        screen.Refresh();
        RenderOptions();
    }

    private void RenderOptions()
    {
        if (bindings == null)
        {
            return;
        }

        for (int index = 0; index < bindings.Count; index++)
        {
            BistroBuilderStaffPlayerTrainingButtonBinding binding = bindings[index];
            if (binding == null)
            {
                continue;
            }

            if (binding.label != null)
            {
                binding.label.text =
                    binding.displayName + "  ·  +" + binding.skillGain +
                    (binding.financialCostCents == 0L
                        ? "  ·  Gratis"
                        : "  ·  Requiere coste");
            }
            if (binding.button != null)
            {
                // 4C rechaza de forma autoritativa cualquier formación de pago
                // hasta disponer de gateway financiero atómico. Presentation no
                // intenta cobrar ni crea una economía alternativa.
                binding.button.interactable = binding.financialCostCents == 0L;
            }
        }
    }

    private void Bind()
    {
        Unbind();
        if (openButton != null)
        {
            openButton.onClick.AddListener(Show);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        if (bindings == null)
        {
            return;
        }

        for (int index = 0; index < bindings.Count; index++)
        {
            BistroBuilderStaffPlayerTrainingButtonBinding binding = bindings[index];
            if (binding == null || binding.button == null)
            {
                continue;
            }

            string id = binding.trainingId;
            UnityAction action = () => Train(id);
            binding.button.onClick.AddListener(action);
            boundActions.Add(new BoundAction
            {
                button = binding.button,
                action = action
            });
        }
    }

    private void Unbind()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Show);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }
        for (int index = 0; index < boundActions.Count; index++)
        {
            BoundAction bound = boundActions[index];
            if (bound != null && bound.button != null && bound.action != null)
            {
                bound.button.onClick.RemoveListener(bound.action);
            }
        }
        boundActions.Clear();
    }

    private void SetVisible(bool visible)
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(visible);
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message ?? string.Empty;
        }
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
        if (screen == null) TryGetComponent(out screen);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
