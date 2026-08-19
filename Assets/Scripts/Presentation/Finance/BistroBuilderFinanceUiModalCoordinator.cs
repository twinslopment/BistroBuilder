using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Integra el nuevo acceso global de Finanzas con las UIs jugables anteriores
/// sin modificar sus autoridades. 2.3JKL-B2 nació antes de 3J y no expone una
/// API pública de registro de nuevos modales; este coordinador añade únicamente
/// el acceso FinanceModal/OpenFinance y preserva el estado visible original.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
[AddComponentMenu("Bistro Builder/Finance/Finance UI Modal Coordinator 3J")]
public sealed class BistroBuilderFinanceUiModalCoordinator : MonoBehaviour
{
    private static readonly HashSet<string> ExistingModalNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MenuEditorModal",
            "DishRecipeAuthoringModal",
            "MenuPortfolioModal",
            "InventoryWarehouseModal",
            "SuppliersModal"
        };

    private static readonly HashSet<string> ExistingGlobalButtonNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "OpenMenuEditor",
            "OpenMenuPortfolio",
            "OpenInventoryWarehouse",
            "OpenSuppliers"
        };

    [SerializeField] private BistroBuilderFinanceRuntimeView financeView;

    private readonly Dictionary<GameObject, bool> suppressedExistingStates =
        new Dictionary<GameObject, bool>();

    private Button financeOpenButton;
    private bool financeOpenButtonOriginalState;
    private bool financeOpenButtonSuppressed;

    public BistroBuilderFinanceRuntimeView FinanceView => financeView;
    public bool IsFinanceAccessSuppressed => financeOpenButtonSuppressed;
    public int SuppressedExistingAccessCount => suppressedExistingStates.Count;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveDependencies();
        if (financeView == null)
        {
            return;
        }

        bool financeOpen = financeView.IsOpen;
        bool anotherModalOpen = IsAnotherKnownModalOpen();

        if (financeOpen)
        {
            SuppressExistingGlobalAccess();
        }
        else if (!anotherModalOpen)
        {
            RestoreExistingGlobalAccess();
        }

        if (anotherModalOpen && !financeOpen)
        {
            SuppressFinanceAccess();
        }
        else
        {
            RestoreFinanceAccess();
        }
    }

    private void OnDisable()
    {
        RestoreExistingGlobalAccess();
        RestoreFinanceAccess();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (financeView == null)
        {
            error = "El coordinador 3J necesita BistroBuilderFinanceRuntimeView.";
            return false;
        }
        if (financeOpenButton == null)
        {
            error = "No se encontró el acceso global OpenFinance.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void ApplyVisibilityForTests()
    {
        LateUpdate();
    }

    public bool AreExistingAccessButtonsHiddenForTests()
    {
        foreach (KeyValuePair<GameObject, bool> pair in suppressedExistingStates)
        {
            if (pair.Key != null && pair.Value && pair.Key.activeSelf)
            {
                return false;
            }
        }
        return suppressedExistingStates.Count > 0;
    }

    private void SuppressExistingGlobalAccess()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null ||
                !ExistingGlobalButtonNames.Contains(button.name))
            {
                continue;
            }

            GameObject target = button.gameObject;
            if (!suppressedExistingStates.ContainsKey(target))
            {
                suppressedExistingStates.Add(target, target.activeSelf);
            }
            if (target.activeSelf)
            {
                target.SetActive(false);
            }
        }
    }

    private void RestoreExistingGlobalAccess()
    {
        foreach (KeyValuePair<GameObject, bool> pair in suppressedExistingStates)
        {
            if (pair.Key != null)
            {
                pair.Key.SetActive(pair.Value);
            }
        }
        suppressedExistingStates.Clear();
    }

    private void SuppressFinanceAccess()
    {
        ResolveFinanceButton();
        if (financeOpenButton == null)
        {
            return;
        }
        if (!financeOpenButtonSuppressed)
        {
            financeOpenButtonOriginalState = financeOpenButton.gameObject.activeSelf;
            financeOpenButtonSuppressed = true;
        }
        if (financeOpenButton.gameObject.activeSelf)
        {
            financeOpenButton.gameObject.SetActive(false);
        }
    }

    private void RestoreFinanceAccess()
    {
        if (!financeOpenButtonSuppressed)
        {
            return;
        }
        ResolveFinanceButton();
        if (financeOpenButton != null)
        {
            financeOpenButton.gameObject.SetActive(financeOpenButtonOriginalState);
        }
        financeOpenButtonSuppressed = false;
    }

    private bool IsAnotherKnownModalOpen()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int index = 0; index < rects.Length; index++)
        {
            RectTransform rect = rects[index];
            if (rect != null &&
                ExistingModalNames.Contains(rect.name) &&
                rect.gameObject.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }

    private void ResolveDependencies()
    {
        if (financeView == null)
        {
            financeView = GetComponent<BistroBuilderFinanceRuntimeView>();
        }
        ResolveFinanceButton();
    }

    private void ResolveFinanceButton()
    {
        if (financeOpenButton != null)
        {
            return;
        }
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] != null &&
                string.Equals(
                    buttons[index].name,
                    "OpenFinance",
                    StringComparison.Ordinal))
            {
                financeOpenButton = buttons[index];
                return;
            }
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }

    private void OnValidate()
    {
        ResolveDependencies();
    }
#endif
}
