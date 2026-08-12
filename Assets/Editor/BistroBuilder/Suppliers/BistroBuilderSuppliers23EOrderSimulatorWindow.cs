#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23EOrderSimulatorWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa Simular para construir un pedido mínimo no destructivo por cada proveedor activo.";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3E - Simulador no destructivo de pedidos")]
    public static void Open()
    {
        BistroBuilderSuppliers23EOrderSimulatorWindow window =
            GetWindow<BistroBuilderSuppliers23EOrderSimulatorWindow>("Simulador 2.3E");
        window.minSize = new Vector2(900f, 620f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simulador no destructivo de PurchaseOrder 2.3E", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Usa precios base únicamente para revisar mínimos, portes, formatos y condiciones. " +
            "La confirmación runtime real usa siempre la cotización vigente de 2.3D.",
            MessageType.Info);
        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Simular pedidos mínimos de los 6 proveedores", GUILayout.Height(34f))) Run();
        GUI.enabled = true;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23EPaths.SupplierDatabasePath);
        BistroBuilderIngredientAuthoringDatabase ingredients =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSuppliers23EPaths.IngredientDatabasePath);
        BistroBuilderSupplierPurchaseOrderSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierPurchaseOrderSettings>(BistroBuilderSuppliers23EPaths.PurchaseOrderSettingsPath);
        if (suppliers == null || ingredients == null || settings == null)
        {
            report = "ERROR: faltan supplier.authoring, ingredient.authoring o supplier.orders.settings.";
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(4096);
        builder.AppendLine("SIMULACIÓN 2.3E — PEDIDOS MÍNIMOS");
        builder.AppendLine("No se ha modificado ningún asset ni estado runtime.");
        builder.AppendLine();

        int simulated = 0;
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive) continue;
            BistroBuilderSupplierPurchaseOrdersSnapshot snapshot =
                BistroBuilderSupplierPurchaseOrderEngine.CreateInitialSnapshot(1, 2303UL, 2304UL);
            BistroBuilderPurchaseOrderRecord order;
            List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs;
            string error;
            if (!BistroBuilderSuppliers23ETestData.TryBuildValidDraftAndInputs(
                    snapshot, supplier, ingredients, settings, 1, out order, out inputs, out error))
            {
                builder.AppendLine(supplier.displayName + " | ERROR: " + error);
                continue;
            }
            BistroBuilderPurchaseOrderConfirmationPreview preview;
            if (!BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(
                    order, supplier, inputs, settings, out preview, out error))
            {
                builder.AppendLine(supplier.displayName + " | ERROR PREVIEW: " + error);
                continue;
            }

            simulated++;
            builder.AppendLine(supplier.displayName + " [" + supplier.SupplierId + "]");
            builder.AppendLine("  Líneas: " + preview.lineCount);
            builder.AppendLine("  Pedido mínimo: " + Money(preview.minimumOrderValueCents));
            builder.AppendLine("  Subtotal: " + Money(preview.subtotalCents));
            builder.AppendLine("  Porte aplicado: " + Money(preview.shippingCostCents));
            builder.AppendLine("  TOTAL: " + Money(preview.totalCents));
            builder.AppendLine("  Lead time cotizado: " + preview.quotedLeadTimeGameHours.ToString("0.##") + " h juego");
            builder.AppendLine("  Confirmable: " + (preview.canConfirm ? "SÍ" : "NO"));
            for (int lineIndex = 0; lineIndex < preview.lines.Count; lineIndex++)
            {
                BistroBuilderPurchaseOrderConfirmedLineSnapshot line = preview.lines[lineIndex];
                builder.AppendLine(
                    "    - " + line.ingredientDisplayName + " | " + line.packageDisplayName +
                    " | " + line.packageCount + " x " + Money(line.effectiveUnitPriceCents) +
                    " = " + Money(line.lineSubtotalCents));
            }
            if (preview.blockers.Count > 0)
            {
                for (int blockerIndex = 0; blockerIndex < preview.blockers.Count; blockerIndex++)
                    builder.AppendLine("    BLOQUEO: " + preview.blockers[blockerIndex]);
            }
            builder.AppendLine();
        }

        builder.AppendLine("RESUMEN");
        builder.AppendLine("Proveedores simulados: " + simulated);
        builder.AppendLine("Regla runtime: el precio mostrado aquí es BASE; en Play Mode 2.3E consulta 2.3D justo antes de confirmar.");
        builder.AppendLine("No se han creado pedidos reales ni escrito Inventory/Recepciones.");
        report = builder.ToString();
    }

    private static string Money(long cents)
    {
        return (cents / 100.0).ToString("0.00") + " €";
    }
}
#endif
