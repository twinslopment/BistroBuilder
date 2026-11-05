using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra inferior E5.5 del modo juego. Presentacion sin autoridades propias:
/// Caja 3G, experiencia 8B, Clima V1 y GameClock son las fuentes canonicas.
/// Se instala en la superficie 21A existente, sin crear un segundo Canvas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Bottom HUD E5.5")]
public sealed class BistroBuilderBottomE55Hud : MonoBehaviour
{
    public const string Revision = "E55-20261105";
    public const string VisualRootName = "BB_BottomE55";
    private static readonly Color White = new Color32(244, 238, 227, 255);
    private static readonly Color Muted = new Color32(191, 181, 165, 255);
    private static readonly Color Gold = new Color32(198, 146, 72, 255);
    private static readonly Color Green = new Color32(134, 202, 69, 255);
    private static readonly Color Amber = new Color32(226, 158, 75, 255);
    private static readonly Color Red = new Color32(218, 98, 79, 255);

    private static Sprite frameSprite, tileSprite, greenSprite, controlSprite, selectedSprite;
    private static readonly Sprite[] IconSprites = new Sprite[8];
    private RectTransform root;
    private Image cashIcon, satisfactionIcon, weatherIcon, pauseIcon;
    private Image satisfactionTrack, satisfactionFill;
    private TMP_Text cashValue, satisfactionValue, weatherLabel, temperatureValue;
    private Button pauseButton;
    private readonly Button[] speedButtons = new Button[3];
    private readonly TMP_Text[] speedLabels = new TMP_Text[3];
    private BistroBuilderFinancialResultsService financialResults;
    private BistroBuilderCustomerExperienceTrackingService experience;
    private BistroBuilderGeneralGameStateService gameState;
    private BistroBuilderClimateService climate;
    private GameClock clock;
    private RestaurantEditModeService editModeService;
    private readonly List<CanvasGroup> hiddenTimeDocks = new List<CanvasGroup>(4);
    private float nextDockScanAt;
    private RectTransform bottomTransform;
    private Image bottomSurface;
    private float nextDataRefresh;
    private bool wasEditing;

    private void OnEnable()
    {
        BuildIfNecessary();
        nextDataRefresh = 0f;
    }

    private void OnDisable()
    {
        // Restaurar los controles antiguos únicamente al retirar E5.5.
        for (int i = 0; i < hiddenTimeDocks.Count; i++)
        {
            CanvasGroup group = hiddenTimeDocks[i];
            if (group == null) continue;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        hiddenTimeDocks.Clear();
        nextDockScanAt = 0f;
    }

    public bool ValidateConfiguration(out string error)
    {
        BuildIfNecessary();
        if (root == null || cashValue == null || satisfactionValue == null ||
            weatherLabel == null || temperatureValue == null || pauseButton == null ||
            speedButtons[0] == null || speedButtons[1] == null || speedButtons[2] == null)
        {
            error = "No se ha construido completamente la barra inferior E5.5.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        BuildIfNecessary();
        if (editModeService == null)
            editModeService = FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        bool editing = editModeService != null && editModeService.IsEditModeActive;
        if (root != null && root.gameObject.activeSelf == editing)
            root.gameObject.SetActive(!editing);

        HideOldDock();
        HideLegacyChildren();
        if (bottomSurface != null && !editing)
        {
            bottomSurface.color = Color.clear;
            bottomSurface.raycastTarget = false;
        }
        if (bottomTransform != null)
        {
            bottomTransform.sizeDelta = new Vector2(0f, editing ? 64f : DesiredHeight());
        }
        if (editing)
        {
            wasEditing = true;
            return;
        }
        if (wasEditing)
        {
            nextDataRefresh = 0f;
            wasEditing = false;
        }
        if (Time.unscaledTime < nextDataRefresh) return;
        nextDataRefresh = Time.unscaledTime + 0.75f;
        ResolveSources();
        RefreshData();
    }

    private float DesiredHeight()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return 82f;
        float logicalHeight = canvas.pixelRect.height / Mathf.Max(0.01f, canvas.scaleFactor);
        return Mathf.Clamp(logicalHeight * 0.080f, 66f, 92f);
    }

    private void BuildIfNecessary()
    {
        // Ocultar el antiguo control incluso si el shell se reconcilia antes
        // del primer LateUpdate o si el HUD se valida estando inactivo.
        HideOldDock();
        if (root != null) return;
        bottomTransform = transform as RectTransform;
        bottomSurface = GetComponent<Image>();
        if (bottomTransform == null) return;
        RectTransform existing = transform.Find(VisualRootName) as RectTransform;
        root = existing != null ? existing : Rect(VisualRootName, transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = new Vector2(26f, 7f);
        root.offsetMax = new Vector2(-26f, -3f);
        Panel(root, MakeFrame(), false);

        Shadow shadow = root.GetComponent<Shadow>();
        if (shadow == null) shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;

        RectTransform row = Rect("ContenidoE55", root);
        Stretch(row, 5f, 5f, 5f, 5f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        RectTransform cash = Capsule(row, "CajaE55", Tile(), 160f, 1.02f);
        cashIcon = Icon(cash, "Monedas", 0, 0.035f, 0.13f, 0.22f, 0.90f);
        Text(cash, "Titulo", "Caja", 13f, Muted, 0.23f, 0.55f, 0.98f, 0.91f, false);
        cashValue = Text(cash, "Valor", "—", 21f, White, 0.23f, 0.08f, 0.98f, 0.57f, true);

        RectTransform satisfaction = Capsule(row, "SatisfaccionE55", GreenTile(), 210f, 1.28f);
        satisfactionIcon = Icon(satisfaction, "Carita", 1, 0.025f, 0.13f, 0.205f, 0.88f);
        Text(satisfaction, "Titulo", "Satisfacción", 12.5f, White, 0.21f, 0.61f, 0.95f, 0.92f, false);
        satisfactionValue = Text(satisfaction, "Valor", "—", 19f, White, 0.21f, 0.15f, 0.42f, 0.62f, true);
        RectTransform track = Rect("BarraBase", satisfaction);
        Place(track, 0.45f, 0.25f, 0.95f, 0.48f);
        satisfactionTrack = Panel(track, Control(), false);
        RectTransform fill = Rect("BarraValor", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        satisfactionFill = Panel(fill, ColorFill(Color.white), false);

        RectTransform weather = Capsule(row, "ClimaE55", Tile(), 165f, 1.03f);
        weatherIcon = Icon(weather, "Tiempo", 2, 0.025f, 0.12f, 0.235f, 0.90f);
        weatherLabel = Text(weather, "Estado", "Climatología", 14f, White, 0.245f, 0.47f, 0.99f, 0.86f, false);
        temperatureValue = Text(weather, "Temperatura", "—", 20f, White, 0.245f, 0.10f, 0.99f, 0.54f, true);

        RectTransform speed = Capsule(row, "VelocidadE55", Tile(), 242f, 1.14f);
        Text(speed, "Titulo", "Velocidad", 10f, Muted, 0.04f, 0.79f, 0.95f, 0.98f, false);
        pauseButton = ControlButton(speed, "Pausa", 0.035f, 0.255f, 0.16f, 0.81f);
        pauseIcon = Icon(pauseButton.transform, "IconoPausa", 6, 0.19f, 0.19f, 0.81f, 0.81f);
        string[] labels = { "1×", "2×", "3×" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float left = 0.275f + i * 0.238f;
            speedButtons[i] = ControlButton(speed, "Speed" + (i + 1), left,
                left + 0.215f, 0.16f, 0.81f);
            speedLabels[i] = Text(speedButtons[i].transform, "Etiqueta", labels[i],
                17f, White, 0f, 0f, 1f, 1f, true);
            speedButtons[i].onClick.AddListener(() => ChangeSpeed(index + 1));
        }
        pauseButton.onClick.AddListener(TogglePause);
        root.SetAsLastSibling();
        HideLegacyChildren();
        HideOldDock();
        ResolveSources();
        RefreshData();
    }

    private void HideLegacyChildren()
    {
        // El cierre de servicio sigue accesible en el menu superior
        // "Cierre del dia"; no se destruye el botón original ni su logica.
        string[] names = { "StatusContent", "BottomDateTime", BistroBuilderUiShell.ServiceActionName };
        foreach (string name in names)
        {
            Transform legacy = transform.Find(name);
            if (legacy != null && legacy.gameObject.activeSelf)
                legacy.gameObject.SetActive(false);
        }
    }

    private void HideOldDock()
    {
        // Buscar todos los docks: pueden existir varios Canvas o réplicas del HUD
        // en una escena. No depender del orden de FindFirstObjectByType.
        if (Application.isPlaying && Time.unscaledTime < nextDockScanAt) return;
        nextDockScanAt = Time.unscaledTime + 0.35f;

        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null) continue;
            Transform child = canvases[i].transform.Find("BB_368B_TimeControlsDock");
            if (child != null) SuppressLegacyDock(child);
        }

        BistroBuilder368BInstalledHudDock[] markers =
            FindObjectsByType<BistroBuilder368BInstalledHudDock>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) SuppressLegacyDock(markers[i].transform);

        GameObject byName = GameObject.Find("BB_368B_TimeControlsDock");
        if (byName != null) SuppressLegacyDock(byName.transform);
    }

    private void SuppressLegacyDock(Transform dock)
    {
        CanvasGroup group = dock.GetComponent<CanvasGroup>();
        if (group == null) group = dock.gameObject.AddComponent<CanvasGroup>();
        if (!hiddenTimeDocks.Contains(group)) hiddenTimeDocks.Add(group);
        // El GameObject permanece activo para las validaciones 368B.
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ResolveSources()
    {
        if (clock == null) clock = FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (financialResults == null)
            financialResults = FindFirstObjectByType<BistroBuilderFinancialResultsService>(FindObjectsInactive.Include);
        if (experience == null)
            experience = FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>(FindObjectsInactive.Include);
        if (gameState == null)
            gameState = FindFirstObjectByType<BistroBuilderGeneralGameStateService>(FindObjectsInactive.Include);
        if (climate == null)
            climate = FindFirstObjectByType<BistroBuilderClimateService>(FindObjectsInactive.Include);
    }

    private void RefreshData()
    {
        if (cashValue == null) return;
        if (financialResults != null &&
            financialResults.TryGetCurrentServiceResult(out BistroBuilderServiceFinancialResult result, out _) &&
            result != null)
            cashValue.text = BistroBuilderFinanceUiFormat.Money(result.revenueCents);
        else
            cashValue.text = "—";

        int sum = 0, guests = 0;
        if (experience != null && gameState != null)
        {
            BistroBuilderReputationRuntimeSnapshot snapshot = experience.CreateRuntimeSnapshot();
            if (snapshot != null && snapshot.visits != null)
            {
                foreach (BistroBuilderReputationVisitRuntimeRecord visit in snapshot.visits)
                {
                    if (visit == null || visit.finalized) continue;
                    if (!BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                        visit, gameState.DayIndex, out BistroBuilderCustomerExperienceRecord score, out _))
                        continue;
                    int count = Mathf.Max(1, visit.partySize);
                    sum += Mathf.RoundToInt(score.overallSatisfactionBasisPoints / 100f) * count;
                    guests += count;
                }
            }
        }
        int percent = guests > 0 ? Mathf.Clamp(Mathf.RoundToInt(sum / (float)guests), 0, 100) : -1;
        satisfactionValue.text = percent >= 0 ? percent + " %" : "—";
        if (satisfactionFill != null)
        {
            RectTransform fill = satisfactionFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(percent / 100f), 1f);
            satisfactionFill.color = percent < 0 ? Muted :
                percent >= 70 ? Green : percent >= 45 ? Amber : Red;
        }

        BistroBuilderWeatherState weather = climate != null && climate.IsInitialized
            ? climate.CurrentWeather : null;
        if (weather == null)
        {
            weatherLabel.text = "Climatología";
            temperatureValue.text = "—";
            if (weatherIcon != null) weatherIcon.sprite = Glyph(2);
        }
        else
        {
            weatherLabel.text = weather.isSnowing ? "Nieve" :
                weather.isRaining ? "Lluvia" : weather.isWindy ? "Viento" :
                weather.cloudState == BistroBuilderCloudState.Cloudy ? "Nublado" : "Soleado";
            temperatureValue.text = Mathf.RoundToInt(weather.temperatureC) + " °C";
            if (weatherIcon != null)
                weatherIcon.sprite = Glyph(weather.isRaining ? 3 :
                    weather.isSnowing ? 4 : weather.isWindy ? 5 :
                    weather.cloudState == BistroBuilderCloudState.Cloudy ? 3 : 2);
        }
        RefreshSpeed();
    }

    private void RefreshSpeed()
    {
        if (pauseButton == null || clock == null) return;
        bool suspended = clock.IsRuntimeSuspended;
        bool paused = clock.IsEffectivelyPaused;
        pauseButton.interactable = !suspended;
        pauseIcon.sprite = Glyph(paused ? 7 : 6);
        PaintControl(pauseButton, paused, null);
        for (int i = 0; i < speedButtons.Length; i++)
        {
            Button button = speedButtons[i];
            if (button == null) continue;
            button.interactable = !suspended;
            bool selected = Mathf.Approximately(clock.SpeedMultiplier, i + 1f);
            PaintControl(button, selected, speedLabels[i]);
        }
    }

    private static void PaintControl(Button button, bool selected, TMP_Text label)
    {
        Image image = button.GetComponent<Image>();
        if (image != null) image.sprite = selected ? Selected() : Control();
        if (label != null) label.color = selected ? new Color32(28, 24, 21, 255) : White;
    }

    private void TogglePause()
    {
        ResolveSources();
        if (clock == null || clock.IsRuntimeSuspended) return;
        clock.SetPaused(!clock.IsPaused);
        RefreshSpeed();
    }

    private void ChangeSpeed(float speed)
    {
        ResolveSources();
        if (clock == null || clock.IsRuntimeSuspended) return;
        clock.SetSpeedMultiplier(speed);
        RefreshSpeed();
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        return obj.GetComponent<RectTransform>();
    }
    private static void Place(RectTransform rect, float xmin, float ymin, float xmax, float ymax)
    {
        rect.anchorMin = new Vector2(xmin, ymin);
        rect.anchorMax = new Vector2(xmax, ymax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Stretch(RectTransform rect, float l, float b, float r, float t)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(l, b); rect.offsetMax = new Vector2(-r, -t);
    }
    private static Image Panel(RectTransform rect, Sprite sprite, bool blocksRaycast)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = blocksRaycast;
        return image;
    }
    private static RectTransform Capsule(Transform parent, string name, Sprite sprite, float minWidth, float flex)
    {
        RectTransform rect = Rect(name, parent);
        Panel(rect, sprite, false);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.minWidth = minWidth; element.flexibleWidth = flex; element.preferredWidth = minWidth;
        return rect;
    }
    private static TMP_Text Text(Transform parent, string name, string caption, float size, Color color,
        float xmin, float ymin, float xmax, float ymax, bool bold)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, xmin, ymin, xmax, ymax);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (BistroBuilderTypography.Body != null) label.font = BistroBuilderTypography.Body;
        label.text = caption; label.fontSize = size;
        label.color = color; label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }
    private static Image Icon(Transform parent, string name, int glyph,
        float xmin, float ymin, float xmax, float ymax)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, xmin, ymin, xmax, ymax);
        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = Glyph(glyph);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }
    private static Button ControlButton(Transform parent, string name,
        float xmin, float xmax, float ymin, float ymax)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, xmin, ymin, xmax, ymax);
        Image image = Panel(rect, Control(), true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.86f, 1f);
        colors.pressedColor = new Color(0.80f, 0.72f, 0.58f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        return button;
    }

    private static Sprite MakeFrame() => frameSprite != null ? frameSprite :
        frameSprite = Rounded(new Color32(18, 19, 20, 252), new Color32(181, 130, 65, 255));
    private static Sprite Tile() => tileSprite != null ? tileSprite :
        tileSprite = Rounded(new Color32(27, 24, 22, 255), new Color32(122, 87, 52, 255));
    private static Sprite GreenTile() => greenSprite != null ? greenSprite :
        greenSprite = Rounded(new Color32(25, 38, 23, 255), new Color32(148, 114, 60, 255));
    private static Sprite Control() => controlSprite != null ? controlSprite :
        controlSprite = Rounded(new Color32(26, 23, 21, 255), new Color32(165, 124, 69, 255));
    private static Sprite Selected() => selectedSprite != null ? selectedSprite :
        selectedSprite = Rounded(new Color32(245, 202, 128, 255), new Color32(241, 215, 170, 255));
    private static Sprite ColorFill(Color color) => Rounded(color, color);

    private static Sprite Rounded(Color interior, Color stroke)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(Mathf.Abs(x - 31.5f), Mathf.Abs(y - 31.5f));
            Vector2 q = p - new Vector2(12f, 12f);
            float dist = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude +
                Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 18f;
            float alpha = Mathf.Clamp01(0.7f - dist);
            Color tint = dist > -1.6f ? stroke : interior;
            float shine = Mathf.Lerp(1.08f, 0.93f, y / 63f);
            pixels[y * size + x] = new Color(
                Mathf.Clamp01(tint.r * shine), Mathf.Clamp01(tint.g * shine),
                Mathf.Clamp01(tint.b * shine), tint.a * alpha);
        }
        texture.SetPixels(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
            new Vector4(22f, 22f, 22f, 22f));
    }

    private static Sprite Glyph(int kind)
    {
        if (IconSprites[kind] != null) return IconSprites[kind];
        const int size = 64;
        Color32[] p = new Color32[size * size];
        Color32 ink = new Color32(37, 34, 27, 255);
        Color32 gold = new Color32(248, 179, 38, 255);
        Color32 edge = new Color32(185, 110, 23, 255);
        Color32 green = new Color32(137, 213, 65, 255);
        Color32 white = new Color32(238, 242, 247, 255);
        if (kind == 0)
        {
            Ellipse(p, 23, 22, 18, 10, edge);
            Ellipse(p, 23, 26, 18, 10, gold);
            Ellipse(p, 42, 30, 16, 10, edge);
            Ellipse(p, 42, 36, 16, 10, gold);
            Ellipse(p, 41, 45, 16, 9, edge);
            Ellipse(p, 41, 48, 16, 9, gold);
            Ellipse(p, 22, 34, 17, 8, edge);
            Ellipse(p, 22, 37, 17, 8, gold);
        }
        else if (kind == 1)
        {
            Ellipse(p, 32, 32, 25, 25, new Color32(70, 123, 27, 255));
            Ellipse(p, 32, 34, 23, 23, green);
            Ellipse(p, 24, 40, 2, 3, ink);
            Ellipse(p, 40, 40, 2, 3, ink);
            for (int x = 20; x <= 44; x++)
            {
                float dx = x - 32f;
                int y = Mathf.RoundToInt(23f + dx * dx * 0.025f);
                Ellipse(p, x, y, 2, 2, ink);
            }
        }
        else if (kind >= 2 && kind <= 5)
        {
            if (kind == 2)
            {
                Ellipse(p, 24, 41, 13, 13, gold);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Line(p, Mathf.RoundToInt(24 + Mathf.Cos(a) * 17),
                        Mathf.RoundToInt(41 + Mathf.Sin(a) * 17),
                        Mathf.RoundToInt(24 + Mathf.Cos(a) * 22),
                        Mathf.RoundToInt(41 + Mathf.Sin(a) * 22), gold, 2);
                }
            }
            Ellipse(p, 37, 24, 22, 13, new Color32(155, 176, 194, 255));
            Ellipse(p, 30, 29, 17, 14, white);
            Ellipse(p, 44, 28, 15, 12, white);
            Ellipse(p, 36, 21, 24, 9, white);
            if (kind == 3 || kind == 4)
            {
                Color32 precip = new Color32(106, 184, 236, 255);
                for (int i = 0; i < 3; i++)
                {
                    int x = 23 + i * 13;
                    if (kind == 3) Line(p, x, 9, x + 4, 2, precip, 2);
                    else { Line(p, x - 3, 6, x + 3, 6, white, 1); Line(p, x, 3, x, 9, white, 1); }
                }
            }
            if (kind == 5) Line(p, 10, 10, 51, 9, white, 2);
        }
        else if (kind == 6)
        {
            for (int x = 20; x <= 27; x++)
            for (int y = 15; y <= 49; y++) p[y * size + x] = white;
            for (int x = 37; x <= 44; x++)
            for (int y = 15; y <= 49; y++) p[y * size + x] = white;
        }
        else if (kind == 7)
        {
            for (int y = 14; y <= 50; y++)
            for (int x = 18; x <= 47; x++)
                if (x <= 18 + (y - 14) / 2 && x >= 18 + (50 - y) / 2)
                    p[y * size + x] = white;
            // Triangulo simple dirigido a la derecha.
            for (int x = 21; x < 46; x++)
            for (int y = 0; y < 64; y++)
                if (Mathf.Abs(y - 32) <= (x - 20) * 0.65f)
                    p[y * size + x] = white;
        }
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(p); texture.Apply();
        IconSprites[kind] = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        return IconSprites[kind];
    }

    private static void Ellipse(Color32[] p, int cx, int cy, int rx, int ry, Color32 color)
    {
        for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(63, cy + ry); y++)
        for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(63, cx + rx); x++)
            if (Mathf.Pow((x - cx) / (float)rx, 2f) +
                Mathf.Pow((y - cy) / (float)ry, 2f) <= 1f) p[y * 64 + x] = color;
    }

    private static void Line(Color32[] p, int x0, int y0, int x1, int y1, Color32 color, int radius)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            Ellipse(p, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), radius, radius, color);
        }
    }
}
