using System;
using System.Threading;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Player settings and save actions, separate from restaurant management.</summary>
public sealed class BistroBuilderOptionsScreen : MonoBehaviour
{
    const string Pref = "BB.Options.";
    static readonly string[] Categories = { "Partida", "Audio", "Vídeo", "Jugabilidad", "Interfaz", "Controles", "Accesibilidad", "Idioma", "Créditos / Legal" };
    static readonly BBIconId[] Icons = { BBIconId.OptionsSave, BBIconId.OptionsAudio, BBIconId.OptionsVideo, BBIconId.OptionsGameplay, BBIconId.OptionsInterface, BBIconId.OptionsControls, BBIconId.OptionsAccessibility, BBIconId.OptionsLanguage, BBIconId.OptionsLegal };
    RectTransform root, content;
    TMP_Text heading, status;
    readonly Button[] tabs = new Button[9];
    BistroBuilderSaveGameService saves;
    GameClock gameClock;
    IDisposable pauseLock;
    int page, revision;
    float nextAutosave;
    bool loading;
    public bool IsOpen => root != null && root.gameObject.activeSelf;
    public static bool ReducedMotion => PlayerPrefs.GetInt(Pref + "ReducedMotion", 0) == 1;

    void Awake()
    {
        saves = FindFirstObjectByType<BistroBuilderSaveGameService>();
        gameClock = FindFirstObjectByType<GameClock>();
        if (saves != null) saves.OperationCompleted += Saved;
        AudioListener.volume = PlayerPrefs.GetFloat(Pref + "Volume", 1);
        if (PlayerPrefs.HasKey(Pref + "VSync")) QualitySettings.vSyncCount = PlayerPrefs.GetInt(Pref + "VSync");
        if (PlayerPrefs.HasKey(Pref + "FrameRate")) Application.targetFrameRate = PlayerPrefs.GetInt(Pref + "FrameRate");
        if (PlayerPrefs.HasKey(Pref + "FullScreen") && Array.IndexOf(Environment.GetCommandLineArgs(), "-screen-fullscreen") < 0)
            Screen.fullScreenMode = PlayerPrefs.GetInt(Pref + "FullScreen") == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        nextAutosave = Time.unscaledTime + 300;
    }
    void OnDestroy()
    {
        pauseLock?.Dispose();
        if (saves != null) saves.OperationCompleted -= Saved;
        if (root != null) Destroy(root.parent.gameObject);
    }
    void Update()
    {
        if (IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        if (IsOpen && saves != null && saves.IsBusy) status.text = saves.CurrentStatusMessage + " · " + Mathf.RoundToInt(saves.CurrentProgress * 100) + "%";
        if (Time.unscaledTime < nextAutosave) return;
        nextAutosave = Time.unscaledTime + 300;
        if (PlayerPrefs.GetInt(Pref + "Autosave", 0) != 1 || saves == null || saves.IsBusy || IsOpen) return;
        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>();
        if (opening != null && opening.IsVisible) return;
        // Dedicated slot; manual saves are never overwritten by autosave.
        saves.TrySaveSlot(999, "Autoguardado", out _);
    }
    public void Toggle() { if (IsOpen) Close(); else Open(); }
    public void Open()
    {
        EnsureUi(); root.gameObject.SetActive(true);
        if (PlayerPrefs.GetInt(Pref + "PauseOptions", 1) == 1 && pauseLock == null) pauseLock = gameClock?.AcquireSimulationLock("Opciones");
        ShowPage(0);
    }
    public void Close()
    {
        revision++;
        if (root != null) root.gameObject.SetActive(false);
        pauseLock?.Dispose(); pauseLock = null;
        PlayerPrefs.Save();
    }
    void EnsureUi()
    {
        if (root != null) return;
        var canvasObject = new GameObject("BB_OptionsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = .5f;
        root = Rect("OptionsPanel", canvas.transform, Vector2.zero, Vector2.one, new Vector2(64, 90), new Vector2(-64, -90));
        Surface(root, BistroBuilderUiTokens.Surface1, BistroBuilderSurfaceLevel.Panel);
        heading = Label(root, "Opciones", 30, 22, 960, 48, BistroBuilderUiStyleRole.Title);
        var close = ActionButton(root, "OptionsClose", "Cerrar · Esc", BBIconId.ActionCancel, Close);
        Position(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, -68), new Vector2(-24, -28));
        var nav = Rect("Categories", root, Vector2.zero, new Vector2(0, 1), new Vector2(20, 60), new Vector2(258, -100));
        var layout = nav.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        for (int i = 0; i < Categories.Length; i++)
        {
            int index = i;
            tabs[i] = ActionButton(nav, "OptionsCategory" + i, Categories[i], Icons[i], () => ShowPage(index));
            Height(tabs[i].gameObject, 48);
        }
        var viewport = Rect("OptionsViewport", root, Vector2.zero, Vector2.one, new Vector2(284, 72), new Vector2(-26, -106));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
        Surface(viewport, BistroBuilderUiTokens.Background, BistroBuilderSurfaceLevel.Base);
        content = Rect("SettingsContent", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(.5f, 1);
        var rows = content.gameObject.AddComponent<VerticalLayoutGroup>(); rows.padding = new RectOffset(24,24,20,20); rows.spacing = 14; rows.childControlHeight = true; rows.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        status = Label(root, "", 30, 0, 1100, 42, BistroBuilderUiStyleRole.Caption);
        Position(status.rectTransform, Vector2.zero, new Vector2(1,0), new Vector2(30, 15), new Vector2(-26, 57));
    }
    public void ShowPage(int index)
    {
        page = index; revision++; loading = false;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        content.anchoredPosition = Vector2.zero;
        heading.text = index < 0 ? "Bistro Builder" : "Opciones · " + Categories[index];
        status.text = "Los ajustes se conservan automáticamente.";
        for (int i = 0; i < tabs.Length; i++)
        { tabs[i].GetComponent<BBIconButton>()?.SetSelected(i == index); tabs[i].GetComponent<BistroBuilderInteractionSurface>()?.SetSelected(i == index); }
        switch (index)
        {
            case -1:
                Note("Tu restaurante, a tu manera", true);
                Row("Continuar jugando", BBIconId.ActionResume, Close);
                Row("Cargar partida", BBIconId.OptionsLoad, () => ListSlots(false));
                Row("Opciones", BBIconId.NavOptions, () => ShowPage(0));
                Row("Salir del juego", BBIconId.OptionsExit, ConfirmExit);
                break;
            case 0:
                Note("Tu partida", true);
                Note("Guarda tu restaurante o recupera una partida anterior. El autoguardado utiliza un espacio independiente.");
                Row("Guardar partida", BBIconId.OptionsSave, () => ListSlots(true));
                Row("Cargar partida", BBIconId.OptionsLoad, () => ListSlots(false));
                ToggleRow("Autosave cada 5 minutos", "Autosave", false, BBIconId.OptionsAutosave);
                Row("Volver al menú principal", BBIconId.OptionsHome, () => ShowPage(-1));
                Row("Salir del juego", BBIconId.OptionsExit, ConfirmExit);
                break;
            case 1:
                Note("Audio", true);
                SliderRow("Volumen general", PlayerPrefs.GetFloat(Pref + "Volume", 1), 0, 1, value => { AudioListener.volume = value; PlayerPrefs.SetFloat(Pref + "Volume", value); }, value => Mathf.RoundToInt(value * 100) + "%");
                Note("Ajusta el volumen de todos los sonidos del juego.");
                break;
            case 2:
                Note("Pantalla y rendimiento", true);
                Row("Usar pantalla completa", BBIconId.OptionsVideo, () => { Screen.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerPrefs.SetInt(Pref + "FullScreen", 1); status.text = "Pantalla completa activada."; });
                Row("Usar ventana", BBIconId.OptionsVideo, () => { Screen.fullScreenMode = FullScreenMode.Windowed; PlayerPrefs.SetInt(Pref + "FullScreen", 0); status.text = "Modo ventana activado."; });
                ToggleRow("Sincronización vertical", "VSync", QualitySettings.vSyncCount > 0, BBIconId.OptionsVideo, value => QualitySettings.vSyncCount = value ? 1 : 0);
                foreach (int fps in new[] { 30, 60, 120 }) { int limit = fps; Row("Limitar a " + limit + " FPS", BBIconId.OptionsVideo, () => { Application.targetFrameRate = limit; PlayerPrefs.SetInt(Pref + "FrameRate", limit); status.text = "Límite establecido: " + limit + " FPS. La sincronización vertical tiene prioridad."; }); }
                break;
            case 3:
                Note("Ritmo del juego", true);
                ToggleRow("Pausar mientras Opciones está abierto", "PauseOptions", true, BBIconId.ActionPause, value => { pauseLock?.Dispose(); pauseLock = value ? gameClock?.AcquireSimulationLock("Opciones") : null; });
                foreach (int speed in new[] { 1, 2, 3 }) { int selected = speed; Row("Velocidad de simulación: " + speed + "×", BBIconId.ActionResume, () => { gameClock?.SetSpeedMultiplier(selected); status.text = "Velocidad de simulación: " + selected + "×"; }); }
                break;
            case 4:
                Note("Interfaz", true);
                ToggleRow("Reducir animaciones de botones e iconos", "ReducedMotion", false, BBIconId.OptionsInterface);
                Note("La selección utiliza un borde dorado. El foco de teclado utiliza un contorno azul. Tab y Mayús + Tab recorren los controles.");
                break;
            case 5:
                Note("Controles", true);
                Note("Seleccionar / colocar     Clic izquierdo\nArrastrar un habitáculo     Mantener clic y arrastrar\nZoom     Rueda del ratón\nDesplazar cámara     Botón central y arrastrar\nGirar cámara     Botón derecho y arrastrar\nRecorrer la interfaz     Tab / Mayús + Tab\nActivar un botón     Intro\nCerrar Opciones     Esc");
                break;
            case 6:
                Note("Accesibilidad", true);
                ToggleRow("Movimiento reducido", "ReducedMotion", false, BBIconId.OptionsAccessibility);
                Note("Foco azul visible al navegar con teclado. Los estados combinan texto, iconos y color. Los controles no disponibles se muestran atenuados.");
                break;
            case 7:
                Note("Idioma", true);
                Note("Español\nIdioma disponible en esta versión del juego.");
                break;
            case 8:
                Note("Créditos y licencias", true);
                Note("Bistro Builder\nDiseña, gestiona y haz crecer tu restaurante.\n\nTipografía de interfaz: Inter — SIL Open Font License.\nTítulos: Recoleta Regular DEMO — Latinotype.\nIconos base: Lucide — ISC License.\nIconos de Opciones: ilustraciones vectoriales creadas para Bistro Builder.\nMotor: Unity.\n\nVersión " + Application.version);
                break;
        }
    }
    async void ListSlots(bool save)
    {
        if (saves == null || saves.IsBusy) { status.text = "El sistema de partidas no está disponible ahora."; return; }
        ShowPage(0); int request = revision;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Note(save ? "Elige dónde guardar" : "Elige la partida que quieres cargar", true);
        status.text = "Buscando partidas…";
        try
        {
            var slots = await saves.ReadSlotSummariesAsync(CancellationToken.None);
            if (this == null || !IsOpen || request != revision) return;
            var occupied = new System.Collections.Generic.HashSet<int>();
            foreach (var slot in slots)
            {
                occupied.Add(slot.SlotIndex); int number = slot.SlotIndex;
                if (save && number == 999) continue;
                string date = DateTime.TryParse(slot.CreatedUtc, out var parsed) ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
                Row(slot.SlotDisplayName + " · " + date + " · " + number, save ? BBIconId.OptionsSave : BBIconId.OptionsLoad,
                    () => Confirm(save ? "¿Sobrescribir esta partida?" : "¿Cargar esta partida? Los cambios sin guardar se perderán.", () => RunSave(number, save)));
            }
            if (save)
            {
                int empty = 1; while (occupied.Contains(empty) && empty < 999) empty++;
                if (empty < 999) { int number = empty; Row("Crear nuevo guardado", BBIconId.OptionsSave, () => RunSave(number, true)); }
            }
            else if (slots.Count == 0) Note("Todavía no hay partidas guardadas.");
            Row("Volver", BBIconId.GeneralBack, () => ShowPage(0));
            status.text = "";
        }
        catch (Exception e) { if (this != null && request == revision) status.text = "No se pudieron leer las partidas: " + e.Message; }
    }
    void RunSave(int slot, bool save)
    {
        if (saves == null || saves.IsBusy) return;
        string error;
        var restaurant = FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        loading = !save;
        bool accepted = save ? saves.TrySaveSlot(slot, restaurant?.RestaurantName ?? "Mi restaurante", out error) : saves.TryLoadSlot(slot, out error);
        status.text = accepted ? (save ? "Guardando…" : "Cargando…") : error;
        if (!accepted) loading = false;
    }
    void Saved(BistroBuilderSaveOperationResult result)
    {
        if (status != null) status.text = result.Succeeded ? "Partida " + (result.OperationKind == BistroBuilderSaveOperationKind.Load ? "cargada." : "guardada.") : result.Message;
        if (loading && result.Succeeded) Close(); loading = false;
    }
    void ConfirmExit() => Confirm("¿Salir del juego? Los cambios sin guardar se perderán.", () => { PlayerPrefs.Save(); Application.Quit(); });
    void Confirm(string question, Action action)
    {
        revision++;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Note(question, true);
        Row("Confirmar", BBIconId.ActionConfirm, () => { if (saves?.IsBusy == true) { status.text = "Espera a que termine el guardado o la carga."; return; } action(); });
        Row("Cancelar", BBIconId.ActionCancel, () => ShowPage(page));
    }
    void Note(string value, bool title = false)
    {
        var text = Label(content, value, 0, 0, 0, 0, title ? BistroBuilderUiStyleRole.Heading : BistroBuilderUiStyleRole.Body);
        text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal;
        var element = text.gameObject.AddComponent<LayoutElement>(); element.minHeight = title ? 38 : 48;
    }
    void Row(string label, BBIconId icon, Action action) { var button = ActionButton(content, "OptionAction", label, icon, action); Height(button.gameObject, 52); }
    void ToggleRow(string label, string key, bool defaultValue, BBIconId icon, Action<bool> apply = null)
    {
        bool value = PlayerPrefs.GetInt(Pref + key, defaultValue ? 1 : 0) == 1;
        Button button = null;
        button = ActionButton(content, "OptionToggle" + key, label + (value ? " · Activado" : " · Desactivado"), icon, () => {
            value = !value; PlayerPrefs.SetInt(Pref + key, value ? 1 : 0); PlayerPrefs.Save(); apply?.Invoke(value);
            button.GetComponentInChildren<TMP_Text>().text = label + (value ? " · Activado" : " · Desactivado");
            button.GetComponent<BBIconButton>().SetSelected(value);
        });
        button.GetComponent<BBIconButton>().SetSelected(value); Height(button.gameObject, 52);
    }
    void SliderRow(string label, float value, float min, float max, Action<float> action, Func<float,string> format)
    {
        var holder = Rect("VolumeControl", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); Height(holder.gameObject, 96);
        var text = Label(holder, label + " · " + format(value), 0, 0, 680, 32, BistroBuilderUiStyleRole.Label);
        var bar = Rect("VolumeSlider", holder, Vector2.zero, new Vector2(1,0), new Vector2(8, 18), new Vector2(-8, 50));
        var bg = Surface(bar, BistroBuilderUiTokens.Surface2, BistroBuilderSurfaceLevel.Base);
        var handle = Rect("Handle", bar, Vector2.zero, Vector2.up, new Vector2(-10,0), new Vector2(10,0));
        var handleImage = Surface(handle, BistroBuilderUiTokens.Attention, BistroBuilderSurfaceLevel.Base);
        var slider = bar.gameObject.AddComponent<Slider>(); slider.targetGraphic = handleImage; slider.handleRect = handle; slider.minValue = min; slider.maxValue = max; slider.value = value;
        slider.onValueChanged.AddListener(v => { action(v); text.text = label + " · " + format(v); });
    }
    static Button ActionButton(Transform parent, string name, string label, BBIconId icon, Action action)
    {
        var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = Surface(rect, Color.white, BistroBuilderSurfaceLevel.Card);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.colors = BistroBuilderUiTokens.ButtonColors(BistroBuilderUiTokens.Surface2, BistroBuilderUiTokens.PrimaryHover, BistroBuilderUiTokens.PrimaryPressed);
        var text = Label(rect, label, 0, 0, 0, 0, BistroBuilderUiStyleRole.Label);
        Position(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 6), new Vector2(-12, -6));
        text.alignment = TextAlignmentOptions.MidlineLeft; text.enableAutoSizing = false; text.fontSize = 14;
        BBIconographyRuntime.Decorate(button, icon);
        button.onClick.AddListener(() => action());
        rect.gameObject.AddComponent<BistroBuilderInteractionSurface>();
        return button;
    }
    static TMP_Text Label(Transform parent, string value, float x, float y, float w, float h, BistroBuilderUiStyleRole role)
    {
        var rect = Rect("Label", parent, new Vector2(0,1), new Vector2(0,1), new Vector2(x, -y-h), new Vector2(x+w,-y));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.color = BistroBuilderUiTokens.TextPrimary; text.raycastTarget = false; BistroBuilderTypography.Apply(text, role); return text;
    }
    static void Height(GameObject obj, float height) { var element = obj.AddComponent<LayoutElement>(); element.minHeight = element.preferredHeight = height; }
    static Image Surface(RectTransform rect, Color color, BistroBuilderSurfaceLevel level) { var image = rect.gameObject.AddComponent<Image>(); image.color = color; BistroBuilderSurface.Apply(image, level); return image; }
    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
    { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); var rect = (RectTransform)obj.transform; Position(rect,min,max,insetMin,insetMax); return rect; }
    static void Position(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = insetMin; rect.offsetMax = insetMax; }
}
