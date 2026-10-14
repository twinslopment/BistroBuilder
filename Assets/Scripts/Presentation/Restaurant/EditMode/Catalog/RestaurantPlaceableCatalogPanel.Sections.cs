using System;
using UnityEngine;

public sealed partial class RestaurantPlaceableCatalogPanel
{
    public RestaurantEditCatalogSection CurrentSection { get; private set; }
    public event Action<RestaurantEditCatalogSection> SectionChanged;
    public string SectionTitle => RestaurantEditCatalogSections.Title(CurrentSection);
    public string SectionSearchHint => RestaurantEditCatalogSections.SearchHint(CurrentSection);

    public void SelectSection(RestaurantEditCatalogSection section)
    {
        CacheDependenciesIfNeeded();
        if(interactionController!=null&&interactionController.HasActivePlacement)
            interactionController.CancelActivePlacement();
        interactionController?.ClearSelection();
        GetComponent<RestaurantPlaceableInspectorPanel>()?.Hide();
        CurrentSection=section;
        selectedCategoryCode=AllCategoriesCode;
        var tool=FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        tool?.CancelCurrentGesture();
        tool?.SetMode(RestaurantEditCatalogSections.IsArchitecture(section)?BistroBuilderConstructionRuntimeMode.Select:BistroBuilderConstructionRuntimeMode.Furniture);
        if(titleText!=null)titleText.text=SectionTitle;
        RebuildCatalogPresentation();
        GetComponent<RestaurantPlaceableCatalogApprovedSkin>()?.OpenSection();
        if (GetComponent<BistroBuilderGalleryViva>() == null)
            GetComponent<RestaurantPlaceableCatalogPreviewSkin>()?.RefreshSectionChrome();
        SectionChanged?.Invoke(section);
        // The catalog changes more than its own UI: B8's selection source
        // must match the active specialist, otherwise selecting a valid
        // furniture piece is cleared on the next LateUpdate.
        SyncEditorV2Authority(section);
        RefreshVisibility();
    }

    public void EnsureEditorV2DefaultAuthority()
    {
        var coordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
            FindObjectsInactive.Include);
        if (coordinator != null &&
            coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.None)
            SyncEditorV2Authority(CurrentSection);
    }

    private static void SyncEditorV2Authority(RestaurantEditCatalogSection section)
    {
        var service = FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        if (service == null || !service.IsEditModeActive)
            return;
        var coordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
            FindObjectsInactive.Include);
        if (coordinator == null)
            return;
        var family = RestaurantEditCatalogSections.IsArchitecture(section)
            ? section == RestaurantEditCatalogSection.Surfaces
                ? BistroBuilderEditorV2ToolFamily.Surfaces
                : BistroBuilderEditorV2ToolFamily.Construction
            : BistroBuilderEditorV2ToolFamily.Furniture;
        string toolId = family == BistroBuilderEditorV2ToolFamily.Furniture
            ? "furniture"
            : family == BistroBuilderEditorV2ToolFamily.Surfaces
                ? "floor" : "select";
        if (!coordinator.TryActivateTool(family, toolId, out string error))
            Debug.LogWarning("[EditorV2] No se pudo activar la autoridad " +
                family + ": " + error);
    }
    void CreateSectionCategories()
    {
        string[] tabs=RestaurantEditCatalogSections.Tabs(CurrentSection);
        for(int i=0;i<tabs.Length;i++)CreateCategoryView(i==0?AllCategoriesCode:1000+i,tabs[i]);
    }
}
