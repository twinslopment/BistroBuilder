using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PERSONAL V1 approved chrome. Visual-only extension of the existing 4F
/// screen; never creates a second Staff/Recruitment authority or mutates saves.
/// Applied once when opening an already-installed scene, including old scenes.
/// </summary>
public sealed partial class BistroBuilderStaffPlayerScreen
{
    private static readonly Color StaffIvory = new Color32(249, 239, 221, 255);
    private static readonly Color StaffPaper = new Color32(255, 250, 239, 255);
    private static readonly Color StaffInset = new Color32(239, 225, 202, 255);
    private static readonly Color StaffInk = new Color32(60, 45, 28, 255);
    private static readonly Color StaffMuted = new Color32(114, 91, 64, 255);
    private static readonly Color StaffBrass = new Color32(176, 131, 72, 255);
    private static readonly Color StaffDanger = new Color32(145, 58, 50, 255);
    private static readonly Color StaffSuccess = new Color32(52, 103, 67, 255);

    private readonly List<GameObject> departmentHeadings = new List<GameObject>();
    private readonly List<Button> candidateRoleFilters = new List<Button>();
    private readonly Dictionary<string, GameObject> employeeRowsById =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> candidateRowsById =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> candidateRolesById =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private string selectedCandidateRole = string.Empty;
    private string pendingTargetId = string.Empty;
    private GameObject confirmationBlocker;
    private bool approvedPresentationReady;

    private void EnsureApprovedPresentation()
    {
        if (approvedPresentationReady || panelRoot == null ||
            staffPanel == null || candidatesPanel == null ||
            employeeNameText == null || candidateNameText == null)
            return;

        approvedPresentationReady = true;
        if (panelRoot.GetComponent<BistroBuilderUiStyleIsolation>() == null)
            panelRoot.AddComponent<BistroBuilderUiStyleIsolation>();

        Image rootImage = panelRoot.GetComponent<Image>();
        if (rootImage != null) rootImage.color = StaffIvory;
        CreateApprovedFrame(panelRoot.transform, "ApprovedIvoryBrassFrame");

        ApplyApprovedRect(headerSummaryText.rectTransform, .28f, .932f, .80f, .981f);
        ApplyApprovedRect(feedbackText.rectTransform, .38f, .868f, .82f, .918f);
        RectTransform title = panelRoot.transform.Find("Title") as RectTransform;
        if (title != null) ApplyApprovedRect(title, .035f, .927f, .255f, .987f);
        ApplyApprovedRect(closeButton.transform as RectTransform, .875f, .927f, .970f, .980f);
        ApplyApprovedRect(staffTabButton.transform as RectTransform, .035f, .867f, .172f, .918f);
        ApplyApprovedRect(candidatesTabButton.transform as RectTransform, .183f, .867f, .326f, .918f);
        ApplyApprovedRect(staffPanel.transform as RectTransform, .025f, .048f, .975f, .846f);
        ApplyApprovedRect(candidatesPanel.transform as RectTransform, .025f, .048f, .975f, .846f);

        StyleApprovedPanel(staffPanel, StaffIvory);
        StyleApprovedPanel(candidatesPanel, StaffIvory);
        ApplyApprovedRosterLayout(staffPanel, employeeListContent, "EmployeeDetail");
        ApplyApprovedRosterLayout(candidatesPanel, candidateListContent, "CandidateDetail");

        Transform staffDetail = employeeNameText.transform.parent;
        Transform candidateDetail = candidateNameText.transform.parent;
        StyleApprovedPanel(staffDetail.gameObject, StaffPaper);
        StyleApprovedPanel(candidateDetail.gameObject, StaffPaper);

        // Fixed header, two informational columns, two secondary cards and
        // a pinned footer. No absolute pixel widths or off-screen actions.
        CreateApprovedCard(staffDetail, "InformationCard", .025f, .395f, .492f, .822f,
            "INFORMACIÓN GENERAL");
        CreateApprovedCard(staffDetail, "AssignmentCard", .505f, .395f, .975f, .822f,
            "ROL Y ASIGNACIÓN");
        CreateApprovedCard(staffDetail, "PerformanceCard", .025f, .136f, .492f, .378f,
            "RENDIMIENTO RECIENTE");
        CreateApprovedCard(staffDetail, "TrainingCard", .505f, .136f, .975f, .378f,
            "FORMACIÓN");

        ApplyApprovedRect(employeeNameText.rectTransform, .045f, .898f, .78f, .972f);
        ApplyApprovedRect(employeeRoleText.rectTransform, .047f, .845f, .80f, .902f);
        ApplyApprovedRect(employeeContractText.rectTransform, .052f, .695f, .465f, .762f);
        ApplyApprovedRect(employeeProgressText.rectTransform, .052f, .616f, .465f, .684f);
        ApplyApprovedRect(employeeSkillsText.rectTransform, .052f, .445f, .463f, .610f);
        ApplyApprovedRect(employeeSessionText.rectTransform, .536f, .512f, .950f, .755f);
        ApplyApprovedRect(employeePerformanceText.rectTransform, .052f, .180f, .462f, .310f);
        employeeSkillsText.fontSize = 15f;
        employeeSkillsText.enableWordWrapping = true;
        employeeSessionText.fontSize = 17f;
        employeeSessionText.enableWordWrapping = true;
        employeePerformanceText.fontSize = 15f;
        employeePerformanceText.enableWordWrapping = true;

        ApplyApprovedRect(toggleAvailabilityButton.transform as RectTransform,
            .028f, .020f, .328f, .110f);
        ApplyApprovedRect(dismissButton.transform as RectTransform,
            .690f, .020f, .973f, .110f);

        // 4F Training installer owns this button and the real modal.
        Transform training = staffDetail.Find("Training");
        if (training is RectTransform trainingRect)
            ApplyApprovedRect(trainingRect, .365f, .020f, .650f, .110f);

        CreateApprovedCard(candidateDetail, "OfferCard", .025f, .400f, .975f, .822f,
            "PERFIL DEL CANDIDATO");
        CreateApprovedCard(candidateDetail, "OfferSkillsCard", .025f, .140f, .975f, .380f,
            "HABILIDADES");
        ApplyApprovedRect(candidateNameText.rectTransform, .045f, .895f, .92f, .974f);
        ApplyApprovedRect(candidateRoleText.rectTransform, .047f, .838f, .92f, .895f);
        ApplyApprovedRect(candidateProfileText.rectTransform, .06f, .700f, .92f, .770f);
        ApplyApprovedRect(candidateSalaryText.rectTransform, .06f, .538f, .92f, .624f);
        ApplyApprovedRect(candidateSkillsText.rectTransform, .06f, .207f, .93f, .322f);
        candidateSkillsText.enableWordWrapping = true;
        ApplyApprovedRect(hireButton.transform as RectTransform,
            .025f, .020f, .487f, .115f);
        ApplyApprovedRect(refreshCandidatesButton.transform as RectTransform,
            .507f, .020f, .975f, .115f);

        // A dedicated, raycast-blocking layer makes the confirmation modal
        // truly modal. It is a sibling behind the existing 4F confirmation.
        RectTransform blocker = NewApprovedRect("ConfirmationBlocker", panelRoot.transform);
        ApplyApprovedRect(blocker, 0f, 0f, 1f, 1f);
        Image dim = blocker.gameObject.AddComponent<Image>();
        dim.color = new Color32(24, 18, 11, 159);
        dim.raycastTarget = true;
        confirmationBlocker = blocker.gameObject;
        confirmationBlocker.transform.SetSiblingIndex(
            confirmationPanel.transform.GetSiblingIndex());
        confirmationBlocker.SetActive(false);

        StyleApprovedPanel(confirmationPanel, StaffPaper);
        RectTransform modal = confirmationPanel.transform as RectTransform;
        ApplyApprovedRect(modal, .285f, .335f, .715f, .665f);
        CreateApprovedFrame(confirmationPanel.transform, "ModalBrassFrame");
        ApplyApprovedRect(confirmationText.rectTransform, .085f, .35f, .915f, .78f);
        confirmationText.alignment = TextAlignmentOptions.Center;
        confirmationText.enableWordWrapping = true;
        ApplyApprovedRect(confirmationCancelButton.transform as RectTransform,
            .09f, .09f, .475f, .28f);
        ApplyApprovedRect(confirmationAcceptButton.transform as RectTransform,
            .525f, .09f, .91f, .28f);
        CreateApprovedLabel(confirmationPanel.transform, "ModalTitle",
            "CONFIRMAR ACCIÓN", .08f, .80f, .92f, .95f, 18f);

        // The TrainingModal, when installed, is a child of the same screen;
        // visual skin is applied without taking over its command bindings.
        Transform trainingModal = panelRoot.transform.Find("TrainingModal");
        if (trainingModal != null)
        {
            StyleApprovedPanel(trainingModal.gameObject, StaffPaper);
            CreateApprovedFrame(trainingModal, "TrainingBrassFrame");
        }

        TMP_Text[] texts = panelRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;
            TMP_FontAsset asset =
                (text.fontSize >= 21f
                    ? BistroBuilderTypography.Emphasis
                    : BistroBuilderTypography.Body);
            if (asset != null) text.font = asset;
            text.color = StaffInk;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        headerSummaryText.color = StaffMuted;
        feedbackText.color = StaffDanger;
        if (title != null)
        {
            TMP_Text heading = title.GetComponent<TMP_Text>();
            if (heading != null) heading.color = StaffInk;
        }

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            ApplyApprovedButtonStyle(buttons[i],
                buttons[i] == dismissButton ? StaffDanger : StaffInset);
        }
        ApplyApprovedTabState();
    }

    private static void ApplyApprovedRosterLayout(GameObject parent,
        RectTransform listContent, string detailName)
    {
        if (parent == null || listContent == null) return;
        RectTransform list = listContent.parent != null
            ? listContent.parent.parent as RectTransform : null;
        RectTransform detail = parent.transform.Find(detailName) as RectTransform;
        if (list != null)
        {
            ApplyApprovedRect(list, 0f, 0f, .445f, 1f);
            StyleApprovedPanel(list.gameObject, StaffPaper);
        }
        if (detail != null)
            ApplyApprovedRect(detail, .459f, 0f, 1f, 1f);
    }

    private static void CreateApprovedFrame(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return;
        RectTransform rect = NewApprovedRect(name, parent);
        ApplyApprovedRect(rect, .004f, .007f, .996f, .993f);
        BistroBuilderTopBarPlate plate =
            rect.gameObject.AddComponent<BistroBuilderTopBarPlate>();
        plate.Cell = false;
        plate.raycastTarget = false;
        rect.SetAsFirstSibling();
    }

    private static RectTransform NewApprovedRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void ApplyApprovedRect(
        RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StyleApprovedPanel(GameObject go, Color baseColor)
    {
        if (go == null) return;
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = baseColor;
        Outline outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = StaffBrass;
        outline.effectDistance = new Vector2(1.15f, -1.15f);
        outline.useGraphicAlpha = false;
    }

    private static void CreateApprovedCard(Transform parent, string name,
        float minX, float minY, float maxX, float maxY, string title)
    {
        RectTransform card = NewApprovedRect(name, parent);
        ApplyApprovedRect(card, minX, minY, maxX, maxY);
        StyleApprovedPanel(card.gameObject, StaffPaper);
        card.SetAsFirstSibling();
        CreateApprovedLabel(parent, name + "Title", title,
            minX + .024f, maxY - .059f, maxX - .02f, maxY - .008f, 14f);
    }

    private static TMP_Text CreateApprovedLabel(
        Transform parent, string name, string value,
        float minX, float minY, float maxX, float maxY, float size)
    {
        RectTransform r = NewApprovedRect(name, parent);
        ApplyApprovedRect(r, minX, minY, maxX, maxY);
        TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.color = StaffMuted;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        TMP_FontAsset font = BistroBuilderTypography.Body;
        if (font != null) text.font = font;
        return text;
    }

    private static void ApplyApprovedButtonStyle(Button button, Color baseColor)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
            Outline outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = baseColor == StaffDanger ? StaffDanger : StaffBrass;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .975f, .91f, 1f);
        colors.pressedColor = new Color(.88f, .82f, .72f, 1f);
        colors.selectedColor = new Color(1f, .97f, .88f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, .38f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .16f;
        button.colors = colors;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = baseColor == StaffDanger ? StaffPaper : StaffInk;
            if (BistroBuilderTypography.Body != null)
                label.font = BistroBuilderTypography.Body;
        }
    }

    private void ApplyApprovedTabState()
    {
        if (!approvedPresentationReady) return;
        Image staffImage = staffTabButton != null
            ? staffTabButton.GetComponent<Image>() : null;
        Image candidatesImage = candidatesTabButton != null
            ? candidatesTabButton.GetComponent<Image>() : null;
        if (staffImage != null)
            staffImage.color = viewMode == ViewMode.Staff ? StaffBrass : StaffInset;
        if (candidatesImage != null)
            candidatesImage.color = viewMode == ViewMode.Candidates ? StaffBrass : StaffInset;
    }

    private void ApplyApprovedRowStyle(GameObject row, bool selected)
    {
        if (row == null) return;
        StyleApprovedPanel(row, selected ? StaffInset : StaffPaper);
        Button button = row.GetComponent<Button>();
        if (button != null) ApplyApprovedButtonStyle(
            button, selected ? StaffInset : StaffPaper);
        TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].color = labels[i].name == "Status" ? StaffSuccess : StaffInk;
            if (BistroBuilderTypography.Body != null)
                labels[i].font = BistroBuilderTypography.Body;
        }
    }

    private GameObject CreateApprovedDepartmentHeading(
        string department, int count)
    {
        RectTransform header = NewApprovedRect(
            "Department_" + department, employeeListContent);
        LayoutElement element = header.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 37f;
        element.preferredHeight = 37f;
        StyleApprovedPanel(header.gameObject, StaffInset);
        TextMeshProUGUI title = CreateApprovedLabel(header, "Label",
            department + "   ·   " + count + " empleado" + (count == 1 ? "" : "s"),
            .03f, 0f, .97f, 1f, 14f) as TextMeshProUGUI;
        if (title != null) title.color = StaffInk;
        departmentHeadings.Add(header.gameObject);
        return header.gameObject;
    }

    private void ClearApprovedRowsAndFilters()
    {
        for (int i = 0; i < departmentHeadings.Count; i++)
            if (departmentHeadings[i] != null)
            {
                departmentHeadings[i].SetActive(false);
                Destroy(departmentHeadings[i]);
            }
        departmentHeadings.Clear();
        employeeRowsById.Clear();
        candidateRowsById.Clear();
        candidateRolesById.Clear();
        for (int i = 0; i < candidateRoleFilters.Count; i++)
            if (candidateRoleFilters[i] != null)
            {
                candidateRoleFilters[i].gameObject.SetActive(false);
                Destroy(candidateRoleFilters[i].gameObject);
            }
        candidateRoleFilters.Clear();
    }

    private void RebuildApprovedCandidateFilters()
    {
        if (!approvedPresentationReady || currentSnapshot == null ||
            candidatesPanel == null) return;

        RectTransform list = candidateListContent.parent != null
            ? candidateListContent.parent.parent as RectTransform : null;
        if (list != null) ApplyApprovedRect(list, 0f, 0f, .445f, .895f);

        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < currentSnapshot.candidates.Count; i++)
        {
            BistroBuilderStaffPlayerCandidateRow row = currentSnapshot.candidates[i];
            if (row != null && !names.ContainsKey(row.roleId))
                names.Add(row.roleId, row.roleDisplayName);
        }
        if (selectedCandidateRole.Length > 0 &&
            !names.ContainsKey(selectedCandidateRole))
            selectedCandidateRole = string.Empty;

        int slots = names.Count + 1;
        CreateApprovedFilter(string.Empty, "Todos",
            0f, 1f / slots);
        int index = 1;
        foreach (KeyValuePair<string, string> item in names)
        {
            string roleId = item.Key;
            CreateApprovedFilter(roleId, item.Value,
                (float)index / slots, (float)(index + 1) / slots);
            index++;
        }
        ApplyCandidateRoleFilter();
    }

    private void CreateApprovedFilter(
        string roleId, string label, float minX, float maxX)
    {
        RectTransform rect =
            NewApprovedRect("Filter_" + (roleId.Length == 0 ? "All" : roleId),
                candidatesPanel.transform);
        ApplyApprovedRect(rect, Mathf.Lerp(0f, .445f, minX) + .002f,
            .907f, Mathf.Lerp(0f, .445f, maxX) - .002f, .982f);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            selectedCandidateRole = roleId;
            ApplyCandidateRoleFilter();
        });
        CreateApprovedLabel(rect, "Label", label, .025f, 0f, .975f, 1f, 13f);
        ApplyApprovedButtonStyle(button,
            selectedCandidateRole == roleId ? StaffBrass : StaffInset);
        candidateRoleFilters.Add(button);
    }

    private void ApplyCandidateRoleFilter()
    {
        if (currentSnapshot == null) return;
        string firstVisible = string.Empty;
        bool selectedVisible = false;
        for (int i = 0; i < currentSnapshot.candidates.Count; i++)
        {
            BistroBuilderStaffPlayerCandidateRow source = currentSnapshot.candidates[i];
            if (source == null) continue;
            bool visible = selectedCandidateRole.Length == 0 ||
                string.Equals(source.roleId, selectedCandidateRole, StringComparison.Ordinal);
            if (candidateRowsById.TryGetValue(source.candidateId, out GameObject row))
                row.SetActive(visible);
            if (visible)
            {
                if (firstVisible.Length == 0) firstVisible = source.candidateId;
                if (source.candidateId == selectedCandidateId) selectedVisible = true;
            }
        }
        if (!selectedVisible) selectedCandidateId = firstVisible;
        for (int i = 0; i < candidateRoleFilters.Count; i++)
        {
            Button button = candidateRoleFilters[i];
            if (button == null) continue;
            string role = button.gameObject.name.Substring("Filter_".Length);
            bool active = selectedCandidateRole.Length == 0
                ? role == "All" : role == selectedCandidateRole;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = active ? StaffBrass : StaffInset;
        }
        RenderSelectedCandidate();
        UpdateApprovedRowSelection();
    }

    private void UpdateApprovedRowSelection()
    {
        foreach (KeyValuePair<string, GameObject> item in employeeRowsById)
            ApplyApprovedRowStyle(item.Value, item.Key == selectedEmployeeId);
        foreach (KeyValuePair<string, GameObject> item in candidateRowsById)
            ApplyApprovedRowStyle(item.Value, item.Key == selectedCandidateId);
    }

    private void RefreshApprovedConfirmation(bool visible)
    {
        if (!approvedPresentationReady) return;
        if (confirmationBlocker != null)
        {
            confirmationBlocker.SetActive(visible);
            if (visible)
                confirmationBlocker.transform.SetSiblingIndex(
                    confirmationPanel.transform.GetSiblingIndex());
        }
        if (visible && confirmationPanel != null)
        {
            confirmationPanel.transform.SetAsLastSibling();
            TMP_Text title =
                confirmationPanel.transform.Find("ModalTitle")?.GetComponent<TMP_Text>();
            TMP_Text accept =
                confirmationAcceptButton.GetComponentInChildren<TMP_Text>(true);
            if (title != null)
                title.text = pendingConfirmation == PendingConfirmation.Hire
                    ? "CONFIRMAR CONTRATACIÓN" : "CONFIRMAR DESPIDO";
            if (accept != null)
                accept.text = pendingConfirmation == PendingConfirmation.Hire
                    ? "Sí, contratar" : "Sí, despedir";
            ApplyApprovedButtonStyle(confirmationAcceptButton,
                pendingConfirmation == PendingConfirmation.Dismiss
                    ? StaffDanger : StaffInset);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(
                    confirmationCancelButton.gameObject);
        }
    }
}
