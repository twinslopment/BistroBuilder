#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23FRuntimeFunctionalTestWindow : EditorWindow
{
    private int passed,failed; private string log=""; private Vector2 scroll;
    [MenuItem(BistroBuilderSuppliers23FPaths.MenuRoot + "2.3F - Prueba funcional runtime")]
    public static void Open(){ GetWindow<BistroBuilderSuppliers23FRuntimeFunctionalTestWindow>(false,"Prueba runtime 2.3F").Show(); }
    private void OnGUI(){ GUILayout.Label("PRUEBA FUNCIONAL RUNTIME 2.3F",EditorStyles.boldLabel); EditorGUILayout.HelpBox("Debe ejecutarse en Play Mode. Analiza las autoridades REALES en solo lectura; no crea pedidos ni toca Inventario.",MessageType.Info); if(GUILayout.Button("Ejecutar prueba completa")) Run(); GUILayout.Label("Correctos: "+passed+"  Fallos: "+failed,EditorStyles.boldLabel); scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(log,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView(); }
    private void Check(bool c,string t,List<string> l){if(c){passed++;l.Add("[OK] "+t);}else{failed++;l.Add("[FALLO] "+t);}}
    private void Run()
    {
        passed=failed=0; List<string> l=new List<string>();
        Check(EditorApplication.isPlaying,"Prueba ejecutada en Play Mode.",l); if(!EditorApplication.isPlaying){log=string.Join("\n",l);return;}
        var market=BistroBuilderSupplierMarketService.Instance??Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        var commercial=BistroBuilderSupplierCommercialIntelligenceService.Instance??Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        var orders=BistroBuilderSupplierPurchaseOrderService.Instance??Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        var smart=BistroBuilderSupplierSmartPurchaseService.Instance??Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>();
        Check(market!=null&&market.IsInitialized,"2.3C runtime disponible.",l); Check(commercial!=null&&commercial.IsInitialized,"2.3D runtime disponible.",l); Check(orders!=null&&orders.IsInitialized,"2.3E runtime disponible.",l); Check(smart!=null,"Existe autoridad runtime 2.3F.",l);
        if(smart==null){log=string.Join("\n",l);return;}
        Check(smart.IsInitialized||smart.TryInitialize(),"2.3F se inicializa.",l); Check(string.IsNullOrEmpty(smart.LastInitializationError),"2.3F no conserva error residual.",l);
        int ordersBefore=orders!=null?orders.OrderCount:-1; string inventoryBefore=BistroBuilderSupplierSmartPurchaseRuntimeResolver.CaptureReadOnlyFingerprint();
        long marketRev=market!=null?market.MarketRevision:-1; long commercialRev=commercial!=null?commercial.CommercialRevision:-1; long orderRev=orders!=null?orders.OrdersRevision:-1;
        BistroBuilderSmartPurchaseReport report; string error; bool ok=smart.TryBuildRecommendations(out report,out error);
        Check(ok,"El análisis real de compra se ejecuta: "+(ok?"OK":error),l);
        if(ok&&report!=null)
        {
            Check(report.plans.Count==3,"Runtime genera Ahorrar / Equilibrado / Urgente.",l);
            Check(report.canonicalIngredientCount>=22,"Runtime reconoce los ingredientes canónicos.",l);
            Check(report.ingredientFactsResolved>0,"Runtime lee stock desde la autoridad canónica sin escribir.",l);
            Check(report.offersEvaluated>=66,"Runtime evalúa el catálogo comercial 2.3B/2.3D.",l);
            Check(!string.IsNullOrWhiteSpace(report.recommendedReason),"La estrategia recomendada publica una razón explicable.",l);
            Check(report.plans.TrueForAll(p=>p.summaryReasons.Count>0),"Las tres estrategias publican razones.",l);
            Check(report.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected==null||i.selected.reasons.Count>0)),"Toda compra propuesta explica el porqué.",l);
            Check(report.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected==null||i.selected.packageCount>0)),"Las cantidades runtime respetan paquetes enteros.",l);
            Check(report.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected==null||i.selected.availability!=BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado)),"No se recomienda una oferta temporalmente agotada.",l);
            Check(report.plans.TrueForAll(p=>!p.containsMinimumOrderGap),"Los planes runtime no publican cestas por debajo del pedido mínimo.",l);
            Check(report.plans.TrueForAll(p=>p.suppliers.TrueForAll(b=>b.meetsMinimumOrder)),"Todas las cestas runtime propuestas son confirmables respecto al mínimo del proveedor.",l);
        }
        string inventoryAfter=BistroBuilderSupplierSmartPurchaseRuntimeResolver.CaptureReadOnlyFingerprint();
        Check(inventoryBefore==inventoryAfter,"El análisis 2.3F no modifica el snapshot/fingerprint de Inventario.",l);
        Check(orders==null||orders.OrderCount==ordersBefore,"Analizar no crea PurchaseOrders.",l);
        Check(market==null||market.MarketRevision==marketRev,"Analizar no modifica MarketRevision 2.3C.",l);
        Check(commercial==null||commercial.CommercialRevision==commercialRev,"Analizar no modifica CommercialRevision 2.3D.",l);
        Check(orders==null||orders.OrdersRevision==orderRev,"Analizar no modifica OrdersRevision 2.3E.",l);
        // Segunda ejecución: determinismo con mismo estado externo (secuencia del informe puede cambiar, por eso comparamos estrategia y planes de negocio).
        BistroBuilderSmartPurchaseReport report2; string error2; bool ok2=smart.TryBuildRecommendations(out report2,out error2); Check(ok2,"Segundo análisis runtime se ejecuta.",l);
        if(ok&&ok2&&report!=null&&report2!=null)
        {
            Check(report.recommendedStrategy==report2.recommendedStrategy,"Mismo estado mantiene la estrategia recomendada.",l);
            Check(report.plans.Count==report2.plans.Count,"Mismo estado mantiene tres planes.",l);
            for(int i=0;i<Mathf.Min(report.plans.Count,report2.plans.Count);i++)
            {
                Check(report.plans[i].strategy==report2.plans[i].strategy,"Plan "+i+" conserva estrategia.",l);
                Check(report.plans[i].totalCents==report2.plans[i].totalCents,"Plan "+report.plans[i].strategy+" conserva coste con estado externo idéntico.",l);
                Check(report.plans[i].ingredientsRecommended==report2.plans[i].ingredientsRecommended,"Plan "+report.plans[i].strategy+" conserva cardinalidad.",l);
            }
        }
        l.Add("[INFO] Diagnóstico de lectura: "+(report!=null?string.Join(" | ",report.diagnostics.ToArray()):error));
        log=string.Join("\n",l.ToArray());
    }
}
#endif
