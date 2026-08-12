#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23IValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> lines = new List<string>();
    private int errors;
    private int warnings;
    private int info;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3I - Validar progresión y desbloqueos")]
    private static void Open()
    {
        BistroBuilderSuppliers23IValidationWindow window = GetWindow<BistroBuilderSuppliers23IValidationWindow>(true, "Validación 2.3I");
        window.minSize = new Vector2(820f, 460f);
        window.RunValidation();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3I — Progresión y desbloqueo de proveedores", EditorStyles.boldLabel);
        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(28f))) RunValidation();
        EditorGUILayout.LabelField("Errores: " + errors + "  Advertencias: " + warnings + "  Información: " + info, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < lines.Count; i++) EditorGUILayout.SelectableLabel(lines[i], GUILayout.Height(18f));
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        lines.Clear(); errors = 0; warnings = 0; info = 0;
        if (EditorApplication.isPlaying)
        {
            Error("La validación estructural debe ejecutarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSupplierProgressionSettings settings = BistroBuilderSuppliers23IPaths.LoadSettings();
        BistroBuilderSupplierAuthoringDatabase suppliers = BistroBuilderSuppliers23IPaths.LoadSuppliers();
        Check(settings != null, "supplier.progression.settings localizado.");
        if (settings != null)
        {
            Check(settings.SchemaId == BistroBuilderSupplierProgressionSettings.CurrentSchemaId, "schemaId 2.3I canónico.");
            Check(settings.SchemaVersion == BistroBuilderSupplierProgressionSettings.CurrentSchemaVersion, "schemaVersion 2.3I canónico.");
            Info("Volumen de compras cualificado: InDelivery=" + settings.CountInDeliveryOrders + ", Delivered=" + settings.CountDeliveredOrders + ".");
        }
        Check(suppliers != null, "supplier.authoring localizado.");
        if (suppliers == null) return;

        int active = 0, fromStart = 0, progressive = 0, lockedWithoutRules = 0;
        HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier == null || !supplier.isActive) continue;
            active++;
            if (string.IsNullOrWhiteSpace(supplier.SupplierId) || !ids.Add(supplier.SupplierId)) Error("SupplierId nulo o duplicado en proveedor activo.");
            if (supplier.unlockProfile == null)
            {
                Error(supplier.SupplierId + ": unlockProfile nulo.");
                continue;
            }
            if (supplier.unlockProfile.availableFromStart)
            {
                fromStart++;
                continue;
            }
            if (supplier.unlockProfile.conditions == null || supplier.unlockProfile.conditions.Count == 0)
            {
                lockedWithoutRules++;
                Error(supplier.SupplierId + ": proveedor bloqueado sin condiciones; sería inaccesible indefinidamente.");
                continue;
            }
            progressive++;
            for (int c = 0; c < supplier.unlockProfile.conditions.Count; c++)
            {
                BistroBuilderSupplierUnlockConditionAuthoring condition = supplier.unlockProfile.conditions[c];
                if (condition == null || condition.kind == BistroBuilderSupplierUnlockRuleKind.Ninguna)
                {
                    Error(supplier.SupplierId + ": condición de desbloqueo nula/Ninguna.");
                }
                else if (condition.numericThreshold < 0L)
                {
                    Error(supplier.SupplierId + ": umbral numérico negativo.");
                }
                else if ((condition.kind == BistroBuilderSupplierUnlockRuleKind.CategoriaCulinaria || condition.kind == BistroBuilderSupplierUnlockRuleKind.ConsumoFamiliaIngrediente) && string.IsNullOrWhiteSpace(condition.stringThreshold))
                {
                    Error(supplier.SupplierId + ": la regla " + condition.kind + " requiere stringThreshold.");
                }
            }
        }

        Check(active == 6, "Hay exactamente 6 proveedores activos en el bloque provisional.");
        Check(fromStart == 2, "Hay exactamente 2 proveedores disponibles desde el inicio.");
        Check(progressive == 4, "Hay exactamente 4 proveedores con progresión configurada.");
        Check(lockedWithoutRules == 0, "No hay proveedores bloqueados sin reglas.");

        ValidateExpectedRule(suppliers, "supplier_distribuciones_norte", BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos, 30000L);
        ValidateExpectedRule(suppliers, "supplier_huerta_clara", BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 3L);
        ValidateExpectedRule(suppliers, "supplier_carnes_selectas", BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 7L);
        ValidateExpectedRule(suppliers, "supplier_costa_fresca", BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 10L);

        Info("Semilla V1: Mercado Central y Hostelería Express disponibles al inicio; Huerta Clara por días; Distribuciones Norte por compras; Carnes Selectas y Costa Fresca por combinación AND de días + compras.");
        Info("2.3I soporta además Facturación, Reputación, Tamaño, Categoría culinaria y Consumo de familia mediante IBistroBuilderSupplierProgressionFactSource; si no existe autoridad conectada, la condición falla cerrada y se explica.");
        Info("Compatibilidad: 2.3C/2.3D pueden seguir simulando mercado/promociones de proveedores bloqueados; 2.3F filtra recomendaciones y Draft inteligentes; 2.3E permanece autoridad de pedidos y la UI debe usar TryCreatePlayerDraft de 2.3I.");
        Info("supplier.catalog no necesita republicación: 2.3B3 no proyecta unlockProfile al contrato runtime de catálogo.");
        Info("Persistencia integral se conectará en 2.3J; 2.3I ya expone CreateSnapshot/TryRestoreSnapshot y enlaza su snapshot a las semillas de 2.3C/2.3D.");
    }

    private void ValidateExpectedRule(BistroBuilderSupplierAuthoringDatabase db, string id, BistroBuilderSupplierUnlockRuleKind kind, long threshold)
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!db.TryGetSupplier(id, out supplier) || supplier == null || supplier.unlockProfile == null) { Error(id + ": proveedor/perfil no localizado."); return; }
        bool found = false;
        if (supplier.unlockProfile.conditions != null)
        {
            for (int i = 0; i < supplier.unlockProfile.conditions.Count; i++)
            {
                BistroBuilderSupplierUnlockConditionAuthoring c = supplier.unlockProfile.conditions[i];
                if (c != null && c.kind == kind && c.numericThreshold == threshold) { found = true; break; }
            }
        }
        Check(found, id + ": regla inicial " + kind + " = " + threshold + " presente.");
    }

    private void Check(bool condition, string message) { if (condition) { info++; lines.Add("[OK] " + message); } else Error(message); }
    private void Error(string message) { errors++; lines.Add("[ERROR] " + message); }
    private void Info(string message) { info++; lines.Add("[INFO] " + message); }
}
#endif
