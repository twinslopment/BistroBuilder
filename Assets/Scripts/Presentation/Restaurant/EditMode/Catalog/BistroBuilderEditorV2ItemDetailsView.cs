using System;
using System.Collections.Generic;
using BistroBuilder.FurnitureFinishes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared native details composition, measured from the approved image.
/// This view only presents published data and dispatches the supplied actions.</summary>
public sealed partial class BistroBuilderEditorV2ItemDetailsView
{
    readonly BistroBuilderGalleryViva gallery;
    readonly RectTransform root, materials, variants;
    readonly TMP_Text title, description;
    readonly TMP_InputField width, depth, height, rx, ry, rz;
    readonly Image preview;
    readonly BistroBuilderGalleryVivaSymbol star;
    readonly List<GameObject> rows = new List<GameObject>();
    RestaurantPlaceableItemDefinition definition;
    Action<string> chooseVariant;
    Action close;
    string variantId;
    RectTransform zoom;
    public RectTransform Root => root;

    public BistroBuilderEditorV2ItemDetailsView(RectTransform parent, BistroBuilderGalleryViva source, Action onClose)
    {
        gallery = source; close = onClose;
        root = BistroBuilderGalleryViva.Node("ReferenceDetails", parent, 0, 0, 318, 730);
        title = Label("Title", "Artículo", 24, 14, 242, 34, 24, true);
        title.enableAutoSizing = true; title.fontSizeMin = 16; title.fontSizeMax = 24;
        var favorite = gallery.SymbolButton("Favorite", root, 271, 13, 30, 30,
            BistroBuilderGalleryVivaSymbol.Shape.Star, () => { gallery.ToggleFavorite(definition); RefreshFavorite(); }, false);
        star = favorite.GetComponentInChildren<BistroBuilderGalleryVivaSymbol>();
        var picture = BistroBuilderGalleryViva.Surface("Preview", root, 16, 50, 286, 172);
        BistroBuilderEditorV2BrassArt.DecoratePreviewFrame(picture);
        preview = BistroBuilderGalleryViva.Picture("Image", picture.transform, 10, 8, 266, 156, null);
        CenterPicture(preview);
        gallery.SymbolButton("Zoom", picture.transform, 246, 134, 33, 32,
            BistroBuilderGalleryVivaSymbol.Shape.Zoom, ToggleZoom);
        description = Label("Description", "", 23, 229, 273, 39, 13);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        Section("Dimensiones", 298);
        width = DimensionField("Width", "Ancho", 326, 0);
        depth = DimensionField("Depth", "Profundidad", 357, 2);
        height = DimensionField("Height", "Altura", 388, 1);
        transformStatus = gallery.Label("TransformStatus", root, "", 23, 271, 273, 24, 11, false, BistroBuilderGalleryVivaArt.Muted);
        transformStatus.textWrappingMode = TextWrappingModes.Normal;
        ratio = gallery.TextButton("DimensionRatio", root, "", 264, 334, 33, 67, () => { ratioLocked = !ratioLocked; RefreshTransformValues(); }, 12);
        BistroBuilderGalleryViva.Symbol("Link",ratio.transform,5,21,23,26,BistroBuilderGalleryVivaSymbol.Shape.Link,new Color32(76,51,32,255));
        Section("Rotación", 428);
        rx = RotationField("X", 27, 0);
        ry = RotationField("Y", 125, 1);
        rz = RotationField("Z", 223, 2);
        Section("Materiales", 500);
        materials = BistroBuilderGalleryViva.Node("Materials", root, 23, 527, 276, 80);
        Section("Variantes", 615);
        variants = BistroBuilderGalleryViva.Node("Variants", root, 23, 644, 276, 72);
    }

    public void Bind(RestaurantPlaceableItemDefinition item, Vector3 rotation,
        Action<Vector3> dimensionAction = null, Action<Vector3> rotationAction = null, Action<string> variantAction = null,
        Func<Vector3> dimensionsReader = null, Func<Vector3> rotationReader = null, Func<string> messageReader = null)
    {
        CloseNumericMenu(); definition = item; resize = dimensionAction; orient = rotationAction; chooseVariant = variantAction;
        readDimensions = dimensionsReader; readRotation = rotationReader; readMessage = messageReader;
        displayedDimensions = new Vector3(float.MinValue, 0, 0); displayedRotation = rotation;
        title.text = item != null ? item.DisplayName : "Artículo seleccionado";
        description.text = item != null ? item.Description : "";
        preview.sprite = item?.InspectorPreview; preview.enabled = preview.sprite != null;
        Vector3 dims = item != null ? item.DimensionsCentimeters : Vector3.zero;
        RefreshTransformValues();
        variantId = item?.FinishProfile?.DefaultVariantId;
        RebuildFinishes(); RefreshFavorite();
    }

    public void RefreshFavorite()
    {
        star.Filled = gallery.IsFavorite(definition); star.SetVerticesDirty();
    }

    void Section(string name, float y)
    {
        Label("Section_" + name, name, 26, y, 242, 26, 18, true);
        BistroBuilderGalleryViva.Symbol("Ornament_" + name, root, 13, y + 9, 6, 9,
            BistroBuilderGalleryVivaSymbol.Shape.Spark, new Color32(197,148,65,255));
        float start=name=="Dimensiones"?136:name=="Rotación"?111:name=="Materiales"?122:114;
        var line = BistroBuilderGalleryViva.Node("Rule_" + name, root, start, y+13, 300-start, .65f).gameObject.AddComponent<Image>();
        line.color = new Color32(210,177,126,130); line.raycastTarget = false;
        BistroBuilderGalleryViva.Symbol("CenterEngraving_"+name,root,155,y+5,10,16,
            BistroBuilderGalleryVivaSymbol.Shape.Spark,new Color32(197,148,65,255));
        BistroBuilderGalleryViva.Symbol("EndEngraving_"+name,root,299,y+10,6,7,
            BistroBuilderGalleryVivaSymbol.Shape.Spark,new Color32(197,148,65,255));
    }

    void RebuildFinishes()
    {
        foreach(var row in rows) if(row != null) UnityEngine.Object.Destroy(row);
        rows.Clear();
        var profile = definition?.FinishProfile;
        var selected = profile?.FindVariant(variantId);
        int count = selected?.Bindings?.Count ?? 0;
        for(int i=0;i<Mathf.Min(count,2);i++)
        {
            var binding = selected.Bindings[i]; var finish = binding?.Finish;
            var button = gallery.TextButton("Material_"+i,materials,"",0,i*40,276,35,
                () => CycleVariant(),13);
            button.interactable = chooseVariant != null && profile.Variants.Count > 1;
            var caption = button.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.text = finish != null ? finish.DisplayName : profile.FindZone(binding.ZoneId)?.DisplayName;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            BistroBuilderGalleryViva.Place(caption.rectTransform,64,0,181,35);
            var swatch = BistroBuilderGalleryViva.Node("Swatch",button.transform,11,3,36,29).gameObject.AddComponent<RawImage>();
            var mat = finish != null ? finish.Material : null;
            swatch.texture = mat != null ? mat.mainTexture : null;
            swatch.color = MaterialColor(mat); swatch.raycastTarget = false;
            BistroBuilderGalleryViva.Symbol("Arrow",button.transform,257,14,9,7,BistroBuilderGalleryVivaSymbol.Shape.Down);
            rows.Add(button.gameObject);
        }
        if(count == 0)
        {
            var original=gallery.TextButton("OriginalMaterial",materials,"",0,0,276,35,null,13);
            original.interactable=false;
            var colors=original.colors;colors.disabledColor=Color.white;original.colors=colors;
            var caption=original.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.text="Acabado original";caption.alignment=TextAlignmentOptions.MidlineLeft;
            BistroBuilderGalleryViva.Place(caption.rectTransform,64,0,181,35);
            var renderer=definition?.Prefab?.GetComponentInChildren<Renderer>(true);
            var mat=renderer!=null?renderer.sharedMaterial:null;
            var swatch=BistroBuilderGalleryViva.Node("Swatch",original.transform,11,3,36,29).gameObject.AddComponent<RawImage>();
            swatch.texture=mat!=null?mat.mainTexture:null;swatch.color=MaterialColor(mat);swatch.raycastTarget=false;
            BistroBuilderGalleryViva.Symbol("Arrow",original.transform,257,14,9,7,BistroBuilderGalleryVivaSymbol.Shape.Down);
            rows.Add(original.gameObject);
        }
        int variantsCount = profile?.Variants?.Count ?? 0;
        for(int i=0;i<Mathf.Min(variantsCount,4);i++)
        {
            var variant = profile.Variants[i]; if(variant==null)continue;
            string id = variant.Id;
            var button = gallery.TextButton("Variant_"+id,variants,"",i*70,0,65,68,()=>SelectVariant(id),12,true,id==variantId);
            button.interactable = chooseVariant != null;
            button.GetComponent<Image>().sprite=BistroBuilderGalleryVivaArt.CardSurface(id==variantId);
            if(variant.Thumbnail != null)
            {
                var image = BistroBuilderGalleryViva.Node("Thumbnail",button.transform,5,5,55,58).gameObject.AddComponent<RawImage>();
                image.texture=variant.Thumbnail; image.raycastTarget=false;
            }
            else
            {
                BistroBuilderGalleryViva.Picture("Thumbnail",button.transform,5,4,55,45,definition.CatalogIcon);
                var name = gallery.Label("Name",button.transform,variant.DisplayName,3,46,59,19,9);
                name.enableAutoSizing=true;name.fontSizeMin=7;name.fontSizeMax=9;
            }
            rows.Add(button.gameObject);
        }
        if(variantsCount==0)
        {
            var card=BistroBuilderGalleryViva.Surface("CurrentVariant",variants,0,0,65,68,true);
            card.sprite=BistroBuilderGalleryVivaArt.CardSurface(true);
            BistroBuilderGalleryViva.Picture("Thumbnail",card.transform,5,5,55,58,definition?.CatalogIcon);
            rows.Add(card.gameObject);
        }
    }

    void SelectVariant(string id) { if(chooseVariant==null)return; chooseVariant(id); variantId=id; RebuildFinishes(); }
    void CycleVariant()
    {
        var profile=definition?.FinishProfile; if(profile==null||profile.Variants.Count==0)return;
        int index=0; for(int i=0;i<profile.Variants.Count;i++) if(profile.Variants[i].Id==variantId)index=i;
        SelectVariant(profile.Variants[(index+1)%profile.Variants.Count].Id);
    }

    void ToggleZoom()
    {
        if(zoom!=null) { zoom.gameObject.SetActive(!zoom.gameObject.activeSelf);return; }
        zoom = BistroBuilderGalleryViva.Surface("PreviewZoom",root,-310,70,600,440).rectTransform;
        BistroBuilderEditorV2BrassArt.Decorate(zoom.GetComponent<Image>(),BistroBuilderEditorV2BrassArt.Style.Frame);
        CenterPicture(BistroBuilderGalleryViva.Picture("LargeImage",zoom,20,20,560,400,definition?.InspectorPreview));
        gallery.SymbolButton("CloseZoom",zoom,556,12,30,30,BistroBuilderGalleryVivaSymbol.Shape.Close,()=>zoom.gameObject.SetActive(false));
    }

    TMP_Text Label(string id,string value,float x,float y,float w,float h,float size,bool heading=false) =>
        gallery.Label(id,root,value,x,y,w,h,size,heading,new Color32(34,29,22,255));
    static string Measure(float v)=>v>0?v.ToString("0.#")+" cm":"—";
    static string Angle(float v)=>Mathf.RoundToInt(Mathf.Repeat(v,360))+"°";
    static Color MaterialColor(Material mat)=>mat==null?new Color32(181,159,127,255):mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.HasProperty("_Color")?mat.color:Color.white;
}
