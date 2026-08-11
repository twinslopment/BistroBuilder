#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23CMarketSimulatorWindow : EditorWindow
{
    private int days = 30;
    private string seedText = "bistro-market-preview";
    private Vector2 scroll;
    private string report = "Pulsa Simular. No se modifica ningún asset ni partida.";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C - Simulador de mercado")]
    public static void Open()
    {
        BistroBuilderSuppliers23CMarketSimulatorWindow window =
            GetWindow<BistroBuilderSuppliers23CMarketSimulatorWindow>("Simulador 2.3C");
        window.minSize = new Vector2(760f, 520f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo del mercado 2.3C", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simula precio actual y disponibilidad usando la misma lógica determinista del runtime. " +
            "No crea promociones, pedidos ni escrituras de inventario.",
            MessageType.Info);

        seedText = EditorGUILayout.TextField("Semilla", seedText);
        days = EditorGUILayout.IntSlider("Día final", days, 5, 120);

        if (GUILayout.Button("Simular", GUILayout.Height(30f)))
        {
            Simulate();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Simulate()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23CPaths.SupplierAuthoringAsset);
        BistroBuilderSupplierMarketSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23CPaths.MarketSettingsAsset);

        if (suppliers == null || settings == null)
        {
            report = "Faltan supplier.authoring o supplier.market.settings.";
            return;
        }

        ulong seed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
            seedText, settings.DeterministicSalt);
        BistroBuilderSupplierMarketSnapshot snapshot =
            BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(suppliers, settings, seed, 1);

        List<BistroBuilderSupplierMarketReviewOutcome> outcomes;
        string error;
        if (!BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                snapshot, suppliers, settings, days, out outcomes, out error))
        {
            report = "ERROR: " + error;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("SIMULACIÓN 2.3C — DÍA " + days);
        builder.AppendLine("Semilla: " + seed);
        builder.AppendLine("Ofertas: " + snapshot.offerStates.Count);
        builder.AppendLine("Revisiones acumuladas: " + snapshot.reviews.Count);
        builder.AppendLine("Cambios conservados en historial: " + snapshot.changes.Count);
        builder.AppendLine("Fingerprint: " + BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshot));
        builder.AppendLine();
        builder.AppendLine("REVISIONES");

        for (int i = 0; i < snapshot.reviews.Count; i++)
        {
            BistroBuilderSupplierMarketReviewRecord review = snapshot.reviews[i];
            builder.AppendLine(
                "Día " + review.gameDay + ": " + review.priceChanges +
                " precio(s), " + review.availabilityChanges +
                " disponibilidad(es), " + review.unchangedOffers + " sin cambios.");
        }

        builder.AppendLine();
        builder.AppendLine("ÚLTIMOS CAMBIOS");
        int start = Mathf.Max(0, snapshot.changes.Count - 30);
        for (int i = start; i < snapshot.changes.Count; i++)
        {
            BistroBuilderSupplierMarketChangeRecord change = snapshot.changes[i];
            builder.AppendLine(
                "Día " + change.gameDay + " | " + change.supplierOfferId +
                " | " + change.changeKind +
                " | " + (change.previousPriceCents / 100.0).ToString("0.00") + " € → " +
                (change.currentPriceCents / 100.0).ToString("0.00") + " €" +
                " | " + change.previousAvailability + " → " + change.currentAvailability);
        }

        report = builder.ToString();
    }
}
#endif
