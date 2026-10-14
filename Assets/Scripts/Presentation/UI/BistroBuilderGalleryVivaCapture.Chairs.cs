using System;
using System.Collections;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderGalleryVivaCapture
{
    private bool chairsCapture;
    private IEnumerator CaptureChairPhotos(string folder, RestaurantPlaceableCatalogPanel panel, StringBuilder report)
    {
        var gallery=panel.GetComponent<BistroBuilderGalleryViva>();
        var expanded=gallery.transform.Find("CatalogContent/BB_GaleriaViva_Native/Expanded");
        var scroll=expanded.Find("ItemsScroll").GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition=1;
        for(int i=0;i<5;i++)yield return null;
        foreach(var card in gallery.ItemContainer.GetComponentsInChildren<BistroBuilderGalleryVivaCard>().Where(x=>x.gameObject.activeSelf))
        {
            var image=card.transform.Find("ItemImage").GetComponent<Image>();
            report.AppendLine(card.Definition.ItemId+"|Photo="+image.sprite.texture.width+"x"+image.sprite.texture.height+"|Pivot="+image.rectTransform.pivot);
            if(image.sprite.texture.width!=1024 || image.sprite.texture.height!=1024 || image.rectTransform.pivot!=new Vector2(.5f,.5f))blackFrame=true;
        }
        yield return Capture(folder,"Chairs_Studio_Top_4K",panel,report,2);
        scroll.verticalNormalizedPosition=0;
        for(int i=0;i<4;i++)yield return null;
        yield return Capture(folder,"Chairs_Studio_Bottom_4K",panel,report,2);
        scroll.verticalNormalizedPosition=1;
        var inspector=panel.GetComponent<RestaurantPlaceableInspectorPanel>();
        foreach(var item in panel.GalleryResults.ToArray())
        {
            inspector.ShowForDefinition(item);
            for(int i=0;i<4;i++)yield return null;
            var view=panel.GetComponentInParent<Canvas>().transform.Find("BB_UIUX_PlaceableInspector/ReferenceDetails");
            var preview=view.Find("Preview/Image").GetComponent<Image>();
            bool ownPhoto=preview.sprite==item.InspectorPreview;
            report.AppendLine("Detail="+item.ItemId+"|OwnPhoto="+ownPhoto+"|Pixels="+
                preview.sprite.texture.width+"x"+preview.sprite.texture.height+"|Pivot="+preview.rectTransform.pivot);
            if(!ownPhoto || preview.sprite.texture.width!=1024 || preview.rectTransform.pivot!=new Vector2(.5f,.5f))blackFrame=true;
            yield return Capture(folder,"Chair_Detail_"+item.ItemId,panel,report);
        }
        Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        for(int i=0;i<25;i++)yield return null;
        yield return Capture(folder,"Chairs_Studio_1280",panel,report);
        BistroBuilderEditorV2ReviewDisplay.EnterFullscreen();
        for(int i=0;i<25;i++)yield return null;
        expanded.Find("Featured/Details").GetComponent<Button>().onClick.Invoke();
        for(int i=0;i<4;i++)yield return null;
        yield return Capture(folder,"Chairs_Studio_Final_Fullscreen",panel,report);
    }
}
