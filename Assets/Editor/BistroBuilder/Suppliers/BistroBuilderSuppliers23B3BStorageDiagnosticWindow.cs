#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2.3B3B — Diagnóstico NO DESTRUCTIVO del almacenamiento real de supplier.catalog.
///
/// Objetivo: dejar de inferir cómo está persistido el catálogo canónico y
/// observar exactamente qué recurso usa el SupplierCatalogService ya validado.
/// No escribe assets, no llama a getters arbitrarios de Unity y no toca runtime.
/// </summary>
public sealed class BistroBuilderSuppliers23B3BStorageDiagnosticWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa Diagnosticar. Esta herramienta no modifica ningún asset.";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3B3B - Diagnosticar storage supplier.catalog")]
    public static void Open()
    {
        GetWindow<BistroBuilderSuppliers23B3BStorageDiagnosticWindow>(
            "Diagnóstico 2.3B3B");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3B3B — DIAGNÓSTICO DE STORAGE CANÓNICO",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Solo inspecciona Assets/Resources y el código/contrato de " +
            "BistroBuilderSupplierCatalogService. No publica ni modifica supplier.catalog.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Diagnosticar", GUILayout.Height(28f)))
            {
                report = BuildReport();
                Debug.Log(report);
            }

            if (GUILayout.Button("Copiar informe", GUILayout.Height(28f)))
            {
                EditorGUIUtility.systemCopyBuffer = report ?? string.Empty;
            }
        }

        EditorGUILayout.Space(6f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report ?? string.Empty, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static string BuildReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("DIAGNÓSTICO 2.3B3B — STORAGE REAL DE supplier.catalog");
        sb.AppendLine("Fecha Editor: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        Type serviceType = FindType("BistroBuilderSupplierCatalogService");
        if (serviceType == null)
        {
            sb.AppendLine("[ERROR] No se encontró BistroBuilderSupplierCatalogService.");
            return sb.ToString();
        }

        sb.AppendLine("=== 1. CONTRATO DEL SERVICIO ===");
        sb.AppendLine("Tipo: " + serviceType.FullName);

        MethodInfo candidateMethod = FindCandidateMethod(serviceType);
        if (candidateMethod != null)
        {
            sb.AppendLine("Método candidato: " + candidateMethod.Name);
            ParameterInfo[] parameters = candidateMethod.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                sb.AppendLine(
                    "  P" + i + ": " + parameters[i].ParameterType.FullName +
                    " " + parameters[i].Name);
            }
        }
        else
        {
            sb.AppendLine("[WARN] No se localizó método Apply*Candidate*.");
        }

        sb.AppendLine();
        sb.AppendLine("Campos declarados relevantes (sin leer getters):");
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in serviceType.GetFields(flags)
                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            string typeName = field.FieldType.FullName ?? field.FieldType.Name;
            bool relevant =
                field.Name.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("snapshot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.FieldType == typeof(TextAsset) ||
                typeof(ScriptableObject).IsAssignableFrom(field.FieldType) ||
                field.FieldType == typeof(string);

            if (relevant)
            {
                sb.AppendLine("  " + field.Name + " : " + typeName);
            }
        }

        sb.AppendLine();
        sb.AppendLine("Métodos relacionados con carga/snapshot/json:");
        foreach (MethodInfo method in serviceType.GetMethods(flags)
                     .Where(m =>
                         m.Name.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         m.Name.IndexOf("Rebuild", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         m.Name.IndexOf("Snapshot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         m.Name.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         m.Name.IndexOf("Catalog", StringComparison.OrdinalIgnoreCase) >= 0)
                     .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("  " + DescribeMethod(method));
        }

        sb.AppendLine();
        sb.AppendLine("=== 2. CÓDIGO REAL DE CARGA ===");
        AppendServiceSourceEvidence(sb);

        sb.AppendLine();
        sb.AppendLine("=== 3. RECURSOS EN Assets/Resources RELACIONADOS ===");
        AppendResourceCandidates(sb, candidateMethod);

        sb.AppendLine();
        sb.AppendLine("=== 4. TIPOS SERIALIZABLES CANDIDATOS A SNAPSHOT/CONTENEDOR ===");
        AppendSerializableContainerCandidates(sb, serviceType, candidateMethod);

        sb.AppendLine();
        sb.AppendLine("FIN DEL DIAGNÓSTICO. No se ha modificado ningún asset.");
        return sb.ToString();
    }

    private static void AppendServiceSourceEvidence(StringBuilder sb)
    {
        string[] guids = AssetDatabase.FindAssets("BistroBuilderSupplierCatalogService t:MonoScript");
        if (guids == null || guids.Length == 0)
        {
            sb.AppendLine("[WARN] No se encontró el MonoScript del servicio mediante AssetDatabase.");
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null || script.GetClass() == null ||
                script.GetClass().Name != "BistroBuilderSupplierCatalogService")
            {
                continue;
            }

            sb.AppendLine("Script: " + path);
            if (!File.Exists(path))
            {
                sb.AppendLine("[WARN] El fichero no es legible desde File.IO.");
                continue;
            }

            string[] lines = File.ReadAllLines(path);
            int printed = 0;
            for (int lineIndex = 0; lineIndex < lines.Length && printed < 80; lineIndex++)
            {
                string line = lines[lineIndex];
                if (ContainsAny(
                        line,
                        "supplier.catalog",
                        "Resources.Load",
                        "Resources.LoadAll",
                        "TextAsset",
                        "JsonUtility",
                        "FromJson",
                        "ToJson",
                        "Snapshot",
                        "TryRebuildCatalog",
                        "resourcePath",
                        "ResourcePath"))
                {
                    sb.AppendLine((lineIndex + 1).ToString("0000") + ": " + line.Trim());
                    printed++;
                }
            }

            if (printed == 0)
            {
                sb.AppendLine("[WARN] No se encontraron líneas de carga con los patrones esperados.");
            }
        }
    }

    private static void AppendResourceCandidates(StringBuilder sb, MethodInfo candidateMethod)
    {
        Type supplierType = null;
        Type productType = null;
        Type ingredientType = null;
        if (candidateMethod != null)
        {
            ParameterInfo[] p = candidateMethod.GetParameters();
            if (p.Length >= 3)
            {
                supplierType = GetEnumerableElementType(p[0].ParameterType);
                productType = GetEnumerableElementType(p[1].ParameterType);
                ingredientType = GetEnumerableElementType(p[2].ParameterType);
            }
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets/Resources" });
        List<string> relevant = new List<string>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
            string name = main != null ? main.name : Path.GetFileNameWithoutExtension(path);
            string typeName = main != null ? main.GetType().FullName : "<sin main asset>";
            bool nameHit =
                path.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0;

            bool contentHit = false;
            string extra = string.Empty;

            TextAsset textAsset = main as TextAsset;
            if (textAsset != null)
            {
                string text = textAsset.text ?? string.Empty;
                contentHit =
                    text.IndexOf("supplier.catalog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (text.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     text.IndexOf("product", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     text.IndexOf("ingredient", StringComparison.OrdinalIgnoreCase) >= 0);

                extra =
                    " | TextAsset chars=" + text.Length +
                    " | schema=" + (text.IndexOf("supplier.catalog", StringComparison.OrdinalIgnoreCase) >= 0 ? "HIT" : "-") +
                    " | suppliers-key=" + JsonKeyHit(text, "supplier") +
                    " | products-key=" + JsonKeyHit(text, "product") +
                    " | ingredients-key=" + JsonKeyHit(text, "ingredient");
            }
            else if (main is ScriptableObject so)
            {
                string structural = DescribeObjectStructure(
                    so,
                    supplierType,
                    productType,
                    ingredientType,
                    0,
                    3,
                    new HashSet<object>(ReferenceEqualityComparer.Instance));
                if (!string.IsNullOrWhiteSpace(structural))
                {
                    contentHit = true;
                    extra = " | " + structural.Replace("\n", " ; ");
                }
            }

            if (nameHit || contentHit)
            {
                relevant.Add(path + " | " + typeName + extra);
            }
        }

        if (relevant.Count == 0)
        {
            sb.AppendLine("No se encontró ningún recurso con nombre/contenido relacionado.");
            return;
        }

        foreach (string line in relevant.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("- " + line);
        }
    }

    private static void AppendSerializableContainerCandidates(
        StringBuilder sb,
        Type serviceType,
        MethodInfo candidateMethod)
    {
        if (candidateMethod == null)
        {
            sb.AppendLine("Sin método candidato no se pueden resolver los tres tipos de elemento.");
            return;
        }

        ParameterInfo[] p = candidateMethod.GetParameters();
        Type supplierType = GetEnumerableElementType(p[0].ParameterType);
        Type productType = GetEnumerableElementType(p[1].ParameterType);
        Type ingredientType = GetEnumerableElementType(p[2].ParameterType);

        List<string> matches = new List<string>();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int a = 0; a < assemblies.Length; a++)
        {
            Type[] types;
            try
            {
                types = assemblies[a].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                continue;
            }

            for (int t = 0; t < types.Length; t++)
            {
                Type type = types[t];
                if (type == null || type == serviceType || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                FieldInfo sf = FindCollectionField(type, supplierType);
                FieldInfo pf = FindCollectionField(type, productType);
                FieldInfo inf = FindCollectionField(type, ingredientType);
                if (sf != null && pf != null && inf != null)
                {
                    matches.Add(
                        type.FullName +
                        " | suppliers=" + sf.Name +
                        " | products=" + pf.Name +
                        " | ingredients=" + inf.Name +
                        " | UnityObject=" + typeof(UnityEngine.Object).IsAssignableFrom(type));
                }
            }
        }

        if (matches.Count == 0)
        {
            sb.AppendLine("No se encontró tipo con las tres colecciones como campos directos.");
        }
        else
        {
            foreach (string match in matches
                         .Distinct()
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                         .Take(40))
            {
                sb.AppendLine("- " + match);
            }
        }
    }

    private static string DescribeObjectStructure(
        object value,
        Type supplierType,
        Type productType,
        Type ingredientType,
        int depth,
        int maxDepth,
        HashSet<object> visited)
    {
        if (value == null || depth > maxDepth || visited.Contains(value))
        {
            return string.Empty;
        }

        visited.Add(value);
        Type type = value.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        List<string> hits = new List<string>();

        foreach (FieldInfo field in type.GetFields(flags))
        {
            Type element = GetEnumerableElementType(field.FieldType);
            if (element != null &&
                (element == supplierType || element == productType || element == ingredientType))
            {
                hits.Add("depth" + depth + ":" + type.Name + "." + field.Name + "<" + element.Name + ">");
                continue;
            }

            if (depth >= maxDepth || ShouldNotTraverse(field.FieldType))
            {
                continue;
            }

            object child;
            try
            {
                child = field.GetValue(value);
            }
            catch
            {
                continue;
            }

            string nested = DescribeObjectStructure(
                child,
                supplierType,
                productType,
                ingredientType,
                depth + 1,
                maxDepth,
                visited);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                hits.Add(nested);
            }
        }

        return string.Join("\n", hits);
    }

    private static bool ShouldNotTraverse(Type type)
    {
        if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(decimal) || type == typeof(DateTime))
        {
            return true;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            return true;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            return true;
        }

        return false;
    }

    private static string JsonKeyHit(string text, string stem)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "-";
        }

        return text.IndexOf("\"" + stem, StringComparison.OrdinalIgnoreCase) >= 0
            ? "HIT"
            : "-";
    }

    private static bool ContainsAny(string source, params string[] needles)
    {
        if (source == null)
        {
            return false;
        }

        for (int i = 0; i < needles.Length; i++)
        {
            if (source.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeMethod(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        return method.ReturnType.Name + " " + method.Name + "(" +
               string.Join(", ", parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
    }

    private static MethodInfo FindCandidateMethod(Type serviceType)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo exact = serviceType.GetMethod("TryApplyCatalogCandidateForEditorTests", flags);
        if (exact != null)
        {
            return exact;
        }

        foreach (MethodInfo method in serviceType.GetMethods(flags))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name.IndexOf("Apply", StringComparison.OrdinalIgnoreCase) >= 0 &&
                method.Name.IndexOf("Candidate", StringComparison.OrdinalIgnoreCase) >= 0 &&
                parameters.Length >= 3 &&
                GetEnumerableElementType(parameters[0].ParameterType) != null &&
                GetEnumerableElementType(parameters[1].ParameterType) != null &&
                GetEnumerableElementType(parameters[2].ParameterType) != null)
            {
                return method;
            }
        }

        return null;
    }

    private static Type FindType(string simpleName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(simpleName, false);
            if (type != null)
            {
                return type;
            }

            try
            {
                type = assembly.GetTypes().FirstOrDefault(t => t != null && t.Name == simpleName);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // Algunas assemblies de paquetes pueden no cargar todos sus tipos.
            }
        }

        return null;
    }

    private static FieldInfo FindCollectionField(Type ownerType, Type elementType)
    {
        if (ownerType == null || elementType == null)
        {
            return null;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in ownerType.GetFields(flags))
        {
            if (GetEnumerableElementType(field.FieldType) == elementType)
            {
                return field;
            }
        }

        return null;
    }

    private static Type GetEnumerableElementType(Type type)
    {
        if (type == null || type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType)
        {
            Type[] args = type.GetGenericArguments();
            if (args.Length == 1 && typeof(IEnumerable).IsAssignableFrom(type))
            {
                return args[0];
            }
        }

        try
        {
            Type enumerable = type.GetInterfaces()
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable != null ? enumerable.GetGenericArguments()[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
