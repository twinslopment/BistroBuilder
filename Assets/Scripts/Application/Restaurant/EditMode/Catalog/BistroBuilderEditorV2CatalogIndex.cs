using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// B9: índice de consulta sobre definiciones canónicas de catálogo.
/// No crea objetos de Unity ni modifica SAVIC, autorizaciones o Placement.
/// Al cambiar de fuente se reconstruye una sola vez; las consultas reutilizan
/// un búfer de resultados propiedad del llamante.
/// </summary>
public sealed class BistroBuilderEditorV2CatalogIndex
{
    private sealed class Row
    {
        public RestaurantPlaceableItemDefinition Item;
        public string SearchText;
    }

    private readonly List<Row> rows = new List<Row>(128);
    private readonly Dictionary<string, RestaurantPlaceableItemDefinition> byId =
        new Dictionary<string, RestaurantPlaceableItemDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<object, List<RestaurantPlaceableItemDefinition>> variants =
        new Dictionary<object, List<RestaurantPlaceableItemDefinition>>();
    private readonly List<Row> matched = new List<Row>(128);
    private readonly List<string> terms = new List<string>(8);
    private int revision;

    public int Count => rows.Count;
    public int Revision => revision;

    public void Rebuild(IReadOnlyList<RestaurantPlaceableItemDefinition> source)
    {
        rows.Clear();
        byId.Clear();
        variants.Clear();
        matched.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                RestaurantPlaceableItemDefinition item = source[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId) ||
                    !item.HasValidPrefab || byId.ContainsKey(item.ItemId))
                    continue;

                byId.Add(item.ItemId, item);
                rows.Add(new Row {
                    Item = item,
                    SearchText = Normalize(item.DisplayName + " " + item.Description +
                        " " + item.Category + " " + item.CatalogSubcategory)
                });

                // Los acabados publicados comparten un perfil canónico.
                // El índice los relaciona, pero NO fusiona ni esconde ItemIds.
                if (item.FinishProfile != null)
                {
                    object key = item.FinishProfile;
                    if (!variants.TryGetValue(key, out var group))
                    {
                        group = new List<RestaurantPlaceableItemDefinition>();
                        variants.Add(key, group);
                    }
                    group.Add(item);
                }
            }
        }
        rows.Sort((a, b) => StableCompare(a.Item, b.Item));
        foreach (var group in variants.Values)
            group.Sort(StableCompare);
        revision++;
    }

    public bool TryGetById(string id, out RestaurantPlaceableItemDefinition item) =>
        byId.TryGetValue(id ?? string.Empty, out item);

    public int CopyRelatedVariants(RestaurantPlaceableItemDefinition item,
        List<RestaurantPlaceableItemDefinition> destination)
    {
        if (destination == null) return 0;
        destination.Clear();
        if (item != null && item.FinishProfile != null &&
            variants.TryGetValue(item.FinishProfile, out var group))
            destination.AddRange(group);
        return destination.Count;
    }

    /// <summary>Filtra catálogo completo. Relevancia = orden canónico estable.</summary>
    public int Query(
        RestaurantEditCatalogSection section, int categoryCode, string search,
        RestaurantPlaceableEnvironmentScope scope, bool favoritesOnly,
        bool recentsOnly, HashSet<string> favorites, IList<string> recents,
        int sortMode, List<RestaurantPlaceableItemDefinition> destination)
    {
        if (destination == null) return 0;
        destination.Clear();
        matched.Clear();
        terms.Clear();

        string normalized = Normalize(search);
        if (normalized.Length > 0)
        {
            string[] parts = normalized.Split(new[] {' '},
                StringSplitOptions.RemoveEmptyEntries);
            for (int t = 0; t < parts.Length; t++)
                terms.Add(parts[t]);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            var item = row.Item;
            if (!RestaurantEditCatalogSections.Matches(section, categoryCode, item))
                continue;

            if (scope != RestaurantPlaceableEnvironmentScope.InteriorAndExterior &&
                item.PlacementScope != RestaurantPlaceableEnvironmentScope.InteriorAndExterior &&
                item.PlacementScope != scope)
                continue;

            if (favoritesOnly && (favorites == null || !favorites.Contains(item.ItemId)))
                continue;
            if (recentsOnly && (recents == null || !recents.Contains(item.ItemId)))
                continue;

            bool searchOk = true;
            for (int t = 0; t < terms.Count; t++)
            {
                if (row.SearchText.IndexOf(terms[t], StringComparison.Ordinal) >= 0)
                    continue;
                searchOk = false;
                break;
            }
            if (searchOk) matched.Add(row);
        }

        if (sortMode == 1)
            matched.Sort((a, b) => {
                int c = a.Item.PurchasePrice.CompareTo(b.Item.PurchasePrice);
                return c != 0 ? c : StableCompare(a.Item, b.Item);
            });
        else if (sortMode == 2)
            matched.Sort((a, b) => {
                int c = b.Item.PurchasePrice.CompareTo(a.Item.PurchasePrice);
                return c != 0 ? c : StableCompare(a.Item, b.Item);
            });
        else if (sortMode == 3)
            matched.Sort((a, b) => {
                int c = string.Compare(a.Item.DisplayName, b.Item.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.Item.ItemId, b.Item.ItemId,
                    StringComparison.Ordinal);
            });

        for (int i = 0; i < matched.Count; i++)
            destination.Add(matched[i].Item);
        return destination.Count;
    }

    private static int StableCompare(RestaurantPlaceableItemDefinition a,
        RestaurantPlaceableItemDefinition b)
    {
        int c = a.Category.CompareTo(b.Category);
        if (c != 0) return c;
        c = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : string.Compare(a.ItemId, b.ItemId, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string source = value.Trim().ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(source.Length);
        bool pendingSpace = false;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
