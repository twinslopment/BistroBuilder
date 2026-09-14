using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public enum BistroBuilderSurfaceLevel { Base, Panel, Card, Floating }
public enum BistroBuilderBorderState { Normal, Hover, Selected, Attention, Critical, Disabled }
public enum BistroBuilderHudGlass { Light, Medium, Strong }

[DisallowMultipleComponent]
public sealed class BistroBuilderSurface : MonoBehaviour
{
    public BistroBuilderSurfaceLevel Level { get; private set; }
    public bool Hud { get; private set; }
    public BistroBuilderHudGlass Glass { get; private set; }
    public BistroBuilderBorderState Border { get; private set; }
    private Image background;
    private BistroBuilderDepthGraphic depth;
    private BistroBuilderInteractionSurface interaction;
    private static Sprite rounded;
    private static readonly Material[] glassMaterials = new Material[3];
    private bool initialized;
    public static BistroBuilderSurface Apply(Image image, BistroBuilderSurfaceLevel level, bool hud=false, BistroBuilderHudGlass glass=BistroBuilderHudGlass.Light)
    {
        if(image==null)return null;
        var surface=image.GetComponent<BistroBuilderSurface>()??image.gameObject.AddComponent<BistroBuilderSurface>();
        surface.Configure(level,hud,glass);return surface;
    }
    public void Configure(BistroBuilderSurfaceLevel level,bool hud=false,BistroBuilderHudGlass glass=BistroBuilderHudGlass.Light)
    {
        if(initialized&&Level==level&&Hud==hud&&Glass==glass)return;
        Level=level;Hud=hud;Glass=glass;initialized=true;
        background=GetComponent<Image>();interaction=GetComponent<BistroBuilderInteractionSurface>();
        if(depth==null)
        {
            var go=new GameObject("Surface depth",typeof(RectTransform),typeof(LayoutElement));go.transform.SetParent(transform,false);go.transform.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout=true;depth=go.AddComponent<BistroBuilderDepthGraphic>();depth.raycastTarget=false;
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(-32,-32);rect.offsetMax=new Vector2(32,32);
        }
        depth.Level=level;depth.SetVerticesDirty();
        background.sprite=Rounded();background.type=Image.Type.Sliced;
        background.material=hud?GlassMaterial(glass):null;
        if(hud)
        {
            var camera=Camera.main;if(camera==null)camera=FindFirstObjectByType<Camera>();
            if(camera!=null)camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
        }
    }
    public void SetBorder(BistroBuilderBorderState state){Border=state;RefreshBorder();}
    private void LateUpdate()=>RefreshBorder();
    private void RefreshBorder()
    {
        if(depth==null)return;var state=Border;
        if(state==BistroBuilderBorderState.Normal&&interaction!=null)
        {
            switch(interaction.State)
            {
                case BistroBuilderSurfaceState.Hover:state=BistroBuilderBorderState.Hover;break;
                case BistroBuilderSurfaceState.Selected:state=BistroBuilderBorderState.Selected;break;
                case BistroBuilderSurfaceState.Disabled:state=BistroBuilderBorderState.Disabled;break;
            }
        }
        if(depth.State==state)return;depth.State=state;depth.SetVerticesDirty();
    }
    public static float Opacity(BistroBuilderHudGlass glass)=>glass==BistroBuilderHudGlass.Light?0.70f:glass==BistroBuilderHudGlass.Medium?0.50f:0.35f;
    public static float Blur(BistroBuilderHudGlass glass)=>glass==BistroBuilderHudGlass.Light?8:glass==BistroBuilderHudGlass.Medium?16:24;
    private static Material GlassMaterial(BistroBuilderHudGlass glass)
    {
        int i=(int)glass;if(glassMaterials[i]!=null)return glassMaterials[i];
        var shader=Resources.Load<Shader>("BistroBuilder/UI/HudGlass");if(shader==null)return null;
        var material=new Material(shader){name="BB HUD "+glass};material.SetFloat("_SurfaceOpacity",Opacity(glass));material.SetFloat("_BlurRadius",Blur(glass));return glassMaterials[i]=material;
    }
    private static Sprite Rounded()
    {
        if(rounded!=null)return rounded;
        const int size=32;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="BB rounded surface",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=Mathf.Max(8.5f-x,0,x-22.5f),dy=Mathf.Max(8.5f-y,0,y-22.5f);
            float alpha=Mathf.Clamp01(8.5f-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*size+x]=new Color(1,1,1,alpha);
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return rounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(0.5f,0.5f),100,0,SpriteMeshType.FullRect,new Vector4(10,10,10,10));
    }
}
