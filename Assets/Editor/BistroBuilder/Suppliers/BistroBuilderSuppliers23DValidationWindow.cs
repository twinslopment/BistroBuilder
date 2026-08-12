#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23DValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> info = new List<string>();

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3D - Validar Motor Comercial Inteligente")]
    public static void Open()
    {
        BistroBuilderSuppliers23DValidationWindow window =
            GetWindow<BistroBuilderSuppliers23DValidationWindow>("Validación 2.3D");
        window.minSize = new Vector2(820f, 560f);
        window.ValidateNow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3D — Motor Comercial Inteligente y ofertas temporales",
            EditorStyles.boldLabel);

        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(30f)))
        {
            ValidateNow();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Errores: " + errors.Count + "  Advertencias: " + warnings.Count +
            "  Información: " + info.Count,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("ERRORES", errors, "Ninguno.");
        DrawSection("ADVERTENCIAS", warnings, "Ninguna.");
        DrawSection("INFORMACIÓN", info, "Ninguna.");
        EditorGUILayout.EndScrollView();
    }

    private void ValidateNow()
    {
        errors.Clear();
        warnings.Clear();
        info.Clear();

        if (EditorApplication.isPlaying)
        {
            errors.Add("La validación estructural 2.3D debe ejecutarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23DPaths.SupplierDatabasePath);
        BistroBuilderIngredientAuthoringDatabase ingredients =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(
                BistroBuilderSuppliers23DPaths.IngredientDatabasePath);
        BistroBuilderSupplierMarketSettings marketSettings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23DPaths.MarketSettingsPath);
        BistroBuilderSupplierCommercialIntelligenceSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(
                BistroBuilderSuppliers23DPaths.CommercialSettingsPath);

        if (suppliers == null) errors.Add("No se localiza supplier.authoring.");
        if (ingredients == null) errors.Add("No se localiza ingredient.authoring.");
        if (marketSettings == null) errors.Add("No se localiza supplier.market.settings de 2.3C.");
        if (settings == null) errors.Add("No se localiza supplier.commercial.settings. Ejecuta el instalador 2.3D.");
        if (errors.Count > 0) return;

        if (settings.SchemaId != BistroBuilderSupplierCommercialIntelligenceSettings.CurrentSchemaId ||
            settings.SchemaVersion != BistroBuilderSupplierCommercialIntelligenceSettings.CurrentSchemaVersion)
        {
            errors.Add("supplier.commercial.settings usa un schema incompatible.");
        }
        if (marketSettings.ReviewEveryGameDays != 5)
        {
            errors.Add("2.3D requiere el ciclo canónico de mercado de 5 días cerrado en 2.3C.");
        }

        int activeSuppliers = 0;
        int activeOffers = 0;
        int promotionEligible = 0;
        HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);

        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }
            activeSuppliers++;
            if (supplier.promotionProfile == null)
            {
                errors.Add(supplier.SupplierId + ": falta promotionProfile.");
                continue;
            }
            if (supplier.promotionProfile.minimumDiscountPercent < 0f ||
                supplier.promotionProfile.maximumDiscountPercent > 95f ||
                supplier.promotionProfile.minimumDiscountPercent > supplier.promotionProfile.maximumDiscountPercent)
            {
                errors.Add(supplier.SupplierId + ": rango de descuento promocional inválido.");
            }
            if (supplier.promotionProfile.minimumDurationDays < 1 ||
                supplier.promotionProfile.maximumDurationDays < supplier.promotionProfile.minimumDurationDays)
            {
                errors.Add(supplier.SupplierId + ": duración promocional inválida.");
            }
            if (supplier.promotionProfile.eligibleCatalogs == BistroBuilderSupplierCatalogFlags.None)
            {
                errors.Add(supplier.SupplierId + ": no tiene familias elegibles para promociones.");
            }

            if (supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive) continue;
                activeOffers++;
                if (!offerIds.Add(offer.SupplierOfferId))
                {
                    errors.Add("SupplierOfferId duplicado: " + offer.SupplierOfferId);
                }
                if (offer.promotionEligible)
                {
                    promotionEligible++;
                }
            }
        }

        if (activeSuppliers != 6)
            errors.Add("Se esperaban 6 proveedores activos y hay " + activeSuppliers + ".");
        if (activeOffers != 66)
            errors.Add("Se esperaban 66 ofertas activas y hay " + activeOffers + ".");
        if (promotionEligible <= 0)
            errors.Add("No existe ninguna oferta elegible para promociones.");

        if (settings.MaximumActivePromotionsPerSupplier < 1 ||
            settings.MaximumActivePromotionsPerSupplier > 8)
        {
            errors.Add("maximumActivePromotionsPerSupplier está fuera de un rango razonable (1..8).");
        }
        if (settings.OfferReuseCooldownDays < 0)
        {
            errors.Add("offerReuseCooldownDays no puede ser negativo.");
        }

        string error;
        BistroBuilderSuppliers23DSimulationResult simulation;
        if (!BistroBuilderSuppliers23DSimulation.TryRun(
                suppliers,
                ingredients,
                marketSettings,
                settings,
                "23d-validation",
                120,
                out simulation,
                out error))
        {
            errors.Add("La simulación no destructiva 2.3D falló: " + error);
        }
        else
        {
            if (simulation.reviews != 24)
                errors.Add("La simulación a día 120 debía procesar 24 revisiones y procesó " + simulation.reviews + ".");
            if (simulation.promotionsStarted <= 0)
                errors.Add("La simulación a 120 días no generó ninguna promoción.");
            if (simulation.maximumSimultaneousPromotions > activeSuppliers * settings.MaximumActivePromotionsPerSupplier)
                errors.Add("Se superó el máximo global derivado de promociones simultáneas.");
            if (!BistroBuilderSupplierCommercialIntelligenceEngine.ValidateSnapshotAgainstAuthoringAndMarket(
                    simulation.commercial,
                    simulation.market,
                    suppliers,
                    out error))
            {
                errors.Add("El snapshot comercial simulado no es válido: " + error);
            }

            info.Add(
                "Simulación día 120: " + simulation.promotionsStarted + " promociones iniciadas, " +
                simulation.promotionsExpired + " finalizadas, máximo simultáneo " +
                simulation.maximumSimultaneousPromotions + ".");
            info.Add(
                "Descuentos observados: " + (simulation.minimumDiscountBasisPoints / 100f).ToString("0.##") +
                "% .. " + (simulation.maximumDiscountBasisPoints / 100f).ToString("0.##") + "%.");
            info.Add(
                "Duraciones observadas: " + simulation.minimumDurationDays + " .. " +
                simulation.maximumDurationDays + " días.");
        }

        info.Add("Proveedores activos: " + activeSuppliers + ".");
        info.Add("Ofertas activas: " + activeOffers + "; elegibles para promoción: " + promotionEligible + ".");
        info.Add("El Motor Comercial Inteligente solo consume autoría + mercado 2.3C; no consulta stock, previsión, recetas ni pedidos del jugador.");
        info.Add("La promoción congela su precio promocional al iniciarse; 2.3E podrá capturar la cotización efectiva al confirmar un pedido.");
        info.Add("La persistencia integral se conectará en 2.3J; 2.3D ya expone CreateSnapshot/TryRestoreSnapshot.");
        Repaint();
    }

    private static void DrawSection(string title, List<string> values, string emptyText)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (values.Count == 0)
        {
            EditorGUILayout.HelpBox(emptyText, MessageType.None);
            return;
        }
        for (int index = 0; index < values.Count; index++)
        {
            EditorGUILayout.HelpBox(values[index],
                title == "ERRORES" ? MessageType.Error :
                title == "ADVERTENCIAS" ? MessageType.Warning : MessageType.Info);
        }
    }
}
#endif
