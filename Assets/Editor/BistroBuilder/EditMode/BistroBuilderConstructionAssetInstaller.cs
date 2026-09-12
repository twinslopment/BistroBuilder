using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Reproducible native Unity assets. Re-running preserves asset GUIDs.</summary>
public static class BistroBuilderConstructionAssetInstaller
{
    private const string Root = "Assets/Resources/BistroBuilder/Construction";
    [MenuItem("Tools/Bistro Builder/Edit Mode/Create Construction Assets")]
    public static void Install()
    {
        Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/Prefabs");
        Directory.CreateDirectory(Root+"/Meshes"); Directory.CreateDirectory(Root+"/Icons"); AssetDatabase.Refresh();
        var kit = AssetDatabase.LoadAssetAtPath<BistroBuilderConstructionAssetKit>(Root+"/ConstructionAssetKit.asset");
        if(kit==null) { kit=ScriptableObject.CreateInstance<BistroBuilderConstructionAssetKit>(); AssetDatabase.CreateAsset(kit,Root+"/ConstructionAssetKit.asset"); }
        kit.wallMaterial=Material("Enlucido_calido",new Color32(217,207,185,255));
        kit.floorMaterial=Material("Suelo_caliza",new Color32(164,153,129,255));
        kit.trimMaterial=Material("Roble_marcos",new Color32(126,87,55,255));
        kit.glassMaterial=Material("Vidrio_azulado",new Color(0.48f,0.77f,0.84f,0.25f),true);
        var metal=Material("Metal_grafito",new Color32(44,57,52,255));
        var modules = new List<GameObject>();
        foreach(float length in new[]{0.5f,1f,2f,4f})
        {
            string name="Pared_"+length.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"m";
            var record=new BistroBuilderWallRecord { axisStart=Vector2.zero,axisEnd=new Vector2(length,0),height=2.8f,thickness=0.12f };
            var mesh=BistroBuilderWallGeometryBuilder.Build(record,Array.Empty<BistroBuilderOpeningRecord>());
            string meshPath=Root+"/Meshes/"+name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(old==null) AssetDatabase.CreateAsset(mesh,meshPath); else { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); mesh=old; }
            var go=new GameObject(name); go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=kit.wallMaterial; go.AddComponent<MeshCollider>().sharedMesh=mesh;
            modules.Add(Prefab(go,name));
        }
        kit.wallModules=modules.ToArray();
        var door=new GameObject("Puerta_roble_abierta");
        Frame(door.transform,0.9f,2.1f,kit.trimMaterial,false);
        var hinge=new GameObject("Bisagra"); hinge.transform.SetParent(door.transform,false);
        hinge.transform.localPosition=new Vector3(-0.45f,0,0); hinge.transform.localRotation=Quaternion.Euler(0,-90,0);
        Part(hinge.transform,"Hoja",new Vector3(0.45f,1.02f,0),new Vector3(0.86f,2.04f,0.045f),kit.trimMaterial);
        Part(hinge.transform,"Tirador",new Vector3(0.78f,1,0.045f),new Vector3(0.04f,0.14f,0.025f),metal);
        kit.doorPrefab=Prefab(door,"Puerta_roble_abierta");
        var window=new GameObject("Ventana_marco_grafito"); Frame(window.transform,1.2f,1.2f,metal,true);
        Part(window.transform,"Cristal",new Vector3(0,0.6f,0),new Vector3(1.12f,1.12f,0.012f),kit.glassMaterial);
        kit.windowPrefab=Prefab(window,"Ventana_marco_grafito");
        foreach(string icon in new[]{"select","wall","module","room","door","window","furniture"}) Icon(icon);
        EditorUtility.SetDirty(kit); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("BB_CONSTRUCTION_ASSETS_PASS | 4 wall modules, door, window, 5 materials, 7 icons");
    }
    private static Material Material(string name,Color color,bool glass=false)
    {
        string path=Root+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.color=color; material.SetFloat("_Smoothness",glass?0.85f:0.24f);
        if(glass)
        {
            material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
            material.SetOverrideTag("RenderType","Transparent");
        }
        EditorUtility.SetDirty(material); return material;
    }
    private static GameObject Prefab(GameObject go,string name)
    { var result=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+name+".prefab"); UnityEngine.Object.DestroyImmediate(go); return result; }
    private static void Frame(Transform parent,float width,float height,Material material,bool window)
    {
        Part(parent,"Jamba izquierda",new Vector3(-width/2,height/2,0),new Vector3(0.06f,height+0.06f,0.17f),material);
        Part(parent,"Jamba derecha",new Vector3(width/2,height/2,0),new Vector3(0.06f,height+0.06f,0.17f),material);
        Part(parent,"Dintel",new Vector3(0,height,0),new Vector3(width+0.06f,0.06f,0.17f),material);
        if(window)
        {
            Part(parent,"Alféizar",Vector3.zero,new Vector3(width+0.12f,0.06f,0.22f),material);
            Part(parent,"Montante",new Vector3(0,height/2,0),new Vector3(0.035f,height,0.08f),material);
        }
    }
    private static void Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=position; go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=material;
    }
    private static void Icon(string name)
    {
        // Deliberately code-native line icons; transparent PNGs are imported as Unity sprites.
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        var pixels=new Color32[64*64]; texture.SetPixels32(pixels);
        void Line(int x0,int y0,int x1,int y1)
        {
            int count=Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0));
            for(int i=0;i<=count;i++)
            {
                float t=count==0?0:(float)i/count; int x=Mathf.RoundToInt(Mathf.Lerp(x0,x1,t)),y=Mathf.RoundToInt(Mathf.Lerp(y0,y1,t));
                for(int dx=-1;dx<=1;dx++) for(int dy=-1;dy<=1;dy++) if(x+dx>=0&&x+dx<64&&y+dy>=0&&y+dy<64) texture.SetPixel(x+dx,y+dy,new Color32(244,240,231,255));
            }
        }
        void Box(int x,int y,int w,int h) { Line(x,y,x+w,y);Line(x+w,y,x+w,y+h);Line(x+w,y+h,x,y+h);Line(x,y+h,x,y); }
        switch(name)
        {
            case "select": Line(16,52,18,12);Line(16,52,46,28);Line(46,28,30,27);Line(30,27,18,12);break;
            case "wall": Box(9,16,46,32);Line(9,32,55,32);Line(32,32,32,48);Line(22,16,22,32);Line(44,16,44,32);break;
            case "module": Box(13,15,38,34);Line(23,15,23,49);Line(41,15,41,49);break;
            case "room": Box(12,12,40,40);Line(22,22,42,42);Line(22,22,22,31);Line(22,22,31,22);Line(42,42,33,42);Line(42,42,42,33);break;
            case "door": Line(13,12,13,52);Line(13,52,49,52);Line(49,52,49,12);Line(20,12,20,46);Line(20,46,39,40);Line(39,40,39,6);Line(39,6,20,12);break;
            case "window": Box(12,12,40,40);Line(32,12,32,52);Line(12,32,52,32);break;
            default: Box(10,28,44,12);Line(15,12,15,28);Line(49,12,49,28);Line(16,40,16,52);Line(48,40,48,52);Line(16,52,48,52);break;
        }
        texture.Apply(); string path=Root+"/Icons/"+name+".png";
        File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=64; importer.alphaIsTransparency=true;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false; importer.SaveAndReimport();
    }
    public static void InstallAndTestBatch()
    {
        try { Install(); BistroBuilderConstructionAuthoringSelfTest.RunInEditor(); BistroBuilderConstructionPlayerRegression.Run(); Debug.Log("BB_CONSTRUCTION_IMPLEMENTATION_PASS"); EditorApplication.Exit(0); }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
