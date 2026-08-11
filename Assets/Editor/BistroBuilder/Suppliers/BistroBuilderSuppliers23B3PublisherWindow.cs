#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Publicador explícito de 2.3B3. No ejecuta cambios al abrir la ventana.
/// Primero muestra el preflight del contrato y la proyección; el usuario
/// decide cuándo publicar sobre supplier.catalog.
/// </summary>
public sealed class BistroBuilderSuppliers23B3PublisherWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa 'Analizar' para comprobar el contrato canónico.";
    private bool canPublish;
    private BistroBuilderSuppliers23B3CanonicalBridge.Contract contract;
    private BistroBuilderSuppliers23B3CanonicalBridge.Projection projection;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3B3 - Publicar autoría en supplier.catalog", priority = 22)]
    public static void OpenWindow()
    {
        BistroBuilderSuppliers23B3PublisherWindow window =
            GetWindow<BistroBuilderSuppliers23B3PublisherWindow>(true, "2.3B3 — Publicar supplier.catalog", true);
        window.minSize = new Vector2(760f, 520f);
        window.Show();
        window.Analyze();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("2.3B3 — CONVERGENCIA CANÓNICA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Publica los 6 proveedores y las 66 ofertas base de los editores de autoría sobre el asset supplier.catalog existente. " +
            "No crea otra autoridad runtime y no toca Inventario ni Recepciones.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analizar", GUILayout.Height(30f)))
        {
            Analyze();
        }

        EditorGUI.BeginDisabledGroup(!canPublish || EditorApplication.isPlayingOrWillChangePlaymode);
        if (GUILayout.Button("Publicar en supplier.catalog", GUILayout.Height(30f)))
        {
            if (EditorUtility.DisplayDialog(
                    "Publicar supplier.catalog",
                    "Se reemplazará únicamente el contenido serializado del catálogo canónico por la proyección validada de supplier.authoring/ingredient.authoring. " +
                    "La operación incluye rollback automático si la verificación post-escritura falla.\n\n¿Continuar?",
                    "Publicar",
                    "Cancelar"))
            {
                Publish();
            }
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("La publicación solo está permitida fuera de Play Mode.", MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        canPublish = false;
        contract = null;
        projection = null;

        if (!BistroBuilderSuppliers23B3CanonicalBridge.TryDiscoverContract(out contract, out string contractError))
        {
            report = "PREFLIGHT 2.3B3 — BLOQUEADO\n\n" + contractError;
            Repaint();
            return;
        }

        if (!BistroBuilderSuppliers23B3CanonicalBridge.TryBuildProjection(contract, out projection, out string projectionError))
        {
            report =
                "PREFLIGHT 2.3B3 — CONTRATO LOCALIZADO, PROYECCIÓN BLOQUEADA\n\n" +
                BistroBuilderSuppliers23B3CanonicalBridge.DescribeContract(contract) +
                "\n\nERROR\n" + projectionError;
            Repaint();
            return;
        }

        if (!BistroBuilderSuppliers23B3CanonicalBridge.TryValidateWithExistingDomain(
                contract, projection, out bool validatorFound, out string domainError))
        {
            report =
                "PREFLIGHT 2.3B3 — PROYECCIÓN RECHAZADA\n\n" +
                BistroBuilderSuppliers23B3CanonicalBridge.DescribeContract(contract) +
                "\n\nVALIDADOR DE DOMINIO\n" +
                (validatorFound ? "Localizado, pero rechazó la proyección.\n" : "No localizado.\n") +
                "ERROR\n" + domainError;
            Repaint();
            return;
        }

        string fingerprint = BistroBuilderSuppliers23B3CanonicalBridge.BuildFingerprint(contract.catalogAsset, contract);

        report =
            "PREFLIGHT 2.3B3 SUPERADO\n\n" +
            BistroBuilderSuppliers23B3CanonicalBridge.DescribeContract(contract) +
            "\n\nPROYECCIÓN\n" +
            "Proveedores: " + projection.suppliers.Count + "\n" +
            "Productos/ofertas base: " + projection.products.Count + "\n" +
            "Ingredientes canónicos referenciados: " + projection.ingredients.Count + "\n" +
            "SupplierId únicos: " + projection.supplierIds.Count + "\n" +
            "ProductId únicos: " + projection.productIds.Count + "\n" +
            "Validación previa: " + (validatorFound ? "validador de dominio existente" : "gates 2.3B3 + FK canónicas; descriptores se reconstruyen en runtime") + "\n" +
            "Fingerprint actual: " + fingerprint.GetHashCode().ToString("X8") + "\n\n" +
            "La publicación es apta. No se ha modificado ningún asset durante este análisis.";

        canPublish = true;
        Repaint();
    }

    private void Publish()
    {
        BistroBuilderSuppliers23B3CanonicalBridge.PublicationResult result =
            BistroBuilderSuppliers23B3CanonicalBridge.Publish();

        if (!result.success)
        {
            report = "PUBLICACIÓN 2.3B3 FALLIDA\n\n" + result.message;
            Debug.LogError("2.3B3: " + result.message);
            canPublish = false;
            Repaint();
            return;
        }

        report =
            "PUBLICACIÓN 2.3B3 SUPERADA\n\n" +
            result.message +
            "\n\nSiguiente paso: ejecutar '2.3B3 - Validar convergencia canónica'.";

        Debug.Log("2.3B3 — " + result.message);
        // No llamamos a Analyze aquí: conservar el informe de éxito evita
        // que el usuario pierda la confirmación de la publicación.
        canPublish = true;
        Repaint();
    }
}
#endif
