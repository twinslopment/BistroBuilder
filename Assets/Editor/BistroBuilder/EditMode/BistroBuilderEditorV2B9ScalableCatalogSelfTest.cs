using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Pruebas B9 del índice de datos y de un ScrollRect virtualizado real.</summary>
public static class BistroBuilderEditorV2B9ScalableCatalogSelfTest
{
    private static readonly List<string> lines = new List<string>();
    private static readonly List<Object> cleanup = new List<Object>();
    private static readonly List<RestaurantPlaceableItemDefinition> source =
        new List<RestaurantPlaceableItemDefinition>(10016);
    private static readonly List<RestaurantPlaceableItemDefinition> results =
        new List<RestaurantPlaceableItemDefinition>(10016);
    private static int passed, failed;

    [MenuItem("Bistro Builder/QA/Editor V2/B9 Scalable Catalog Self Test")]
    public static void RunFromMenu() => Run();
    public static void RunFromCommandLine() =>
        EditorApplication.Exit(Run() ? 0 : 1);

    private static bool Run()
    {
        passed = failed = 0;
        cleanup.Clear();
        source.Clear();
        lines.Clear();
        lines.Add("EDITOR V2 B9 - SCALABLE CATALOG");
        try
        {
            TestIndexAndFilters();
            TestVirtualization();
            TestRealSceneCatalog();
        }
        catch (Exception e)
        {
            failed++;
            lines.Add("FAIL - EXCEPTION " + e.GetType().Name + ": " + e.Message);
            lines.Add(e.StackTrace ?? "");
        }
        finally
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }
        lines.Add("Resultado: " + passed + " OK / " + failed + " fallos.");
        File.WriteAllLines("EditorV2_B9_ScalableCatalog_Report.txt", lines);
        for (int i = 0; i < lines.Count; i++)
            UnityEngine.Debug.Log(lines[i]);
        return failed == 0;
    }

    private static void Check(bool ok, string message)
    {
        if (ok) passed++; else failed++;
        lines.Add((ok ? "OK - " : "FAIL - ") + message);
    }
    private static void Metric(string key, double value) =>
        lines.Add("METRIC - " + key + "=" + value.ToString("F3",
            System.Globalization.CultureInfo.InvariantCulture));

    private static void TestIndexAndFilters()
    {
        var root = new GameObject("__B9Prefab");
        cleanup.Add(root);
        var prefab = root.AddComponent<RestaurantPlaceableObject>();

        var idField = typeof(RestaurantPlaceableItemDefinition).GetField("itemId",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var nameField = typeof(RestaurantPlaceableItemDefinition).GetField("displayName",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var descField = typeof(RestaurantPlaceableItemDefinition).GetField("description",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var categoryField = typeof(RestaurantPlaceableItemDefinition).GetField("category",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var scopeField = typeof(RestaurantPlaceableItemDefinition).GetField("placementScope",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var priceField = typeof(RestaurantPlaceableItemDefinition).GetField("purchasePrice",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var prefabField = typeof(RestaurantPlaceableItemDefinition).GetField("prefab",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var subcategoryField = typeof(RestaurantPlaceableItemDefinition).GetField("catalogSubcategory",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            // La escena cambia más tarde: conservar fixtures independientes de escena.
            item.hideFlags = HideFlags.HideAndDontSave;
            cleanup.Add(item);
            idField.SetValue(item, "b9_" + i.ToString("D5"));
            nameField.SetValue(item, i % 3 == 0 ? "Silla roble " + i :
                i % 3 == 1 ? "Mesa nogal " + i : "Lámpara lino " + i);
            descField.SetValue(item, "Mueble de interior para pruebas de búsqueda");
            categoryField.SetValue(item, i % 3 == 0 ?
                RestaurantPlaceableItemCategory.Seating :
                i % 3 == 1 ? RestaurantPlaceableItemCategory.Furniture :
                RestaurantPlaceableItemCategory.Lighting);
            scopeField.SetValue(item, i % 5 == 0 ?
                RestaurantPlaceableEnvironmentScope.ExteriorOnly :
                RestaurantPlaceableEnvironmentScope.InteriorAndExterior);
            priceField.SetValue(item, i % 1000);
            prefabField.SetValue(item, prefab);
            subcategoryField.SetValue(item, "ambient");
            source.Add(item);
        }
        Metric("CREATE_10000_MS", sw.Elapsed.TotalMilliseconds);
        var index = new BistroBuilderEditorV2CatalogIndex();
        sw.Restart();
        index.Rebuild(source);
        Metric("INDEX_10000_MS", sw.Elapsed.TotalMilliseconds);
        Check(index.Count == 10000, "10000 definiciones indexadas sin pérdida");
        var scope = RestaurantPlaceableEnvironmentScope.InteriorAndExterior;
        int total = index.Query(RestaurantEditCatalogSection.Build, -1, "",
            scope, false, false, null, null, 0, results);
        Check(total == 10000, "Todos devuelve exactamente 10000 artículos");
        Check(results[0] != null && results[9999] != null &&
            string.Compare(results[0].ItemId, results[9999].ItemId,
                StringComparison.Ordinal) != 0, "orden canónico estable");

        sw.Restart();
        int matches = index.Query(RestaurantEditCatalogSection.Build, -1,
            "silla roble", scope, false, false, null, null, 0, results);
        Metric("QUERY_10000_MS", sw.Elapsed.TotalMilliseconds);
        Check(matches > 3000 && matches < 4000,
            "búsqueda multi-término obtiene subconjunto correcto");
        Check(index.Query(RestaurantEditCatalogSection.Build, -1,
            "SILLA RÓBLE", scope, false, false, null, null, 0, results) == matches,
            "búsqueda ignora acentos y mayúsculas");
        Check(index.Query(RestaurantEditCatalogSection.Build,
            (int)RestaurantPlaceableItemCategory.Lighting, "", scope,
            false, false, null, null, 0, results) > 3000,
            "categoría de iluminación filtrada sin tarjetas");

        Check(index.Query(RestaurantEditCatalogSection.Build, -1,
            "", RestaurantPlaceableEnvironmentScope.InteriorOnly,
            false, false, null, null, 0, results) == 8000,
            "filtro de ámbito evita incluir exterior exclusivo");

        var favorites = new HashSet<string>(StringComparer.Ordinal)
            {"b9_00001", "b9_00300", "b9_09999"};
        Check(index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, true, false, favorites, null, 0, results) == 3,
            "favoritos actúan sobre índice, no tarjetas");
        var recents = new List<string> {"b9_00000", "b9_00002"};
        Check(index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, false, true, null, recents, 0, results) == 2,
            "recientes filtran independientemente");
        Check(index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, true, true, favorites, recents, 0, results) == 0,
            "combinación de filtros consistente");

        int sortCount = index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, false, false, null, null, 1, results);
        bool asc = true;
        for (int i = 1; i < results.Count; i++)
            asc &= results[i].PurchasePrice >= results[i-1].PurchasePrice;
        Check(sortCount == 10000 && asc, "orden por precio ascendente estable");
        index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, false, false, null, null, 2, results);
        bool desc = true;
        for (int i = 1; i < results.Count; i++)
            desc &= results[i].PurchasePrice <= results[i-1].PurchasePrice;
        Check(desc, "orden por precio descendente estable");
        index.Query(RestaurantEditCatalogSection.Build, -1,
            "", scope, false, false, null, null, 3, results);
        Check(results.Count == 10000, "orden alfabético conserva cardinalidad");

        int revision = index.Revision;
        source.Add(source[0]);
        index.Rebuild(source);
        Check(index.Count == 10000 && index.Revision == revision + 1,
            "republicación idempotente elimina duplicados y avanza revisión");
        source.RemoveAt(source.Count - 1);
        source.RemoveAt(42);
        index.Rebuild(source);
        Check(index.Count == 9999 && !index.TryGetById("b9_00042", out _),
            "invalidación retira un artículo desaparecido");
        source.Insert(42, cleanup.Count > 0 ?
            (RestaurantPlaceableItemDefinition)cleanup[43] : null);
        index.Rebuild(source);
        Check(index.Count == 10000, "reincorporación incremental sin duplicados");

        sw.Restart();
        for (int i = 0; i < 100; i++)
        {
            index.Query(RestaurantEditCatalogSection.Build, -1,
                (i & 1) == 0 ? "mesa nogal" : "lámpara",
                scope, false, false, null, null, i % 4, results);
        }
        Metric("100_SEARCHES_MS", sw.Elapsed.TotalMilliseconds);
        Check(sw.ElapsedMilliseconds < 3000,
            "100 búsquedas de 10000 artículos dentro de 3 s");

        // Obtener de nuevo el conjunto completo para stress del viewport.
        index.Query(RestaurantEditCatalogSection.Build, -1, "",
            scope, false, false, null, null, 0, results);
    }

    private static void TestRealSceneCatalog()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        var panels = Object.FindObjectsByType<RestaurantPlaceableCatalogPanel>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var services = Object.FindObjectsByType<RestaurantPlaceableCatalogService>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(panels.Length > 0 && services.Length > 0,
            "escena real contiene UI y autoridad del catálogo");
        if (panels.Length == 0 || services.Length == 0) return;

        services[0].RebuildCatalog();
        var panel = panels[0];
        var methodFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(RestaurantPlaceableCatalogPanel).GetMethod(
            "CacheDependenciesIfNeeded", methodFlags).Invoke(panel, null);
        typeof(RestaurantPlaceableCatalogPanel).GetMethod(
            "InitializeIfNeeded", methodFlags).Invoke(panel, null);

        var virtualList = panel.VirtualList;
        Check(virtualList != null && virtualList.IsReady,
            "catálogo real instala virtualizador B9 sin otra UI");
        if (virtualList == null || !virtualList.IsReady) return;
        Check(virtualList.FilteredCount == services[0].AvailableItemCount,
            "catálogo real muestra datos publicados con identidades canónicas");
        Check(virtualList.PoolCount <= 128,
            "escena real no materializa catálogo completo");
        Check(virtualList.ColumnCount >= 1,
            "cálculo de columnas válido en UI real");
        int before = virtualList.RebindCount;
        for (int i = 0; i < 100; i++) virtualList.RefreshWindow(false);
        Check(virtualList.RebindCount == before,
            "catálogo real no vuelve a vincular tarjetas sin desplazamiento");

        // La vista visible y el filtro se actualizan sin invalidar Placement.
        int matching = panel.ApplyB9Query("imposible__b9__sin_resultado",
            RestaurantPlaceableEnvironmentScope.InteriorAndExterior,
            false, false, null, null, 0);
        Check(matching == 0 && virtualList.VisibleCount == 0,
            "búsqueda sin resultados limpia el viewport real");

        // Stress con los skins reales y 10.000 fichas sintéticas:
        // las pruebas anteriores usan un Canvas desnudo, sin bordes,
        // favoritos ni textos TMP. Este caso exige la integración de ambas
        // capas gráficas actuales sin perder el pool durante el scroll.
        var actualScroll = panel.transform.Find("CatalogContent/ItemsScroll")
            ?.GetComponent<ScrollRect>();
        if (actualScroll != null)
        {
            // En batchmode el Canvas inactivo puede tener viewport 0×0.
            // Establecer tamaño físico temporal evita una prueba falsa
            // donde 300 scrolls no reciclan ninguna tarjeta.
            RectTransform viewport = actualScroll.viewport;
            Vector2 previousSize = viewport.sizeDelta;
            viewport.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, 410f);
            viewport.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, 620f);
            virtualList.InvalidateGeometry();
            virtualList.SetResults(results, false);
            Metric("REAL_SKIN_CONTENT_HEIGHT", virtualList.ContentHeight);
            Metric("REAL_SKIN_VIEWPORT_HEIGHT", viewport.rect.height);
            Metric("REAL_SKIN_COUNT", virtualList.FilteredCount);
            int styledPool = virtualList.PoolCount;
            int bindingsBefore = virtualList.RebindCount;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 300; i++)
            {
                float y = Mathf.Min(
                    Mathf.Max(0f, virtualList.ContentHeight - 620f),
                    i * 300f);
                actualScroll.content.anchoredPosition = new Vector2(0f, y);
                virtualList.RefreshWindow(false);
            }
            Metric("REAL_SKIN_300_SCROLL_UPDATES_MS",
                sw.Elapsed.TotalMilliseconds);
            Metric("REAL_SKIN_REBINDS",
                virtualList.RebindCount - bindingsBefore);
            Check(styledPool >= 8 &&
                virtualList.PoolCount == styledPool && styledPool < 60,
                "skins reales reciclan tarjetas visibles con 10000 artículos");
            Check(virtualList.RebindCount - bindingsBefore >= 100,
                "scroll real vinculó al menos 100 tarjetas distintas");
            Check(sw.ElapsedMilliseconds < 8000,
                "300 scroll updates con skins existentes en menos de 8 s");
            viewport.sizeDelta = previousSize;
        }
        else Check(false, "escena real expone su ScrollRect de artículos");

        int restored = panel.ApplyB9Query("",
            RestaurantPlaceableEnvironmentScope.InteriorAndExterior,
            false, false, null, null, 0);
        Check(restored == services[0].AvailableItemCount,
            "limpiar búsqueda restaura catálogo real sin reconstruir panel");
    }

    private static void TestVirtualization()
    {
        var go = new GameObject("__B9Canvas", typeof(RectTransform), typeof(Canvas));
        cleanup.Add(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(go.transform, false);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewRect = (RectTransform)viewport.transform;
        viewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 410f);
        viewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 620f);
        var content = new GameObject("Items", typeof(RectTransform),
            typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = (RectTransform)content.transform;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(178.5f, 245f);
        grid.spacing = new Vector2(11f, 12f);
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        var templateGo = new GameObject("Template", typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(RestaurantPlaceableCatalogItemView));
        templateGo.transform.SetParent(go.transform, false);
        var template = templateGo.GetComponent<RestaurantPlaceableCatalogItemView>();
        typeof(RestaurantPlaceableCatalogItemView).GetField("button",
            BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(template, templateGo.GetComponent<Button>());
        templateGo.SetActive(false);

        var virtualList = go.AddComponent<BistroBuilderEditorV2CatalogVirtualizedList>();
        RestaurantPlaceableItemDefinition clicked = null;
        virtualList.Configure(contentRect, template, item => clicked = item);
        virtualList.SetResults(results, false);
        Check(virtualList.IsReady && virtualList.FilteredCount == 10000,
            "ScrollRect real recibe 10000 artículos sin materializarlos");
        Check(virtualList.ColumnCount == 2, "viewport amplio usa dos columnas");
        Check(virtualList.PoolCount < 40 && virtualList.VisibleCount < 40,
            "pool limitado a ventana visible + overscan");
        Check(!grid.enabled && !content.GetComponent<ContentSizeFitter>().enabled,
            "layout pesado deshabilitado con dimensiones virtuales");
        Check(virtualList.ContentHeight > 100000f,
            "altura de scroll representa todos los artículos");

        int poolCount = virtualList.PoolCount;
        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= 1000; i++)
        {
            float y = i * 10f;
            contentRect.anchoredPosition = new Vector2(0f, y);
            virtualList.RefreshWindow(false);
        }
        Metric("1000_SCROLL_UPDATES_MS", sw.Elapsed.TotalMilliseconds);
        Check(virtualList.PoolCount == poolCount,
            "1000 desplazamientos no crean tarjetas nuevas");

        contentRect.anchoredPosition = new Vector2(0f, virtualList.ContentHeight - 620f);
        virtualList.RefreshWindow(true);
        Check(virtualList.FirstVisibleIndex > 9950,
            "salto al final encuentra las últimas referencias indexadas");

        RestaurantPlaceableCatalogItemView bound = null;
        foreach (Transform child in contentRect)
        {
            if (!child.gameObject.activeSelf) continue;
            bound = child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (bound != null && bound.Definition != null) break;
        }
        var expected = bound != null ? bound.Definition : null;
        if (bound != null)
            bound.GetComponent<Button>().onClick.Invoke();
        Check(expected != null && ReferenceEquals(clicked, expected),
            "tarjeta reciclada envía el artículo visible correcto");

        viewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
        virtualList.InvalidateGeometry();
        Check(virtualList.ColumnCount == 1,
            "viewport compacto pasa a una columna sin recrear catálogo");
        Check(virtualList.PoolCount < 40,
            "redimensionar no crea miles de tarjetas");
        virtualList.SetResults(null, false);
        Check(virtualList.FilteredCount == 0 && virtualList.VisibleCount == 0,
            "sin resultados desactiva todas las tarjetas");

        virtualList.SetResults(results, false);
        virtualList.SetInteractable(false);
        bool allDisabled = true;
        foreach (Transform child in contentRect)
        {
            var card = child.GetComponent<RestaurantPlaceableCatalogItemView>();
            if (card == null) continue;
            allDisabled &= !card.GetComponent<Button>().interactable;
        }
        Check(allDisabled, "colocación activa bloquea todas las tarjetas incluso recicladas");

        contentRect.anchoredPosition = Vector2.zero;
        virtualList.RefreshWindow(true);
        for (int i = 0; i < 20; i++) virtualList.RefreshWindow(false);
        long before = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < 10000; i++) virtualList.RefreshWindow(false);
        sw.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Metric("10000_UNCHANGED_FRAMES_MS", sw.Elapsed.TotalMilliseconds);
        Metric("10000_UNCHANGED_FRAMES_BYTES", bytes);
        Check(bytes < 4096, "10000 frames sin scroll evitan asignaciones");
        Check(sw.ElapsedMilliseconds < 3000,
            "10000 frames estables quedan bajo 3 segundos");
    }
}
