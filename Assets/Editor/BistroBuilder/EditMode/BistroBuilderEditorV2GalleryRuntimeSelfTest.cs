using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real Play Mode smoke/regression of Gallery V2 and B8 Inspector.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2GalleryRuntimeSelfTest
{
    private const string Stage = "BB.EditorV2Gallery.Stage";
    private const string Result = "BB.EditorV2Gallery.Result";
    private const string Report = "EditorV2_GalleryInspector_Runtime_Report.txt";

    static BistroBuilderEditorV2GalleryRuntimeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/Gallery and Inspector Runtime")]
    public static void RunFromMenu() => StartTest(false);

    public static void RunFromCommandLine() => StartTest(true);

    private static void StartTest(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode ya está activo.");
        File.WriteAllText(Path.GetFullPath(Report), "Gallery/Inspector starting\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage, cli ? "running_cli" : "running_menu");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__GalleryInspectorV2_QA")
                .AddComponent<BistroBuilderEditorV2GalleryDriver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("_cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }

    public static void Finish(bool ok, string text)
    {
        File.WriteAllText(Path.GetFullPath(Report),
            (ok ? "PASS\n" : "FAIL\n") + text + "\n");
        SessionState.SetBool(Result, ok);
        SessionState.SetString(Stage,
            SessionState.GetString(Stage, "running_menu").EndsWith("_cli",
                StringComparison.Ordinal) ? "done_cli" : "done_menu");
        if (ok) Debug.Log("[EditorV2 Gallery Inspector] PASS " + text);
        else Debug.LogError("[EditorV2 Gallery Inspector] FAIL " + text);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2GalleryDriver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int i = 0; i < 16; i++) yield return null;
        string result = "";
        IEnumerator routine = Verify(s => result = s);
        while (true)
        {
            bool hasStep = false;
            object step = null;
            Exception failure = null;
            try
            {
                hasStep = routine.MoveNext();
                if (hasStep) step = routine.Current;
            }
            catch (Exception error)
            {
                failure = error;
            }
            if (failure != null)
            {
                BistroBuilderEditorV2GalleryRuntimeSelfTest.Finish(
                    false, failure.ToString());
                yield break;
            }
            if (!hasStep) break;
            yield return step;
        }
        BistroBuilderEditorV2GalleryRuntimeSelfTest.Finish(true, result);
    }

    private IEnumerator Verify(Action<string> finish)
    {
        var mode = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogPanel>(
            FindObjectsInactive.Include);
        var inspector = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionInspector>(FindObjectsInactive.Include);
        var legacy = Object.FindFirstObjectByType<RestaurantPlaceableInspectorPanel>(
            FindObjectsInactive.Include);
        var b8 = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);

        Assert(mode != null && catalog != null && inspector != null &&
               legacy != null && b8 != null && registry != null,
            "faltan componentes canónicos en la escena");

        if (!mode.IsEditModeActive &&
            !mode.TryEnterEditMode(out _, out string reason))
            throw new InvalidOperationException("Entrar edición: " + reason);
        catalog.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 4; i++) yield return null;

        Assert(catalog.GetComponents<BistroBuilderEditorV2SelectionInspector>().Length == 1,
            "no duplicar inspector B8");
        var showcase = catalog.GetComponent<BistroBuilderEditorV2GalleryShowcase>();
        Assert(showcase != null &&
            catalog.GetComponents<BistroBuilderEditorV2GalleryShowcase>().Length == 1,
            "Galería Viva sin escaparate duplicado");
        Assert(showcase.IsShowing && showcase.Featured != null,
            "Destacado vinculado a un artículo real");
        var products = Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>(
            FindObjectsInactive.Include);
        if (products != null && products.AvailableItems != null &&
            products.AvailableItems.Any(item => item != null &&
                item.HasValidPrefab && item.PurchasePrice > 0 &&
                item.InspectorPreview != null))
        {
            Assert(showcase.Featured.PurchasePrice > 0 &&
                showcase.Featured.InspectorPreview != null,
                "Destacado no debe promocionar un cubo gratuito si hay producto autorado");
        }
        var showcaseRoot = catalog.transform.Find(
            "CatalogContent/BB_EditorV2_GalleryShowcase");
        Assert(showcaseRoot != null && showcaseRoot.gameObject.activeSelf,
            "Destacado visible en catálogo");
        var labelPrice = showcaseRoot.Find("FeaturedCard/Price")
            ?.GetComponent<TMP_Text>();
        Assert(labelPrice != null && labelPrice.text ==
            showcase.Featured.PurchasePrice.ToString("N0") + " €",
            "Precio de Destacado procede de definición real");
        showcase.OpenFeatured();
        var detailFromShowcase = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "BB_UIUX_PlaceableInspector");
        Assert(detailFromShowcase != null &&
            detailFromShowcase.gameObject.activeSelf,
            "Destacado abre ficha canónica del inspector");
        var itemScroll = catalog.transform.Find("CatalogContent/ItemsScroll")
            as RectTransform;
        Assert(itemScroll != null && itemScroll.offsetMax.y < -300f,
            "Destacado reserva espacio sin ocultar la cuadrícula");
        Assert(catalog.GetComponents<RestaurantPlaceableCatalogPreviewSkin>().Length == 1,
            "piel preview única");
        Assert(catalog.GetComponents<RestaurantPlaceableCatalogApprovedSkin>().Length == 1,
            "piel approved única");
        var content = catalog.transform.Find("CatalogContent");
        Assert(content != null, "Catálogo original");
        var title = content.Find("Header/PreviewTitle")?.GetComponent<TMP_Text>();
        Assert(title != null && title.text == "Catálogo Galería Viva",
            "Título Galería Viva: " + (title != null ? title.text : "NULL"));
        var recoleta = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        Assert(recoleta != null && title.font == recoleta,
            "Título oficial Recoleta aplicado al catálogo");
        var premium = catalog.GetComponent<BistroBuilderEditorV2PremiumLayout>();
        Assert(premium != null && premium.IsReady,
            "la disposición premium V2 debe construirse realmente");
        var categoryColumn = content.Find("CategoryBar") as RectTransform;
        var searchBox = content.Find("ApprovedSearch") as RectTransform;
        var itemList = content.Find("ItemsScroll") as RectTransform;
        var newTabs = content.Find("BB_EditorV2_ApprovedTabs") as RectTransform;
        Assert(categoryColumn != null && searchBox != null &&
            itemList != null && newTabs != null, "cinco zonas V2 montadas");
        Assert(categoryColumn.anchorMax.x == 0 &&
            categoryColumn.anchoredPosition.x < itemList.offsetMin.x &&
            categoryColumn.rect.height > 250f,
            "categorías realmente verticales a la izquierda del catálogo");
        Assert(content.GetComponent<RectTransform>().rect.width >= 580f,
            "Galería Viva debe ocupar una proporción legible de pantalla");
        Assert(itemList.offsetMin.x >= 160f &&
            itemList.offsetMax.y <= -430f &&
            newTabs.rect.height > 25f,
            "pestañas, destacado y tarjetas con espacio reservado");
        var firstCategory = categoryColumn.GetComponentsInChildren<
            RestaurantPlaceableCatalogCategoryView>(true)
            .FirstOrDefault(v => v.gameObject.activeSelf);
        var readableLabel = firstCategory != null ?
            firstCategory.transform.Find("PreviewLabel")?.GetComponent<TMP_Text>()
            : null;
        Assert(readableLabel != null && readableLabel.fontSize >= 15f &&
            readableLabel.alignment == TextAlignmentOptions.MidlineLeft,
            "categorías deben tener rótulos legibles en filas horizontales");
        var background = content.GetComponent<Image>();
        Assert(background != null &&
            background.sprite == BistroBuilderEditorV2BrassArt.Get(
                BistroBuilderEditorV2BrassArt.Style.Frame) &&
            background.type == Image.Type.Sliced,
            "Marco marfil/latón en nueve cortes, sin color oliva plano");
        catalog.SelectSection(RestaurantEditCatalogSection.Surfaces);
        for (int i = 0; i < 3; i++) yield return null;
        Assert(title.text.StartsWith("Taller ·", StringComparison.Ordinal),
            "Taller de superficies");
        catalog.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 2; i++) yield return null;

        var members = new List<BistroBuilderEditorV2Selection>();
        var definitions = new List<RestaurantPlaceableItemDefinition>();
        foreach (RestaurantPlaceableObject item in registry.RegisteredPlaceables)
        {
            if (item == null || !item.HasInstanceId || item.ItemDefinition == null)
                continue;
            members.Add(new BistroBuilderEditorV2Selection
            {
                family = BistroBuilderEditorV2ToolFamily.Furniture,
                kind = BistroBuilderEditorV2SelectionKind.Furniture,
                stableId = item.InstanceId,
                displayName = item.DisplayName,
                capabilities = BistroBuilderEditorV2SelectionCapability.Inspect,
                persistentIdentity = true
            });
            definitions.Add(item.ItemDefinition);
            if (members.Count == 2) break;
        }
        Assert(members.Count == 2, "dos artículos reales necesarios");
        legacy.ShowForDefinition(definitions[0]);
        var oldInspector = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "BB_UIUX_PlaceableInspector");
        Assert(oldInspector != null && oldInspector.gameObject.activeSelf,
            "ficha del catálogo accesible antes de seleccionar mundo");
        // Use the same actual furniture selection authority as player clicks.
        // Direct B8.ReplaceSelectionSet used to pass a one-frame-only test while
        // the coordinator cleared the synthetic selection on the next frame.
        var furnitureController = Object.FindFirstObjectByType<
            RestaurantEditInteractionController>(FindObjectsInactive.Include);
        Assert(furnitureController != null, "autoridad real mobiliario");
        RestaurantPlaceableObject firstObject;
        RestaurantPlaceableObject secondObject;
        bool resolvedFirst = registry.TryGetByInstanceId(
            members[0].stableId, out firstObject);
        bool resolvedSecond = registry.TryGetByInstanceId(
            members[1].stableId, out secondObject);
        Assert(resolvedFirst && resolvedSecond,
            "instancias reales por identidad");
        Assert(furnitureController.TrySelectPlaceable(secondObject),
            "selección física inicial");
        yield return null;
        Assert(furnitureController.TrySelectPlaceable(firstObject, true, false),
            "multiselección física aditiva");
        for (int stableFrame = 0; stableFrame < 8; stableFrame++) yield return null;
        Assert(b8.SelectionCount == 2 &&
               b8.PrimarySelection.stableId == members[0].stableId,
            "B8 debe conservar dos piezas seleccionadas entre frames"); 
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(inspector.IsShowing && inspector.VisibleSelectionCount == 2,
            "inspector reconoce conjunto real");
        var halo = catalog.GetComponent<BistroBuilderEditorV2FootprintHalo>();
        Assert(halo != null &&
            catalog.GetComponents<BistroBuilderEditorV2FootprintHalo>().Length == 1,
            "Halo de Huella V2 es único e idempotente");
        halo.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(halo.VisibleMemberCount == 2 && halo.HasGroupOutline,
            "Halo sigue ambas huellas reales y tiene contorno de grupo");
        var lines = halo.GetComponentsInChildren<LineRenderer>(true);
        Assert(lines.Length >= 4 && lines.Any(line => line.positionCount >= 5),
            "Halo representa geometría de footprint, no offsets de pantalla");
        Assert(!oldInspector.gameObject.activeSelf,
            "ficha antigua se oculta al seleccionar muebles del mundo");
        var root = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "BB_EditorV2_SelectionInspector");
        Assert(root != null, "raíz inspector V2");
        var count = root.Find("Panel/SelectionCount")?.GetComponent<TMP_Text>();
        Assert(count != null && count.text.Contains("2 artículos"),
            "contador muestra selección real");

        inspector.ExpandInspector();
        inspector.SetViewGroup(true);
        Assert(root.Find("Panel/ScrollArea/Viewport/Pages/GroupDetails")
            .gameObject.activeSelf, "vista Conjunto");
        var rows = root.Find("Panel/ScrollArea/Viewport/Pages/GroupDetails/Members");
        Assert(rows != null && rows.childCount == 2,
            "dos filas de miembros reales");
        Button switchButton = null;
        foreach (Transform row in rows)
        {
            var rowText = row.Find("MemberLabel")?.GetComponent<TMP_Text>();
            if (rowText != null &&
                !rowText.text.StartsWith("Principal", StringComparison.Ordinal))
                switchButton = row.GetComponent<Button>();
        }
        Assert(switchButton != null, "fila secundaria accesible");
        switchButton.onClick.Invoke();
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(b8.PrimarySelection.stableId == members[1].stableId,
            "botón de fila cambia el principal real B8");

        inspector.SetViewGroup(false);
        var label = root.Find("Panel/Heading/SelectedTitle")?.GetComponent<TMP_Text>();
        Assert(label != null && label.text == members[1].displayName,
            "datos del nuevo principal");
        inspector.CollapseInspector();
        Assert(!inspector.IsExpanded &&
            root.Find("InspectorRail").gameObject.activeSelf,
            "plegado conserva grupo");

        // Don't mutate world: only the B8 read model has been exercised.
        Assert(furnitureController.ClearSelection(),
            "deselección desde la autoridad real del mobiliario");
        b8.ClearSelectionSet();
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(!inspector.IsShowing || !inspector.IsExpanded,
            "deselección no deja panel sobre el restaurante");
        yield return new WaitForSecondsRealtime(.30f);
        halo.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(halo.VisibleMemberCount == 0 && !halo.HasGroupOutline,
            "Halo se retira por completo después de deseleccionar" +
            " members=" + halo.VisibleMemberCount +
            " group=" + halo.HasGroupOutline +
            " opacity=" + halo.Opacity +
            " b8=" + b8.SelectionCount +
            " local=" + furnitureController.HasSelection);

        finish("Galería Viva y Taller conectados; pieles únicas; marfil/latón; " +
            "2 muebles reales de registry; selección B8, Principal/Conjunto, " +
            "cambio de principal, plegado y deselección. Sin duplicar autoridad.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Gallery V2 QA: " + message);
    }
}
