using System;
using System.Linq;
using System.Reflection;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;

public static class BistroBuilderWallCrossingPlayTest
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Check(bool ok,string reason){if(!ok)throw new Exception("Wall crossing: "+reason);}
    static BistroBuilderWallRecord Wall(float x1,float y1,float x2,float y2)=>new BistroBuilderWallRecord{
        wallId=BistroBuilderEditId.NewId(),axisStart=new Vector2(x1,y1),axisEnd=new Vector2(x2,y2),height=2.8f,thickness=.12f,wallDefinitionId="wall.default"};
    public static void Run()
    {
        var horizontal=Wall(-5,0,5,0);var vertical=Wall(0,-3,0,3);
        Check(BistroBuilderWallCrossingPolicy.Crosses(horizontal,vertical),"perpendicular crossing detected");
        Check(BistroBuilderWallCrossingPolicy.Crosses(Wall(-3,-3,3,3),Wall(-3,3,3,-3)),"legacy diagonal crossing detected");
        Check(!BistroBuilderWallCrossingPolicy.Crosses(horizontal,Wall(0,0,0,3)),"T junction allowed");
        Check(!BistroBuilderWallCrossingPolicy.Crosses(horizontal,Wall(5,0,5,3)),"corner allowed");
        Check(!BistroBuilderWallCrossingPolicy.Crosses(horizontal,Wall(-5,1,5,1)),"parallel walls allowed");
        vertical.baseElevation=4;
        Check(!BistroBuilderWallCrossingPolicy.Crosses(horizontal,vertical),"separate elevations allowed");vertical.baseElevation=0;
        vertical.buildPlaneId="upstairs";
        Check(!BistroBuilderWallCrossingPolicy.Crosses(horizontal,vertical),"separate floors allowed");vertical.buildPlaneId="default";
        var old=new BistroBuilderEditDocument();old.walls.Add(horizontal.DeepClone());old.walls.Add(vertical.DeepClone());
        var legacy=new BistroBuilderEditSession(old);
        Check(!legacy.TryReview(out var diagnostics)&&diagnostics.Any(d=>d.code==BistroBuilderWallCrossingPolicy.Code),"legacy crossing blocks commit");
        legacy=new BistroBuilderEditSession(old);
        var shifted=vertical.DeepClone();shifted.axisStart.x=shifted.axisEnd.x=1;
        Check(!legacy.TryExecute(new BistroBuilderAtomicEditCommand("Move old crossing",new BistroBuilderUpdateWallCommand(shifted)),out _,out _),"legacy crossing cannot be moved into a new crossing");
        Check(legacy.TryExecute(new BistroBuilderAtomicEditCommand("Repair crossing",new BistroBuilderDeleteWallCommand(vertical.wallId)),out _,out var error),"legacy crossing can be deleted: "+error);
        Check(legacy.TryUndo(out error)&&legacy.TryRedo(out error),"legacy repair remains undoable");

        var tool=UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        var coordinator=Field<BistroBuilderEditRuntimeCoordinator>(tool,"coordinator");
        tool.TryCancelDraft(out _);tool.SetMode(BistroBuilderConstructionRuntimeMode.WallModule);
        Check(coordinator.TryBeginSession(out error),"start runtime session: "+error);
        try
        {
            Check(coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Base",new BistroBuilderCreateWallCommand(horizontal)),out _,out error),"base wall: "+error);
            Call(tool,"RefreshQueries");
            string before=coordinator.Session.Draft.ComputeFingerprint();long revision=coordinator.Session.DraftRevision;
            tool.ConfigureModule(6,90);Call(tool,"RenderModule",new Vector2(0,-3));
            Check(tool.IsPreviewBlocked,"crossing module shows blocked preview");
            Call(tool,"PlaceModule",new Vector2(0,-3));
            Check(coordinator.Session.Draft.ComputeFingerprint()==before&&coordinator.Session.DraftRevision==revision,"crossing module does not change document/history");
            tool.SetMode(BistroBuilderConstructionRuntimeMode.Wall);
            Call(tool,"BeginWall",new Vector2(1,-2));Call(tool,"UpdateGesture",new Vector2(1,2));
            Check(tool.IsPreviewBlocked,"drawn crossing shows blocked preview");
            Call(tool,"ConfirmTwoClick",new Vector2(1,2));
            Check(coordinator.Session.Draft.ComputeFingerprint()==before,"drawn crossing cannot be confirmed");
            tool.CancelCurrentGesture();
            Call(tool,"BeginRoom",new Vector2(-1,-1));Call(tool,"UpdateGesture",new Vector2(1,1));
            Check(tool.IsPreviewBlocked,"room crossing existing wall blocked");tool.CancelCurrentGesture();

            var movable=Wall(-2,1,2,1);
            Check(coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Parallel",new BistroBuilderCreateWallCommand(movable)),out _,out error),"parallel wall added");
            tool.SetMode(BistroBuilderConstructionRuntimeMode.Select);Call(tool,"RefreshQueries");
            Call(tool,"HandlePointerPressed",new Vector2(0,1));tool.CancelCurrentGesture();
            Check(tool.SelectedId==movable.wallId,"select wall to rotate");before=coordinator.Session.Draft.ComputeFingerprint();revision=coordinator.Session.DraftRevision;
            Check(!tool.TryRotateArchitecture(out error),"rotation through existing wall rejected");
            Check(coordinator.Session.Draft.ComputeFingerprint()==before&&coordinator.Session.DraftRevision==revision,"rejected rotation is atomic");
            var moved=movable.DeepClone();moved.axisStart=new Vector2(2,-2);moved.axisEnd=new Vector2(2,2);
            Check(!coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Move crossing",new BistroBuilderUpdateWallCommand(moved)),out _,out error),"move/endpoint update crossing rejected");
            Check(coordinator.Session.Draft.ComputeFingerprint()==before,"rejected move preserves document");
            Check(!coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Duplicate",new BistroBuilderCreateWallCommand(vertical)),out _,out error),"duplicated crossing rejected");
            Check(coordinator.TryExecute(new BistroBuilderAtomicEditCommand("T join",new BistroBuilderCreateWallCommand(Wall(-3,0,-3,2))),out _,out error),"valid T join still builds: "+error);
            Check(coordinator.TryUndo(out error)&&coordinator.TryRedo(out error),"valid join undo/redo");
            System.IO.File.WriteAllText("Logs/WallCrossingTest.txt","PASS: crossing geometry, corners/T joins, planes/heights, legacy repair+undo/redo, blocked module and draw previews, room, rotation, move, duplicate, atomic rejection and valid joins.");
            Debug.Log("BB_WALL_CROSSING_PASS");
        }
        finally{tool.TryCancelDraft(out _);tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);}
    }
}
