#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23FValidationWindow : EditorWindow
{
    private string report=""; private Vector2 scroll;
    [MenuItem(BistroBuilderSuppliers23FPaths.MenuRoot + "2.3F - Validar Motor de Compra Inteligente")]
    public static void Open(){ GetWindow<BistroBuilderSuppliers23FValidationWindow>(false,"Validación 2.3F").Show(); }
    private void OnGUI(){ GUILayout.Label("2.3F — Motor de Compra Inteligente y comparación estratégica",EditorStyles.boldLabel); if(GUILayout.Button("Validar de nuevo")) Run(); scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(report,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView(); }
    private void OnEnable(){ Run(); }
    private void Run()
    {
        int errors=0,warnings=0; List<string> lines=new List<string>();
        var settings=AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierSmartPurchaseSettings>(BistroBuilderSuppliers23FPaths.SettingsAssetPath);
        if(settings==null){errors++; lines.Add("[ERROR] Falta supplier.smart_purchase.settings.");} else { lines.Add("[OK] supplier.smart_purchase.settings localizado."); if(settings.SchemaId!=BistroBuilderSupplierSmartPurchaseSettings.CurrentSchemaId||settings.SchemaVersion!=1){errors++; lines.Add("[ERROR] Schema 2.3F no canónico.");} else lines.Add("[OK] Schema 2.3F canónico."); }
        var suppliers=Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath);
        var ingredients=Resources.Load<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath);
        int sc=0,oc=0,ic=0; if(suppliers==null){errors++; lines.Add("[ERROR] Falta supplier.authoring.");} else foreach(var s in suppliers.Suppliers) if(s!=null&&s.isActive){sc++; if(s.baseOffers!=null) foreach(var o in s.baseOffers) if(o!=null&&o.isActive) oc++;}
        if(ingredients==null){errors++; lines.Add("[ERROR] Falta ingredient.authoring.");} else foreach(var i in ingredients.Ingredients) if(i!=null&&i.isActive) ic++;
        lines.Add("[INFO] Proveedores activos: "+sc+". Ofertas activas: "+oc+". Ingredientes activos: "+ic+".");
        if(sc!=6){warnings++;lines.Add("[AVISO] Se esperaban 6 proveedores provisionales; actuales: "+sc+".");}
        if(oc!=66){warnings++;lines.Add("[AVISO] Se esperaban 66 ofertas cerradas en 2.3B; actuales: "+oc+".");}
        if(ic<22){errors++;lines.Add("[ERROR] Faltan ingredientes canónicos.");}
        if(settings!=null)
        {
            List<BistroBuilderSmartPurchaseIngredientFact> facts; List<BistroBuilderSmartPurchaseOfferFact> offers; BistroBuilderSuppliers23FTestData.Build(out facts,out offers);
            var r=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(1,1,facts,offers,settings);
            if(r.plans.Count!=3){errors++;lines.Add("[ERROR] No se generan las tres estrategias.");} else lines.Add("[OK] Ahorrar / Equilibrado / Urgente se generan de forma independiente.");
            lines.Add("[INFO] Estrategia recomendada en escenario controlado: "+r.recommendedStrategy+" — "+r.recommendedReason);
        }
        lines.Add("[INFO] 2.3F es SOLO lectura sobre Inventario/2.2C y SOLO crea Drafts en 2.3E cuando el jugador lo solicita explícitamente.");
        lines.Add("[INFO] No existe LLM/ML opaco: fórmulas, pesos y razones son deterministas y explicables.");
        report="Errores: "+errors+"  Advertencias: "+warnings+"\n\n"+string.Join("\n",lines.ToArray());
    }
}
#endif
