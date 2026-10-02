using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BistroBuilder.UI.Iconography;

/// <summary>
/// Approved 01/10/2026 PERSONAL plate: faithful left SALA/COCINA tables and
/// right portrait/profile/XP/skill bars. Adds only presentation on top of the
/// 4F serialized scene; Staff/Recruitment/Development keep all authority.
/// </summary>
public sealed partial class BistroBuilderStaffPlayerScreen
{
    private readonly List<GameObject> departmentHeadings = new List<GameObject>(8);
    private readonly List<Button> candidateRoleFilters = new List<Button>(8);
    private readonly Dictionary<Button, string> candidateFilterIds =
        new Dictionary<Button, string>();
    private readonly Dictionary<string, GameObject> employeeRowsById =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> candidateRowsById =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);

    private readonly Image[] employeeSkillBars = new Image[4];
    private readonly TMP_Text[] employeeSkillValues = new TMP_Text[4];
    private readonly Image[] candidateSkillBars = new Image[4];
    private readonly TMP_Text[] candidateSkillValues = new TMP_Text[4];
    private readonly TMP_Text[] employeeInfo = new TMP_Text[4];
    private readonly TMP_Text[] candidateInfo = new TMP_Text[3];
    private TMP_Text employeeXpLabel, employeePerformanceLabel;
    private Image employeeXpFill, employeePortrait, candidatePortrait;
    private TMP_Text employeePortraitCaption, candidatePortraitCaption;
    private Button scheduleShortcut, approvedScheduleTab, approvedViewSwitcher;
    private RectTransform approvedViewMenu;
    private bool approvedViewMenuOpen;
    private RectTransform employeeDetail, candidateDetail;
    private GameObject confirmationBlocker;
    private bool approvedPresentationReady;
    private string selectedCandidateRole = string.Empty;
    private string pendingTargetId = string.Empty;

    public void OnCancel(BaseEventData eventData)
    {
        if (pendingConfirmation == PendingConfirmation.None) return;
        CancelConfirmation();
        if (eventData != null) eventData.Use();
    }

    private void EnsureApprovedPresentation()
    {
        if (approvedPresentationReady || panelRoot == null || staffPanel == null ||
            candidatesPanel == null || employeeNameText == null ||
            candidateNameText == null) return;
        approvedPresentationReady = true;
        var v = typeof(BistroBuilderStaffVisuals);
        if (panelRoot.GetComponent<BistroBuilderUiStyleIsolation>() == null)
            panelRoot.AddComponent<BistroBuilderUiStyleIsolation>();

        Image original = panelRoot.GetComponent<Image>();
        if (original != null) original.color = BistroBuilderStaffVisuals.Ivory;
        BistroBuilderStaffVisuals.Frame(panelRoot.transform, "ApprovedPersonalFrame");
        // Leave the complete raised frame visible; the former full-bleed paper
        // hid its brass bevel and made the approved screen look like a flat form.
        RectTransform paper = BistroBuilderStaffVisuals.Panel("ApprovedPersonalPaper",
            panelRoot.transform, .014f, .019f, .986f, .982f,
            BistroBuilderStaffVisuals.Ivory, false);
        paper.SetSiblingIndex(1);
        BistroBuilderStaffVisuals.Panel("PersonalTopGoldRail", panelRoot.transform,
            .017f, .979f, .982f, .982f,
            BistroBuilderStaffVisuals.Brass, false);
        BistroBuilderStaffVisuals.Panel("PersonalBottomGoldRail", panelRoot.transform,
            .017f, .019f, .982f, .022f,
            BistroBuilderStaffVisuals.Brass, false);
        foreach (var corner in new[]
        {
            new Vector2(.012f,.964f), new Vector2(.979f,.964f),
            new Vector2(.012f,.029f), new Vector2(.979f,.029f)
        })
            BistroBuilderStaffVisuals.Panel("BrassRivet", panelRoot.transform,
                corner.x, corner.y, corner.x + .006f, corner.y + .011f,
                BistroBuilderStaffVisuals.Brass, false);

        var head = panelRoot.transform.Find("Title") as RectTransform;
        BistroBuilderStaffVisuals.Place(head, .078f, .908f, .325f, .984f);
        if (head != null)
        {
            BistroBuilderStaffVisuals.TitleStyle(
                head.GetComponent<TMP_Text>(), 39f);
            // Match the softly embossed reference title without touching the
            // global shell's type hierarchy.
            Shadow titleShadow = head.GetComponent<Shadow>();
            if (titleShadow == null)
                titleShadow = head.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color32(70, 43, 19, 98);
            titleShadow.effectDistance = new Vector2(1.2f, -1.35f);
            titleShadow.useGraphicAlpha = true;
        }
        var staffIcon = BistroBuilderStaffVisuals.Node("PersonalHeaderIcon",
            panelRoot.transform, .039f, .914f, .074f, .982f);
        Image nav = staffIcon.gameObject.AddComponent<Image>();
        nav.sprite = BistroBuilderStaffVisuals.RoleIcon(string.Empty);
        nav.preserveAspect = true;
        nav.color = BistroBuilderStaffVisuals.Ink;
        nav.raycastTarget = false;
        RectTransform taglineCard = BistroBuilderStaffVisuals.Panel(
            "ApprovedPersonalTaglineCard", panelRoot.transform,
            .621f, .918f, .956f, .981f,
            BistroBuilderStaffVisuals.Inset);
        BistroBuilderStaffVisuals.Label("ApprovedPersonalTagline", taglineCard,
            "Contrata, asigna y mejora a tu equipo para ofrecer la mejor experiencia.",
            13.5f, .019f, .04f, .981f, .96f, true,
            TextAlignmentOptions.Center);
        // The approved reference has a single clean header. The dynamic count
        // remains in the snapshot, but is presented in its actual context
        // (Sala/Cocina and Horarios), never as another header line.
        headerSummaryText.gameObject.SetActive(false);
        BistroBuilderStaffVisuals.Separator("HeaderGoldRule", panelRoot.transform,
            .025f, .907f, .975f);
        BistroBuilderStaffVisuals.Place(closeButton.transform as RectTransform,
            .954f, .949f, .980f, .984f);
        BistroBuilderStaffVisuals.ButtonStyle(closeButton);
        TMP_Text closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeLabel != null) closeLabel.text = "×";

        // The supplied reference has NO permanent tabs. Keep the three existing
        // actions and reveal them only from the PERSONAL title. This retains
        // the canonical 4F/5E routing without stealing a whole horizontal row.
        approvedViewMenu = BistroBuilderStaffVisuals.Panel(
            "ApprovedViewMenu", panelRoot.transform,
            .025f, .831f, .440f, .905f, BistroBuilderStaffVisuals.Paper);
        BistroBuilderStaffVisuals.Place(staffTabButton.transform as RectTransform,
            .031f, .842f, .158f, .896f);
        BistroBuilderStaffVisuals.Place(candidatesTabButton.transform as RectTransform,
            .164f, .842f, .291f, .896f);
        BistroBuilderStaffVisuals.ButtonStyle(staffTabButton);
        BistroBuilderStaffVisuals.ButtonStyle(candidatesTabButton);
        approvedScheduleTab = BistroBuilderStaffVisuals.NewButton(
            "ApprovedScheduleTab", panelRoot.transform, "Horarios",
            .297f, .842f, .432f, .896f);
        approvedScheduleTab.onClick.AddListener(() =>
        {
            SetApprovedViewMenuOpen(false);
            OpenApprovedSchedule();
        });
        staffTabButton.onClick.AddListener(() => SetApprovedViewMenuOpen(false));
        candidatesTabButton.onClick.AddListener(() => SetApprovedViewMenuOpen(false));
        RectTransform switcher = BistroBuilderStaffVisuals.Node(
            "ApprovedViewSwitcher", panelRoot.transform,
            .025f, .908f, .332f, .983f);
        Image switcherHit = switcher.gameObject.AddComponent<Image>();
        switcherHit.color = new Color(1f, 1f, 1f, .001f);
        switcherHit.raycastTarget = true;
        approvedViewSwitcher = switcher.gameObject.AddComponent<Button>();
        approvedViewSwitcher.targetGraphic = switcherHit;
        approvedViewSwitcher.transition = Selectable.Transition.None;
        approvedViewSwitcher.onClick.AddListener(ToggleApprovedViewMenu);
        SetApprovedViewMenuOpen(false);
        BistroBuilderStaffVisuals.Place(feedbackText.rectTransform,
            .435f, .900f, .950f, .920f);
        BistroBuilderStaffVisuals.TextStyle(feedbackText, 13f, false,
            TextAlignmentOptions.Right, BistroBuilderStaffVisuals.Danger);

        // V5 reference: content begins immediately below PERSONAL, with no
        // permanently visible navigation strip consuming a table row.
        BistroBuilderStaffVisuals.Place(staffPanel.transform as RectTransform,
            .021f, .025f, .979f, .896f);
        BistroBuilderStaffVisuals.Place(candidatesPanel.transform as RectTransform,
            .021f, .025f, .979f, .896f);
        Image staffBg = staffPanel.GetComponent<Image>();
        if (staffBg == null) staffBg = staffPanel.AddComponent<Image>();
        staffBg.color = BistroBuilderStaffVisuals.Ivory;
        staffBg.raycastTarget = false;
        Image candidateBg = candidatesPanel.GetComponent<Image>();
        if (candidateBg == null) candidateBg = candidatesPanel.AddComponent<Image>();
        candidateBg.color = BistroBuilderStaffVisuals.Ivory;
        candidateBg.raycastTarget = false;

        // Match the reference: grouped table occupies 56%; right inspector 43%.
        // Compact table + separate informational footer; the old scroll filled
        // the whole plate and cut the final hiring row at lower resolutions.
        StyleApprovedScroll(employeeListContent, .000f, .069f, .559f, .991f,
            staffPanel.transform);
        RectTransform staffFoot = BistroBuilderStaffVisuals.Panel(
            "ApprovedStaffFooter", staffPanel.transform,
            .006f, .006f, .559f, .063f, BistroBuilderStaffVisuals.Inset);
        BistroBuilderStaffVisuals.Label("ApprovedStaffFooterText", staffFoot,
            "Los cocineros aparecen en Cocina durante el servicio; consulta sus turnos en Horarios.",
            BistroBuilderStaffVisuals.CaptionSize, .018f, .08f, .982f, .92f);
        StyleApprovedScroll(candidateListContent, .006f, .006f, .559f, .856f,
            candidatesPanel.transform);
        employeeDetail = employeeNameText.transform.parent as RectTransform;
        candidateDetail = candidateNameText.transform.parent as RectTransform;
        ConfigureInspector(employeeDetail, .568f, .006f, .996f, .991f);
        ConfigureInspector(candidateDetail, .568f, .006f, .996f, .991f);
        ConfigureEmployeeInspector();
        ConfigureCandidateInspector();
        ConfigureConfirmation();
        ConfigureExistingTraining();

        // Do not let the global icon decorator obscure the approved labels.
        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Transform injected = buttons[i] != null
                ? buttons[i].transform.Find("BB_Icon21B") : null;
            if (injected != null) injected.gameObject.SetActive(false);
        }
        ApplyApprovedTabState();
    }

    private static void StyleApprovedScroll(RectTransform content,
        float x0, float y0, float x1, float y1, Transform expectedParent)
    {
        if (content == null || content.parent == null ||
            content.parent.parent == null) return;
        RectTransform scroll = content.parent.parent as RectTransform;
        if (scroll == null || scroll.parent != expectedParent) return;
        BistroBuilderStaffVisuals.Place(scroll, x0, y0, x1, y1);
        Image bg = scroll.GetComponent<Image>();
        if (bg == null) bg = scroll.gameObject.AddComponent<Image>();
        // Stand-alone Sala/Cocina cards, not a third bulky frame around them.
        bg.color = Color.clear;
        bg.raycastTarget = false;
        Outline outline = scroll.GetComponent<Outline>();
        if (outline == null) outline = scroll.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.clear;
        outline.effectDistance = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = 0f;
            layout.padding = new RectOffset(1, 1, 0, 0);
            layout.childForceExpandHeight = false;
        }
        ScrollRect component = scroll.GetComponent<ScrollRect>();
        if (component != null) component.scrollSensitivity = 24f;
    }

    private static void ConfigureInspector(RectTransform detail,
        float x0, float y0, float x1, float y1)
    {
        if (detail == null) return;
        BistroBuilderStaffVisuals.Place(detail, x0, y0, x1, y1);
        Image bg = detail.GetComponent<Image>();
        if (bg == null) bg = detail.gameObject.AddComponent<Image>();
        bg.color = BistroBuilderStaffVisuals.Paper;
        bg.raycastTarget = false;
        // Draw the shared authored plate behind every inspector child; no
        // opaque overlay and no duplicate UI authority.
        if (detail.Find("ApprovedInspectorBevel") == null)
            BistroBuilderStaffVisuals.Frame(detail, "ApprovedInspectorBevel");
        Outline line = detail.GetComponent<Outline>();
        if (line == null) line = detail.gameObject.AddComponent<Outline>();
        line.effectColor = BistroBuilderStaffVisuals.Border;
        line.effectDistance = new Vector2(1.25f, -1.25f);
        BistroBuilderStaffVisuals.Separator("InspectorTopRule", detail,
            .015f, .977f, .985f);
    }

    private static void SetInactive(TMP_Text text)
    {
        if (text != null) text.gameObject.SetActive(false);
    }

    private void ConfigureEmployeeInspector()
    {
        if (employeeDetail == null) return;
        BistroBuilderStaffVisuals.PortraitFrame("HeroPortrait", employeeDetail,
            .027f, .597f, .326f, .965f, string.Empty, string.Empty, true);
        employeePortrait = employeeDetail.Find("HeroPortrait/PortraitMat/Portrait")
            ?.GetComponent<Image>();
        employeePortraitCaption = employeeDetail.Find(
            "HeroPortrait/PortraitMat/PortraitRole")?.GetComponent<TMP_Text>();
        BistroBuilderStaffVisuals.Place(employeeNameText.rectTransform,
            .358f, .865f, .960f, .963f);
        BistroBuilderStaffVisuals.TitleStyle(employeeNameText, 28f);
        BistroBuilderStaffVisuals.Place(employeeRoleText.rectTransform,
            .360f, .799f, .930f, .870f);
        BistroBuilderStaffVisuals.TextStyle(employeeRoleText, 16f, true);

        SetInactive(employeeContractText);
        SetInactive(employeeProgressText);
        SetInactive(employeeSkillsText);
        SetInactive(employeeSessionText);
        SetInactive(employeePerformanceText);
        BistroBuilderStaffVisuals.Panel("EmployeeLevelBadge", employeeDetail,
            .357f, .735f, .532f, .789f, BistroBuilderStaffVisuals.Amber);
        BistroBuilderStaffVisuals.DecorativeIcon("LevelIcon", employeeDetail,
            BBIconId.CustomerVip, .361f, .746f, .391f, .777f);
        BistroBuilderStaffVisuals.Label("LevelLegend", employeeDetail,
            "NIVEL", 13f, .393f, .737f, .454f, .785f, true);
        employeeXpLabel = BistroBuilderStaffVisuals.Label("LevelNumber",
            employeeDetail, "—", 17f, .459f, .735f, .522f, .784f, true,
            TextAlignmentOptions.Center);
        BistroBuilderStaffVisuals.Bar("LevelXPBar", employeeDetail,
            .542f, .746f, .809f, .769f, out employeeXpFill);
        BistroBuilderStaffVisuals.Label("XPNote", employeeDetail,
            "EXPERIENCIA", 12f, .834f, .776f, .962f, .803f);
        TMP_Text xpValue = BistroBuilderStaffVisuals.Label("XPValue", employeeDetail,
            "—", 13f, .814f, .735f, .966f, .778f, false,
            TextAlignmentOptions.Right);
        employeePerformanceLabel = BistroBuilderStaffVisuals.Label(
            "EmployeePerformanceNote", employeeDetail, string.Empty,
            12f, .040f, .132f, .960f, .166f, false,
            TextAlignmentOptions.Center);
        employeePerformanceLabel.color = BistroBuilderStaffVisuals.Muted;
        // Reuse a reference rather than searching every frame.
        employeeXpLabel = xpValue;

        string[] keys = { "Rol", "Salario", "Asignación", "Estado" };
        for (int i = 0; i < keys.Length; i++)
        {
            float top = .693f - i * .057f;
            RectTransform iconRoot = BistroBuilderStaffVisuals.Node(
                "InfoIcon_" + i, employeeDetail,
                .357f, top - .039f, .389f, top - .004f);
            Image infoIcon = iconRoot.gameObject.AddComponent<Image>();
            infoIcon.sprite = BistroBuilderStaffVisuals.StaffFactIcon(i);
            infoIcon.color = Color.white;
            infoIcon.preserveAspect = true;
            infoIcon.raycastTarget = false;
            BistroBuilderStaffVisuals.Label("InfoLabel_" + i, employeeDetail,
                keys[i], BistroBuilderStaffVisuals.FactSize,
                .400f, top - .043f, .622f, top, false);
            employeeInfo[i] = BistroBuilderStaffVisuals.Label("InfoValue_" + i,
                employeeDetail, "—", BistroBuilderStaffVisuals.FactSize,
                .626f, top - .043f, .961f, top,
                false, TextAlignmentOptions.MidlineRight);
            BistroBuilderStaffVisuals.Separator("InfoRule_" + i,
                employeeDetail, .356f, top - .047f, .960f);
        }
        BistroBuilderStaffVisuals.Separator("SkillsGoldRule", employeeDetail,
            .025f, .445f, .975f);
        BistroBuilderStaffVisuals.DecorativeIcon("SkillsIcon", employeeDetail,
            BBIconId.EconomyReport, .042f, .405f, .075f, .438f);
        TMP_Text employeeSkillsHeader = BistroBuilderStaffVisuals.Label(
            "SkillsHeader", employeeDetail,
            "HABILIDADES", 18f, .083f, .398f, .960f, .444f, true);
        BistroBuilderStaffVisuals.TitleStyle(employeeSkillsHeader, 18f);
        BuildApprovedSkills(employeeDetail, employeeSkillBars, employeeSkillValues);

        BistroBuilderStaffVisuals.Place(
            toggleAvailabilityButton.transform as RectTransform,
            .022f, .024f, .250f, .119f);
        BistroBuilderStaffVisuals.ButtonStyle(toggleAvailabilityButton);
        BistroBuilderStaffVisuals.TextStyle(toggleAvailabilityButtonText, 14f,
            false, TextAlignmentOptions.Center);
        Transform training = employeeDetail.Find("Training");
        if (training is RectTransform trainingRect)
        {
            BistroBuilderStaffVisuals.Place(trainingRect,
                .264f, .024f, .490f, .119f);
            BistroBuilderStaffVisuals.ButtonStyle(training.GetComponent<Button>());
        }
        scheduleShortcut = BistroBuilderStaffVisuals.NewButton("ReassignButton",
            employeeDetail, "Ver horarios", .505f, .024f, .737f, .119f);
        scheduleShortcut.onClick.AddListener(OpenApprovedSchedule);
        BistroBuilderStaffVisuals.Place(
            dismissButton.transform as RectTransform,
            .751f, .024f, .974f, .119f);
        BistroBuilderStaffVisuals.ButtonStyle(dismissButton, true);
    }

    private void ConfigureCandidateInspector()
    {
        if (candidateDetail == null) return;
        BistroBuilderStaffVisuals.PortraitFrame("HeroPortrait", candidateDetail,
            .027f, .597f, .326f, .965f, string.Empty, string.Empty, true);
        candidatePortrait = candidateDetail.Find("HeroPortrait/PortraitMat/Portrait")
            ?.GetComponent<Image>();
        candidatePortraitCaption = candidateDetail.Find(
            "HeroPortrait/PortraitMat/PortraitRole")?.GetComponent<TMP_Text>();
        BistroBuilderStaffVisuals.Place(candidateNameText.rectTransform,
            .358f, .865f, .960f, .963f);
        BistroBuilderStaffVisuals.TitleStyle(candidateNameText, 28f);
        BistroBuilderStaffVisuals.Place(candidateRoleText.rectTransform,
            .360f, .799f, .930f, .870f);
        BistroBuilderStaffVisuals.TextStyle(candidateRoleText, 16f, true);
        SetInactive(candidateProfileText);
        SetInactive(candidateSalaryText);
        SetInactive(candidateSkillsText);
        string[] fields = {"PERFIL", "SALARIO", "EXPERIENCIA"};
        for (int i = 0; i < fields.Length; i++)
        {
            float top = .730f - i * .084f;
            BistroBuilderStaffVisuals.Label("CandidateMeta_" + i,
                candidateDetail, fields[i], 16f,
                .360f, top - .052f, .60f, top, true);
            candidateInfo[i] = BistroBuilderStaffVisuals.Label(
                "CandidateValue_" + i, candidateDetail, "—", 16f,
                .60f, top - .052f, .961f, top,
                false, TextAlignmentOptions.MidlineRight);
            BistroBuilderStaffVisuals.Separator("CandidateRule_" + i,
                candidateDetail, .355f, top - .055f, .958f);
        }
        BistroBuilderStaffVisuals.Separator("CandidateSkillRule", candidateDetail,
            .025f, .445f, .975f);
        BistroBuilderStaffVisuals.DecorativeIcon("SkillsIcon", candidateDetail,
            BBIconId.EconomyReport, .042f, .405f, .075f, .438f);
        TMP_Text candidateSkillsHeader = BistroBuilderStaffVisuals.Label(
            "CandidateSkillHeader", candidateDetail,
            "HABILIDADES", 18f, .083f, .398f, .96f, .444f, true);
        BistroBuilderStaffVisuals.TitleStyle(candidateSkillsHeader, 18f);
        BuildApprovedSkills(candidateDetail, candidateSkillBars,
            candidateSkillValues);
        BistroBuilderStaffVisuals.Place(hireButton.transform as RectTransform,
            .032f, .025f, .489f, .121f);
        BistroBuilderStaffVisuals.ButtonStyle(hireButton, false, true);
        BistroBuilderStaffVisuals.Place(
            refreshCandidatesButton.transform as RectTransform,
            .511f, .025f, .968f, .121f);
        BistroBuilderStaffVisuals.ButtonStyle(refreshCandidatesButton);
        TMP_Text hireLabel = hireButton.GetComponentInChildren<TMP_Text>(true);
        if (hireLabel != null) hireLabel.text = "Contratar";
        TMP_Text refreshLabel =
            refreshCandidatesButton.GetComponentInChildren<TMP_Text>(true);
        if (refreshLabel != null) refreshLabel.text = "Renovar mercado";
    }

    private static void BuildApprovedSkills(Transform host,
        Image[] bars, TMP_Text[] values)
    {
        string[] titles = {"Velocidad", "Atención", "Organización", "Trato"};
        for (int i = 0; i < titles.Length; i++)
        {
            float y = .348f - .063f * i;
            BistroBuilderStaffVisuals.Label("Skill_" + i, host, titles[i],
                BistroBuilderStaffVisuals.SkillSize, .052f, y, .341f, y + .046f);
            BistroBuilderStaffVisuals.Bar("SkillTrack_" + i, host,
                .360f, y + .012f, .850f, y + .039f, out bars[i]);
            values[i] = BistroBuilderStaffVisuals.Label("SkillValue_" + i,
                host, "—", BistroBuilderStaffVisuals.SkillSize, .865f, y, .955f, y + .049f,
                false, TextAlignmentOptions.MidlineRight);
        }
    }

    private void ConfigureConfirmation()
    {
        RectTransform blocker = BistroBuilderStaffVisuals.Node(
            "PersonalModalBlocker", panelRoot.transform, 0f, 0f, 1f, 1f);
        Image dim = blocker.gameObject.AddComponent<Image>();
        dim.color = new Color32(26, 18, 11, 165);
        dim.raycastTarget = true;
        confirmationBlocker = blocker.gameObject;
        confirmationBlocker.SetActive(false);

        RectTransform modal = confirmationPanel.transform as RectTransform;
        BistroBuilderStaffVisuals.Place(modal, .297f, .314f, .703f, .677f);
        Image background = confirmationPanel.GetComponent<Image>();
        if (background == null)
            background = confirmationPanel.AddComponent<Image>();
        background.color = BistroBuilderStaffVisuals.Paper;
        background.raycastTarget = true;
        BistroBuilderStaffVisuals.Frame(confirmationPanel.transform,
            "ApprovedConfirmationFrame");
        BistroBuilderStaffVisuals.Label("ModalTitle", confirmationPanel.transform,
            "CONFIRMAR ACCIÓN", 25f, .06f, .795f, .94f, .95f, true,
            TextAlignmentOptions.Center);
        BistroBuilderStaffVisuals.Place(confirmationText.rectTransform,
            .08f, .326f, .92f, .787f);
        BistroBuilderStaffVisuals.TextStyle(confirmationText, 19f, false,
            TextAlignmentOptions.Center);
        confirmationText.textWrappingMode = TextWrappingModes.Normal;
        BistroBuilderStaffVisuals.Place(
            confirmationCancelButton.transform as RectTransform,
            .085f, .08f, .476f, .278f);
        BistroBuilderStaffVisuals.Place(
            confirmationAcceptButton.transform as RectTransform,
            .525f, .08f, .915f, .278f);
        BistroBuilderStaffVisuals.ButtonStyle(confirmationCancelButton);
        BistroBuilderStaffVisuals.ButtonStyle(confirmationAcceptButton,
            false, true);
        confirmationBlocker.transform.SetSiblingIndex(
            confirmationPanel.transform.GetSiblingIndex());
        confirmationPanel.transform.SetAsLastSibling();
    }

    private void ConfigureExistingTraining()
    {
        Transform modal = panelRoot.transform.Find("TrainingModal");
        if (modal == null) return;
        Image background = modal.GetComponent<Image>();
        if (background == null)
            background = modal.gameObject.AddComponent<Image>();
        background.color = BistroBuilderStaffVisuals.Paper;
        background.raycastTarget = true;
        if (modal.Find("ApprovedTrainingFrame") == null)
            BistroBuilderStaffVisuals.Frame(modal, "ApprovedTrainingFrame");
        TMP_Text[] labels = modal.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text t in labels)
            BistroBuilderStaffVisuals.TextStyle(t,
                t.name == "Title" ? 25f : 16f, t.name == "Title");
        Button[] buttons = modal.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
            BistroBuilderStaffVisuals.ButtonStyle(b);
    }

    private void OpenApprovedSchedule()
    {
        var screens = FindObjectsByType<BistroBuilderStaffSchedulePlayerScreen>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        BistroBuilderStaffSchedulePlayerScreen target = null;
        foreach (var screen in screens)
        {
            if (screen == null || screen.gameObject.scene != gameObject.scene)
                continue;
            if (target != null)
            {
                ShowFeedback("Hay más de una pantalla de Horarios 5E.");
                return;
            }
            target = screen;
        }
        if (target == null)
        {
            ShowFeedback("La pantalla de Horarios 5E no está instalada.");
            return;
        }
        if (!target.ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }
        Hide();
        target.ShowFromPersonal(this);
    }

    private void ToggleApprovedViewMenu() =>
        SetApprovedViewMenuOpen(!approvedViewMenuOpen);

    private void SetApprovedViewMenuOpen(bool visible)
    {
        if (approvedViewMenu == null || staffTabButton == null ||
            candidatesTabButton == null || approvedScheduleTab == null) return;
        approvedViewMenuOpen = visible;
        approvedViewMenu.gameObject.SetActive(visible);
        staffTabButton.gameObject.SetActive(visible);
        candidatesTabButton.gameObject.SetActive(visible);
        approvedScheduleTab.gameObject.SetActive(visible);
        if (!visible) return;
        // Keep an opened contextual menu above the two table surfaces.
        approvedViewMenu.SetAsLastSibling();
        staffTabButton.transform.SetAsLastSibling();
        candidatesTabButton.transform.SetAsLastSibling();
        approvedScheduleTab.transform.SetAsLastSibling();
    }

    private void ApplyApprovedTabState()
    {
        if (!approvedPresentationReady) return;
        BistroBuilderStaffVisuals.ButtonStyle(staffTabButton, false,
            viewMode == ViewMode.Staff);
        BistroBuilderStaffVisuals.ButtonStyle(candidatesTabButton, false,
            viewMode == ViewMode.Candidates);
    }

    private static void SetPortrait(Image image, TMP_Text caption,
        string roleId, string fullName)
    {
        if (image == null) return;
        Sprite photo = BistroBuilderStaffVisuals.Portrait(roleId, fullName);
        image.sprite = photo != null ? photo
            : BistroBuilderStaffVisuals.RoleIcon(roleId);
        image.color = photo == null
            ? BistroBuilderStaffVisuals.Muted : Color.white;
        image.enabled = image.sprite != null;
        if (caption == null) return;
        caption.text = photo != null || string.IsNullOrWhiteSpace(fullName)
            ? string.Empty : roleId == "cook" ? "COCINA"
                : roleId == "waiter" ? "SALA" : "PERSONAL";
    }

    private void RefreshApprovedRolePortrait(bool employee, string roleId)
    {
        if (!approvedPresentationReady) return;
        string person = employee
            ? FindSelectedEmployee()?.fullName : FindSelectedCandidate()?.fullName;
        SetPortrait(employee ? employeePortrait : candidatePortrait,
            employee ? employeePortraitCaption : candidatePortraitCaption,
            roleId, person);
    }

    private static string ApprovedMoney(long cents)
    {
        return (cents / 100m).ToString("0.00") + " € / servicio";
    }

    private static void ShowSkills(BistroBuilderEmployeeSkillSet skills,
        Image[] bars, TMP_Text[] values)
    {
        int[] score = skills == null ? new int[4] :
            new[] { skills.speed, skills.attentiveness,
                    skills.organization, skills.hospitality };
        for (int i = 0; i < bars.Length; i++)
        {
            BistroBuilderStaffVisuals.SetBar(bars[i], score[i] / 100f);
            if (values[i] != null) values[i].text = skills == null
                ? "—" : score[i].ToString();
        }
    }

    private void UpdateApprovedEmployeeDetails(BistroBuilderStaffPlayerEmployeeRow row)
    {
        if (!approvedPresentationReady) return;
        employeeInfo[0].text = row?.roleDisplayName ?? "—";
        employeeInfo[1].text = row == null ? "—"
            : ApprovedMoney(row.salaryCentsPerService);
        employeeInfo[2].text = row == null ? "—"
            : row.departmentDisplayName;
        employeeInfo[3].text = row == null ? "—"
            : row.hasServiceAssignment
                ? (row.sessionStatus == BistroBuilderEmployeeSessionStatus.Working
                    ? "● Trabajando" : "● Asignado")
                : row.availability == BistroBuilderEmployeeAvailability.Available
                    ? "● Disponible" : "● No disponible";
        employeeInfo[3].color = row != null &&
            row.availability == BistroBuilderEmployeeAvailability.Available
            ? BistroBuilderStaffVisuals.Green : BistroBuilderStaffVisuals.Muted;

        TMP_Text level = employeeDetail.Find("LevelNumber")?.GetComponent<TMP_Text>();
        if (level != null) level.text = row == null
            ? "—" : Math.Max(1, row.level).ToString();
        employeeXpLabel.text = row == null ? "—"
            : row.experiencePoints + " / " +
              Math.Max(row.experiencePoints, row.nextLevelExperience) + " XP";
        long previous = row?.experienceAtLevel ?? 0L;
        long next = row?.nextLevelExperience ?? 0L;
        float progress = row == null ? 0f : next <= previous
            ? 1f : (float)(row.experiencePoints - previous) /
                (next - previous);
        BistroBuilderStaffVisuals.SetBar(employeeXpFill, progress);
        ShowSkills(row?.skills, employeeSkillBars, employeeSkillValues);
        if (employeePerformanceLabel != null)
            employeePerformanceLabel.text = row == null ? string.Empty
                : BuildPerformance(row.performance);
        SetPortrait(employeePortrait, employeePortraitCaption,
            row?.roleId, row?.fullName);
    }

    private void UpdateApprovedCandidateDetails(BistroBuilderStaffPlayerCandidateRow row)
    {
        if (!approvedPresentationReady) return;
        candidateInfo[0].text = row == null ? "—"
            : FormatCandidateProfile(row.profile);
        candidateInfo[1].text = row == null ? "—"
            : ApprovedMoney(row.expectedSalaryCentsPerService);
        candidateInfo[2].text = row == null ? "—"
            : row.experiencePoints + " XP";
        ShowSkills(row?.skills, candidateSkillBars, candidateSkillValues);
        SetPortrait(candidatePortrait, candidatePortraitCaption,
            row?.roleId, row?.fullName);
    }

    private static void ApprovedRowCells(GameObject row, bool employee,
        string department)
    {
        if (row == null) return;
        LayoutElement e = row.GetComponent<LayoutElement>();
        if (e != null) { e.minHeight = 40f; e.preferredHeight = 40f; }
        void Cell(string name, float a, float b)
        {
            RectTransform rect = row.transform.Find(name) as RectTransform;
            if (rect != null)
                BistroBuilderStaffVisuals.Place(rect, a, 0f, b, 1f);
            if (rect != null)
                BistroBuilderStaffVisuals.TextStyle(
                    rect.GetComponent<TMP_Text>(), name == "Name"
                    ? BistroBuilderStaffVisuals.RowNameSize
                    : BistroBuilderStaffVisuals.RowDataSize,
                    name == "Name",
                    name == "Name" ? TextAlignmentOptions.MidlineLeft
                        : TextAlignmentOptions.Center);
        }
        if (employee)
        {
            Cell("Name", .106f, .298f);
            Cell("Role", .303f, .434f);
            Cell("Level", .436f, .538f);
            Cell("Salary", .713f, .837f);
            Cell("Status", .842f, .992f);
            var assign = BistroBuilderStaffVisuals.Label("Assignment", row.transform,
                department, BistroBuilderStaffVisuals.RowDataSize,
                .542f, 0f, .708f, 1f,
                false, TextAlignmentOptions.Center);
            assign.color = BistroBuilderStaffVisuals.Muted;
            TMP_Text status = row.transform.Find("Status")?.GetComponent<TMP_Text>();
            if (status != null && !string.IsNullOrWhiteSpace(status.text))
            {
                bool inactive = status.text.Contains("No disponible") ||
                    status.text.Contains("Inactivo");
                status.richText = true;
                status.text = "<color=" + (inactive ? "#987452" : "#4B9431") +
                    ">●</color> " + status.text.TrimStart('●', ' ');
                status.color = inactive ? BistroBuilderStaffVisuals.Muted
                    : BistroBuilderStaffVisuals.Ink;
            }
            TMP_Text level = row.transform.Find("Level")?.GetComponent<TMP_Text>();
            if (level != null) level.text = level.text.Replace("Nivel ", "");
            TMP_Text salary = row.transform.Find("Salary")?.GetComponent<TMP_Text>();
            if (salary != null)
                salary.text = salary.text.Replace(" / servicio", "");
            // Original reference: continuous, understated column separators.
            // Unlike outlines around each row these do not turn the table into
            // a collection of independent cards.
            float[] boundaries = { .300f, .435f, .540f, .710f, .840f };
            for (int i = 0; i < boundaries.Length; i++)
                BistroBuilderStaffVisuals.Panel("RowDivider_" + i,
                    row.transform, boundaries[i], .04f,
                    boundaries[i] + .001f, .96f,
                    new Color32(191, 152, 109, 65), false);
        }
        else
        {
            Cell("Name", .110f, .343f);
            Cell("Role", .347f, .529f);
            Cell("Profile", .538f, .716f);
            Cell("Salary", .722f, .974f);
        }
    }

    private void AddApprovedRoleIcon(GameObject row, string roleId)
    {
        if (row == null) return;
        string fullName = row.transform.Find("Name")?.GetComponent<TMP_Text>()?.text;
        BistroBuilderStaffVisuals.PortraitFrame("RowPortrait", row.transform,
            .018f, .054f, .075f, .946f, roleId, fullName, false);
        // Real small portrait if art is supplied, role symbol otherwise.
    }

    private void ApplyApprovedRowStyle(GameObject row, bool selected)
    {
        if (row == null) return;
        // A table row is NOT a stand-alone raised button. The old generic
        // ButtonStyle also centered the first TMP label and destroyed the
        // approved tabular alignment. Keep rows flat and their content intact.
        Image bg = row.GetComponent<Image>();
        if (bg == null) bg = row.AddComponent<Image>();
        // The reference uses a muted honey selection, not the saturated
        // orange command-button colour from the former implementation.
        Color baseColor = selected
            ? new Color32(240, 207, 159, 255)
            : new Color32(250, 237, 219, 255);
        bg.color = baseColor;
        Outline edge = row.GetComponent<Outline>();
        if (edge == null) edge = row.AddComponent<Outline>();
        edge.effectColor = selected ? BistroBuilderStaffVisuals.Brass
            : Color.clear;
        edge.effectDistance = new Vector2(0f, -1f);
        edge.useGraphicAlpha = false;
        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            // Tint MULTIPLIES Image.color; using baseColor twice darkened every
            // row and made the approved parchment table look orange/brown.
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 245, 228, 255);
            colors.pressedColor = new Color32(232, 209, 178, 255);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(baseColor.r, baseColor.g,
                baseColor.b, .45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .14f;
            button.colors = colors;
        }
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null || text.name == "Status") continue;
            text.color = text.name == "Role" || text.name == "Assignment"
                ? BistroBuilderStaffVisuals.Muted : BistroBuilderStaffVisuals.Ink;
        }
    }

    private GameObject CreateApprovedDepartmentHeading(string department, int count)
    {
        // The approved image separates SALA and COCINA with a narrow gutter;
        // rows within each section form ONE continuous table, not button cards.
        if (departmentHeadings.Count > 0)
        {
            RectTransform gap = BistroBuilderStaffVisuals.Node(
                "ApprovedDepartmentGap", employeeListContent, 0f, 0f, 1f, 1f);
            LayoutElement space = gap.gameObject.AddComponent<LayoutElement>();
            space.minHeight = space.preferredHeight = 7f;
            departmentHeadings.Add(gap.gameObject);
        }
        RectTransform container = BistroBuilderStaffVisuals.Node(
            "Department_" + department, employeeListContent, 0f, 0f, 1f, 1f);
        LayoutElement layout = container.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 69f;
        layout.preferredHeight = 69f;
        var band = BistroBuilderStaffVisuals.Panel("DepartmentBand", container,
            .005f, .40f, .995f, .99f,
            new Color32(241, 222, 197, 255));
        string caption = string.Equals(department, "SALA",
            StringComparison.OrdinalIgnoreCase)
            ? "Personal de sala que atiende a los clientes."
            : string.Equals(department, "COCINA", StringComparison.OrdinalIgnoreCase)
                ? "Personal de cocina que prepara los platos."
                : "Personal asignado a " + department.ToLowerInvariant() + ".";
        bool kitchen = string.Equals(department, "COCINA",
            StringComparison.OrdinalIgnoreCase);
        RectTransform roleIcon = BistroBuilderStaffVisuals.Node(
            "DepartmentRoleIcon", band, .018f, .22f, .052f, .80f);
        Image roleImage = roleIcon.gameObject.AddComponent<Image>();
        roleImage.sprite = BistroBuilderStaffVisuals.RoleIcon(kitchen ? "cook" : "waiter");
        roleImage.preserveAspect = true;
        roleImage.color = BistroBuilderStaffVisuals.Ink;
        roleImage.raycastTarget = false;
        var title = BistroBuilderStaffVisuals.Label("DepartmentTitle", band,
            department, 22f, .059f, .10f, .25f, .91f, true);
        BistroBuilderStaffVisuals.TitleStyle(title, 22f);
        BistroBuilderStaffVisuals.Label("DepartmentCaption", band,
            caption, BistroBuilderStaffVisuals.CaptionSize,
            .25f, .10f, .77f, .91f);
        BistroBuilderStaffVisuals.Label("DepartmentCount", band,
            count + " EN PLANTILLA", 13f, .77f, .10f, .973f, .91f,
            true, TextAlignmentOptions.Right);

        var columns = BistroBuilderStaffVisuals.Panel("DepartmentColumns",
            container, .005f, .01f, .995f, .40f, BistroBuilderStaffVisuals.Paper);
        string[] labels = {"NOMBRE", "ROL", "NIVEL", "ASIGNACIÓN", "SALARIO", "ESTADO"};
        float[] x = {.105f, .300f, .435f, .540f, .710f, .840f, .99f};
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text text = BistroBuilderStaffVisuals.Label("Column_" + i,
                columns, labels[i], BistroBuilderStaffVisuals.ColumnSize,
                x[i], 0f, x[i+1], 1f, true,
                TextAlignmentOptions.Center);
            text.color = BistroBuilderStaffVisuals.Ink;
            if (i < labels.Length - 1)
                BistroBuilderStaffVisuals.Panel("ColumnDivider_" + i,
                    columns, x[i + 1], .13f, x[i + 1] + .001f, .87f,
                    new Color32(186, 151, 109, 120), false);
        }
        departmentHeadings.Add(container.gameObject);
        return container.gameObject;
    }

    private void CreateApprovedVacancy(BistroBuilderStaffPlayerDepartmentRow department)
    {
        RectTransform vacancy = BistroBuilderStaffVisuals.Panel(
            "Vacancy_" + department.departmentId, employeeListContent,
            0f, 0f, 1f, 1f, BistroBuilderStaffVisuals.Paper);
        LayoutElement le = vacancy.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 52f;
        le.preferredHeight = 52f;
        BistroBuilderStaffVisuals.Panel("VacancyPlusFrame", vacancy,
            .018f, .105f, .094f, .895f, BistroBuilderStaffVisuals.Paper);
        BistroBuilderStaffVisuals.Label("VacancyPlus", vacancy, "+",
            27f, .024f, .10f, .088f, .89f, true,
            TextAlignmentOptions.Center).color = BistroBuilderStaffVisuals.Brass;
        BistroBuilderStaffVisuals.Label("VacancyText", vacancy,
            "Puesto disponible", BistroBuilderStaffVisuals.RowDataSize,
            .110f, 0f, .315f, 1f)
            .color = BistroBuilderStaffVisuals.Muted;
        float[] emptyLeft = { .325f, .462f, .557f, .706f };
        float[] emptyRight = { .46f, .55f, .697f, .833f };
        for (int i = 0; i < emptyLeft.Length; i++)
            BistroBuilderStaffVisuals.Label("VacancyEmpty_" + i, vacancy,
                "—", BistroBuilderStaffVisuals.RowDataSize,
                emptyLeft[i], 0f, emptyRight[i], 1f,
                false, TextAlignmentOptions.Center);
        Button open = BistroBuilderStaffVisuals.NewButton("VacancyHire",
            vacancy, "Contratar", .850f, .14f, .986f, .86f,
            true);
        string targetRole = department.roleIds.Count > 0
            ? department.roleIds[0] : string.Empty;
        open.onClick.AddListener(() =>
        {
            selectedCandidateRole = targetRole;
            ShowCandidates();
            ApplyCandidateRoleFilter();
        });
        departmentHeadings.Add(vacancy.gameObject);
    }

    private void ClearApprovedRowsAndFilters()
    {
        foreach (GameObject go in departmentHeadings)
            if (go != null) { go.SetActive(false); Destroy(go); }
        departmentHeadings.Clear();
        employeeRowsById.Clear();
        candidateRowsById.Clear();
        foreach (Button button in candidateRoleFilters)
            if (button != null)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
        candidateRoleFilters.Clear();
        candidateFilterIds.Clear();
    }

    private void RebuildApprovedCandidateFilters()
    {
        if (!approvedPresentationReady || currentSnapshot == null) return;
        var roles = new List<BistroBuilderStaffPlayerCandidateRow>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in currentSnapshot.candidates)
            if (candidate != null && unique.Add(candidate.roleId))
                roles.Add(candidate);
        roles.Sort((a, b) =>
        {
            int order = a.departmentSortOrder.CompareTo(b.departmentSortOrder);
            return order != 0 ? order
                : string.Compare(a.roleDisplayName, b.roleDisplayName,
                    StringComparison.OrdinalIgnoreCase);
        });
        if (!string.IsNullOrEmpty(selectedCandidateRole) &&
            !unique.Contains(selectedCandidateRole))
            selectedCandidateRole = string.Empty;

        int slots = roles.Count + 1;
        CreateApprovedFilter(string.Empty, "Todos", 0f, 1f / slots);
        for (int i = 0; i < roles.Count; i++)
            CreateApprovedFilter(roles[i].roleId, roles[i].roleDisplayName,
                (float)(i + 1) / slots, (float)(i + 2) / slots);
        ApplyCandidateRoleFilter();
    }

    private void CreateApprovedFilter(string roleId, string title,
        float t0, float t1)
    {
        float left = .012f + .540f * t0;
        float right = .012f + .540f * t1;
        Button button = BistroBuilderStaffVisuals.NewButton(
            "Filter_" + (roleId.Length == 0 ? "All" : roleId),
            candidatesPanel.transform, title, left + .004f, .891f,
            right - .004f, .976f,
            selectedCandidateRole == roleId);
        button.onClick.AddListener(() =>
        {
            selectedCandidateRole = roleId;
            ApplyCandidateRoleFilter();
        });
        candidateRoleFilters.Add(button);
        candidateFilterIds[button] = roleId;
    }

    private void ApplyCandidateRoleFilter()
    {
        if (currentSnapshot == null) return;
        bool selectedVisible = false;
        string first = string.Empty;
        foreach (var candidate in currentSnapshot.candidates)
        {
            if (candidate == null) continue;
            bool visible = string.IsNullOrEmpty(selectedCandidateRole) ||
                string.Equals(candidate.roleId, selectedCandidateRole,
                    StringComparison.Ordinal);
            if (candidateRowsById.TryGetValue(candidate.candidateId, out GameObject row))
                row.SetActive(visible);
            if (!visible) continue;
            if (first.Length == 0) first = candidate.candidateId;
            if (candidate.candidateId == selectedCandidateId) selectedVisible = true;
        }
        if (!selectedVisible) selectedCandidateId = first;
        foreach (Button b in candidateRoleFilters)
        {
            if (b == null || !candidateFilterIds.TryGetValue(b, out string id))
                continue;
            BistroBuilderStaffVisuals.ButtonStyle(b, false,
                id == selectedCandidateRole);
        }
        RenderSelectedCandidate();
        UpdateApprovedRowSelection();
    }

    private void UpdateApprovedRowSelection()
    {
        foreach (var item in employeeRowsById)
            ApplyApprovedRowStyle(item.Value, item.Key == selectedEmployeeId);
        foreach (var item in candidateRowsById)
            ApplyApprovedRowStyle(item.Value, item.Key == selectedCandidateId);
    }

    private void RefreshApprovedConfirmation(bool visible)
    {
        if (!approvedPresentationReady) return;
        if (confirmationBlocker != null)
        {
            confirmationBlocker.SetActive(visible);
            if (visible) confirmationBlocker.transform.SetSiblingIndex(
                confirmationPanel.transform.GetSiblingIndex());
        }
        if (!visible) return;
        confirmationPanel.transform.SetAsLastSibling();
        TMP_Text heading =
            confirmationPanel.transform.Find("ModalTitle")?.GetComponent<TMP_Text>();
        TMP_Text accept =
            confirmationAcceptButton.GetComponentInChildren<TMP_Text>(true);
        bool hire = pendingConfirmation == PendingConfirmation.Hire;
        if (heading != null) heading.text = hire
            ? "CONFIRMAR CONTRATACIÓN" : "CONFIRMAR DESPIDO";
        if (accept != null) accept.text = hire ? "Sí, contratar" : "Sí, despedir";
        BistroBuilderStaffVisuals.ButtonStyle(confirmationAcceptButton,
            !hire, hire);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                confirmationCancelButton.gameObject);
    }
}
