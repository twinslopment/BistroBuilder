using System;
using BistroBuilder.ConstructionAuthoring;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderConstructionPlayerRegression
{
    public static void Run()
    {
        var table=Resources.Load<BistroBuilderEditFinanceTariffTable>("BistroBuilder/Finance/BB_EditMode_PlaytestTariffs");
        var catalog=ConstructionCatalogAdapter.FromTariffs(table);
        var session=new BistroBuilderEditSession(new BistroBuilderEditDocument());
        var query=new ArchitectureQueryCache(); query.Refresh(session);
        var template=new BistroBuilderWallRecord { wallId=BistroBuilderEditId.NewId(),axisEnd=Vector2.right };
        var gesture=new ConstructionGesture();
        Check(gesture.BeginRectangle(session,query,Vector2.zero,template,catalog,"zone.dining",1,out _),"start room");
        Check(gesture.Update(new Vector2(6,4)) && gesture.Confirm(session.TryExecute,out _),"first room");
        string first=session.Draft.ComputeFingerprint();
        query.Refresh(session); gesture=new ConstructionGesture();
        Check(gesture.BeginRectangle(session,query,new Vector2(6,0),template,catalog,"zone.kitchen",1,out _),"start adjacent room");
        Check(gesture.Update(new Vector2(9,3)) && gesture.Confirm(session.TryExecute,out _),"partial shared wall");
        Check(session.Draft.walls.Count==7 && session.Draft.rooms.Count==2,"two rooms share wall");
        Check(session.TryUndo(out _) && session.Draft.ComputeFingerprint()==first,"shared room atomic undo");
        Check(session.TryRedo(out _) && session.Draft.rooms.Count==2,"shared room redo");
        var wall=session.Draft.walls[0];
        var door=new BistroBuilderOpeningRecord { openingId=BistroBuilderEditId.NewId(),hostWallId=wall.wallId,axisPosition01=0.5f };
        Check(session.TryExecute(new BistroBuilderCreateOpeningCommand(door),out _,out _),"door");
        string before=session.Draft.ComputeFingerprint();
        var overlap=door.DeepClone(); overlap.openingId=BistroBuilderEditId.NewId();
        Check(!session.TryExecute(new BistroBuilderAtomicEditCommand("Insertar hueco",new BistroBuilderCreateOpeningCommand(overlap)),out _,out _) && session.Draft.ComputeFingerprint()==before,"overlapping opening rejected atomically");
        var kit=BistroBuilderConstructionAssetKit.Load();
        var floor=BistroBuilderPlanarGeometryBuilder.BuildHorizontalPolygon(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
        var vertices=floor.vertices;var triangles=floor.triangles;
        for(int t=0;t<triangles.Length;t+=3)
            Check(Vector3.Cross(vertices[triangles[t+1]]-vertices[triangles[t]],vertices[triangles[t+2]]-vertices[triangles[t]]).y>0,"floor faces upward");
        UnityEngine.Object.DestroyImmediate(floor);
        Check(kit!=null && kit.wallModules.Length==4 && kit.doorPrefab!=null && kit.windowPrefab!=null,"asset kit");
        Check(kit.doorPrefab.GetComponentsInChildren<Collider>().Length==0,"door passage not blocked by asset");
        foreach(var material in new[]{kit.wallMaterial,kit.floorMaterial,kit.trimMaterial,kit.glassMaterial})
            Check(material!=null && material.shader.name.StartsWith("Universal Render Pipeline/"),"URP material");
        foreach(string icon in new[]{"select","wall","module","room","door","window","furniture"})
            Check(Resources.Load<Sprite>("BistroBuilder/Construction/Icons/"+icon)!=null,"icon "+icon);
        Debug.Log("BB_CONSTRUCTION_PLAYER_REGRESSION_PASS | shared rooms, history, openings, assets");
    }
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
