#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adaptador Editor-only para descubrir ingredientes canónicos sin acoplar 2.3A
/// a una versión concreta de RecipeCatalogService/SupplierCatalogService.
///
/// Regla de seguridad: nunca invoca getters arbitrarios de UnityEngine.Object;
/// solo inspecciona campos declarados. Los getters se permiten únicamente en
/// DTOs administrados normales. Esto evita efectos laterales tipo ValidTRS.
/// </summary>
internal static class BistroBuilderCanonicalIngredientAuthoringDiscovery23A2
{
    internal sealed class DiscoveredIngredient
    {
        public string ingredientId;
        public string displayName;
        public string unit;
        public string category;
    }

    private static readonly string[] RootTypeNames =
    {
        "BistroBuilderSupplierCatalogService",
        "BistroBuilderRecipeCatalogService",
        "BistroBuilderInventoryService"
    };

    public static int TrySynchronizeIntoDatabase(
        BistroBuilderIngredientAuthoringDatabase database,
        bool showDialog,
        out string sourceDescription)
    {
        sourceDescription = string.Empty;

        if (database == null)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("2.3A", "No existe la base de ingredientes de autoría.", "Aceptar");
            }

            return 0;
        }

        List<DiscoveredIngredient> discovered = new List<DiscoveredIngredient>();
        Discover(discovered, out sourceDescription);

        if (discovered.Count == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Sincronizar ingredientes",
                    "No se pudieron descubrir ingredientes canónicos en los objetos/catálogos cargados.\n\n" +
                    "Abre la escena principal de Bistro Builder. Si sigue vacío, entra en Play Mode y vuelve a pulsar Sincronizar. " +
                    "La herramienta no crea IDs inventados.",
                    "Aceptar");
            }

            return 0;
        }

        Undo.RecordObject(database, "Sincronizar ingredientes canónicos 2.3A");

        int changes = 0;
        for (int index = 0; index < discovered.Count; index++)
        {
            DiscoveredIngredient canonical = discovered[index];
            if (string.IsNullOrWhiteSpace(canonical.ingredientId))
            {
                continue;
            }

            if (!database.TryGetIngredient(canonical.ingredientId, out BistroBuilderIngredientAuthoringRecord record))
            {
                record = new BistroBuilderIngredientAuthoringRecord();
                record.AssignStableIdOnce(canonical.ingredientId);
                database.EditorIngredients.Add(record);
                changes++;
            }

            if (!string.Equals(record.displayNameSnapshot ?? string.Empty, canonical.displayName ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(record.canonicalUnitSnapshot ?? string.Empty, canonical.unit ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(record.categorySnapshot ?? string.Empty, canonical.category ?? string.Empty, StringComparison.Ordinal))
            {
                record.RefreshCanonicalSnapshot(canonical.displayName, canonical.unit, canonical.category);
                changes++;
            }
        }

        database.EditorIngredients.Sort(
            (left, right) => string.Compare(
                left?.displayNameSnapshot ?? left?.IngredientId,
                right?.displayNameSnapshot ?? right?.IngredientId,
                StringComparison.CurrentCultureIgnoreCase));

        if (changes > 0)
        {
            database.EditorTouchRevision();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Sincronización completada",
                "Ingredientes descubiertos: " + discovered.Count + "\n" +
                "Cambios aplicados: " + changes + "\n" +
                "Fuente: " + sourceDescription,
                "Aceptar");
        }

        return discovered.Count;
    }

    public static int Discover(List<DiscoveredIngredient> buffer, out string sourceDescription)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        buffer.Clear();
        sourceDescription = string.Empty;

        Dictionary<string, DiscoveredIngredient> byId =
            new Dictionary<string, DiscoveredIngredient>(StringComparer.Ordinal);

        List<string> sources = new List<string>();
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int rootIndex = 0; rootIndex < RootTypeNames.Length; rootIndex++)
        {
            string requiredTypeName = RootTypeNames[rootIndex];

            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || !string.Equals(behaviour.GetType().Name, requiredTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                int before = byId.Count;
                HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                TraverseValue(behaviour, 0, 5, visited, byId);

                if (byId.Count > before)
                {
                    sources.Add(requiredTypeName + " (" + (byId.Count - before) + ")");
                }
            }
        }

        foreach (KeyValuePair<string, DiscoveredIngredient> pair in byId)
        {
            buffer.Add(pair.Value);
        }

        buffer.Sort((left, right) => string.Compare(left.displayName ?? left.ingredientId, right.displayName ?? right.ingredientId, StringComparison.CurrentCultureIgnoreCase));
        sourceDescription = sources.Count == 0 ? "sin fuente compatible" : string.Join(", ", sources);
        return buffer.Count;
    }

    private static void TraverseValue(
        object value,
        int depth,
        int maxDepth,
        HashSet<object> visited,
        Dictionary<string, DiscoveredIngredient> byId)
    {
        if (value == null || depth > maxDepth)
        {
            return;
        }

        Type type = value.GetType();

        if (IsScalar(type))
        {
            return;
        }

        if (!type.IsValueType)
        {
            if (visited.Contains(value))
            {
                return;
            }

            visited.Add(value);
        }

        // También puede extraer ScriptableObject/Component si sus datos están en campos.
        // ReadStringMember nunca invoca properties cuando target es UnityEngine.Object.
        if (TryExtractIngredient(value, out DiscoveredIngredient ingredient))
        {
            if (!string.IsNullOrWhiteSpace(ingredient.ingredientId))
            {
                byId[ingredient.ingredientId] = ingredient;
            }
        }

        if (value is IEnumerable enumerable && !(value is string))
        {
            foreach (object item in enumerable)
            {
                TraverseValue(item, depth + 1, maxDepth, visited, byId);
            }

            return;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        FieldInfo[] fields = type.GetFields(flags);

        for (int index = 0; index < fields.Length; index++)
        {
            FieldInfo field = fields[index];

            if (field.IsStatic || field.IsLiteral || IsScalar(field.FieldType))
            {
                continue;
            }

            string name = field.Name.ToLowerInvariant();
            Type fieldType = field.FieldType;

            // En raíces Unity solo recorremos datos que tengan semántica de catálogo/ingrediente.
            if (value is UnityEngine.Object &&
                !name.Contains("ingredient") &&
                !name.Contains("catalog") &&
                !name.Contains("recipe") &&
                !name.Contains("definition") &&
                !name.Contains("product") &&
                !typeof(IEnumerable).IsAssignableFrom(fieldType))
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

            TraverseValue(child, depth + 1, maxDepth, visited, byId);
        }
    }

    private static bool TryExtractIngredient(object value, out DiscoveredIngredient ingredient)
    {
        ingredient = null;
        Type type = value.GetType();

        string typeName = type.Name.ToLowerInvariant();
        bool semanticallyIngredient = typeName.Contains("ingredient");

        string id = ReadStringMember(value, "IngredientId", "ingredientId", "Id", "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string normalizedId = BistroBuilderIngredientAuthoringRecord.NormalizeIngredientId(id);

        if (!semanticallyIngredient &&
            !normalizedId.Contains("ingredient") &&
            !HasMember(type, "Unit") &&
            !HasMember(type, "BaseUnit"))
        {
            return false;
        }

        ingredient = new DiscoveredIngredient
        {
            ingredientId = normalizedId,
            displayName = ReadStringMember(value, "DisplayName", "displayName", "Name", "name"),
            unit = ReadStringMember(value, "BaseUnitId", "baseUnitId", "UnitId", "unitId", "BaseUnit", "baseUnit", "Unit", "unit", "InventoryUnit", "inventoryUnit"),
            category = ReadStringMember(value, "CategoryId", "categoryId", "Category", "category", "IngredientCategory", "ingredientCategory")
        };

        if (string.IsNullOrWhiteSpace(ingredient.displayName))
        {
            ingredient.displayName = normalizedId;
        }

        return true;
    }

    private static string ReadStringMember(object target, params string[] memberNames)
    {
        if (target == null)
        {
            return string.Empty;
        }

        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < memberNames.Length; index++)
        {
            string name = memberNames[index];
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                try
                {
                    return ConvertToDisplayString(field.GetValue(target));
                }
                catch
                {
                    // Continuar con el siguiente candidato.
                }
            }

            // No invocar propiedades nativas de UnityEngine.Object.
            if (target is UnityEngine.Object)
            {
                continue;
            }

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return ConvertToDisplayString(property.GetValue(target, null));
                }
                catch
                {
                    // Continuar con el siguiente candidato.
                }
            }
        }

        return string.Empty;
    }

    private static bool HasMember(Type type, string name)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetField(name, flags) != null || type.GetProperty(name, flags) != null;
    }

    private static string ConvertToDisplayString(object value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text.Trim();
        }

        Type type = value.GetType();
        if (type.IsEnum || type.IsPrimitive || value is decimal)
        {
            return value.ToString();
        }

        // Algunos Value Objects guardan el identificador en Value/Id.
        string nested = ReadDirectScalar(value, "Value", "value", "Id", "id", "Symbol", "symbol");
        return !string.IsNullOrWhiteSpace(nested) ? nested : value.ToString();
    }

    private static string ReadDirectScalar(object target, params string[] names)
    {
        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int index = 0; index < names.Length; index++)
        {
            FieldInfo field = type.GetField(names[index], flags);
            if (field != null)
            {
                object value = field.GetValue(target);
                if (value != null && IsScalar(value.GetType()))
                {
                    return value.ToString();
                }
            }

            if (!(target is UnityEngine.Object))
            {
                PropertyInfo property = type.GetProperty(names[index], flags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    object value = property.GetValue(target, null);
                    if (value != null && IsScalar(value.GetType()))
                    {
                        return value.ToString();
                    }
                }
            }
        }

        return string.Empty;
    }

    private static bool IsScalar(Type type)
    {
        return type == null ||
               type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(Guid);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
