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
        GetComponent<RestaurantPlaceableCatalogPreviewSkin>()?.RefreshSectionChrome();
        SectionChanged?.Invoke(section);
        RefreshVisibility();
    }
    void CreateSectionCategories()
    {
        string[] tabs=RestaurantEditCatalogSections.Tabs(CurrentSection);
        for(int i=0;i<tabs.Length;i++)CreateCategoryView(i==0?AllCategoriesCode:1000+i,tabs[i]);
    }
}
