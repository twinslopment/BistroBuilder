using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderGalleryVivaQa
{
    private const string Key = "BB.GalleryViva.Qa";
    static BistroBuilderGalleryVivaQa() { EditorApplication.playModeStateChanged += OnState; }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/GalleryViva");
        File.WriteAllText("Logs/GalleryViva/functional.txt", "RUNNING\n");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("__GalleryVivaQa").AddComponent<BistroBuilderGalleryVivaQaDriver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Passed", false) ? 0 : 1);
        }
    }
    public static void Finish(bool pass, string result)
    {
        File.WriteAllText("Logs/GalleryViva/functional.txt", (pass ? "PASS\n" : "FAIL\n") + result);
        SessionState.SetBool(Key + ".Passed", pass);
        if (pass) Debug.Log("[GalleryViva] PASS " + result); else Debug.LogError("[GalleryViva] FAIL " + result);
        EditorApplication.ExitPlaymode();
    }
}

public sealed partial class BistroBuilderGalleryVivaQaDriver : MonoBehaviour
{
    private readonly List<string> checks = new List<string>();
    private void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
    private IEnumerator Start()
    {
        var routine = Verify();
        while (true)
        {
            object step = null; bool more = false; Exception failure = null;
            try { more = routine.MoveNext(); if (more) step = routine.Current; } catch (Exception e) { failure = e; }
            if (failure != null) { BistroBuilderGalleryVivaQa.Finish(false, string.Join("\n", checks) + "\n" + failure); yield break; }
            if (!more) break; yield return step;
        }
        BistroBuilderGalleryVivaQa.Finish(true, string.Join("\n", checks) + "\nCHECKS=" + checks.Count);
    }
    private IEnumerator Verify()
    {
        for (int i = 0; i < 30; i++) yield return null;
        FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include)?.Hide();
        var mode = FindFirstObjectByType<RestaurantEditModeService>();
        Check(mode != null && (mode.IsEditModeActive || mode.TryEnterEditMode(out _, out _)), "Mode entered through canonical service");
        var panel = FindFirstObjectByType<RestaurantPlaceableCatalogPanel>();
        panel.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 8; i++) yield return null;
        Canvas.ForceUpdateCanvases();
        var gallery = panel.GetComponent<BistroBuilderGalleryViva>();
        Check(gallery != null && gallery.IsReady && gallery.OwnsPresentation, "Native gallery is the presentation owner");
        var title = gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/Title").GetComponent<TMPro.TMP_Text>();
        title.ForceMeshUpdate();
        Check(title.textInfo.characterInfo.Count(x => x.isVisible) > 5, "Gallery title has visible glyphs: " + title.text + " rect=" + title.rectTransform.rect + " font=" + title.font.name + " characters=" + title.textInfo.characterCount);
        Check(!FindObjectsByType<BistroBuilderEditorV2DiagnosisOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(x => x.enabled), "Prototype IMGUI diagnostic overlay is excluded from V2");
        Check(!FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().IsPlaytestPanelVisible, "Prototype construction IMGUI is excluded from V2");
        var source = FindFirstObjectByType<RestaurantPlaceableCatalogService>().AvailableItems.Where(x => x != null && x.HasValidPrefab).ToList();
        Check(source.Count > 2 && gallery.FilteredCount == source.Select(x => x.ItemId).Distinct().Count(), "Catalog uses all canonical real definitions");
        Check(panel.VirtualList.ColumnCount == 4, "Approved four-column grid");
        Check(BistroBuilderApprovedGlyphsR4.Font != null && BistroBuilderApprovedGlyphsR4.Font.characterTable.Count>=28,"All 28 approved vector silhouettes are present in the native SDF font");
        var vivaRoot=gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded");
        Check(title.text == "Catálogo", "Catalog title contains only Catálogo");
        Check(BistroBuilderCatalogOptionStyle.Validate(vivaRoot as RectTransform, out var styleReport), "Native option font/vector/label/grid validation: " + styleReport);
        for (int category = 0; category < 9; category++)
        {
            gallery.SelectCategory(category);
            Check(BistroBuilderCatalogOptionStyle.Validate(vivaRoot as RectTransform, out styleReport), "Uniform native option style after category " + category + ": " + styleReport);
        }
        gallery.SelectCategory(0);
        for (int tab = 0; tab < 3; tab++)
        {
            gallery.SetTab(tab);
            Check(BistroBuilderCatalogOptionStyle.Validate(vivaRoot as RectTransform, out styleReport), "Native option style survives scope tab " + tab + ": " + styleReport);
        }
        gallery.ResetFilters();
        var fleur=vivaRoot.Find("FleurDeLis").GetComponent<TMPro.TMP_Text>();fleur.ForceMeshUpdate();
        Check(fleur.textInfo.characterInfo.Any(x=>x.isVisible),"Approved fleur silhouette actually produces a native text mesh");
        var gridCards=gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().Where(x=>x.gameObject.activeSelf).ToArray();
        var gridViewport=vivaRoot.Find("ItemsScroll/Viewport") as RectTransform;
        foreach(var gridCard in gridCards.Take(4)) {
            var price=gridCard.transform.Find("Price") as RectTransform;
            Check(ScreenBounds(price).yMin>=ScreenBounds(gridViewport).yMin-1,"First grid row retains fully visible prices: "+gridCard.Definition.ItemId);
        }
        Check(panel.VirtualList.PoolCount <= 32, "B9 pool bounded by viewport");
        ClickThroughRaycaster(gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/Category_Asientos").GetComponent<Button>());
        Check(panel.GalleryResults.All(x => x.Category == RestaurantPlaceableItemCategory.Seating), "Category rail receives real pointer click");
        for(int i=0;i<4;i++)yield return null;
        Canvas.ForceUpdateCanvases();
        var chairsScroll=vivaRoot.Find("ItemsScroll").GetComponent<ScrollRect>();
        var chairsBar=chairsScroll.verticalScrollbar;
        var panelBounds=ScreenBounds(vivaRoot as RectTransform);
        var barBounds=ScreenBounds(chairsBar.transform as RectTransform);
        var viewBounds=ScreenBounds(chairsScroll.viewport);
        Check(chairsBar.name=="BrassScrollbar" && chairsBar.GetComponent<Image>().sprite.name!="", "Gallery owns its native brass scrollbar");
        Check(barBounds.xMax<=panelBounds.xMax-4 && barBounds.yMin>=panelBounds.yMin+4, "Scrollbar stays inset inside catalogue frame");
        Check(viewBounds.xMax<barBounds.xMin && viewBounds.yMin>=panelBounds.yMin+4, "Masked chair viewport is inset and separate from scrollbar");
        var actualHandleBounds=ScreenBounds(chairsBar.handleRect);
        Check(actualHandleBounds.xMin>=barBounds.xMin && actualHandleBounds.xMax<=barBounds.xMax && actualHandleBounds.yMin>=barBounds.yMin && actualHandleBounds.yMax<=barBounds.yMax, "Actual brass handle stays inside the channel on both axes");
        Check(chairsScroll.viewport.GetComponent<RectMask2D>().enabled, "Chair images and text are clipped by the native viewport");
        foreach(var headingName in new[]{"RelatedHeading","AllHeading"}) {
            var heading=vivaRoot.Find(headingName).GetComponent<TMPro.TMP_Text>();heading.ForceMeshUpdate();
            Check(!heading.isTextOverflowing && heading.textInfo.characterInfo.Any(x=>x.isVisible), "Chair section heading fits and renders: "+headingName);
        }
        foreach(var chairCard in gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().Where(x=>x.gameObject.activeSelf)) {
            var photo=chairCard.transform.Find("ItemImage").GetComponent<Image>();
            Check(!BistroBuilderCatalogThumbnailService.IsGeneratedIcon(photo.sprite) || (photo.sprite.texture.width==1024 && photo.sprite.texture.height==1024),
                "Chair native card preserves manual art or uses full 1024-pixel derivative: "+chairCard.Definition.ItemId);
            Check(photo.rectTransform.pivot==new Vector2(.5f,.5f) &&
                Mathf.Abs(photo.rectTransform.anchoredPosition.x-((RectTransform)chairCard.transform).rect.width*.5f)<.01f,
                "Chair photo is centered within the native card: "+chairCard.Definition.ItemId);
            var name=chairCard.transform.Find("Name").GetComponent<TMPro.TMP_Text>();name.ForceMeshUpdate();
            var price=chairCard.transform.Find("Price").GetComponent<TMPro.TMP_Text>();price.ForceMeshUpdate();
            Check(name.textInfo.characterInfo.Count(x=>x.isVisible)>=6 && name.textInfo.lineCount<=3, "Chair name occupies up to three readable lines: "+chairCard.Definition.ItemId);
            Check(name.GetPreferredValues("Contemporánea",float.PositiveInfinity,float.PositiveInfinity).x<=name.rectTransform.rect.width && !name.isTextOverflowing, "Compact chair label preserves whole words and its complete published distinction: "+chairCard.Definition.ItemId);
            Check(!price.isTextOverflowing && price.textInfo.characterInfo.Any(x=>x.isVisible), "Chair price renders within its reserved line: "+chairCard.Definition.ItemId);
            Check(-price.rectTransform.anchoredPosition.y+price.rectTransform.rect.height<=((RectTransform)chairCard.transform).rect.height, "Chair price remains within its uniform card: "+chairCard.Definition.ItemId);
        }
        for(int i=0;i<4;i++) {
            var relatedCard=vivaRoot.Find("Related_"+i);if(!relatedCard.gameObject.activeSelf)continue;
            foreach(var labelName in new[]{"Name","Price"}) {
                var label=relatedCard.Find(labelName).GetComponent<TMPro.TMP_Text>();label.ForceMeshUpdate();
                Check(label.textInfo.characterInfo.Any(x=>x.isVisible), "Related chair text actually renders: "+i+"/"+labelName);
            }
        }
        chairsScroll.verticalNormalizedPosition=1; Canvas.ForceUpdateCanvases();
        var thumb=ScreenBounds(chairsBar.handleRect);
        var drag=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,position=thumb.center };
        UnityEngine.EventSystems.ExecuteEvents.Execute(chairsBar.gameObject,drag,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
        drag.position=new Vector2(thumb.center.x,barBounds.yMin+5);
        UnityEngine.EventSystems.ExecuteEvents.Execute(chairsBar.gameObject,drag,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(chairsBar.gameObject,drag,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
        for(int i=0;i<4;i++)yield return null;
        Check(chairsScroll.verticalNormalizedPosition<.1f, "Native scrollbar thumb drag reaches final chair row");
        var lastChair=gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().First(x=>x.gameObject.activeSelf && x.Definition==panel.GalleryResults[panel.GalleryResults.Count-1]);
        Check(ScreenBounds(lastChair.transform.Find("Price") as RectTransform).yMin>=viewBounds.yMin-1, "Last chair price is fully visible after scroll");
        chairsScroll.verticalNormalizedPosition=1;
        gallery.SelectCategory(0);
        var first = panel.GalleryResults[0]; var last = panel.GalleryResults[panel.GalleryResults.Count - 1];
        gallery.SetTab(1); Check(gallery.FilteredCount == 0 && gallery.Featured == null, "Empty favorites also clear featured");
        gallery.ToggleFavorite(first); Check(gallery.FilteredCount == 1 && gallery.Featured == first, "Favorites query and editorial agree");
        gallery.SetTab(0); gallery.Remember(first); gallery.Remember(last); gallery.SetTab(2);
        Check(gallery.FilteredCount == 2 && panel.GalleryResults[0] == last, "Recents ordered newest first");
        gallery.ResetFilters();
        gallery.SearchField.text = "BASICA";
        Check(gallery.FilteredCount > 0 && panel.GalleryResults.All(x => x.DisplayName.Contains("básica")), "Case and accent insensitive search");
        gallery.SearchField.text = "__no_such_real_item__";
        Check(gallery.FilteredCount == 0 && gallery.Featured == null && gallery.RelatedCount == 0, "No stale featured or related after empty search");
        gallery.ResetFilters(); gallery.SetPriceRange(11, 60);
        Check(gallery.FilteredCount == source.Count(x => x.PurchasePrice >= 11 && x.PurchasePrice <= 60), "Real price interval filter");
        gallery.SetSort(1); Check(panel.GalleryResults.Zip(panel.GalleryResults.Skip(1), (a, b) => a.PurchasePrice <= b.PurchasePrice).All(x => x), "Ascending real prices");
        gallery.SetSort(2); Check(panel.GalleryResults.Zip(panel.GalleryResults.Skip(1), (a, b) => a.PurchasePrice >= b.PurchasePrice).All(x => x), "Descending real prices");
        gallery.SetScope(RestaurantPlaceableEnvironmentScope.ExteriorOnly);
        Check(panel.GalleryResults.All(x => x.PlacementScope != RestaurantPlaceableEnvironmentScope.InteriorOnly), "Scope combined with price filter");
        gallery.ResetFilters(); gallery.SelectCategory(2);
        Check(panel.GalleryResults.All(x => x.Category == RestaurantPlaceableItemCategory.Seating), "Seating category");
        gallery.SetTab(1); gallery.SelectCategory(0);
        Check(gallery.SelectedTab == 1 && gallery.FilteredCount == 1, "Category change preserves selected filters");
        gallery.ResetFilters();
        gallery.SetCollapsed(true); Check(gallery.IsCollapsed && gallery.FilteredCount == source.Count, "Collapse preserves query");
        gallery.SetCollapsed(false);
        for (int i = 0; i < 4; i++) yield return null;
        var filter = gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/Filters").GetComponent<Button>();
        ClickThroughRaycaster(filter);
        Check(gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/FiltersPopup").gameObject.activeSelf, "Filter popup receives actual UI raycast");
        yield return null;
        var popup = gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/FiltersPopup");
        var low = popup.Find("MinimumPrice").GetComponent<TMPro.TMP_InputField>();
        var high = popup.Find("MaximumPrice").GetComponent<TMPro.TMP_InputField>();
        low.text = "11"; high.text = "60";
        ClickThroughRaycaster(popup.Find("ApplyFilters").GetComponent<Button>());
        Check(panel.GalleryResults.Count == source.Count(x => x.PurchasePrice >= 11 && x.PurchasePrice <= 60), "Native price inputs and apply button execute combined query");
        ClickThroughRaycaster(filter);
        yield return null;
        ClickThroughRaycaster(popup.Find("Scope_2").GetComponent<Button>());
        Check(panel.GalleryResults.All(x => x.PlacementScope != RestaurantPlaceableEnvironmentScope.InteriorOnly), "Native exterior button executes scope query");
        ClickThroughRaycaster(popup.Find("ResetFilters").GetComponent<Button>());
        Check(gallery.FilteredCount == source.Count, "Native reset button clears combined filters");
        ClickThroughRaycaster(filter);
        // Pooled card favorite must target its CURRENT definition, not the template.
        var card = gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().First(x => x.gameObject.activeSelf);
        var bound = card.Definition; bool before = gallery.IsFavorite(bound);
        ClickThroughRaycaster(card.transform.Find("Favorite").GetComponent<Button>());
        Check(gallery.IsFavorite(bound) != before, "Recycled favorite callback targets current item");
        RestaurantPlaceableItemDefinition clicked = null;
        panel.ItemSelected += item => clicked = item;
        ClickThroughRaycaster(card.Button);
        var interaction = FindFirstObjectByType<RestaurantEditInteractionController>();
        Check(clicked == bound && interaction.HasActivePlacement, "Native card uses canonical provisional placement");
        interaction.CancelActivePlacement();
        gallery.SearchField.text = "básica";
        yield return null;
        var rebound = gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().First(x => x.gameObject.activeSelf);
        var reboundItem = rebound.Definition;
        bool reboundBefore = gallery.IsFavorite(reboundItem);
        ClickThroughRaycaster(rebound.transform.Find("Favorite").GetComponent<Button>());
        Check(gallery.IsFavorite(reboundItem) != reboundBefore, "Favorite still correct after pool rebind to another query");
        gallery.ResetFilters();
        for (int i = 0; i < 50; i++) yield return null;
        Check(!gallery.transform.Find("CatalogContent/ItemsScroll").gameObject.activeSelf &&
            !gallery.transform.Find("CatalogContent/CategoryBar").gameObject.activeSelf,
            "Legacy catalog remains hidden after filters and theme scan");
        var content = gallery.transform.Find("CatalogContent");
        Check(content.Cast<Transform>().All(x => !x.gameObject.activeSelf || x.name == "BB_GaleriaViva_Native"), "Only native V2 content may be active in gallery host");
        Check(!panel.GetComponent<RestaurantPlaceableCatalogApprovedSkin>().enabled && !panel.GetComponent<RestaurantPlaceableCatalogPreviewSkin>().enabled && !panel.GetComponent<BistroBuilderEditorV2GalleryShowcase>().enabled && !panel.GetComponent<BistroBuilderEditorV2PremiumLayout>().enabled, "Previous catalog renderers and layout are disabled");
        Check(gallery.Featured == null || panel.GalleryResults.Contains(gallery.Featured), "Featured never escapes active query");
        gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded/Featured/Details").GetComponent<Button>().onClick.Invoke();
        for (int i = 0; i < 5; i++) yield return null;
        var canvasRoot = panel.GetComponentInParent<Canvas>().transform;
        var itemInspector = canvasRoot.Find("BB_UIUX_PlaceableInspector");
        Check(itemInspector != null && itemInspector.GetComponent<BistroBuilderEditChromeSurface>() != null, "Product inspector owns the native V2 paper palette");
        var itemTexts = itemInspector.GetComponentsInChildren<TMPro.TMP_Text>().Where(x => !string.IsNullOrWhiteSpace(x.text) && x.name != "Glyph" && x.name != "Icon").ToArray();
        Check(itemTexts.Length > 3 && itemTexts.All(x => !(x.color.r > .8f && x.color.g > .8f && x.color.b > .8f)), "Product inspector has no white text on light paper");
        var detailView = itemInspector.Find("ReferenceDetails");
        Check(detailView.gameObject.activeInHierarchy && !itemInspector.Find("Viewport").gameObject.activeSelf,
            "Only the approved native details composition is visible");
        Check(detailView.Find("Preview").GetComponent<RectTransform>().rect.height == 172,
            "Product preview uses the approved 172px reference height");
        var productTitle = detailView.Find("Title").GetComponent<TMPro.TMP_Text>(); productTitle.ForceMeshUpdate();
        Check(productTitle.textInfo.characterInfo.Count(x => x.isVisible) > 3, "Product inspector title has visible glyphs");
        var favoriteItem=gallery.Featured;bool inspectorFavoriteBefore=gallery.IsFavorite(favoriteItem);
        ClickThroughRaycaster(detailView.Find("Favorite").GetComponent<Button>());
        Check(gallery.IsFavorite(favoriteItem)!=inspectorFavoriteBefore,"Inspector star updates the same favorites query");
        ClickThroughRaycaster(detailView.Find("Preview/Zoom").GetComponent<Button>());
        Check(detailView.Find("PreviewZoom").gameObject.activeSelf,"Native product zoom opens");
        yield return null;
        ClickThroughRaycaster(detailView.Find("PreviewZoom/CloseZoom").GetComponent<Button>());
        Check(!detailView.Find("PreviewZoom").gameObject.activeSelf,"Native product zoom closes");
        yield return null;
        Check(SingleInspector(canvasRoot),"Catalog details has one inspector owner");
        var transformChecks = VerifyCatalogTransforms(detailView, favoriteItem);
        while (transformChecks.MoveNext()) yield return transformChecks.Current;
        VerifyComposition(panel, itemInspector as RectTransform);
        VerifyCompactFooter();
        VerifyToolbarDisabled();
        var lightingChecks=VerifyHeaderLighting();
        while(lightingChecks.MoveNext())yield return lightingChecks.Current;
        panel.SelectSection(RestaurantEditCatalogSection.Walls);
        for (int i = 0; i < 5; i++) yield return null;
        var workshop = canvasRoot.Find("BB_ArchitectureCatalog");
        var workshopTitle = workshop.Find("Title").GetComponent<TMPro.TMP_Text>();
        Check(workshop.gameObject.activeInHierarchy && workshopTitle.text == "Taller · Paredes" && workshopTitle.font != null, "Construction opens native V2 Workshop");
        ClickThroughRaycaster(workshop.Find("Categories/Tab1").GetComponent<Button>());
        Check(FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().Mode == BistroBuilderConstructionRuntimeMode.Select, "Native Workshop edit tab calls canonical construction tool");
        panel.SelectSection(RestaurantEditCatalogSection.Surfaces);
        for (int i = 0; i < 5; i++) yield return null;
        Check(workshopTitle.text == "Taller · Superficies", "Surfaces reuse native V2 Workshop");
        Check(BistroBuilderGalleryVivaCapture.PrepareSurfaceSelection(out var surfaceError),"Select real room through Construction authority: "+surfaceError);
        for(int i=0;i<8;i++)yield return null;
        var selectionAuthority=FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>();
        Check(selectionAuthority.Current.IsValid && selectionAuthority.Current.family==BistroBuilderEditorV2ToolFamily.Surfaces,"Reproduce user surface selection");
        Check(SingleInspector(canvasRoot) && canvasRoot.Find("BB_ArchitectureInspector").gameObject.activeInHierarchy &&
            !canvasRoot.Find("BB_EditorV2_SelectionInspector").gameObject.activeInHierarchy && !itemInspector.gameObject.activeInHierarchy,
            "Selected surface has exactly one inspector; B8 and catalog cannot overlay it");
        VerifyComposition(panel,canvasRoot.Find("BB_ArchitectureInspector") as RectTransform);
        var architectureViewport=canvasRoot.Find("BB_ArchitectureInspector/Viewport") as RectTransform;
        foreach(RectTransform child in architectureViewport.Find("Content"))
            if(child.gameObject.activeInHierarchy)Check(child.rect.width<=architectureViewport.rect.width+1,"Architecture content stays within inspector: "+child.name);
        panel.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 5; i++) yield return null;
        Check(content.Cast<Transform>().All(x => !x.gameObject.activeSelf || x.name == "BB_GaleriaViva_Native"), "Returning from Workshop never revives previous gallery");
        var coordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>();
        coordinator.TryActivateTool(BistroBuilderEditorV2ToolFamily.Furniture, "select", out _);
        Check(BistroBuilderGalleryVivaCapture.SelectToolbarFixture(out var toolbarItem),"Toolbar selects a real deletable object: "+toolbarItem);
        var selectedDelete=Toolbar().Find("EditSelectionTools/Buttons/EditDelete").GetComponent<Button>();
        for(int frame=0;frame<60&&!selectedDelete.IsInteractable();frame++)yield return null;
        yield return null;
        VerifyToolbarPointerStates();
        var heldDelete=Toolbar().Find("EditSelectionTools/Buttons/EditDelete").GetComponent<Button>();
        var heldData=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(heldDelete.gameObject,heldData,ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(heldDelete.gameObject,heldData,ExecuteEvents.pointerDownHandler);
        interaction.ClearSelection();
        // Read models refresh every .35 seconds; observe the real authority's
        // availability change before testing the presentation's cancellation.
        for(int frame=0;frame<60&&heldDelete.IsInteractable();frame++)yield return null;
        yield return null;
        Check(!heldDelete.IsInteractable(),"Canonical availability updates after clearing the world selection");
        Check(heldDelete.transform.Find("Icon").GetComponent<RectTransform>().anchoredPosition.y==-3 &&
            heldDelete.transform.Find("Label").GetComponent<RectTransform>().anchoredPosition.y==-34,
            "Losing the canonical selection cancels a held press and restores its layout");
        VerifyToolbarDisabled();
        var worldItems = FindObjectsByType<RestaurantPlaceableObject>(FindObjectsSortMode.None);
        Check(worldItems.Length > 1 && interaction.TrySelectPlaceable(worldItems[0]) && interaction.TrySelectPlaceable(worldItems[1], true, false), "Real world multiselection uses B8 furniture authority");
        for (int i = 0; i < 8; i++) yield return null;
        var selectedInspector = canvasRoot.Find("BB_EditorV2_SelectionInspector");
        Check(selectedInspector.GetComponent<BistroBuilderEditChromeSurface>() != null && !itemInspector.gameObject.activeSelf, "B8 selection shows its V2 inspector and hides product inspector");
        Check(selectedInspector.Find("ReferenceDetails").gameObject.activeInHierarchy && !selectedInspector.Find("Panel").gameObject.activeSelf,
            "World selection opens approved details without extra Principal/Conjunto headers");
        Check(SingleInspector(canvasRoot),"Multiselection still has exactly one inspector");
        var chromeTop=FindFirstObjectByType<BistroBuilderUiShell>().GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeTopBarName);
        var pill=chromeTop.Find("SelectionPill") as RectTransform;
        Check(Mathf.Abs(pill.rect.width-280)<1 && Mathf.Abs(pill.rect.height-43)<1,"Selection pill retains 280x43 reference proportions");
        var view=panel.GetComponent<BistroBuilderEditorV2SelectionInspector>();view.SetViewGroup(true);
        Check(selectedInspector.Find("Panel").gameObject.activeSelf&&!selectedInspector.Find("ReferenceDetails").gameObject.activeSelf,"Selection pill switches to the actual B8 group view without stacking pages");
        view.SetViewGroup(false);
        VerifyComposition(panel,selectedInspector as RectTransform);
        var worldTransformChecks = VerifyWorldTransformsAndPersistence();
        while (worldTransformChecks.MoveNext()) yield return worldTransformChecks.Current;
    }
    private Transform Toolbar()
    {
        var shell=FindFirstObjectByType<BistroBuilderUiShell>();
        return shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeBottomBarName);
    }
    private void VerifyToolbarDisabled()
    {
        var delete=Toolbar().Find("EditSelectionTools/Buttons/EditDelete").GetComponent<Button>();
        var style=delete.GetComponent<BistroBuilderEditChromeControl>();
        var surface=delete.GetComponent<Image>();var label=delete.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
        Check(!delete.IsInteractable(),"Delete without a world selection stays unavailable");
        Check(style.IsToolbarControl && style.ToolbarState==BistroBuilderEditorV2ToolbarArt.State.Disabled && surface.color.a==1,
            "Unavailable delete uses a fully opaque disabled surface");
        var original=surface.sprite;var point=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        int invocations=0;UnityEngine.Events.UnityAction probe=()=>invocations++;delete.onClick.AddListener(probe);
        ExecuteEvents.Execute(delete.gameObject,point,ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(delete.gameObject,point,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(delete.gameObject,point,ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(delete.gameObject,point,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(delete.gameObject,point,ExecuteEvents.pointerExitHandler);
        delete.onClick.RemoveListener(probe);
        Check(invocations==0 && surface.sprite==original,"Unavailable delete ignores hover/press/click and cannot execute its action");
        var colors=BistroBuilderEditorV2ToolbarArt.Colors(true,style.ToolbarState);
        Check(Contrast(label.color,colors.Top)>=4.5f && Contrast(label.color,colors.Bottom)>=4.5f,
            "Unavailable delete has readable text at both gradient ends");
    }
    private void VerifyToolbarPointerStates()
    {
        var footer=Toolbar();var buttons=new List<Button>();
        foreach(string group in new[]{"ModeTools","EditSelectionTools","EditViewTools"})
            buttons.AddRange(footer.Find(group+"/Buttons").GetComponentsInChildren<Button>());
        Check(buttons.Count==11,"The toolbar still exposes exactly eleven native actions");
        foreach(var button in buttons) {
            var control=button.GetComponent<BistroBuilderEditChromeControl>();
            var icon=button.transform.Find("Icon").GetComponent<BistroBuilderEditChromeIcon>();
            var label=button.transform.Find("Label").GetComponent<TMPro.TMP_Text>();label.ForceMeshUpdate();
            Check(control.IsToolbarControl && button.transition==Selectable.Transition.None && button.GetComponent<Image>().color.a==1,
                "Toolbar surface owns opaque hover/press visuals: "+button.name);
            Check(Mathf.Abs(icon.rectTransform.anchoredPosition.x+icon.rectTransform.rect.width*.5f-
                ((RectTransform)button.transform).rect.width*.5f)<.01f && !label.isTextOverflowing,
                "Toolbar icon is centered and its native label fits: "+button.name);
            Check(ScreenBounds(icon.rectTransform).yMin>ScreenBounds(label.rectTransform).yMax && label.fontSize==13,
                "Toolbar icon and label retain a clear gap: "+button.name);
            var p=BistroBuilderEditorV2ToolbarArt.Colors(button.name=="EditDelete",control.ToolbarState);
            Check(Contrast(label.color,p.Top)>=4.5f && Contrast(label.color,p.Bottom)>=4.5f,
                "Toolbar label contrasts at both surface gradient ends: "+button.name);
        }
        var delete=footer.Find("EditSelectionTools/Buttons/EditDelete").GetComponent<Button>();
        Check(delete.IsInteractable(),"A canonical single selection enables Delete");
        var glyph=delete.transform.Find("Icon/ApprovedVectorGlyph").GetComponent<TMPro.TMP_Text>();glyph.ForceMeshUpdate();
        Check(glyph.font==BistroBuilderEditorV2ToolbarArt.DeleteFont && glyph.textInfo.characterCount==1 &&
            glyph.textInfo.characterInfo[0].isVisible,"Delete renders its clean native SDF bin silhouette");
        var labelDelete=delete.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
        var palette=BistroBuilderEditorV2ToolbarArt.Colors(true,delete.GetComponent<BistroBuilderEditChromeControl>().ToolbarState);
        Check(Contrast(labelDelete.color,palette.Top)>=4.5f && Contrast(labelDelete.color,palette.Bottom)>=4.5f,
            "Available Delete text contrasts with both burgundy gradient ends");
        foreach(var button in buttons.Where(x=>x.IsInteractable())) {
            var style=button.GetComponent<BistroBuilderEditChromeControl>();var surface=button.GetComponent<Image>();
            var icon=button.transform.Find("Icon") as RectTransform;var label=button.transform.Find("Label") as RectTransform;
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            // A resting baseline must begin outside the control, regardless of
            // the editor's real mouse position or a previously selected control.
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);
            var iconPosition=icon.anchoredPosition;var labelPosition=label.anchoredPosition;var rest=surface.sprite;
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerEnterHandler);var hover=surface.sprite;
            Check(hover!=rest,"Available toolbar hover changes only its surface: "+button.name);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
            Check(surface.sprite!=hover && icon.anchoredPosition==iconPosition+Vector2.down && label.anchoredPosition==labelPosition+Vector2.down,
                "Toolbar press has a controlled one-unit inset: "+button.name);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
            Check(surface.sprite==hover && icon.anchoredPosition==iconPosition && label.anchoredPosition==labelPosition,
                "Toolbar release restores its content and cached hover: "+button.name);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);
            Check(surface.sprite==rest,"Toolbar pointer exit restores its cached resting surface: "+button.name);
            data.button=PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
            Check(surface.sprite==rest && icon.anchoredPosition==iconPosition,"Right click never presses a toolbar action: "+button.name);
        }
    }
    private static float Contrast(Color a,Color b)
    {
        float Linear(float v)=>v<=.04045f?v/12.92f:Mathf.Pow((v+.055f)/1.055f,2.4f);
        float L(Color c)=>.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
        float x=L(a),y=L(b);return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);
    }
    private IEnumerator VerifyHeaderLighting()
    {
        var shell=FindFirstObjectByType<BistroBuilderUiShell>();
        var header=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeTopBarName) as RectTransform;
        var bounds=ScreenBounds(header);
        Check(Mathf.Abs(bounds.yMax-Screen.height)<.75f,"Header touches the top screen edge without a restaurant gap");
        Check(Mathf.Abs(bounds.xMin)<.75f&&Mathf.Abs(bounds.xMax-Screen.width)<.75f,"Header covers the full screen width");
        var paper=header.Find("V2Paper").GetComponent<RawImage>();
        var p=ScreenBounds(paper.rectTransform);
        Check(Mathf.Abs(p.xMin-bounds.xMin)<.75f && Mathf.Abs(p.xMax-bounds.xMax)<.75f &&
            Mathf.Abs(p.yMin-bounds.yMin)<.75f && Mathf.Abs(p.yMax-bounds.yMax)<.75f && paper.color.a==1,
            "Header has continuous opaque paper to every edge and corner");
        var edge=header.Find("HeaderOrnaments").GetComponent<Image>();
        Check(edge.depth>paper.depth,"Header ornaments render above the continuous paper");
        var logo=ScreenBounds(header.Find("OfficialLogo") as RectTransform);
        Check(logo.yMax<=bounds.yMax+.5f && logo.yMin>=bounds.yMin-.5f,"Logo remains inside the docked header without clipping");
        var day=header.Find("EditDay").GetComponent<Button>();
        var label=day.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
        var glyph=day.transform.Find("Icon/ApprovedVectorGlyph").GetComponent<TMPro.TMP_Text>();
        string originalGlyph=glyph.text;var originalFont=glyph.font;
        var labelPosition=label.rectTransform.anchoredPosition;var labelSize=label.rectTransform.sizeDelta;
        var menu=header.Find("LightingPreviewMenu");
        string[] names={"Día","Atardecer","Noche","Día"};int[] presets={0,1,2,0};
        for(int i=0;i<presets.Length;i++) {
            ClickThroughRaycaster(day);
            Check(menu.gameObject.activeInHierarchy,"Lighting menu opens for "+names[i]);
            // Activated dropdown graphics need a rendered frame to register
            // with the canvas/raycaster, including in the batch-mode pump.
            for(int frame=0;frame<3;frame++)yield return null;
            ClickThroughRaycaster(menu.Find("LightPreset"+presets[i]).GetComponent<Button>());
            yield return null;
            label.ForceMeshUpdate();glyph.ForceMeshUpdate();
            Check(label.text==names[i] && !menu.gameObject.activeSelf,"Lighting selection updates its label and closes its menu: "+names[i]);
            Check(glyph.text==originalGlyph && glyph.font==originalFont && glyph.textInfo.characterCount==1,
                "Lighting selection preserves the sun silhouette: "+names[i]);
            Check(label.fontSize==14 && !label.enableAutoSizing && label.rectTransform.anchoredPosition==labelPosition &&
                label.rectTransform.sizeDelta==labelSize,"Lighting text keeps its size and position: "+names[i]);
            Check(label.preferredWidth<=label.rectTransform.rect.width+.5f && !label.isTextOverflowing,
                "Lighting text fits before the dropdown arrow: "+names[i]+" width="+label.preferredWidth);
            var textBounds=ScreenBounds(label.rectTransform);var arrowBounds=ScreenBounds(day.transform.Find("Chevron") as RectTransform);
            Check(textBounds.xMax<arrowBounds.xMin,"Lighting label never overlaps its arrow: "+names[i]);
        }
    }
    private void VerifyCompactFooter()
    {
        var shell=FindFirstObjectByType<BistroBuilderUiShell>();
        var chrome=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeBottomBarName);
        foreach(string group in new[]{"ModeTools","EditSelectionTools","EditViewTools"}) {
            Check(chrome.Find(group+"/FineDivider")==null && chrome.Find(group+"/DividerDiamond")==null,"Only the approved outer ornaments frame the footer: "+group);
            Check(chrome.Find(group+"/Buttons").GetComponent<HorizontalLayoutGroup>()==null,"Reference button positions remain explicit: "+group);
        }
        var footer=chrome as RectTransform;
        var bounds=ScreenBounds(footer);
        Check(Mathf.Abs(bounds.yMin)<.75f,"Footer touches the bottom screen edge without a restaurant gap");
        Check(Mathf.Abs(bounds.xMin)<.75f&&Mathf.Abs(bounds.xMax-Screen.width)<.75f,"Opaque footer covers the full screen width");
        Check(bounds.height<Screen.height*.1f,"Compact footer takes less than a tenth of the screen height");
        var paper=chrome.Find("V2Paper").GetComponent<RawImage>();
        var paperBounds=ScreenBounds(paper.rectTransform);
        Check(Mathf.Abs(paperBounds.xMin-bounds.xMin)<.75f && Mathf.Abs(paperBounds.xMax-bounds.xMax)<.75f &&
            Mathf.Abs(paperBounds.yMin-bounds.yMin)<.75f && Mathf.Abs(paperBounds.yMax-bounds.yMax)<.75f && paper.color.a==1,
            "Continuous opaque paper covers the entire footer including its corners | Footer="+bounds+" Paper="+paperBounds+" Alpha="+paper.color.a);
        foreach(string group in new[]{"ModeTools","EditSelectionTools","EditViewTools"}) {
            var title=chrome.Find(group+"/Caption").GetComponent<TMPro.TMP_Text>();
            title.ForceMeshUpdate();
            var titleBounds=ScreenBounds(title.rectTransform);
            Check(title.fontSize<=16 && !title.isTextOverflowing,"Compact footer title fits its cell: "+group);
            Check(titleBounds.yMax<=paperBounds.yMax+.5f && titleBounds.yMin>=paperBounds.yMin,
                "Footer title is entirely over paper: "+group);
            var buttons=chrome.Find(group+"/Buttons").GetComponentsInChildren<Button>();
            Check(buttons.All(button=>ScreenBounds((RectTransform)button.transform).yMax<titleBounds.yMin),
                "Footer title and buttons never overlap: "+group);
        }
        var select=chrome.Find("ModeTools/Buttons/EditSelect").GetComponent<RectTransform>();
        Check(select.anchoredPosition==new Vector2(60,-22)&&select.sizeDelta==new Vector2(124,54),"Footer buttons use the approved compact box");
        foreach(var icon in chrome.GetComponentsInChildren<BistroBuilderEditChromeIcon>())
            Check(icon.UsesApprovedVector,"Footer icon uses a native SDF silhouette: "+icon.Symbol);
    }
    private static bool SingleInspector(Transform canvas)
    {
        int count=0;foreach(string id in new[]{"BB_UIUX_PlaceableInspector","BB_EditorV2_SelectionInspector","BB_ArchitectureInspector"})
        {var node=canvas.Find(id);if(node!=null&&node.gameObject.activeInHierarchy)count++;}return count==1;
    }
    private void VerifyComposition(RestaurantPlaceableCatalogPanel panel, RectTransform inspector)
    {
        var shell=FindFirstObjectByType<BistroBuilderUiShell>();
        var top=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeTopBarName) as RectTransform;
        var bottom=shell.GetComponentInParent<Canvas>().transform.Find(BistroBuilderUiShell.RootName+"/"+BistroBuilderUiShell.EditModeBottomBarName) as RectTransform;
        Check(top!=null&&bottom!=null,"Reference chrome exists");if(top==null||bottom==null)return;
        var t=ScreenBounds(top);var b=ScreenBounds(bottom);var r=ScreenBounds(inspector);
        Check(t.yMin>r.yMax && r.yMin>b.yMax,"Inspector lies strictly between the top and bottom bars");
        Check(b.yMin>=-1 && b.yMax<Screen.height && b.xMin>=-1 && b.xMax<=Screen.width+1,"All bottom tools stay inside the viewport");
        Check(bottom.Find("ModeTools").GetComponent<Image>()==null && bottom.Find("EditSelectionTools").GetComponent<Image>()==null && bottom.Find("EditViewTools").GetComponent<Image>()==null,
            "Bottom tool groups have no nested panel frames");
        foreach(string path in new[]{"EditorV2ModeIdentity/Title"})
        {var label=top.Find(path).GetComponent<TMPro.TMP_Text>();label.ForceMeshUpdate();Check(label.textInfo.characterInfo.Count(x=>x.isVisible)>4,"Top identity has rendered glyphs");}
        foreach(string path in new[]{"ModeTools/Caption","EditSelectionTools/Caption","EditViewTools/Caption"})
        {var label=bottom.Find(path).GetComponent<TMPro.TMP_Text>();label.ForceMeshUpdate();Check(label.textInfo.characterInfo.Count(x=>x.isVisible)>3,"Bottom caption renders: "+path);}
    }
    private static Rect ScreenBounds(RectTransform r)
    {
        var corners=new Vector3[4];r.GetWorldCorners(corners);
        var canvas=r.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    private void ClickThroughRaycaster(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = button.transform as RectTransform;
        var canvas = button.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Raycast " + button.name + " point=" + point + " graphicDepth=" + button.targetGraphic.depth + " culled=" + button.targetGraphic.canvasRenderer.cull + " hits=" + string.Join(",", hits.Take(4).Select(x => x.gameObject.name + "/" + x.gameObject.GetComponentInParent<Button>()?.name)));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
}
