#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23FAutotestWindow : EditorWindow
{
    private int passed,failed; private string log=""; private Vector2 scroll;
    [MenuItem(BistroBuilderSuppliers23FPaths.MenuRoot + "2.3F - Autotest Motor de Compra Inteligente")]
    public static void Open(){ GetWindow<BistroBuilderSuppliers23FAutotestWindow>(false,"Autotest 2.3F").Show(); }
    private void OnGUI(){ GUILayout.Label("AUTOTEST 2.3F — Compra Inteligente",EditorStyles.boldLabel); if(GUILayout.Button("Ejecutar autotest")) Run(); GUILayout.Label("Pruebas superadas: "+passed+" / Pruebas fallidas: "+failed,EditorStyles.boldLabel); scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(log,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView(); }
    private void OnEnable(){ Run(); }
    private void Check(bool condition,string text,List<string> lines){ if(condition){passed++;lines.Add("[OK] "+text);}else{failed++;lines.Add("[FALLO] "+text);} }
    private void Run()
    {
        passed=failed=0; List<string> lines=new List<string>();
        var settings=AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierSmartPurchaseSettings>(BistroBuilderSuppliers23FPaths.SettingsAssetPath);
        Check(!EditorApplication.isPlaying,"Autotest ejecutado en Edit Mode.",lines);
        Check(settings!=null,"supplier.smart_purchase.settings existe.",lines); if(settings==null){log=string.Join("\n",lines);return;}
        Check(settings.SchemaId==BistroBuilderSupplierSmartPurchaseSettings.CurrentSchemaId,"schemaId canónico.",lines);
        Check(settings.SchemaVersion==1,"schemaVersion canónico.",lines);
        Check(settings.savingCostWeight>settings.savingSpeedWeight,"Ahorrar prioriza coste sobre velocidad.",lines);
        Check(settings.urgentSpeedWeight>settings.urgentCostWeight,"Urgente prioriza velocidad sobre coste.",lines);
        Check(settings.urgentStockoutWeight>settings.balancedStockoutWeight,"Urgente penaliza más la rotura.",lines);
        Check(settings.balancedReliabilityWeight>0,"Equilibrado considera fiabilidad.",lines);

        List<BistroBuilderSmartPurchaseIngredientFact> facts; List<BistroBuilderSmartPurchaseOfferFact> offers; BistroBuilderSuppliers23FTestData.Build(out facts,out offers);
        var a=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,1,facts,offers,settings);
        var b=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,1,facts,offers,settings);
        Check(a!=null,"Se construye informe.",lines); Check(a.plans.Count==3,"Se generan tres planes.",lines);
        Check(a.plans[0].strategy==BistroBuilderSmartPurchaseStrategy.Ahorrar,"Plan Ahorrar presente.",lines);
        Check(a.plans[1].strategy==BistroBuilderSmartPurchaseStrategy.Equilibrado,"Plan Equilibrado presente.",lines);
        Check(a.plans[2].strategy==BistroBuilderSmartPurchaseStrategy.Urgente,"Plan Urgente presente.",lines);
        Check(a.recommendedStrategy==BistroBuilderSmartPurchaseStrategy.Urgente,"Un escenario con merluza a cero recomienda Urgente.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredientsRecommended>0),"Las tres estrategias generan recomendaciones cuando existe necesidad.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected!=null)),"Toda recomendación tiene candidato seleccionado.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.packageCount>0)),"Todos los candidatos redondean a paquetes enteros positivos.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.purchasedMicrounits>0)),"Toda compra propuesta tiene cantidad física positiva.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.estimatedTotalCents>0)),"Todo candidato tiene coste positivo.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.targetStockMicrounits>=0)),"Los objetivos de stock no son negativos.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.projectedOverstockMicrounits>=0)),"El sobrestock proyectado no es negativo.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.reliability01>=0f&&i.selected.reliability01<=1f)),"Fiabilidad acotada 0..1.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.wasteRisk01>=0f&&i.selected.wasteRisk01<=1f)),"Riesgo de desperdicio acotado 0..1.",lines);
        Check(a.plans.TrueForAll(p=>p.ingredients.TrueForAll(i=>i.selected.reasons.Count>0)),"Las recomendaciones siempre explican el porqué.",lines);
        Check(a.plans.TrueForAll(p=>p.summaryReasons.Count>0),"Cada estrategia publica una explicación resumida.",lines);
        Check(a.plans.TrueForAll(p=>p.suppliers.TrueForAll(s=>s.totalCents==s.subtotalCents+s.shippingCents)),"Totales de proveedor = subtotal + portes.",lines);
        Check(a.plans.TrueForAll(p=>p.totalCents==p.subtotalCents+p.shippingCents),"Totales de plan = subtotal + portes.",lines);
        Check(a.plans.TrueForAll(p=>!p.containsMinimumOrderGap),"Ningún plan final conserva cestas por debajo del pedido mínimo.",lines);
        Check(a.plans.TrueForAll(p=>p.suppliers.TrueForAll(b=>b.meetsMinimumOrder)),"Todas las cestas publicadas son confirmables respecto al pedido mínimo.",lines);
        var tomateSaving=a.plans[0].ingredients.Find(x=>x.ingredientId=="ingredient_tomate")?.selected;
        var tomateBalanced=a.plans[1].ingredients.Find(x=>x.ingredientId=="ingredient_tomate")?.selected;
        var tomateUrgent=a.plans[2].ingredients.Find(x=>x.ingredientId=="ingredient_tomate")?.selected;
        Check(tomateSaving!=null && tomateBalanced!=null && tomateUrgent!=null,"Tomate está presente en las tres estrategias.",lines);
        Check(tomateSaving!=null && tomateBalanced!=null && tomateUrgent!=null && tomateSaving.supplierOfferId==tomateBalanced.supplierOfferId && tomateBalanced.supplierOfferId==tomateUrgent.supplierOfferId,"El escenario controlado selecciona la misma oferta rápida de tomate en las tres estrategias.",lines);
        Check(tomateSaving!=null && tomateBalanced!=null && tomateUrgent!=null && tomateSaving.normalizedCostPerMillionMicrounitsCents==tomateBalanced.normalizedCostPerMillionMicrounitsCents && tomateBalanced.normalizedCostPerMillionMicrounitsCents==tomateUrgent.normalizedCostPerMillionMicrounitsCents,"El coste normalizado de una misma oferta no cambia por comprar distinta cobertura ni por portes standalone.",lines);
        Check(a.plans.TrueForAll(p=>p.suppliers.TrueForAll(b=>b.totalCents==b.subtotalCents+b.shippingCents)),"Las cestas consolidadas publican subtotal + portes una sola vez por proveedor.",lines);
        Check(a.plans[2].ingredients.Exists(x=>x.ingredientId=="ingredient_merluza"),"Urgente atiende la merluza sin stock.",lines);
        Check(!a.plans[2].ingredients.Exists(x=>x.ingredientId=="ingredient_aceite_oliva"),"Urgente aplaza una necesidad de riesgo bajo si el único pedido accionable exigiría forzar un mínimo desproporcionado.",lines);
        var merluzaUrg=a.plans[2].ingredients.Find(x=>x.ingredientId=="ingredient_merluza");
        Check(merluzaUrg!=null && merluzaUrg.selected.supplierId=="supplier_express","Urgente elige la alternativa rápida en rotura crítica.",lines);
        var merluzaSave=a.plans[0].ingredients.Find(x=>x.ingredientId=="ingredient_merluza");
        Check(merluzaSave!=null,"Ahorrar también cubre una necesidad crítica.",lines);
        Check(a.plans[0].ingredients.Exists(x=>x.alternatives.Count>=2),"Se conservan alternativas comparables.",lines);

        string ja=JsonUtility.ToJson(a); string jb=JsonUtility.ToJson(b);
        Check(ja==jb,"Misma entrada produce informe determinista idéntico.",lines);
        var cloneFacts=new List<BistroBuilderSmartPurchaseIngredientFact>(); foreach(var f in facts) cloneFacts.Add(f.DeepClone()); cloneFacts[0].availableMicrounits+=100000000;
        var c=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,2,cloneFacts,offers,settings);
        Check(JsonUtility.ToJson(c)!=ja,"Cambiar stock cambia el análisis.",lines);
        Check(facts[0].availableMicrounits==2000000,"El motor no muta los hechos de inventario de entrada.",lines);
        Check(offers[0].effectiveUnitPriceCents==1050,"El motor no muta ofertas de entrada.",lines);

        // Casos de riesgo y disponibilidad.
        var zero=facts[0].DeepClone(); zero.availableMicrounits=0; Check(BistroBuilderSupplierSmartPurchaseEngine.EvaluateRisk(zero,settings)==BistroBuilderSmartPurchaseRisk.Critico,"Stock cero = riesgo crítico.",lines);
        var safe=facts[0].DeepClone(); safe.availableMicrounits=100000000; Check(BistroBuilderSupplierSmartPurchaseEngine.EvaluateRisk(safe,settings)<=BistroBuilderSmartPurchaseRisk.Bajo,"Cobertura holgada no se marca como crítica.",lines);
        var unavailable=new List<BistroBuilderSmartPurchaseOfferFact>(); foreach(var o in offers) unavailable.Add(o.DeepClone()); foreach(var o in unavailable) if(o.ingredientId=="ingredient_merluza"){o.availableForNewOrders=false;o.availability=BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;}
        var d=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,3,facts,unavailable,settings);
        Check(d.plans.TrueForAll(p=>!p.ingredients.Exists(x=>x.ingredientId=="ingredient_merluza")),"Temporalmente agotado nunca se propone para nuevos pedidos.",lines);

        // Completa una matriz de invariantes por estrategia/candidato.
        foreach(var p in a.plans)
        {
            Check(p.ingredientsEvaluated==3,""+p.strategy+": evalúa los 3 ingredientes controlados.",lines);
            Check(p.supplierCount==p.suppliers.Count,""+p.strategy+": supplierCount coherente.",lines);
            Check(!float.IsNaN(p.score) && !float.IsInfinity(p.score),""+p.strategy+": score finito.",lines);
            foreach(var rec in p.ingredients)
            {
                Check(!string.IsNullOrWhiteSpace(rec.ingredientId),p.strategy+": IngredientId presente.",lines);
                Check(rec.alternatives.Count>=1,p.strategy+": existe al menos una alternativa.",lines);
                Check(!float.IsNaN(rec.selected.score) && !float.IsInfinity(rec.selected.score),p.strategy+": score de candidato finito.",lines);
            }
        }
        log=string.Join("\n",lines.ToArray());
    }
}
#endif
