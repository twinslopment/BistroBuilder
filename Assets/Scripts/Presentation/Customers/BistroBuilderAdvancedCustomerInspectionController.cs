using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 10G. Permite pulsar sobre un miembro visual de CustomerGroup y muestra una
/// ficha compacta que se actualiza en tiempo real mientras permanece abierta.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedCustomerInspectionController : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedCustomerInspectionService inspectionService;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField, Min(0.05f)] private float refreshIntervalSeconds = 0.20f;

    private RectTransform canvasRect;
    private RectTransform panel;
    private Text titleText;
    private Text serviceStateText;
    private Text behaviorText;
    private Text detailsText;
    private Text messageText;
    private Image patienceFill;
    private CustomerGroup selectedGroup;
    private int selectedMemberIndex;
    private BistroBuilderAdvancedCustomerInspectionSnapshot currentSnapshot;
    private float nextRefreshTime;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;
    public string CurrentDisplayName => currentSnapshot?.displayName ?? string.Empty;
    public BistroBuilderAdvancedCustomerInspectionSnapshot CurrentSnapshot =>
        currentSnapshot?.DeepClone();

    private void Awake()
    {
        CacheDependencies();
        EnsureUi();
    }
    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            !IsPointerOverUi())
        {
            Vector2 screenPoint = Mouse.current.position.ReadValue();
            TryInspectAtScreenPoint(screenPoint, out _);
        }

        if (IsOpen && Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
            RefreshSelection();
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        EnsureUi();
        if (inspectionService == null || worldCamera == null || canvas == null)
        {
            error = "10G necesita InspectionService, cámara y Canvas HUD.";
            return false;
        }
        if (!inspectionService.ValidateConfiguration(out error)) return false;
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            error = "El Canvas de 10G necesita GraphicRaycaster.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryInspectForTest(
        CustomerGroup group, int memberIndex, out string error)
    {
        error = string.Empty;
        EnsureUi();
        if (inspectionService == null ||
            !inspectionService.TryBuildSnapshot(group, memberIndex,
                out var snapshot, out error))
            return false;
        selectedGroup = group;
        selectedMemberIndex = memberIndex;
        ApplySnapshot(snapshot);
        ShowPanelAt(new Vector2(Screen.width * 0.68f, Screen.height * 0.62f));
        return true;
    }
    public bool TryInspectAtScreenPoint(Vector2 screenPoint, out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (worldCamera == null || inspectionService == null)
        {
            error = "10G no dispone de cámara o servicio de inspección.";
            return false;
        }

        Ray ray = worldCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(
            ray, 500f, ~0, QueryTriggerInteraction.Collide);
        BistroBuilderAdvancedCustomerMemberHitTarget bestTarget = null;
        float bestScreenDistance = float.PositiveInfinity;
        float bestRayDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var target = hits[i].collider != null
                ? hits[i].collider.GetComponentInParent<
                    BistroBuilderAdvancedCustomerMemberHitTarget>() : null;
            if (target == null || !target.ValidateConfiguration(out _)) continue;
            Vector3 projected = worldCamera.WorldToScreenPoint(target.transform.position);
            if (projected.z <= 0f) continue;
            float screenDistance = Vector2.SqrMagnitude(
                new Vector2(projected.x, projected.y) - screenPoint);
            if (screenDistance < bestScreenDistance - 0.01f ||
                (Mathf.Abs(screenDistance - bestScreenDistance) <= 0.01f &&
                 hits[i].distance < bestRayDistance))
            {
                bestTarget = target;
                bestScreenDistance = screenDistance;
                bestRayDistance = hits[i].distance;
            }
        }
        if (bestTarget != null)
        {
            if (!inspectionService.TryBuildSnapshot(
                    bestTarget.CustomerGroup, bestTarget.MemberIndex,
                    out var snapshot, out error))
                return false;
            selectedGroup = bestTarget.CustomerGroup;
            selectedMemberIndex = bestTarget.MemberIndex;
            ApplySnapshot(snapshot);
            ShowPanelAt(screenPoint);
            return true;
        }
        Close();
        error = "No se ha pulsado sobre un cliente individual.";
        return false;
    }

    public void Close()
    {
        selectedGroup = null;
        selectedMemberIndex = 0;
        currentSnapshot = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }
    private void RefreshSelection()
    {
        if (selectedGroup == null || selectedMemberIndex < 1)
        {
            Close();
            return;
        }
        if (!inspectionService.TryBuildSnapshot(
                selectedGroup, selectedMemberIndex,
                out var snapshot, out _))
        {
            Close();
            return;
        }
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(BistroBuilderAdvancedCustomerInspectionSnapshot snapshot)
    {
        currentSnapshot = snapshot?.DeepClone();
        if (snapshot == null || panel == null) return;
        titleText.text = snapshot.displayName +
            (snapshot.loyaltyTier == BistroBuilderCustomerLoyaltyTier.New
                ? string.Empty : "  ·  " + snapshot.loyaltyLabel);
        serviceStateText.text = snapshot.serviceStateLabel;
        behaviorText.text = snapshot.moodLabel + "  ·  " + snapshot.patienceLabel;
        string satisfaction = snapshot.satisfactionBasisPoints >= 0
            ? "Satisfacción: " + snapshot.satisfactionLabel : "Satisfacción: en curso";
        string profile = string.IsNullOrWhiteSpace(snapshot.archetypeLabel)
            ? string.Empty : snapshot.archetypeLabel + "  ·  ";
        detailsText.text = profile + satisfaction +
            (string.IsNullOrWhiteSpace(snapshot.specialNeedsLabel)
                ? string.Empty : "\n" + snapshot.specialNeedsLabel);
        messageText.text = snapshot.contextualMessage;
        SetPatienceFill(snapshot.remainingPatienceBasisPoints);
    }
    private void EnsureUi()
    {
        if (panel != null) return;
        ResolveCanvas();
        if (canvas == null) return;
        canvasRect = canvas.transform as RectTransform;

        panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_CustomerInspectionCard", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        panel.sizeDelta = new Vector2(360f, 235f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel, BistroBuilderMenuEditorUiFactory.Surface);

        RectTransform border = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Accent", panel, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(4f, 0f));
        BistroBuilderMenuEditorUiFactory.AddImage(
            border, BistroBuilderMenuEditorUiFactory.Accent).raycastTarget = false;

        titleText = CreateCardText("Title", 18, FontStyle.Bold,
            new Vector2(18f, -42f), new Vector2(-42f, -10f));
        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close", panel, "×", Close,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 18);
        SetOffsets(closeButton.transform as RectTransform,
            new Vector2(-38f, -38f), new Vector2(-8f, -8f), true);
        serviceStateText = CreateCardText("ServiceState", 15, FontStyle.Bold,
            new Vector2(18f, -72f), new Vector2(-18f, -46f));
        behaviorText = CreateCardText("Behavior", 14, FontStyle.Bold,
            new Vector2(18f, -100f), new Vector2(-18f, -74f));

        RectTransform patienceBar = BistroBuilderMenuEditorUiFactory.CreateRect(
            "PatienceBar", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(18f, -115f), new Vector2(-18f, -105f));
        BistroBuilderMenuEditorUiFactory.AddImage(
            patienceBar, BistroBuilderMenuEditorUiFactory.SurfaceRaised).raycastTarget = false;
        RectTransform fill = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Fill", patienceBar, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        patienceFill = BistroBuilderMenuEditorUiFactory.AddImage(
            fill, BistroBuilderMenuEditorUiFactory.Positive);
        patienceFill.raycastTarget = false;

        messageText = CreateCardText("Message", 14, FontStyle.Italic,
            new Vector2(18f, -158f), new Vector2(-18f, -120f));
        detailsText = CreateCardText("Details", 12, FontStyle.Normal,
            new Vector2(18f, -218f), new Vector2(-18f, -164f));
        detailsText.color = BistroBuilderMenuEditorUiFactory.TextSecondary;
        panel.gameObject.SetActive(false);
    }

    private Text CreateCardText(
        string name, int fontSize, FontStyle style,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            name, panel, string.Empty, fontSize, TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary, style);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return text;
    }
    private void SetPatienceFill(int remainingBasisPoints)
    {
        if (patienceFill == null) return;
        float amount = Mathf.Clamp01(remainingBasisPoints / 10000f);
        RectTransform rect = patienceFill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(amount, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        patienceFill.color = amount >= 0.7f
            ? BistroBuilderMenuEditorUiFactory.Positive
            : amount >= 0.4f
                ? BistroBuilderMenuEditorUiFactory.Warning
                : BistroBuilderMenuEditorUiFactory.Negative;
    }

    private void ShowPanelAt(Vector2 screenPoint)
    {
        EnsureUi();
        if (panel == null || canvasRect == null) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            localPoint = Vector2.zero;

        Vector2 half = panel.sizeDelta * 0.5f;
        Rect bounds = canvasRect.rect;
        localPoint.x = Mathf.Clamp(localPoint.x + half.x * 0.25f,
            bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f);
        localPoint.y = Mathf.Clamp(localPoint.y + half.y * 0.20f,
            bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f);
        panel.anchoredPosition = localPoint;
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(true);
    }
    private static void SetOffsets(
        RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, bool topRight)
    {
        if (rect == null) return;
        Vector2 anchor = topRight ? Vector2.one : new Vector2(0f, 1f);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new System.Collections.Generic.List<RaycastResult>(8);
        EventSystem.current.RaycastAll(pointer, results);
        return results.Count > 0;
    }

    private void ResolveCanvas()
    {
        if (canvas != null)
        {
            canvasRect = canvas.transform as RectTransform;
            return;
        }
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        float bestArea = -1f;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate == null || !candidate.isActiveAndEnabled ||
                candidate.GetComponent<GraphicRaycaster>() == null) continue;
            RectTransform rect = candidate.transform as RectTransform;
            float area = rect != null ? Mathf.Abs(rect.rect.width * rect.rect.height) : 0f;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }
        canvas = best;
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }
    private void CacheDependencies()
    {
        if (inspectionService == null) TryGetComponent(out inspectionService);
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) worldCamera = FindFirstObjectByType<Camera>();
        ResolveCanvas();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate()
    {
        CacheDependencies();
        refreshIntervalSeconds = Mathf.Max(0.05f, refreshIntervalSeconds);
    }
#endif
}
