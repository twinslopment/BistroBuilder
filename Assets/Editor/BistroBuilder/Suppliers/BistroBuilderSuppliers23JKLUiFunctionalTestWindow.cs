using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>JKL-B: smoke test funcional de la UI jugable sin crear compras.</summary>
public sealed class BistroBuilderSuppliers23JKLUiFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> lines = new List<string>();
    private int passed;
    private int failed;
    private int capturedErrors;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3JKL-B - Prueba funcional UI jugable", false, 2904)]
    private static void Open()
    {
        GetWindow<BistroBuilderSuppliers23JKLUiFunctionalTestWindow>("UI 2.3JKL-B");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3JKL-B — UI JUGABLE DEFINITIVA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. Valida B2 (aislamiento de cabeceras, tooltips y selectores con scroll) " +
            "y recorre Proveedores/Catálogo/Compra Inteligente/Pedidos sin confirmar ni crear pedidos.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Ejecutar prueba funcional UI", GUILayout.Height(34f))) Run();
        }
        EditorGUILayout.LabelField("Correctos: " + passed + " · Fallos: " + failed, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < lines.Count; i++) EditorGUILayout.LabelField(lines[i], EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        lines.Clear();
        passed = 0;
        failed = 0;
        capturedErrors = 0;
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        BistroBuilderUnifiedUiInteractionService uiInteraction =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderUnifiedUiInteractionService>();
        Check(uiInteraction != null, "B2 mantiene una capa transversal de interacción UI.");
        if (uiInteraction != null)
        {
            Check(uiInteraction.ValidateConfiguration(out string uiError),
                "B2 está enlazado al Canvas HUD canónico. " + uiError);
            uiInteraction.RunImmediateScanForTests();
            Check(uiInteraction.GlobalAccessButtonCount >= 4,
                "B2 reconoce los accesos globales MENÚ/CARTAS/INVENTARIO/PROVEEDORES para aislarlos durante modales.");
            Check(uiInteraction.SelectorTriggerCount >= 10,
                "B2 detecta al menos 10 controles cíclicos actuales para reemplazarlos por selector con scroll.");
            Check(uiInteraction.TooltipTriggerCount >= 20,
                "B2 publica tooltips contextuales en Carta/Inventario/Proveedores.");

            Check(uiInteraction.TryGetSelectorTrigger("Category", out BistroBuilderScrollableSelectorTrigger categorySelector),
                "Categoría de plato usa selector desplazable B2.");
            if (categorySelector != null)
            {
                Check(categorySelector.TryEnumerateOptionsForTest(out int categoryOptions, out string selectorError) &&
                      categoryOptions > 0,
                    "El selector de Categoría puede enumerar opciones sin alterar el valor actual. " + selectorError);
            }

            Check(uiInteraction.TryGetSelectorTrigger("Course", out BistroBuilderScrollableSelectorTrigger courseSelector),
                "Pase gastronómico usa selector desplazable B2.");
            if (courseSelector != null)
            {
                Check(courseSelector.TryEnumerateOptionsForTest(out int courseOptions, out string courseError) &&
                      courseOptions >= 2,
                    "El selector de Pase gastronómico enumera varias opciones y vuelve al valor inicial. " + courseError);
            }
            Check(uiInteraction.TryGetSelectorTrigger("Station", out BistroBuilderScrollableSelectorTrigger stationSelector),
                "Estación de cocina usa selector desplazable B2.");
            if (stationSelector != null)
            {
                Check(stationSelector.TryEnumerateOptionsForTest(out int stationOptions, out string stationError) &&
                      stationOptions >= 2,
                    "El selector de Estación de cocina enumera varias opciones y vuelve al valor inicial. " + stationError);
            }
            Check(uiInteraction.TryGetSelectorTrigger("MealService", out _),
                "Servicio del día usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("ServiceMode", out _),
                "Modalidad de servicio usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("Filter", out _),
                "Filtro de Inventario usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("Sort", out _),
                "Orden de Inventario usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("Reason", out _),
                "Motivo de ajuste de Inventario usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("Tipo", out _),
                "Tipo de regla de carta usa selector desplazable B2.");
            Check(uiInteraction.TryGetSelectorTrigger("Cartadestino", out _),
                "Carta destino usa selector desplazable B2.");

            Check(uiInteraction.TryGetTooltipForControl("EventId", out string tooltipTitle, out string tooltipBody) &&
                  !string.IsNullOrWhiteSpace(tooltipTitle) && !string.IsNullOrWhiteSpace(tooltipBody),
                "B2 aporta ayuda contextual legible para event_id.");
            Check(uiInteraction.TryGetTooltipForControl("PromotionId", out _, out string promotionHelp) &&
                  !string.IsNullOrWhiteSpace(promotionHelp),
                "B2 aporta ayuda contextual legible para promotion_id.");
        }

        BistroBuilderSupplierPlayerRuntimeView view =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPlayerRuntimeView>();
        Check(view != null, "Existe exactamente una vista jugable 2.3K accesible.");
        if (view == null)
        {
            Application.logMessageReceived -= HandleLog;
            return;
        }

        Check(view.TryOpenFromInterface(out string error), "La UI se abre desde su interfaz pública. " + error);
        if (uiInteraction != null)
        {
            uiInteraction.RunImmediateScanForTests();
            uiInteraction.ApplyContextVisibilityForTests();
            Check(uiInteraction.IsGlobalAccessSuppressed &&
                  uiInteraction.AreOriginallyVisibleGlobalAccessButtonsHiddenForTests(),
                "B2 oculta los accesos globales de Carta/Inventario/Proveedores mientras un modal funcional está abierto.");
        }
        Check(view.IsOpen, "El modal de Proveedores queda visible.");
        Check(view.VisualTreeBuilt, "El árbol visual runtime está construido.");
        Check(view.TryValidateVisibleContent(out error), "RectMask2D y controles esenciales son válidos. " + error);
        Check(view.TryValidateStableInteractionVisuals(out error),
            "B1 elimina ColorTint/Selected automáticos en controles persistentes. " + error);
        Check(BistroBuilderSupplierPlayerUiFormat.HumanizeIdentifier("FrutasYVerduras") == "Frutas y verduras",
            "B1 humaniza identificadores CamelCase para jugador.");
        Check(BistroBuilderSupplierPlayerUiFormat.HumanizeFlagsText("Generalista, PescadosYMariscos, AceitesYCondimentos") ==
              "Generalista, Pescados y mariscos, Aceites y condimentos",
            "B1 humaniza listas de categorías/flags sin tocar autoría.");

        Check(view.TrySelectSectionForTests(BistroBuilderSupplierPlayerSection.Suppliers, out error),
            "Sección Proveedores visible. " + error);
        Check(view.VisibleRowCount == 6, "Proveedores muestra exactamente los 6 proveedores activos.");
        Check(view.TryValidateStableInteractionVisuals(out error),
            "Filas de Proveedores no reintroducen transiciones de pulsación. " + error);
        Check(!ContainsVisibleText(view, "DISP.") && !ContainsVisibleText(view, "BLOQ."),
            "B1 elimina abreviaturas técnicas DISP./BLOQ. de la UI visible.");

        Check(view.TrySelectSectionForTests(BistroBuilderSupplierPlayerSection.Catalog, out error),
            "Sección Catálogo visible para proveedor inicial desbloqueado. " + error);
        Check(view.VisibleRowCount > 0, "Catálogo renderiza ofertas reales.");
        Check(view.TryValidateStableInteractionVisuals(out error),
            "Filas de Catálogo mantienen interacción estable sin flash. " + error);

        Check(view.TrySelectSectionForTests(BistroBuilderSupplierPlayerSection.SmartPurchase, out error),
            "Sección Compra Inteligente visible. " + error);
        Check(view.VisibleRowCount > 0, "Compra Inteligente muestra las estrategias/estado real.");
        Check(view.TryValidateStableInteractionVisuals(out error),
            "Estrategias de Compra Inteligente mantienen interacción estable. " + error);

        Check(view.TrySelectSectionForTests(BistroBuilderSupplierPlayerSection.Orders, out error),
            "Sección Pedidos visible. " + error);
        Check(view.VisibleRowCount > 0, "Pedidos muestra pedidos reales o estado vacío jugable.");
        Check(view.TryValidateStableInteractionVisuals(out error),
            "Filas/botones de Pedidos mantienen interacción estable. " + error);

        BistroBuilderSupplierProgressionService progression =
            BistroBuilderSupplierProgressionService.Instance;
        Check(progression != null && progression.IsInitialized, "2.3I disponible durante la UI.");
        if (progression != null)
        {
            List<BistroBuilderSupplierAccessEvaluation> access = new List<BistroBuilderSupplierAccessEvaluation>();
            progression.CopySupplierAccess(access, true);
            Check(access.Count == 6, "La UI se apoya en seis accesos de progresión reales.");
        }

        view.Close();
        Check(!view.IsOpen, "Cerrar UI oculta el modal y devuelve input al juego.");
        if (uiInteraction != null)
        {
            uiInteraction.ApplyContextVisibilityForTests();
            Check(!uiInteraction.IsGlobalAccessSuppressed,
                "B2 restaura los accesos globales al cerrar el modal.");
        }
        Application.logMessageReceived -= HandleLog;
        Check(capturedErrors == 0, "La prueba UI no captura Error/Exception/Assert (capturados=" + capturedErrors + ").");
        Repaint();
    }

    private static bool ContainsVisibleText(
        BistroBuilderSupplierPlayerRuntimeView view,
        string fragment)
    {
        if (view == null || string.IsNullOrEmpty(fragment)) return false;
        Text[] texts = view.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < texts.Length; index++)
        {
            Text text = texts[index];
            if (text != null && text.gameObject.activeInHierarchy &&
                !string.IsNullOrEmpty(text.text) && text.text.Contains(fragment))
            {
                return true;
            }
        }
        return false;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) capturedErrors++;
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + message);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + message);
        }
    }
}
