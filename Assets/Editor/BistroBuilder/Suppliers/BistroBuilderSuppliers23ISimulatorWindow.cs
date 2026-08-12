#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23ISimulatorWindow : EditorWindow
{
    private int finalDay = 30;
    private long qualifiedPurchaseCentsPerDay = 10000L;
    private Vector2 scroll;
    private string report = string.Empty;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3I - Simulador de desbloqueos")]
    private static void Open()
    {
        BistroBuilderSuppliers23ISimulatorWindow window = GetWindow<BistroBuilderSuppliers23ISimulatorWindow>(true, "Simulador 2.3I");
        window.minSize = new Vector2(820f, 500f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo de progresión 2.3I", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Simula días y volumen cualificado sin crear PurchaseOrder, sin tocar supplier.authoring y sin modificar ninguna autoridad runtime.", MessageType.Info);
        finalDay = EditorGUILayout.IntSlider("Día final", finalDay, 1, 180);
        qualifiedPurchaseCentsPerDay = EditorGUILayout.LongField("Compras cualificadas/día (céntimos)", qualifiedPurchaseCentsPerDay);
        qualifiedPurchaseCentsPerDay = System.Math.Max(0L, qualifiedPurchaseCentsPerDay);
        if (GUILayout.Button("Simular progresión", GUILayout.Height(30f))) Simulate();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Simulate()
    {
        BistroBuilderSupplierAuthoringDatabase db = BistroBuilderSuppliers23IPaths.LoadSuppliers();
        if (db == null) { report = "Falta supplier.authoring."; return; }
        List<BistroBuilderSupplierAuthoringRecord> suppliers = new List<BistroBuilderSupplierAuthoringRecord>();
        db.CopySuppliers(suppliers, true);
        Dictionary<string, int> unlockDay = new Dictionary<string, int>(System.StringComparer.Ordinal);
        for (int day = 1; day <= finalDay; day++)
        {
            long purchase = System.Math.Max(0L, qualifiedPurchaseCentsPerDay) * System.Math.Max(0, day - 1);
            BistroBuilderSupplierProgressionFacts facts = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = day,
                daysOpen = System.Math.Max(0, day - 1),
                qualifiedPurchaseVolumeCents = purchase
            };
            for (int i = 0; i < suppliers.Count; i++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers[i];
                if (supplier == null || unlockDay.ContainsKey(supplier.SupplierId)) continue;
                if (BistroBuilderSupplierProgressionEngine.Evaluate(supplier, facts).isUnlocked) unlockDay[supplier.SupplierId] = day;
            }
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("SIMULACIÓN 2.3I — DÍA " + finalDay);
        sb.AppendLine("Compras cualificadas por día: " + (qualifiedPurchaseCentsPerDay / 100d).ToString("0.00") + " €");
        sb.AppendLine("Volumen acumulado al final: " + ((qualifiedPurchaseCentsPerDay * System.Math.Max(0, finalDay - 1)) / 100d).ToString("0.00") + " €");
        sb.AppendLine();
        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[i];
            if (supplier == null) continue;
            int day;
            bool unlocked = unlockDay.TryGetValue(supplier.SupplierId, out day);
            sb.AppendLine((unlocked ? "[DESBLOQUEA DÍA " + day + "] " : "[SIGUE BLOQUEADO] ") + supplier.displayName + " | " + supplier.SupplierId);
            BistroBuilderSupplierProgressionFacts endFacts = new BistroBuilderSupplierProgressionFacts
            {
                currentGameDay = finalDay,
                daysOpen = System.Math.Max(0, finalDay - 1),
                qualifiedPurchaseVolumeCents = qualifiedPurchaseCentsPerDay * System.Math.Max(0, finalDay - 1)
            };
            BistroBuilderSupplierAccessEvaluation evaluation = BistroBuilderSupplierProgressionEngine.Evaluate(supplier, endFacts);
            for (int c = 0; c < evaluation.conditions.Count; c++)
            {
                BistroBuilderSupplierUnlockConditionResult condition = evaluation.conditions[c];
                sb.AppendLine("  - " + condition.reasonText + " | " + (condition.satisfied ? "OK" : "pendiente"));
            }
        }
        sb.AppendLine();
        sb.AppendLine("Regla: los desbloqueos son permanentes una vez alcanzados en runtime; esta ventana solo calcula el primer día teórico.");
        sb.AppendLine("No se ha modificado ningún asset, PurchaseOrder, mercado, promoción, logística, entrega física, Inventario ni Recepciones.");
        report = sb.ToString();
    }
}
#endif
