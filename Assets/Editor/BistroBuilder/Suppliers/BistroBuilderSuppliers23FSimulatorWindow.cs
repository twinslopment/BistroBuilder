#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23FSimulatorWindow : EditorWindow
{
    private string output=""; private Vector2 scroll;
    [MenuItem(BistroBuilderSuppliers23FPaths.MenuRoot + "2.3F - Simulador de comparación estratégica")]
    public static void Open(){ GetWindow<BistroBuilderSuppliers23FSimulatorWindow>(false,"Simulador 2.3F").Show(); }
    private void OnGUI(){ GUILayout.Label("Simulador no destructivo de Compra Inteligente",EditorStyles.boldLabel); EditorGUILayout.HelpBox("Compara Ahorrar / Equilibrado / Urgente con un escenario controlado. No crea PurchaseOrder ni escribe Inventario.",MessageType.Info); if(GUILayout.Button("Simular estrategias")) Run(); scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(output,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView(); }
    private void OnEnable(){ Run(); }
    private void Run()
    {
        var settings=AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierSmartPurchaseSettings>(BistroBuilderSuppliers23FPaths.SettingsAssetPath); if(settings==null){output="Falta instalar 2.3F.";return;}
        List<BistroBuilderSmartPurchaseIngredientFact> facts; List<BistroBuilderSmartPurchaseOfferFact> offers; BistroBuilderSuppliers23FTestData.Build(out facts,out offers);
        var r=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,1,facts,offers,settings);
        StringBuilder sb=new StringBuilder(); sb.AppendLine("SIMULACIÓN 2.3F — COMPARACIÓN ESTRATÉGICA"); sb.AppendLine("Recomendación global: "+r.recommendedStrategy); sb.AppendLine("Motivo: "+r.recommendedReason); sb.AppendLine();
        foreach(var p in r.plans)
        {
            float coverage = p.strategy==BistroBuilderSmartPurchaseStrategy.Ahorrar ? settings.savingTargetCoverageDays : (p.strategy==BistroBuilderSmartPurchaseStrategy.Urgente ? settings.urgentTargetCoverageDays : settings.balancedTargetCoverageDays);
            sb.AppendLine("=== "+p.strategy.ToString().ToUpperInvariant()+" ===");
            sb.AppendLine("Desembolso estimado: "+(p.totalCents/100f).ToString("0.00")+" € | Producto: "+(p.subtotalCents/100f).ToString("0.00")+" € | Portes consolidados: "+(p.shippingCents/100f).ToString("0.00")+" € | Cobertura objetivo: "+coverage.ToString("0.#")+" días | Score: "+p.score.ToString("0.0"));
            sb.AppendLine("Proveedores: "+p.supplierCount+" | Ingredientes recomendados: "+p.ingredientsRecommended+" | Cestas confirmables: "+(!p.containsMinimumOrderGap?"Sí":"NO"));
            foreach(var rec in p.ingredients)
            {
                var c=rec.selected;
                sb.AppendLine("- "+rec.ingredientDisplayName+": "+c.supplierDisplayName+" | "+c.packageCount+" x "+c.packageDisplayName+" | producto "+(c.lineSubtotalCents/100f).ToString("0.00")+" € | llegada "+c.leadTimeGameHours.ToString("0.#")+" h | riesgo "+rec.currentRisk);
                if(c.reasons.Count>0) sb.AppendLine("  Por qué: "+c.reasons[0]);
                if(rec.alternatives.Count>1) sb.AppendLine("  Alternativa: "+rec.alternatives[1].supplierDisplayName+" | producto "+(rec.alternatives[1].lineSubtotalCents/100f).ToString("0.00")+" € | "+rec.alternatives[1].leadTimeGameHours.ToString("0.#")+" h");
            }
            sb.AppendLine("  Cestas consolidadas:");
            foreach(var basket in p.suppliers)
                sb.AppendLine("  - "+basket.supplierDisplayName+": subtotal "+(basket.subtotalCents/100f).ToString("0.00")+" € | portes "+(basket.shippingCents/100f).ToString("0.00")+" € | total "+(basket.totalCents/100f).ToString("0.00")+" € | mínimo "+(basket.meetsMinimumOrder?"OK":"NO"));
            foreach(var reason in p.summaryReasons) sb.AppendLine("  · "+reason); sb.AppendLine();
        }
        sb.AppendLine("No se ha modificado ningún asset, PurchaseOrder, Inventario, mercado ni promoción."); output=sb.ToString();
    }
}
#endif
