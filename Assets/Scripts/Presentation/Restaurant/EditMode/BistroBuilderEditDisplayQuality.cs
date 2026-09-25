using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Sharper geometry during editing; restores the camera's previous settings on exit.</summary>
public sealed class BistroBuilderEditDisplayQuality : MonoBehaviour
{
    RestaurantEditModeService service;
    UniversalAdditionalCameraData data;
    AntialiasingMode previousMode;
    AntialiasingQuality previousQuality;
    bool applied;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var mode=FindFirstObjectByType<RestaurantEditModeService>();
        if(mode!=null&&mode.GetComponent<BistroBuilderEditDisplayQuality>()==null)mode.gameObject.AddComponent<BistroBuilderEditDisplayQuality>();
    }
    void OnEnable()
    {
        service=GetComponent<RestaurantEditModeService>();
        if(service==null)return;
        service.EditModeEntered+=Apply;service.EditModeExited+=Restore;
        if(service.IsEditModeActive)Apply();
    }
    void OnDisable()
    {
        if(service!=null){service.EditModeEntered-=Apply;service.EditModeExited-=Restore;}
        Restore();
    }
    void Apply()
    {
        if(applied||Camera.main==null)return;
        data=Camera.main.GetUniversalAdditionalCameraData();
        previousMode=data.antialiasing;previousQuality=data.antialiasingQuality;
        data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality=AntialiasingQuality.High;applied=true;
    }
    void Restore()
    {
        if(applied&&data!=null){data.antialiasing=previousMode;data.antialiasingQuality=previousQuality;}
        applied=false;
    }
}
