using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 2.3JKL-B2 — capa transversal de interacción para las UIs jugables de
/// Carta, Inventario y Proveedores.
///
/// Responsabilidades estrictamente de Presentation:
/// - aislar los accesos globales cuando hay un modal funcional abierto;
/// - añadir ayuda contextual por hover a los controles jugables;
/// - sustituir los antiguos botones "clic para ciclar" por un selector
///   desplazable, sin tocar las autoridades ni sus datos.
///
/// El selector reutiliza deliberadamente el callback canónico del botón
/// original para aplicar una opción. No refleja campos privados, no escribe
/// en dominios y devuelve el control cíclico a su valor inicial cuando solo
/// enumera las opciones.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Unified UI Interaction Service 2.3JKL-B2")]
public sealed class BistroBuilderUnifiedUiInteractionService : MonoBehaviour
{
    public const string RuntimeRevision = "UI-2.3JKL-B2.2";

    private static readonly HashSet<string> KnownModalRootNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MenuEditorModal",
            "DishRecipeAuthoringModal",
            "MenuPortfolioModal",
            "InventoryWarehouseModal",
            "SuppliersModal"
        };

    private static readonly HashSet<string> GlobalAccessButtonNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "OpenMenuEditor",
            "OpenMenuPortfolio",
            "OpenInventoryWarehouse",
            "OpenSuppliers"
        };

    private static readonly HashSet<string> ScrollSelectorControlNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MealService",
            "ServiceMode",
            "Category",
            "Course",
            "Station",
            "Unit",
            "Filter",
            "Sort",
            "Reason",
            "Tipo",
            "Cartadestino"
        };

    [SerializeField, Range(0.10f, 2f)]
    private float rescanIntervalSeconds = 0.35f;

    [SerializeField, Range(0f, 1.5f)]
    private float tooltipDelaySeconds = 0.32f;

    [SerializeField, Range(280f, 620f)]
    private float selectorWidth = 440f;

    private readonly List<RectTransform> modalRoots = new List<RectTransform>(12);
    private readonly List<Button> globalAccessButtons = new List<Button>(8);
    private readonly List<BistroBuilderScrollableSelectorTrigger> selectorTriggers =
        new List<BistroBuilderScrollableSelectorTrigger>(24);
    private readonly Dictionary<GameObject, bool> suppressedOriginalStates =
        new Dictionary<GameObject, bool>();

    private Canvas canvas;
    private RectTransform canvasRect;
    private float nextScanTime;
    private bool globalAccessSuppressed;

    private RectTransform tooltipLayer;
    private RectTransform tooltipCard;
    private Text tooltipTitle;
    private Text tooltipBody;
    private string pendingTooltipTitle;
    private string pendingTooltipBody;
    private Vector2 pendingTooltipScreenPosition;
    private float pendingTooltipAt;
    private bool tooltipPending;

    private RectTransform selectorLayer;
    private RectTransform selectorCard;
    private RectTransform selectorContent;
    private Text selectorTitleText;
    private Text selectorHintText;
    private bool selectorOpen;

    public int SelectorTriggerCount => selectorTriggers.Count;

    public int TooltipTriggerCount
    {
        get
        {
            if (canvas == null) return 0;
            return canvas.GetComponentsInChildren<BistroBuilderTooltipTrigger>(true).Length;
        }
    }

    public int GlobalAccessButtonCount => globalAccessButtons.Count;
    public bool IsGlobalAccessSuppressed => globalAccessSuppressed;
    public bool IsSelectorOpen => selectorOpen;

    private void Awake()
    {
        ResolveCanvas();
        if (Application.isPlaying)
        {
            EnsureOverlayInfrastructure();
            RunImmediateScanForTests();
        }
    }

    private void OnEnable()
    {
        ResolveCanvas();
        if (Application.isPlaying)
        {
            EnsureOverlayInfrastructure();
            RunImmediateScanForTests();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Time.unscaledTime >= nextScanTime)
        {
            RunImmediateScanForTests();
        }

        UpdateTooltip();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        ApplyContextVisibilityForTests();
    }

    private void OnDisable()
    {
        RestoreGlobalAccessButtons();
        HideTooltip();
        CloseSelector();
    }

    private void OnDestroy()
    {
        RestoreGlobalAccessButtons();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveCanvas();
        if (canvas == null)
        {
            error = "2.3JKL-B2 debe estar bajo el Canvas HUD canónico.";
            return false;
        }
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            error = "El Canvas HUD no dispone de GraphicRaycaster.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reescaneo público para validadores. Es seguro: solo añade componentes de
    /// Presentation y no abre ni muta ninguna autoridad de dominio.
    /// </summary>
    public void RunImmediateScanForTests()
    {
        if (!Application.isPlaying) return;
        ResolveCanvas();
        if (canvas == null) return;

        EnsureOverlayInfrastructure();
        modalRoots.Clear();
        globalAccessButtons.Clear();
        selectorTriggers.Clear();

        RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && KnownModalRootNames.Contains(rect.name))
            {
                modalRoots.Add(rect);
            }
        }

        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || IsInternalUi(button.transform)) continue;

            if (GlobalAccessButtonNames.Contains(button.name))
            {
                globalAccessButtons.Add(button);
            }

            if (IsInsideKnownModule(button.transform) &&
                ScrollSelectorControlNames.Contains(button.name))
            {
                BistroBuilderScrollableSelectorTrigger trigger =
                    button.GetComponent<BistroBuilderScrollableSelectorTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<BistroBuilderScrollableSelectorTrigger>();
                }
                trigger.Initialize(this, button);
                selectorTriggers.Add(trigger);
            }
        }

        Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null || IsInternalUi(selectable.transform) ||
                !IsInsideKnownModule(selectable.transform))
            {
                continue;
            }

            // Los hit-target de selectores reciben su tooltip desde el trigger;
            // el control original queda debajo y no recibe eventos de puntero.
            if (selectable.GetComponent<BistroBuilderScrollableSelectorTrigger>() != null)
            {
                continue;
            }

            EnsureTooltipTrigger(selectable.gameObject, selectable);
        }

        // Incluye tooltips de los accesos globales aunque estén fuera del modal.
        for (int i = 0; i < globalAccessButtons.Count; i++)
        {
            Button button = globalAccessButtons[i];
            if (button != null) EnsureTooltipTrigger(button.gameObject, button);
        }

        nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, rescanIntervalSeconds);
    }

    public void ApplyContextVisibilityForTests()
    {
        if (!Application.isPlaying || canvas == null) return;

        bool hasOpenModal = false;
        for (int i = 0; i < modalRoots.Count; i++)
        {
            RectTransform modal = modalRoots[i];
            if (modal != null && modal.gameObject.activeInHierarchy)
            {
                hasOpenModal = true;
                break;
            }
        }

        if (hasOpenModal)
        {
            SuppressGlobalAccessButtons();
        }
        else
        {
            RestoreGlobalAccessButtons();
        }
    }

    public bool AreOriginallyVisibleGlobalAccessButtonsHiddenForTests()
    {
        if (!globalAccessSuppressed) return false;
        foreach (KeyValuePair<GameObject, bool> pair in suppressedOriginalStates)
        {
            if (pair.Key != null && pair.Value && pair.Key.activeSelf)
            {
                return false;
            }
        }
        return true;
    }

    public bool TryGetSelectorTrigger(
        string controlName,
        out BistroBuilderScrollableSelectorTrigger trigger)
    {
        trigger = null;
        if (string.IsNullOrWhiteSpace(controlName)) return false;
        for (int i = 0; i < selectorTriggers.Count; i++)
        {
            BistroBuilderScrollableSelectorTrigger current = selectorTriggers[i];
            if (current != null && string.Equals(
                    current.OriginalControlName,
                    controlName,
                    StringComparison.Ordinal))
            {
                trigger = current;
                return true;
            }
        }
        return false;
    }

    public bool TryGetTooltipForControl(
        string controlName,
        out string title,
        out string description)
    {
        title = string.Empty;
        description = string.Empty;
        if (canvas == null || string.IsNullOrWhiteSpace(controlName)) return false;

        Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null || !string.Equals(
                    selectable.name,
                    controlName,
                    StringComparison.Ordinal))
            {
                continue;
            }
            BuildHelp(selectable, out title, out description);
            return !string.IsNullOrWhiteSpace(description);
        }
        return false;
    }

    internal void OpenSelector(BistroBuilderScrollableSelectorTrigger trigger)
    {
        if (trigger == null || !Application.isPlaying) return;
        EnsureOverlayInfrastructure();
        HideTooltip();

        if (!trigger.TryEnumerateOptions(
                out List<string> options,
                out string current,
                out string error))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning("2.3JKL-B2 no pudo abrir selector: " + error, trigger);
            }
            return;
        }

        ClearSelectorOptions();
        selectorTitleText.text = "Seleccionar " + trigger.SelectorTitle;
        selectorHintText.text = options.Count > 6
            ? "Elige una opción · desplázate con la rueda del ratón."
            : "Elige una opción.";

        for (int i = 0; i < options.Count; i++)
        {
            string option = options[i];
            string captured = option;
            string visibleOption = FormatSelectorOption(trigger.OriginalControlName, option);
            if (string.Equals(option, current, StringComparison.Ordinal))
            {
                visibleOption = "✓  " + visibleOption;
            }
            Button row = BistroBuilderMenuEditorUiFactory.CreateButton(
                "BB_B2_Option_" + i,
                selectorContent,
                visibleOption,
                () =>
                {
                    if (!trigger.TrySelectLabel(captured, out string selectError) &&
                        !string.IsNullOrWhiteSpace(selectError))
                    {
                        Debug.LogWarning("2.3JKL-B2: " + selectError, trigger);
                    }
                    CloseSelector();
                },
                string.Equals(option, current, StringComparison.Ordinal)
                    ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                    : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                14
            );
            StabilizeButton(row);
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(row, 42f);
        }

        float height = Mathf.Clamp(120f + options.Count * 47f, 220f, 610f);
        selectorCard.sizeDelta = new Vector2(selectorWidth, height);
        selectorLayer.gameObject.SetActive(true);
        selectorLayer.SetAsLastSibling();
        selectorOpen = true;
        Canvas.ForceUpdateCanvases();
    }

    public void CloseSelector()
    {
        selectorOpen = false;
        if (selectorLayer != null) selectorLayer.gameObject.SetActive(false);
    }

    internal void RequestTooltip(
        string title,
        string description,
        Vector2 screenPosition)
    {
        if (selectorOpen || string.IsNullOrWhiteSpace(description)) return;
        pendingTooltipTitle = title ?? string.Empty;
        pendingTooltipBody = description ?? string.Empty;
        pendingTooltipScreenPosition = screenPosition;
        pendingTooltipAt = Time.unscaledTime + Mathf.Max(0f, tooltipDelaySeconds);
        tooltipPending = true;
    }

    internal void UpdateTooltipPointer(Vector2 screenPosition)
    {
        pendingTooltipScreenPosition = screenPosition;
        if (tooltipCard != null && tooltipCard.gameObject.activeSelf)
        {
            PositionTooltip(screenPosition);
        }
    }

    internal void CancelTooltip()
    {
        tooltipPending = false;
        HideTooltip();
    }

    internal void BuildHelp(
        Selectable selectable,
        out string title,
        out string description)
    {
        string label = GetSelectableLabel(selectable);
        string name = selectable != null ? selectable.name : string.Empty;
        title = string.IsNullOrWhiteSpace(label)
            ? HumanizeControlName(name)
            : FirstLine(label);

        switch (name)
        {
            case "MealService":
                description = "Selecciona el servicio del día que estás editando. La lista permite elegir directamente sin recorrer valores uno a uno.";
                return;
            case "ServiceMode":
                description = "Selecciona la modalidad de servicio de la carta: mesa, barra u otra modalidad disponible.";
                return;
            case "Category":
                description = "Categoría visible del plato. Abre el selector para elegirla directamente de la lista disponible.";
                return;
            case "Course":
                description = "Pase gastronómico del plato: bienvenida, entrante, principal, postre, bebida, etc.";
                return;
            case "Station":
                description = "Estación de cocina responsable de preparar este plato.";
                return;
            case "Unit":
                description = "Unidad de medida de este ingrediente. Solo se muestran unidades compatibles con su dimensión.";
                return;
            case "Filter":
                description = "Filtra el listado de Inventario/Almacén por estado de stock, criticidad o caducidad.";
                return;
            case "Sort":
                description = "Cambia el criterio de ordenación del listado sin modificar las existencias.";
                return;
            case "Reason":
                description = "Motivo administrativo que quedará registrado si realizas un ajuste manual de inventario.";
                return;
            case "Tipo":
                description = "Tipo de regla automática de la carta: horario, temporada, evento, promoción o regla compuesta.";
                return;
            case "Cartadestino":
                description = "Carta que se activará cuando esta regla cumpla sus condiciones.";
                return;
            case "EventId":
                description = "Identificador del evento que puedes activar o desactivar para probar reglas de carta. No crea un evento nuevo.";
                return;
            case "PromotionId":
                description = "Identificador de promoción usado por el contexto de reglas. Sirve para simular su activación o desactivación.";
                return;
            case "RequiredEvent":
                description = "EventId que debe estar activo para que la regla seleccionada pueda aplicarse.";
                return;
            case "RequiredPromotion":
                description = "PromotionId que debe estar activo para que la regla seleccionada pueda aplicarse.";
                return;
            case "Priority":
                description = "Prioridad de la regla. Si varias coinciden, el sistema resuelve por prioridad, especificidad y RuleId estable.";
                return;
            case "StartDate":
                description = "Fecha inicial opcional de la regla, con formato YYYY-MM-DD.";
                return;
            case "EndDate":
                description = "Fecha final opcional de la regla, con formato YYYY-MM-DD.";
                return;
            case "StartTime":
                description = "Hora inicial opcional de la regla, con formato HH:mm.";
                return;
            case "EndTime":
                description = "Hora final opcional de la regla, con formato HH:mm.";
                return;
            case "+Evento":
                description = "Activa temporalmente el event_id escrito a la izquierda para comprobar cómo resuelven las reglas automáticas.";
                return;
            case "+Promo":
                description = "Activa temporalmente el promotion_id escrito a la izquierda para comprobar cómo resuelven las reglas automáticas.";
                return;
            case "−":
                description = "Desactiva el evento o promoción indicado en el campo situado a la izquierda.";
                return;
            case "Fijarcomobase":
                description = "Marca la carta seleccionada como carta base cuando ninguna regla de mayor prioridad resulte aplicable.";
                return;
            case "Activarmanual":
                description = "Fuerza temporalmente la carta seleccionada como activa, por encima de las reglas automáticas.";
                return;
            case "Reglasautomáticas":
                description = "Elimina la activación manual y devuelve la elección de carta al motor de reglas.";
                return;
            case "Editarcartaactiva":
                description = "Abre el editor de platos de la carta que está activa en este momento.";
                return;
            case "Nuevaregla":
                description = "Limpia el formulario y prepara una nueva regla de activación sin guardar nada todavía.";
                return;
            case "Eliminarregla":
                description = "Elimina la regla seleccionada después de validarla mediante el servicio canónico de cartas.";
                return;
            case "Guardaregla":
                description = "Valida y guarda la regla actual; la resolución de carta se recalcula inmediatamente.";
                return;
            case "OpenMenuEditor":
                description = "Abre el editor de la carta activa y sus platos.";
                return;
            case "OpenMenuPortfolio":
                description = "Abre la gestión de cartas independientes y sus reglas automáticas.";
                return;
            case "OpenInventoryWarehouse":
                description = "Abre Inventario/Almacén para consultar existencias, alertas, movimientos y recepciones.";
                return;
            case "OpenSuppliers":
                description = "Abre Proveedores: catálogo, Compra Inteligente, pedidos y progreso de desbloqueo.";
                return;
        }

        switch (title.ToUpperInvariant())
        {
            case "EXISTENCIAS":
                description = "Muestra stock físico, reservado, disponible, mínimos y estado de cada ingrediente.";
                return;
            case "ALERTAS":
                description = "Muestra únicamente las alertas activas derivadas del Inventario y la planificación.";
                return;
            case "MOVIMIENTOS":
                description = "Consulta el historial jugable de entradas, consumos y correcciones del ledger canónico.";
                return;
            case "RECEPCIONES":
                description = "Consulta las recepciones de mercancía reconstruidas desde los movimientos Purchase del inventario.";
                return;
            case "COMPROBAR APERTURA":
                description = "Evalúa stock bajo, crítico, agotado y próxima caducidad antes de abrir el servicio; informa, no bloquea.";
                return;
            case "COMPRA INTELIGENTE":
                description = "Compara Ahorrar, Equilibrado y Urgente utilizando stock, previsión, precios, plazos, fiabilidad y desperdicio.";
                return;
            case "VER CATÁLOGO":
                description = "Abre las ofertas vigentes del proveedor seleccionado.";
                return;
            case "VER PEDIDOS":
                description = "Abre el seguimiento de pedidos reales y su estado logístico.";
                return;
            case "REVISAR Y CONFIRMAR":
                description = "Abre el resumen de confirmación. La cotización se vuelve a validar antes de congelar el pedido.";
                return;
            case "CONFIRMAR PEDIDO":
                description = "Confirma el pedido con la cotización vigente y congela precio, portes y condiciones comerciales.";
                return;
            case "CANCELAR PEDIDO":
            case "CANCELAR BORRADOR":
                description = "Cancela el pedido si su estado actual todavía permite cancelación.";
                return;
        }

        if (selectable is InputField)
        {
            description = "Introduce o edita este valor. Los cambios solo se aplicarán mediante la acción de guardar o confirmar correspondiente.";
            return;
        }
        if (selectable is Toggle)
        {
            description = "Activa o desactiva «" + title + "».";
            return;
        }

        if (name.StartsWith("Supplier_", StringComparison.Ordinal))
        {
            description = "Selecciona este proveedor para ver condiciones comerciales, fiabilidad, catálogo y progreso de desbloqueo.";
            return;
        }
        if (name.StartsWith("Offer_", StringComparison.Ordinal))
        {
            description = "Selecciona esta oferta para revisar formato, precio actual, disponibilidad y condiciones de compra.";
            return;
        }
        if (name.StartsWith("Plan_", StringComparison.Ordinal))
        {
            description = "Selecciona esta estrategia de Compra Inteligente para revisar coste, cobertura, riesgos y motivos de la recomendación.";
            return;
        }
        if (name.StartsWith("Order_", StringComparison.Ordinal))
        {
            description = "Selecciona este pedido para consultar su estado, precio congelado, logística y recepción.";
            return;
        }

        description = string.IsNullOrWhiteSpace(title)
            ? "Control interactivo de esta pantalla."
            : "Acción: " + title + ".";
    }

    internal string GetSelectorTitle(Button button)
    {
        if (button == null) return "opción";
        switch (button.name)
        {
            case "MealService": return "servicio del día";
            case "ServiceMode": return "modalidad de servicio";
            case "Category": return "categoría";
            case "Course": return "pase gastronómico";
            case "Station": return "estación de cocina";
            case "Unit": return "unidad";
            case "Filter": return "filtro";
            case "Sort": return "orden";
            case "Reason": return "motivo del ajuste";
            case "Tipo": return "tipo de regla";
            case "Cartadestino": return "carta destino";
            default: return HumanizeControlName(button.name).ToLowerInvariant();
        }
    }

    private void ResolveCanvas()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
        }
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    private bool IsInsideKnownModule(Transform target)
    {
        if (target == null) return false;
        Transform current = target;
        while (current != null && current != canvas.transform)
        {
            if (KnownModalRootNames.Contains(current.name)) return true;
            current = current.parent;
        }
        return false;
    }

    private bool IsInternalUi(Transform target)
    {
        if (target == null) return false;
        if (target.name.StartsWith("BB_B2_", StringComparison.Ordinal)) return true;
        if (tooltipLayer != null && target.IsChildOf(tooltipLayer)) return true;
        if (selectorLayer != null && target.IsChildOf(selectorLayer)) return true;
        return false;
    }

    private void SuppressGlobalAccessButtons()
    {
        globalAccessSuppressed = true;
        for (int i = 0; i < globalAccessButtons.Count; i++)
        {
            Button button = globalAccessButtons[i];
            if (button == null) continue;
            GameObject go = button.gameObject;
            // Solo tomamos posesión temporal de accesos que realmente estaban
            // visibles. El modal que se abre suele ocultar su propio botón;
            // memorizar ese false haría que lo volviéramos a ocultar después
            // de que la vista lo restaurase al cerrar.
            if (!go.activeSelf) continue;
            if (!suppressedOriginalStates.ContainsKey(go))
            {
                suppressedOriginalStates.Add(go, true);
            }
            go.SetActive(false);
        }
    }

    private void RestoreGlobalAccessButtons()
    {
        if (!globalAccessSuppressed && suppressedOriginalStates.Count == 0) return;
        foreach (KeyValuePair<GameObject, bool> pair in suppressedOriginalStates)
        {
            if (pair.Key != null) pair.Key.SetActive(pair.Value);
        }
        suppressedOriginalStates.Clear();
        globalAccessSuppressed = false;
    }

    private void EnsureTooltipTrigger(GameObject target, Selectable selectable)
    {
        if (target == null || selectable == null) return;
        BistroBuilderTooltipTrigger trigger = target.GetComponent<BistroBuilderTooltipTrigger>();
        if (trigger == null) trigger = target.AddComponent<BistroBuilderTooltipTrigger>();
        BuildHelp(selectable, out string title, out string description);
        trigger.Initialize(this, title, description);
    }

    private void EnsureOverlayInfrastructure()
    {
        if (canvas == null || canvasRect == null) return;
        if (tooltipLayer == null) BuildTooltipLayer();
        if (selectorLayer == null) BuildSelectorLayer();
    }

    private void BuildTooltipLayer()
    {
        tooltipLayer = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_B2_TooltipLayer",
            canvas.transform,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        tooltipLayer.SetAsLastSibling();

        tooltipCard = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_B2_TooltipCard",
            tooltipLayer,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        tooltipCard.pivot = new Vector2(0f, 1f);
        tooltipCard.sizeDelta = new Vector2(420f, 96f);
        Image background = BistroBuilderMenuEditorUiFactory.AddImage(
            tooltipCard,
            new Color(0.055f, 0.065f, 0.06f, 0.985f)
        );
        background.raycastTarget = false;

        tooltipTitle = BistroBuilderMenuEditorUiFactory.CreateText(
            "BB_B2_TooltipTitle",
            tooltipCard,
            string.Empty,
            14,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(tooltipTitle.rectTransform, 0f, 0.58f, 1f, 1f, new Vector2(14f, 8f), new Vector2(-14f, -8f));

        tooltipBody = BistroBuilderMenuEditorUiFactory.CreateText(
            "BB_B2_TooltipBody",
            tooltipCard,
            string.Empty,
            12,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        tooltipBody.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(tooltipBody.rectTransform, 0f, 0f, 1f, 0.62f, new Vector2(14f, 10f), new Vector2(-14f, -4f));

        tooltipCard.gameObject.SetActive(false);
    }

    private void BuildSelectorLayer()
    {
        selectorLayer = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_B2_SelectorLayer",
            canvas.transform,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Image dim = BistroBuilderMenuEditorUiFactory.AddImage(
            selectorLayer,
            new Color(0.01f, 0.012f, 0.011f, 0.72f)
        );
        dim.raycastTarget = true;
        Button backdrop = selectorLayer.gameObject.AddComponent<Button>();
        backdrop.targetGraphic = dim;
        backdrop.transition = Selectable.Transition.None;
        backdrop.navigation = new Navigation { mode = Navigation.Mode.None };
        backdrop.onClick.AddListener(CloseSelector);

        selectorCard = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_B2_SelectorCard",
            selectorLayer,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        selectorCard.pivot = new Vector2(0.5f, 0.5f);
        selectorCard.sizeDelta = new Vector2(selectorWidth, 420f);
        Image cardImage = BistroBuilderMenuEditorUiFactory.AddImage(
            selectorCard,
            new Color(0.075f, 0.085f, 0.08f, 1f)
        );
        cardImage.raycastTarget = true;

        selectorTitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "BB_B2_SelectorTitle",
            selectorCard,
            "Seleccionar opción",
            20,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(selectorTitleText.rectTransform, 0f, 0.86f, 0.76f, 1f, new Vector2(18f, 0f), Vector2.zero);

        Button close = BistroBuilderMenuEditorUiFactory.CreateButton(
            "BB_B2_SelectorClose",
            selectorCard,
            "CERRAR",
            CloseSelector,
            new Color(0.23f, 0.16f, 0.15f, 1f),
            12
        );
        StabilizeButton(close);
        SetRect(close.GetComponent<RectTransform>(), 0.77f, 0.89f, 0.96f, 0.97f, Vector2.zero, Vector2.zero);

        selectorHintText = BistroBuilderMenuEditorUiFactory.CreateText(
            "BB_B2_SelectorHint",
            selectorCard,
            string.Empty,
            11,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(selectorHintText.rectTransform, 0.04f, 0.78f, 0.96f, 0.86f, Vector2.zero, Vector2.zero);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "BB_B2_SelectorScroll",
            selectorCard,
            out selectorContent
        );
        SetRect(scroll.GetComponent<RectTransform>(), 0.04f, 0.06f, 0.94f, 0.77f, Vector2.zero, Vector2.zero);
        AttachElegantScrollbar(scroll, selectorCard);
        selectorLayer.gameObject.SetActive(false);
    }

    private static void AttachElegantScrollbar(ScrollRect scroll, RectTransform parent)
    {
        if (scroll == null || parent == null) return;

        RectTransform barRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "BB_B2_SelectorScrollbar",
            parent,
            new Vector2(0.945f, 0.06f),
            new Vector2(0.97f, 0.77f),
            Vector2.zero,
            Vector2.zero
        );
        Image background = BistroBuilderMenuEditorUiFactory.AddImage(
            barRoot,
            new Color(0.10f, 0.115f, 0.105f, 0.75f)
        );
        background.raycastTarget = true;

        Scrollbar scrollbar = barRoot.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = background;

        RectTransform slidingArea = BistroBuilderMenuEditorUiFactory.CreateRect(
            "SlidingArea",
            barRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(3f, 3f),
            new Vector2(-3f, -3f)
        );
        RectTransform handle = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Handle",
            slidingArea,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Image handleImage = BistroBuilderMenuEditorUiFactory.AddImage(
            handle,
            new Color(0.46f, 0.50f, 0.46f, 0.95f)
        );
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = 4f;
    }

    private void ClearSelectorOptions()
    {
        if (selectorContent == null) return;
        for (int i = selectorContent.childCount - 1; i >= 0; i--)
        {
            Destroy(selectorContent.GetChild(i).gameObject);
        }
    }

    private void UpdateTooltip()
    {
        if (!tooltipPending || selectorOpen) return;
        if (Time.unscaledTime < pendingTooltipAt) return;
        tooltipPending = false;
        ShowTooltip(pendingTooltipTitle, pendingTooltipBody, pendingTooltipScreenPosition);
    }

    private void ShowTooltip(string title, string body, Vector2 screenPosition)
    {
        EnsureOverlayInfrastructure();
        if (tooltipCard == null) return;
        tooltipTitle.text = title ?? string.Empty;
        tooltipBody.text = body ?? string.Empty;
        tooltipCard.gameObject.SetActive(true);
        tooltipLayer.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        float bodyHeight = Mathf.Clamp(tooltipBody.preferredHeight, 32f, 150f);
        tooltipCard.sizeDelta = new Vector2(420f, Mathf.Clamp(58f + bodyHeight, 86f, 210f));
        PositionTooltip(screenPosition);
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        if (tooltipCard == null || canvasRect == null) return;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 local))
        {
            return;
        }

        Vector2 size = tooltipCard.sizeDelta;
        Rect rect = canvasRect.rect;
        float x = local.x + 18f;
        float y = local.y - 18f;
        x = Mathf.Clamp(x, rect.xMin + 8f, rect.xMax - size.x - 8f);
        y = Mathf.Clamp(y, rect.yMin + size.y + 8f, rect.yMax - 8f);
        tooltipCard.anchoredPosition = new Vector2(x, y);
    }

    private void HideTooltip()
    {
        tooltipPending = false;
        if (tooltipCard != null) tooltipCard.gameObject.SetActive(false);
    }

    private static string GetSelectableLabel(Selectable selectable)
    {
        if (selectable == null) return string.Empty;
        if (selectable is InputField input)
        {
            Text placeholder = input.placeholder as Text;
            if (placeholder != null && !string.IsNullOrWhiteSpace(placeholder.text))
                return placeholder.text.Trim();
        }

        Text[] texts = selectable.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                return text.text.Trim();
        }
        return string.Empty;
    }

    private static string FirstLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        int newline = value.IndexOf('\n');
        return (newline >= 0 ? value.Substring(0, newline) : value).Trim();
    }

    private static string FormatSelectorOption(string controlName, string value)
    {
        string text = value ?? string.Empty;
        string[] prefixes;
        switch (controlName)
        {
            case "Filter": prefixes = new[] { "Filtro:" }; break;
            case "Sort": prefixes = new[] { "Orden:" }; break;
            case "Reason": prefixes = new[] { "Motivo:" }; break;
            case "Tipo": prefixes = new[] { "Tipo:" }; break;
            case "Cartadestino": prefixes = new[] { "Destino:" }; break;
            default: prefixes = Array.Empty<string>(); break;
        }

        for (int i = 0; i < prefixes.Length; i++)
        {
            string prefix = prefixes[i];
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(prefix.Length).Trim();
                break;
            }
        }
        return string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    private static string HumanizeControlName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "opción";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    private static void StabilizeButton(Button button)
    {
        if (button == null) return;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

/// <summary>
/// Trigger ligero añadido a cada control para ayuda contextual por hover.
/// </summary>
public sealed class BistroBuilderTooltipTrigger :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler
{
    private BistroBuilderUnifiedUiInteractionService service;
    private string title;
    private string description;

    public string Title => title ?? string.Empty;
    public string Description => description ?? string.Empty;

    public void Initialize(
        BistroBuilderUnifiedUiInteractionService owner,
        string tooltipTitle,
        string tooltipDescription)
    {
        service = owner;
        title = tooltipTitle ?? string.Empty;
        description = tooltipDescription ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        service?.RequestTooltip(title, description, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        service?.UpdateTooltipPointer(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        service?.CancelTooltip();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        service?.CancelTooltip();
    }
}

/// <summary>
/// Interceptor Presentation para los controles antiguos basados en CycleX().
/// El overlay transparente evita ejecutar el ciclo al hacer clic y abre en su
/// lugar una lista desplazable. Para aplicar una opción invoca el callback
/// original el número mínimo de veces necesario, preservando la lógica previa.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderScrollableSelectorTrigger : MonoBehaviour
{
    private const int MaximumCycleProbe = 64;

    private BistroBuilderUnifiedUiInteractionService service;
    private Button originalButton;
    private Button hitButton;
    private Image hitImage;

    public string OriginalControlName => originalButton != null
        ? originalButton.name
        : string.Empty;

    public string SelectorTitle => service != null
        ? service.GetSelectorTitle(originalButton)
        : "opción";

    public int LastEnumeratedOptionCount { get; private set; }

    public void Initialize(
        BistroBuilderUnifiedUiInteractionService owner,
        Button original)
    {
        service = owner;
        originalButton = original;
        EnsureHitTarget();
    }

    private void LateUpdate()
    {
        if (hitButton == null || hitImage == null || originalButton == null) return;
        bool interactive = originalButton.interactable && originalButton.gameObject.activeInHierarchy;
        hitButton.interactable = interactive;
        hitImage.raycastTarget = interactive;
    }

    public bool TryEnumerateOptionsForTest(out int count, out string error)
    {
        bool ok = TryEnumerateOptions(out List<string> options, out _, out error);
        count = options != null ? options.Count : 0;
        return ok;
    }

    internal bool TryEnumerateOptions(
        out List<string> options,
        out string current,
        out string error)
    {
        options = new List<string>();
        current = GetLabel();
        error = string.Empty;
        LastEnumeratedOptionCount = 0;

        if (originalButton == null)
        {
            error = "El control original ya no existe.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(current))
        {
            error = "El control " + originalButton.name + " no publica una etiqueta legible.";
            return false;
        }

        // Algunos controles tienen un dominio visual fijo y conocido. En esos
        // casos no necesitamos mutar temporalmente el formulario para descubrir
        // sus opciones. Esto es especialmente importante para Course: su estado
        // pertenece al borrador de autoría de plato y una enumeración basada en
        // CycleCourse podía quedar desincronizada del label durante una prueba
        // funcional aunque el valor de dominio siguiera siendo válido.
        //
        // La enumeración no mutante mantiene la selección actual intacta y
        // TrySelectLabel continúa reutilizando el callback canónico cuando el
        // jugador elige realmente una opción.
        if (TryEnumerateKnownNonMutatingOptions(
                originalButton.name,
                current,
                options))
        {
            LastEnumeratedOptionCount = options.Count;
            return options.Count > 0;
        }

        options.Add(current);
        string initial = current;
        bool returned = false;

        for (int i = 0; i < MaximumCycleProbe; i++)
        {
            originalButton.onClick.Invoke();
            string value = GetLabel();
            if (string.Equals(value, initial, StringComparison.Ordinal))
            {
                returned = true;
                break;
            }
            if (!string.IsNullOrWhiteSpace(value) && !options.Contains(value))
            {
                options.Add(value);
            }
        }

        if (!returned)
        {
            // Intento defensivo de volver al valor de entrada antes de abortar.
            for (int i = 0; i < MaximumCycleProbe; i++)
            {
                if (string.Equals(GetLabel(), initial, StringComparison.Ordinal))
                {
                    returned = true;
                    break;
                }
                originalButton.onClick.Invoke();
            }
        }

        if (!returned)
        {
            error = "El selector " + originalButton.name +
                    " no volvió a su valor inicial durante la enumeración segura.";
            return false;
        }

        current = initial;
        LastEnumeratedOptionCount = options.Count;
        return options.Count > 0;
    }

    private static bool TryEnumerateKnownNonMutatingOptions(
        string controlName,
        string current,
        List<string> destination)
    {
        if (destination == null || string.IsNullOrWhiteSpace(controlName))
        {
            return false;
        }

        string[] labels;

        if (string.Equals(controlName, "Course", StringComparison.Ordinal))
        {
            // Orden visual canónico de BistroBuilderDishCourse según la UI 2.1G.
            // "Sin pase" se conserva como opción defensiva para valores None/futuros
            // ya soportados por GetCourseLabel. No se escribe ningún dato aquí.
            labels = new[]
            {
                "Sin pase",
                "Bienvenida",
                "Entrante",
                "Principal",
                "Postre",
                "Bebida"
            };
        }
        else if (string.Equals(controlName, "Station", StringComparison.Ordinal))
        {
            // Orden visual canónico de BistroBuilderKitchenStationType según
            // BistroBuilderDishRecipeAuthoringRuntimeView.GetStationLabel().
            // Station comparte el mismo riesgo que Course: descubrir opciones
            // invocando CycleStation puede dejar el índice interno y la etiqueta
            // temporalmente desincronizados durante una prueba/refresh. Por eso
            // se enumera sin mutar el formulario y el callback canónico solo se
            // usa cuando el jugador elige una opción de verdad.
            labels = new[]
            {
                "Sin estación",
                "Preparación fría",
                "Cocina caliente",
                "Parrilla",
                "Freidora",
                "Horno",
                "Pastelería",
                "Barra"
            };
        }
        else
        {
            return false;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            if (!destination.Contains(labels[i]))
            {
                destination.Add(labels[i]);
            }
        }

        // Si una versión futura publica una etiqueta distinta, no la perdemos: la
        // incorporamos como valor actual para que el selector siga siendo seguro.
        if (!string.IsNullOrWhiteSpace(current) && !destination.Contains(current))
        {
            destination.Insert(0, current);
        }

        return destination.Count > 0;
    }

    internal bool TrySelectLabel(string target, out string error)
    {
        error = string.Empty;
        if (originalButton == null)
        {
            error = "El control original ya no existe.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(target))
        {
            error = "La opción solicitada está vacía.";
            return false;
        }

        if (string.Equals(GetLabel(), target, StringComparison.Ordinal)) return true;

        for (int i = 0; i < MaximumCycleProbe; i++)
        {
            originalButton.onClick.Invoke();
            if (string.Equals(GetLabel(), target, StringComparison.Ordinal)) return true;
        }

        error = "No se pudo alcanzar la opción «" + target + "» en " + originalButton.name + ".";
        return false;
    }

    private void EnsureHitTarget()
    {
        if (originalButton == null) return;
        Transform existing = originalButton.transform.Find("BB_B2_SelectorHitTarget");
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = new GameObject(
                "BB_B2_SelectorHitTarget",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            target.layer = originalButton.gameObject.layer;
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.SetParent(originalButton.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
        }

        hitImage = target.GetComponent<Image>();
        hitImage.color = new Color(0f, 0f, 0f, 0.001f);
        hitImage.raycastTarget = true;

        hitButton = target.GetComponent<Button>();
        hitButton.targetGraphic = hitImage;
        hitButton.transition = Selectable.Transition.None;
        hitButton.navigation = new Navigation { mode = Navigation.Mode.None };
        hitButton.onClick.RemoveAllListeners();
        hitButton.onClick.AddListener(() => service?.OpenSelector(this));

        BistroBuilderTooltipTrigger tooltip = target.GetComponent<BistroBuilderTooltipTrigger>();
        if (tooltip == null) tooltip = target.AddComponent<BistroBuilderTooltipTrigger>();
        service.BuildHelp(originalButton, out string title, out string description);
        tooltip.Initialize(service, title, description);
    }

    private string GetLabel()
    {
        if (originalButton == null) return string.Empty;
        Text[] texts = originalButton.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.transform.name.StartsWith("BB_B2_", StringComparison.Ordinal)) continue;
            if (!string.IsNullOrWhiteSpace(text.text)) return text.text.Trim();
        }
        return string.Empty;
    }
}
