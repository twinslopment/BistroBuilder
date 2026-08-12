#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23DCommercialSimulatorWindow : EditorWindow
{
    private string seedText = "bistro-commercial-preview";
    private int finalDay = 180;
    private Vector2 scroll;
    private string report = "";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3D - Simulador comercial inteligente")]
    public static void Open()
    {
        BistroBuilderSuppliers23DCommercialSimulatorWindow window =
            GetWindow<BistroBuilderSuppliers23DCommercialSimulatorWindow>("Simulador 2.3D");
        window.minSize = new Vector2(900f, 620f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo del Motor Comercial Inteligente", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simula el mercado 2.3C y las decisiones comerciales 2.3D con la misma lógica determinista del runtime. " +
            "No consulta Inventario, no crea pedidos y no modifica ningún asset.",
            MessageType.Info);

        seedText = EditorGUILayout.TextField("Semilla", seedText);
        finalDay = EditorGUILayout.IntSlider("Día final", finalDay, 30, 365);

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Simular", GUILayout.Height(34f)))
        {
            Simulate();
        }
        GUI.enabled = true;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Simulate()
    {
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

        string error;
        BistroBuilderSuppliers23DSimulationResult simulation;
        if (!BistroBuilderSuppliers23DSimulation.TryRun(
                suppliers,
                ingredients,
                marketSettings,
                settings,
                seedText,
                finalDay,
                out simulation,
                out error))
        {
            report = "SIMULACIÓN 2.3D FALLIDA\n" + error;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("SIMULACIÓN 2.3D — DÍA " + finalDay);
        builder.AppendLine("Semilla texto: " + seedText);
        builder.AppendLine("Revisiones de mercado/comerciales: " + simulation.reviews);
        builder.AppendLine("Campañas iniciadas: " + simulation.campaigns);
        builder.AppendLine("Promociones iniciadas: " + simulation.promotionsStarted);
        builder.AppendLine("Promociones finalizadas: " + simulation.promotionsExpired);
        builder.AppendLine("Promociones activas al final: " + simulation.commercial.activePromotions.Count);
        builder.AppendLine("Máximo simultáneo observado: " + simulation.maximumSimultaneousPromotions);
        builder.AppendLine("Descuento observado: " +
            (simulation.minimumDiscountBasisPoints / 100f).ToString("0.##") + "% .. " +
            (simulation.maximumDiscountBasisPoints / 100f).ToString("0.##") + "%");
        builder.AppendLine("Duración observada: " + simulation.minimumDurationDays + " .. " +
            simulation.maximumDurationDays + " días");
        builder.AppendLine("Fingerprint comercial: " +
            BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(simulation.commercial));

        builder.AppendLine();
        builder.AppendLine("PROMOCIONES POR PROVEEDOR");
        List<string> supplierKeys = new List<string>(simulation.promotionsBySupplier.Keys);
        supplierKeys.Sort(StringComparer.Ordinal);
        for (int index = 0; index < supplierKeys.Count; index++)
        {
            builder.AppendLine("- " + supplierKeys[index] + ": " +
                simulation.promotionsBySupplier[supplierKeys[index]]);
        }

        builder.AppendLine();
        builder.AppendLine("MOTIVOS EXPLICABLES");
        List<string> reasonKeys = new List<string>(simulation.promotionsByReason.Keys);
        reasonKeys.Sort(StringComparer.Ordinal);
        for (int index = 0; index < reasonKeys.Count; index++)
        {
            builder.AppendLine("- " + reasonKeys[index] + ": " +
                simulation.promotionsByReason[reasonKeys[index]]);
        }

        builder.AppendLine();
        builder.AppendLine("PROMOCIONES ACTIVAS AL FINAL");
        if (simulation.commercial.activePromotions.Count == 0)
        {
            builder.AppendLine("Ninguna.");
        }
        else
        {
            for (int index = 0; index < simulation.commercial.activePromotions.Count; index++)
            {
                AppendPromotion(builder, simulation.commercial.activePromotions[index]);
            }
        }

        builder.AppendLine();
        builder.AppendLine("ÚLTIMAS PROMOCIONES FINALIZADAS");
        int start = Math.Max(0, simulation.commercial.promotionHistory.Count - 20);
        if (simulation.commercial.promotionHistory.Count == 0)
        {
            builder.AppendLine("Ninguna.");
        }
        else
        {
            for (int index = start; index < simulation.commercial.promotionHistory.Count; index++)
            {
                AppendPromotion(builder, simulation.commercial.promotionHistory[index]);
            }
        }

        builder.AppendLine();
        builder.AppendLine("REGLA DE INDEPENDENCIA");
        builder.AppendLine(
            "2.3D decide a partir de perfiles del proveedor + estado de mercado + historial comercial + semilla. " +
            "No usa stock del restaurante, reservas, previsión 2.2C, recetas ni demanda del jugador.");

        report = builder.ToString();
    }

    private static void AppendPromotion(
        System.Text.StringBuilder builder,
        BistroBuilderSupplierPromotionRecord promotion)
    {
        if (promotion == null)
        {
            return;
        }
        builder.AppendLine(
            "Día " + promotion.startGameDay + " → " + (promotion.endGameDayExclusive - 1) +
            " | " + promotion.supplierId +
            " | " + promotion.supplierOfferId +
            " | " + (promotion.discountBasisPoints / 100f).ToString("0.##") + "%" +
            " | " + (promotion.referenceMarketPriceCents / 100f).ToString("0.00") + " € → " +
            (promotion.promotionalPriceCents / 100f).ToString("0.00") + " €" +
            " | " + promotion.reasonCode);
    }
}
#endif
