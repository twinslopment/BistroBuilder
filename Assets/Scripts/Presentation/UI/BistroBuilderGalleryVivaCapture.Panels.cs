using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderGalleryVivaCapture
{
    private bool panelsCapture;
    private IEnumerator CapturePanels(string folder, RestaurantPlaceableCatalogPanel panel, StringBuilder report)
    {
        var gallery = panel.GetComponent<BistroBuilderGalleryViva>();
        var canvas = panel.GetComponentInParent<Canvas>().transform;
        var expanded = gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded");
        var title = expanded.Find("Title").GetComponent<TMP_Text>();
        report.AppendLine("CatalogTitle=" + title.text);
        if (title.text != "Catálogo") blackFrame = true;
        foreach (Transform child in expanded)
        {
            if (!child.name.StartsWith("Category_", StringComparison.Ordinal)) continue;
            var label = child.Find("Caption").GetComponent<TMP_Text>(); label.ForceMeshUpdate();
            report.AppendLine(child.name + "|Font=" + label.font.name + "|Size=" + label.fontSize + "|Overflow=" + label.isTextOverflowing);
            if (label.isTextOverflowing) blackFrame = true;
        }
        var view = canvas.Find("BB_UIUX_PlaceableInspector/ReferenceDetails");
        var image = view.Find("Preview/Image").GetComponent<Image>();
        report.AppendLine("PreviewPivot=" + image.rectTransform.pivot + "|Center=" + image.rectTransform.anchoredPosition);
        if (image.rectTransform.pivot != new Vector2(.5f, .5f)) blackFrame = true;
        yield return Capture(folder, "Panels_Default_4K", panel, report, 2);
        var width = view.Find("Width").GetComponent<TMP_InputField>();
        width.transform.Find("Presets").GetComponent<Button>().onClick.Invoke();
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture(folder, "Panels_Dimensions_Menu", panel, report);
        view.Find("NumericPresets/Value3").GetComponent<Button>().onClick.Invoke();
        view.Find("RotationY/Presets").GetComponent<Button>().onClick.Invoke();
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture(folder, "Panels_Rotation_Menu", panel, report);
        view.Find("NumericPresets/Value2").GetComponent<Button>().onClick.Invoke();
        for (int i = 0; i < 5; i++) yield return null;
        var interaction = FindFirstObjectByType<RestaurantEditInteractionController>();
        var item = gallery.Featured;
        report.AppendLine("ConfiguredDimensions=" + interaction.GetInspectorDimensions(item) + "|Rotation=" + interaction.GetInspectorRotation(item));
        yield return Capture(folder, "Panels_Configured", panel, report);
        if (!interaction.TryBeginPlaceableCreation(item)) blackFrame = true;
        for (int i = 0; i < 5; i++) yield return null;
        var provisional = FindFirstObjectByType<RestaurantPlaceableCreationService>().ActiveProvisionalPlaceable;
        report.AppendLine("ActualProvisionalScale=" + provisional?.transform.localScale + "|Rotation=" + provisional?.transform.eulerAngles);
        interaction.CancelActivePlacement();
        interaction.TrySetInspectorDimensions(item, null, item.DimensionsCentimeters);
        interaction.TrySetInspectorRotation(item, null, Vector3.zero);
        gallery.ToggleFavorite(item); gallery.SetTab(1);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture(folder, "Panels_Favorites", panel, report);
        gallery.Remember(item); gallery.SetTab(2);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture(folder, "Panels_Recents", panel, report);
        gallery.ResetFilters(); gallery.SelectCategory(5);
        for (int i = 0; i < 3; i++) yield return null;
        yield return Capture(folder, "Panels_Storage", panel, report);
        gallery.SelectCategory(2);
        for(int i=0;i<5;i++)yield return null;
        expanded.Find("Featured/Details").GetComponent<Button>().onClick.Invoke();
        for(int i=0;i<5;i++)yield return null;
        yield return Capture(folder,"Panels_Chairs_Top_4K",panel,report,2);
        var scroll=expanded.Find("ItemsScroll").GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition=0;
        for(int i=0;i<5;i++)yield return null;
        report.AppendLine("ChairsBottomScroll="+scroll.verticalNormalizedPosition+"|Bar="+scroll.verticalScrollbar.name);
        var trackGraphic=scroll.verticalScrollbar.GetComponent<Image>();
        var thumbGraphic=scroll.verticalScrollbar.targetGraphic as Image;
        float trackCap=trackGraphic.sprite.border.y/trackGraphic.pixelsPerUnit;
        float thumbCap=thumbGraphic.sprite.border.y/thumbGraphic.pixelsPerUnit;
        report.AppendLine("BrassRoundedEndHeight="+trackCap+"|ThumbRoundedEndHeight="+thumbCap);
        if(trackCap>12.1f || thumbCap>12.1f)blackFrame=true;
        yield return Capture(folder,"Panels_Chairs_Bottom_4K",panel,report,2);
        Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        for(int i=0;i<25;i++)yield return null;
        scroll.verticalNormalizedPosition=1;
        yield return Capture(folder,"Panels_Chairs_1280",panel,report);
        BistroBuilderEditorV2ReviewDisplay.EnterFullscreen();
        for(int i=0;i<25;i++)yield return null;
        gallery.SelectCategory(1);
        for (int i = 0; i < 3; i++) yield return null;
        expanded.Find("Featured/Details").GetComponent<Button>().onClick.Invoke();
        for(int i=0;i<5;i++)yield return null;
        yield return Capture(folder, "Panels_Final_Fullscreen", panel, report);
    }
}
