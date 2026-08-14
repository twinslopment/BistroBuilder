using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BistroBuilderSuppliers23JKLValidationResult
{
    public readonly List<string> Correct = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> Errors = new List<string>();
    public int CorrectCount => Correct.Count;
    public int WarningCount => Warnings.Count;
    public int ErrorCount => Errors.Count;

    public string BuildReport()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("VALIDACIÓN 2.3JKL — CIERRE INTEGRAL DE PROVEEDORES");
        sb.AppendLine("Correctos: " + CorrectCount + " · Advertencias: " + WarningCount + " · Errores: " + ErrorCount);
        sb.AppendLine();
        for (int i = 0; i < Correct.Count; i++) sb.AppendLine("[OK] " + Correct[i]);
        for (int i = 0; i < Warnings.Count; i++) sb.AppendLine("[AVISO] " + Warnings[i]);
        for (int i = 0; i < Errors.Count; i++) sb.AppendLine("[ERROR] " + Errors[i]);
        return sb.ToString();
    }
}

public sealed class BistroBuilderSuppliers23JKLValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa Validar.";
    private MessageType type = MessageType.Info;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3JKL - Validar cierre integral", false, 2901)]
    private static void Open()
    {
        GetWindow<BistroBuilderSuppliers23JKLValidationWindow>("Validación 2.3JKL");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("2.3JKL — Persistencia + UI + integración final", EditorStyles.boldLabel);
        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(32f))) RunValidation();
        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, type);
        EditorGUILayout.EndScrollView();
    }

    private void OnEnable()
    {
        RunValidation();
    }

    public static BistroBuilderSuppliers23JKLValidationResult ValidateCurrentProject()
    {
        BistroBuilderSuppliers23JKLValidationResult r = new BistroBuilderSuppliers23JKLValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            r.Errors.Add("No hay escena activa válida.");
            return r;
        }
        r.Correct.Add("Escena activa: " + scene.name + ".");

        GameObject gameSystems = BistroBuilderSuppliers23JKLInstaller.FindGameSystems(scene);
        if (gameSystems == null)
        {
            r.Errors.Add("Falta GameSystems.");
            return r;
        }
        r.Correct.Add("GameSystems localizado.");

        BistroBuilderSaveGameService save = gameSystems.GetComponent<BistroBuilderSaveGameService>();
        if (save == null) r.Errors.Add("Falta BistroBuilderSaveGameService.");
        else
        {
            save.RefreshExtensions();
            r.Correct.Add("SaveGameService localizado.");
            if (save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId))
                r.Correct.Add("supplier.integrated.runtime registrado en la plataforma universal.");
            else r.Errors.Add("SaveGameService no registra supplier.integrated.runtime.");
        }

        BistroBuilderSupplierIntegratedSaveSectionProvider[] providers =
            gameSystems.GetComponents<BistroBuilderSupplierIntegratedSaveSectionProvider>();
        BistroBuilderSupplierIntegratedSaveSectionProvider provider =
            providers.Length > 0 ? providers[0] : null;
        if (providers.Length != 1)
            r.Errors.Add("Debe existir exactamente un BistroBuilderSupplierIntegratedSaveSectionProvider; actuales=" + providers.Length + ".");
        else r.Correct.Add("Existe exactamente un provider integrado 2.3J.");
        if (provider == null) r.Errors.Add("Falta BistroBuilderSupplierIntegratedSaveSectionProvider.");
        else
        {
            if (provider.ValidateConfiguration(out string error)) r.Correct.Add("Proveedor 2.3J configurado.");
            else r.Errors.Add(error);
            if (provider.SectionId == BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId &&
                provider.SectionVersion == BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionVersion &&
                provider.LoadOrder == 230 && !provider.IsRequired)
                r.Correct.Add("Contrato de sección 2.3J estable y compatible con saves antiguos.");
            else r.Errors.Add("Metadatos de supplier.integrated.runtime incoherentes.");
        }

        BistroBuilderGoodsReceivingService receiving = gameSystems.GetComponent<BistroBuilderGoodsReceivingService>();
        if (receiving == null) r.Errors.Add("Falta autoridad 2.2B BistroBuilderGoodsReceivingService.");
        else r.Correct.Add("Autoridad canónica 2.2B localizada.");

        BistroBuilderSupplierReceivingBridge23L[] bridges =
            gameSystems.GetComponents<BistroBuilderSupplierReceivingBridge23L>();
        BistroBuilderSupplierReceivingBridge23L bridge = bridges.Length > 0 ? bridges[0] : null;
        if (bridges.Length != 1)
            r.Errors.Add("Debe existir exactamente un bridge 2.3L; actuales=" + bridges.Length + ".");
        else r.Correct.Add("Existe exactamente un bridge Handoff→2.2B.");
        if (bridge == null) r.Errors.Add("Falta bridge 2.3L Handoff→2.2B.");
        else if (bridge.ValidateConfiguration(out string bridgeError))
            r.Correct.Add("Bridge 2.3L apunta a Recepciones 2.2B y no a un inventario paralelo.");
        else r.Errors.Add(bridgeError);

        Canvas canvas = BistroBuilderSuppliers23JKLInstaller.FindCanonicalHudCanvas(scene);
        if (canvas == null) r.Errors.Add("Falta MainHUD/Canvas canónico.");
        else
        {
            r.Correct.Add("Canvas HUD canónico localizado.");
            if (canvas.GetComponent<GraphicRaycaster>() != null)
                r.Correct.Add("Canvas dispone de GraphicRaycaster para la UI 2.3K.");
            else r.Errors.Add("MainHUD/Canvas no tiene GraphicRaycaster.");

            BistroBuilderUnifiedUiInteractionService[] uiInteractionServices =
                canvas.GetComponents<BistroBuilderUnifiedUiInteractionService>();
            if (uiInteractionServices.Length != 1)
            {
                r.Errors.Add("Debe existir exactamente una capa UI transversal 2.3JKL-B2; actuales=" +
                    uiInteractionServices.Length + ".");
            }
            else if (uiInteractionServices[0].ValidateConfiguration(out string uiInteractionError))
            {
                r.Correct.Add("2.3JKL-B2 instalado sobre el Canvas canónico: aislamiento contextual, tooltips y selectores scroll.");
                if (EditorApplication.isPlaying)
                {
                    uiInteractionServices[0].RunImmediateScanForTests();
                    if (uiInteractionServices[0].SelectorTriggerCount >= 10)
                        r.Correct.Add("B2 detecta los selectores cíclicos actuales de Carta/Inventario para sustituirlos por listas desplazables.");
                    else
                        r.Errors.Add("B2 solo detecta " + uiInteractionServices[0].SelectorTriggerCount +
                            " selectores cíclicos; se esperaban al menos 10 en las UIs actuales.");

                    if (uiInteractionServices[0].TooltipTriggerCount >= 20)
                        r.Correct.Add("B2 publica ayuda contextual por hover en controles jugables de Carta/Inventario/Proveedores.");
                    else
                        r.Errors.Add("B2 solo ha decorado " + uiInteractionServices[0].TooltipTriggerCount +
                            " controles con tooltip; la cobertura transversal es insuficiente.");
                }
            }
            else
            {
                r.Errors.Add("2.3JKL-B2: " + uiInteractionError);
            }

            Transform root = null;
            int rootCount = 0;
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Transform child = canvas.transform.GetChild(i);
                if (child != null && string.Equals(
                        child.name,
                        BistroBuilderSuppliers23JKLInstaller.UiRootName,
                        StringComparison.Ordinal))
                {
                    rootCount++;
                    if (root == null) root = child;
                }
            }
            if (rootCount != 1)
                r.Errors.Add("Debe existir exactamente una raíz UI de Proveedores; actuales=" + rootCount + ".");
            else r.Correct.Add("Existe exactamente una raíz UI jugable 2.3K.");

            if (root == null) r.Errors.Add("Falta raíz UI " + BistroBuilderSuppliers23JKLInstaller.UiRootName + ".");
            else
            {
                BistroBuilderSupplierPlayerRuntimeView[] views =
                    root.GetComponents<BistroBuilderSupplierPlayerRuntimeView>();
                if (views.Length != 1)
                    r.Errors.Add("La raíz 2.3K debe contener exactamente una RuntimeView; actuales=" + views.Length + ".");
                else if (views[0].ValidateConfiguration(out string viewError))
                    r.Correct.Add("UI 2.3K configurada sobre autoría canónica.");
                else r.Errors.Add(viewError);
            }
        }

        BistroBuilderSupplierAuthoringDatabase supplierDb =
            Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath);
        BistroBuilderIngredientAuthoringDatabase ingredientDb =
            Resources.Load<BistroBuilderIngredientAuthoringDatabase>(
                BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath);
        if (supplierDb == null) r.Errors.Add("Falta supplier.authoring.");
        else
        {
            int active = 0;
            int activeOffers = 0;
            int readableBranding = 0;
            int logos = 0;
            int initial = 0;
            int progressive = 0;
            for (int i = 0; i < supplierDb.Suppliers.Count; i++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = supplierDb.Suppliers[i];
                if (supplier == null || !supplier.isActive) continue;
                active++;
                if (!string.IsNullOrWhiteSpace(supplier.displayName)) readableBranding++;
                if (supplier.logo != null) logos++;
                bool availableFromStart = supplier.unlockProfile != null &&
                                          supplier.unlockProfile.availableFromStart;
                bool hasConditions = supplier.unlockProfile != null &&
                                     supplier.unlockProfile.conditions != null &&
                                     supplier.unlockProfile.conditions.Count > 0;
                if (availableFromStart) initial++;
                else if (hasConditions) progressive++;
                if (supplier.baseOffers != null)
                {
                    for (int o = 0; o < supplier.baseOffers.Count; o++)
                        if (supplier.baseOffers[o] != null && supplier.baseOffers[o].isActive) activeOffers++;
                }
            }
            if (active == 6) r.Correct.Add("Autoría conserva exactamente 6 proveedores activos.");
            else r.Errors.Add("Se esperaban 6 proveedores activos y hay " + active + ".");
            if (activeOffers == 66) r.Correct.Add("Autoría conserva exactamente 66 ofertas base activas.");
            else r.Errors.Add("Se esperaban 66 ofertas activas y hay " + activeOffers + ".");
            if (readableBranding == active) r.Correct.Add("Todos los proveedores disponen de identidad textual para branding/UI.");
            else r.Errors.Add("Hay proveedores activos sin nombre visible para branding.");
            if (initial == 2 && progressive == 4)
                r.Correct.Add("Progresión 2.3I conserva 2 proveedores iniciales + 4 progresivos.");
            else r.Errors.Add("Progresión de autoría inesperada: iniciales=" + initial + " progresivos=" + progressive + ".");
            if (logos < active)
                r.Warnings.Add("Logos de proveedor asignados: " + logos + "/" + active +
                    ". La UI/vehículos usan fallback nombre + colores mientras falten assets visuales.");
            else r.Correct.Add("Todos los proveedores tienen logo asignado.");
        }
        if (ingredientDb == null) r.Errors.Add("Falta ingredient.authoring.");
        else
        {
            int active = 0;
            int formats = 0;
            int images = 0;
            for (int i = 0; i < ingredientDb.Ingredients.Count; i++)
            {
                BistroBuilderIngredientAuthoringRecord ingredient = ingredientDb.Ingredients[i];
                if (ingredient == null || !ingredient.isActive) continue;
                active++;
                if (ingredient.displayImage != null) images++;
                if (ingredient.commercialPackages != null)
                    for (int p = 0; p < ingredient.commercialPackages.Count; p++)
                        if (ingredient.commercialPackages[p] != null && ingredient.commercialPackages[p].isActive) formats++;
            }
            if (active >= 22) r.Correct.Add("Ingredient authoring conserva al menos 22 ingredientes activos.");
            else r.Errors.Add("Ingredient authoring contiene menos de 22 ingredientes activos.");
            if (formats == 44) r.Correct.Add("Se conservan 44 formatos comerciales canónicos.");
            else r.Errors.Add("Formatos comerciales activos actuales: " + formats + " (esperados: 44).");
            if (images < active)
                r.Warnings.Add("Imágenes de ingrediente asignadas: " + images + "/" + active +
                    ". La UI usa identificación textual/branding como fallback hasta completar assets visuales.");
            else r.Correct.Add("Todos los ingredientes activos tienen imagen asignada.");
        }

        // Verificación de contrato puro de conversión H->2.2B.
        BistroBuilderSupplierReceivingHandoff synthetic = new BistroBuilderSupplierReceivingHandoff
        {
            purchaseOrderId = "purchase_order_validation",
            logisticsPlanId = "logistics_plan_validation",
            supplierId = "supplier_mercado_central",
            lines = new List<BistroBuilderSupplierDeliveryManifestLine>
            {
                new BistroBuilderSupplierDeliveryManifestLine
                {
                    ingredientId = "ingredient_validation",
                    totalNetQuantityMicrounits = 1000000L,
                    packageCount = 1
                }
            }
        };
        if (BistroBuilderSupplierReceivingBridge23L.TryConvertHandoffLines(
                synthetic, out List<BistroBuilderInventoryQuantityLine> converted, out string conversionError) &&
            converted.Count == 1 && converted[0].CanonicalMilliUnits == 1000L)
            r.Correct.Add("Conversión exacta 2.3H micro-units → inventory canonical milli-units validada (1.000.000→1.000).");
        else r.Errors.Add("Conversión H→2.2B inválida: " + conversionError);

        if (EditorApplication.isPlaying)
        {
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierMarketService>(r, "2.3C");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierCommercialIntelligenceService>(r, "2.3D");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierPurchaseOrderService>(r, "2.3E");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierSmartPurchaseService>(r, "2.3F");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierLogisticsService>(r, "2.3G");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierDeliveryPresentationService>(r, "2.3H");
            ValidateSingleRuntimeAuthority<BistroBuilderSupplierProgressionService>(r, "2.3I");

            string captureError = string.Empty;
            BistroBuilderSupplierIntegratedSaveState state = null;
            if (provider != null && provider.TryCaptureIntegratedState(out state, out captureError))
            {
                r.Correct.Add("Snapshot integrado runtime capturable y consistente. Fingerprint " + state.BuildFingerprint() + ".");
                if (state.market.currentGameDay == state.commercial.currentGameDay &&
                    state.market.currentGameDay == state.orders.currentGameDay &&
                    state.market.currentGameDay == state.logistics.currentGameDay &&
                    state.market.currentGameDay == state.deliveryPresentation.currentGameDay &&
                    state.market.currentGameDay == state.progression.currentGameDay)
                    r.Correct.Add("C/D/E/G/H/I comparten día canónico en runtime.");
            }
            else r.Errors.Add("No se pudo capturar snapshot integrado runtime: " +
                              (provider == null ? "provider 2.3J ausente." : captureError));
        }
        else
        {
            r.Correct.Add("Validación estructural ejecutada en Edit Mode; snapshot runtime se comprobará en prueba funcional.");
        }

        return r;
    }

    private static void ValidateSingleRuntimeAuthority<T>(
        BistroBuilderSuppliers23JKLValidationResult result,
        string label) where T : Component
    {
        T[] items = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (items.Length == 1)
            result.Correct.Add(label + " mantiene exactamente una autoridad runtime.");
        else
            result.Errors.Add(label + " tiene " + items.Length +
                " autoridades runtime; se esperaba exactamente una.");
    }

    private void RunValidation()
    {
        BistroBuilderSuppliers23JKLValidationResult r = ValidateCurrentProject();
        report = r.BuildReport();
        type = r.ErrorCount > 0 ? MessageType.Error : r.WarningCount > 0 ? MessageType.Warning : MessageType.Info;
        Repaint();
    }
}
