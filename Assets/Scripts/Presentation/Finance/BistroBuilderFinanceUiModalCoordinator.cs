using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Integra el nuevo acceso global de Finanzas con las UIs jugables anteriores
/// sin modificar sus autoridades. 2.3JKL-B2 nació antes de 3J y no expone una
/// API pública de registro de nuevos modales; este coordinador añade únicamente
/// el acceso FinanceModal/OpenFinance y preserva el estado visible original.
///
/// Las referencias estables se cachean: LateUpdate no recorre la jerarquía HUD
/// ni genera arrays por frame.
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
            "SuppliersModal",
            "ReservationsPanel",
            "SchedulePanel",
            "StaffPanel",
            "TrainingModal"
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
    private readonly List<RectTransform> existingModalRoots =
        new List<RectTransform>(8);
    private readonly List<Button> existingGlobalButtons =
        new List<Button>(8);

    private Canvas canvas;
    private Button financeOpenButton;
    private bool referencesCached;
    private bool financeOpenButtonOriginalState;
    private bool financeOpenButtonSuppressed;

    public BistroBuilderFinanceRuntimeView FinanceView => financeView;
    public bool IsFinanceAccessSuppressed => financeOpenButtonSuppressed;
    public int SuppressedExistingAccessCount => suppressedExistingStates.Count;

    private void Awake()
    {
        ResolveDependencies();
        CacheStableReferences();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        CacheStableReferences();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveDependencies();
        if (!referencesCached)
        {
            CacheStableReferences();
        }
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
        CacheStableReferences();
        if (financeView == null)
        {
            error = "El coordinador 3J necesita BistroBuilderFinanceRuntimeView.";
            return false;
        }
        if (canvas == null)
        {
            error = "El coordinador 3J debe vivir bajo el Canvas HUD canónico.";
            return false;
        }
        if (financeOpenButton == null)
        {
            error = "No se encontró el acceso global OpenFinance.";
            return false;
        }
        if (existingGlobalButtons.Count == 0)
        {
            error = "No se localizaron accesos globales previos que coordinar.";
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
        for (int index = 0; index < existingGlobalButtons.Count; index++)
        {
            Button button = existingGlobalButtons[index];
            if (button == null || button.gameObject.scene != gameObject.scene)
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
        if (financeOpenButton != null)
        {
            financeOpenButton.gameObject.SetActive(financeOpenButtonOriginalState);
        }
        financeOpenButtonSuppressed = false;
    }

    private bool IsAnotherKnownModalOpen()
    {
        for (int index = 0; index < existingModalRoots.Count; index++)
        {
            RectTransform modal = existingModalRoots[index];
            if (modal != null && modal.gameObject.activeInHierarchy)
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
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
    }

    private void CacheStableReferences()
    {
        ResolveDependencies();
        referencesCached = false;
        financeOpenButton = null;
        existingModalRoots.Clear();
        existingGlobalButtons.Clear();

        if (canvas == null)
        {
            return;
        }

        RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < rects.Length; index++)
        {
            RectTransform rect = rects[index];
            if (rect != null &&
                rect.gameObject.scene == gameObject.scene &&
                ExistingModalNames.Contains(rect.name))
            {
                existingModalRoots.Add(rect);
            }
        }

        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null || button.gameObject.scene != gameObject.scene)
            {
                continue;
            }
            if (string.Equals(
                    button.name,
                    "OpenFinance",
                    StringComparison.Ordinal))
            {
                financeOpenButton = button;
            }
            else if (ExistingGlobalButtonNames.Contains(button.name) ||
                     button.name.StartsWith("Open", StringComparison.Ordinal))
            {
                // Regla extensible: los módulos posteriores pueden publicar un
                // acceso global Open* sin obligar a modificar Finanzas.
                existingGlobalButtons.Add(button);
            }
        }

        referencesCached = financeOpenButton != null;
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
