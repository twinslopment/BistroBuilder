using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    private readonly Dictionary<string,BistroBuilderApprovedTopBarHotspot> approvedTopBarHotspots = new();
    private BistroBuilderApprovedTopBarHotspot approvedOptionsHotspot;
    private BistroBuilderTopBarArtCatalog approvedArt;
    private Vector2 approvedLayoutSize;
    private float approvedLayoutScale;
    private readonly List<RectTransform> approvedCells = new();
    private readonly List<RectTransform> approvedIconHolders = new();
    private readonly List<TMP_Text> approvedLabels = new();
    private BistroBuilderTopBarArtwork approvedLogo;

    private void EnsureApprovedTopBarV3Content()
    {
        if(topNavigation==null)return;
        if(topNavigation.GetComponent<BistroBuilderTopBarSurface>()==null)topNavigation.gameObject.AddComponent<BistroBuilderTopBarSurface>();
        topNavigation.GetComponent<Image>().color=Color.clear;
        var fitter=topNavigation.GetComponent<AspectRatioFitter>();
        if(fitter!=null)fitter.enabled=false;
        foreach(string name in new[]{"ApprovedTopBarV3Background","NavigationContent","Wordmark","BrandDivider","IdentityDivider","RestaurantIdentity","ClockDivider","Calendar","Clock"})
        {var old=topNavigation.Find(name);if(old!=null)old.gameObject.SetActive(false);}
        if(topNavigation.Find("ApprovedFrame")==null){var frame=NewUi("ApprovedFrame",topNavigation).AddComponent<BistroBuilderTopBarPlate>();frame.raycastTarget=false;Stretch(frame.rectTransform);frame.transform.SetAsFirstSibling();}
        if(approvedArt==null)approvedArt=JsonUtility.FromJson<BistroBuilderTopBarArtCatalog>(Resources.Load<TextAsset>("BistroBuilder/UI/TopBar/Parts/catalog").text);
        navContent=topNavigation;
        topNavigation.SetAsLastSibling();
    }
    private void EnsureApprovedTopBarV3Buttons()
    {
        EnsureApprovedTopBarV3Content();
        if(approvedCells.Count>0){LayoutApprovedTopBar(true);return;}
        proxyButtons.Clear(); topPresenters.Clear();
        identityButton=CreateApprovedButton("ApprovedIdentity");
        identityButton.onClick.AddListener(()=>ToggleTopPopup(true));
        approvedLogo=CreateApprovedArtwork(identityButton.transform,"ApprovedLogo",10);
        restaurantHeading=EnsureApprovedHiddenText(identityButton.transform,"RestaurantNameState","Mi restaurante");
        serviceHeading=EnsureApprovedHiddenText(identityButton.transform,"ServiceState","");
        for(int i=0;i<10;i++)
        {
            string title=i<Navigation.Length?Navigation[i].Label:"Opciones";
            Button button=CreateApprovedButton("BBNav_"+Sanitize(title));
            approvedCells.Add((RectTransform)button.transform);
            var holder=(RectTransform)NewUi("ArtworkHolder",button.transform).transform;
            approvedIconHolders.Add(holder);
            var icon=CreateApprovedArtwork(holder,"ApprovedIcon",i);
            icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=icon.rectTransform.pivot=new Vector2(.5f,.5f);
            var label=NewUi("ApprovedLabel",button.transform).AddComponent<TextMeshProUGUI>();
            label.font=BistroBuilderTypography.Title??BistroBuilderTypography.Body;
            label.text=title;label.color=new Color(.18f,.13f,.08f);label.fontSize=14;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=14;
            approvedLabels.Add(label);
            var fx=button.gameObject.AddComponent<BistroBuilderApprovedTopBarHotspot>();fx.Configure(button,icon.rectTransform,approvedArt.entries[i]);
            if(i<Navigation.Length){proxyButtons[title]=button;approvedTopBarHotspots[title]=fx;}
            else {optionsButton=button;approvedOptionsHotspot=fx;button.onClick.AddListener(()=>{
                if(topPopup!=null)topPopup.gameObject.SetActive(false);
                (GetComponent<BistroBuilderOptionsScreen>()??gameObject.AddComponent<BistroBuilderOptionsScreen>()).Toggle();RefreshIconNavigation();});}
            if(i>0){var divider=HeaderImage(button.transform,"ApprovedDivider");divider.color=new Color(.49f,.34f,.19f,.19f);divider.preserveAspect=false;
                var r=divider.rectTransform;r.anchorMin=new Vector2(0,.12f);r.anchorMax=new Vector2(0,.88f);r.sizeDelta=new Vector2(1,0);r.anchoredPosition=Vector2.zero;}
        }
        calendarHeading=EnsureApprovedHiddenText(topNavigation,"ApprovedCalendarState","");
        timeHeading=EnsureApprovedHiddenText(topNavigation,"ApprovedClockState","");
        SuppressLegacyTopBarArtifacts();EnsureTopPopup();
        if(GetComponent<BistroBuilderOptionsScreen>()==null)gameObject.AddComponent<BistroBuilderOptionsScreen>();
        LayoutApprovedTopBar(true);RefreshIconNavigation();
    }
    private Button CreateApprovedButton(string name)
    {
        var go=NewUi(name,topNavigation);var hit=go.AddComponent<Image>();hit.color=Color.clear;
        var button=go.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
        return button;
    }
    private BistroBuilderTopBarArtwork CreateApprovedArtwork(Transform parent,string name,int index)
    {
        var art=NewUi(name,parent).AddComponent<BistroBuilderTopBarArtwork>();art.Configure(approvedArt.entries[index]);return art;
    }
    private void LayoutApprovedTopBar(bool force=false)
    {
        if(topNavigation==null||approvedCells.Count!=10||shellRoot==null)return;
        Vector2 viewport=shellRoot.rect.size;float scale=canvas!=null?Mathf.Max(.01f,canvas.scaleFactor):1;
        if(!force&&viewport==approvedLayoutSize&&Mathf.Abs(scale-approvedLayoutScale)<.001f)return;
        approvedLayoutSize=viewport;approvedLayoutScale=scale;
        float physicalHeight=viewport.y*scale;
        float h=Mathf.Clamp(physicalHeight*.089f,76,144)/scale;
        float margin=Mathf.Clamp(viewport.x*.012f,8,24);
        topNavigation.anchorMin=new Vector2(0,1);topNavigation.anchorMax=new Vector2(1,1);topNavigation.pivot=new Vector2(.5f,1);
        topNavigation.anchoredPosition=new Vector2(0,-8/scale);topNavigation.sizeDelta=new Vector2(-margin*2,h);
        float width=viewport.x-margin*2;
        // A compact brand at narrow widths leaves all ten destinations reachable.
        float brand=Mathf.Min(h*1.98f,width*.18f);
        float cell=(width-brand-20)/10;
        PlaceHeader((RectTransform)identityButton.transform,5,0,brand,h);
        float logoH=Mathf.Min(h-4,(brand-8)/approvedLogo.Aspect);
        PlaceHeader(approvedLogo.rectTransform,(brand-logoH*approvedLogo.Aspect)/2,(h-logoH)/2,logoH*approvedLogo.Aspect,logoH);
        for(int i=0;i<10;i++)
        {
            PlaceHeader(approvedCells[i],brand+8+i*cell,5,cell,h-10);
            var art=approvedIconHolders[i].GetComponentInChildren<BistroBuilderTopBarArtwork>();
            float iconH=Mathf.Min(h*.51f,(cell-16/scale)/art.Aspect);
            float iconW=iconH*art.Aspect;
            PlaceHeader(approvedIconHolders[i],(cell-iconW)/2,Mathf.Max(4,(h-35/scale-iconH)/2),iconW,iconH);
            art.rectTransform.sizeDelta=new Vector2(iconW,iconH);
            PlaceHeader(approvedLabels[i].rectTransform,3,h-32/scale,cell-6,22/scale);
            approvedLabels[i].fontSizeMax=Mathf.Clamp(physicalHeight/78,11,20)/scale;
            approvedLabels[i].fontSizeMin=10/scale;
        }
        if(activityPanel!=null)activityPanel.anchoredPosition=new Vector2(activityPanel.anchoredPosition.x,-h-20/scale);
    }
    private static TMP_Text EnsureApprovedHiddenText(Transform parent,string name,string initial)
    {
        var go=NewUi(name,parent);var text=go.AddComponent<TextMeshProUGUI>();text.text=initial;text.raycastTarget=false;go.SetActive(false);return text;
    }
    private void RefreshApprovedTopBarV3State()
    {
        foreach(var pair in approvedTopBarHotspots){pair.Value.SetSelected(pair.Key==selectedNavigation);if(proxyButtons.TryGetValue(pair.Key,out var b))pair.Value.SetInteractable(b!=null&&b.interactable);}
        if(approvedOptionsHotspot!=null)approvedOptionsHotspot.SetSelected(GetComponent<BistroBuilderOptionsScreen>()?.IsOpen==true);
    }
}
