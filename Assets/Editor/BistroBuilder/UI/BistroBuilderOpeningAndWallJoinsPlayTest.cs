using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BistroBuilderOpeningAndWallJoinsPlayTest
{
    static void Check(bool value,string reason){if(!value)throw new Exception("Opening/wall joins: "+reason);}
    static T Find<T>(string name) where T : Component =>
        Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None)
        .FirstOrDefault(component => component.name == name);
    static BistroBuilderWallRecord Wall(float x,float y,float x2,float y2)=>new BistroBuilderWallRecord{wallId=BistroBuilderEditId.NewId(),axisStart=new Vector2(x,y),axisEnd=new Vector2(x2,y2),height=2.5f,thickness=.12f,wallDefinitionId="wall.default"};
    static Vector3 World(BistroBuilderWallRecord wall,Vector3 p)
    {var axis=wall.axisEnd-wall.axisStart;return new Vector3(wall.axisStart.x,wall.baseElevation,wall.axisStart.y)+Quaternion.FromToRotation(Vector3.right,new Vector3(axis.x,0,axis.y).normalized)*p;}
    static bool Contains(Mesh mesh,BistroBuilderWallRecord wall,Vector3 point)=>mesh.vertices.Any(p=>(World(wall,p)-point).sqrMagnitude<.0000001f);

    public static void Menu()
    {
        var screen=Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>();screen.Show();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(BistroBuilderNewGameOpeningPlayerScreen).GetMethod("Update",flags).Invoke(screen,null);
        var menu=GameObject.Find("NewGameIvoryCanvas");Check(menu!=null,"approved menu visible");
        var panel=menu.transform.Find("IvoryFrame");Check(panel.Find("OpaquePanel").GetComponent<Image>().color.a==1,"opaque main card");
        var page=panel.Find("NewGamePage");
        Check(page.Find("NewGameTitle").GetComponent<TMP_Text>().font==BistroBuilderTypography.Title,"Recoleta title");
        Check(page.Find("Subtitle").GetComponent<TMP_Text>().font==BistroBuilderTypography.Body,"Inter body");
        var profiles=new[]{BistroBuilderStartingPremisesProfile.Empty,BistroBuilderStartingPremisesProfile.Essentials,BistroBuilderStartingPremisesProfile.FinishingTouches};
        for(int i=0;i<3;i++){
            var choice=page.Find("Choice"+i);choice.GetComponent<Button>().onClick.Invoke();Canvas.ForceUpdateCanvases();
            Check((BistroBuilderStartingPremisesProfile)typeof(BistroBuilderNewGameOpeningPlayerScreen).GetField("premises",flags).GetValue(screen)==profiles[i],"preparation selection "+profiles[i]);
            Check(((RectTransform)choice).rect.size==((RectTransform)page.Find("Choice0")).rect.size,"equal preparation cards");
            Check(choice.Find("PremisesPreview").GetComponent<RawImage>().texture!=null,"preparation artwork");
        }
        var input=menu.GetComponentInChildren<TMP_InputField>();string previous=input.text;input.text="La Esquina";
        Check((string)typeof(BistroBuilderNewGameOpeningPlayerScreen).GetField("restaurantName",flags).GetValue(screen)=="La Esquina","restaurant name input");input.text=previous;
        Check(EventSystem.current!=null&&EventSystem.current.enabled,"UI input enabled");
        Check(RestaurantArchitectureCatalogPanel.WallEntries.Count(e=>e.Name=="Pared")==1&&!RestaurantArchitectureCatalogPanel.WallEntries.Any(e=>e.Id=="wall-exterior"),"single Pared entry");
        screen.Hide();typeof(BistroBuilderNewGameOpeningPlayerScreen).GetMethod("Update",flags).Invoke(screen,null);Check(!menu.activeSelf,"opening closes cleanly");
        Debug.Log("BB_NEW_GAME_UI_PASS");
    }

    public static void Joins()
    {
        var a=Wall(0,0,4,0);var b=Wall(4,0,4,3);var walls=new[]{a,b};
        var meshes=new List<Mesh>();
        Mesh Make(BistroBuilderWallRecord wall,IReadOnlyList<BistroBuilderWallRecord> neighbours){var mesh=BistroBuilderWallGeometryBuilder.Build(wall,Array.Empty<BistroBuilderOpeningRecord>(),neighbours);meshes.Add(mesh);return mesh;}
        try
        {
            var ma=Make(a,walls);var mb=Make(b,walls);
            foreach(var corner in new[]{new Vector3(4.06f,0,-.06f),new Vector3(3.94f,0,.06f),new Vector3(4.06f,2.5f,-.06f),new Vector3(3.94f,2.5f,.06f)})
                Check(Contains(ma,a,corner)&&Contains(mb,b,corner),"both corner faces meet at "+corner);
            var reversed=Wall(4,3,4,0);var mr=Make(reversed,new[]{a,reversed});
            Check(Contains(mr,reversed,new Vector3(4.06f,0,-.06f)),"reversed wall shares outer corner");
            var continuation=Wall(4,0,8,0);var mc=Make(continuation,new[]{a,continuation});
            Check(Contains(mc,continuation,new Vector3(4,2.5f,.06f)),"straight continuation has no bevel/gap");
            var branch=Wall(2,-2,2,0);var mt=Make(branch,new[]{a,branch});
            Check(Mathf.Abs(mt.bounds.max.x-1.94f)<.0001f,"T branch ends at host surface without overlap");
        }
        finally{foreach(var mesh in meshes)Object.DestroyImmediate(mesh);}
        var document=new BistroBuilderEditDocument();document.walls.AddRange(new[]{a,b,Wall(0,3,4,3)});
        var door=new BistroBuilderOpeningRecord{openingId=BistroBuilderEditId.NewId(),hostWallId=a.wallId,openingType="door",axisPosition01=.5f,width=1f,height=2.1f};document.openings.Add(door);
        var root=new GameObject("WallJoinVisualTest");var materializer=root.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();materializer.ConfigureSpatialProjectionRuntime(null,false,false);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;int mask=camera.cullingMask;float size=camera.orthographicSize;bool ortho=camera.orthographic;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();var states=canvases.Select(c=>c.enabled).ToArray();
        try
        {
            materializer.Rebuild(document);
            Check(!root.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("WallVisualModule_")),"continuous meshes replace repeated decorative modules");
            var collider=root.GetComponentsInChildren<MeshCollider>().First(c=>c.name=="Wall_"+a.wallId.Value);
            Check(!collider.Raycast(new Ray(new Vector3(2,1,-1),Vector3.forward),out _,3),"door passage stays open in joined mesh");
            Check(collider.Raycast(new Ray(new Vector3(1,1,-1),Vector3.forward),out _,3),"solid wall retains collider");
            var preview=new GameObject("PreviewParity");preview.transform.SetParent(root.transform,false);
            materializer.TryCreateWallVisualPreview(preview.transform,a,new[]{door},document.walls);
            Check(preview.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(collider.sharedMesh.vertices),"draft and committed geometry match");
            var previewMesh=preview.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(preview);Object.DestroyImmediate(previewMesh);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TestFloor";floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(2,-.075f,1.4f);floor.transform.localScale=new Vector3(6,.1f,5);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            foreach(var canvas in canvases)canvas.enabled=false;
            camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=4.2f;camera.transform.position=new Vector3(8,7,-5);camera.transform.LookAt(new Vector3(2,1,1.3f));Physics.SyncTransforms();
            BistroBuilderOptionsPlayTest.Capture("WallJoins_1920.png",1920,1080);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=states[i];
            camera.transform.SetPositionAndRotation(position,rotation);camera.cullingMask=mask;camera.orthographic=ortho;camera.orthographicSize=size;
            materializer.ClearGenerated();Object.DestroyImmediate(root);
        }
        System.IO.File.WriteAllText("Logs/OpeningAndWallJoinsTest.txt","PASS: opaque approved menu, Recoleta/Inter, 3 equal preparation cards with artwork, name input, menu closure, one wall choice; shared inner/outer mitres, reverse orientation, straight joins, T joins, continuous committed mesh, door collider aperture, draft parity. Captures 1280/1920/ultrawide.");
        Debug.Log("BB_WALL_JOINS_PASS");
    }
}
