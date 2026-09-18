using System;
using System.Collections.Generic;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;

public static partial class BistroBuilderConstructionAuthoringSelfTest
{
    private sealed class StagingCaptureCommand : BistroBuilderEditCommandBase
    {
        public BistroBuilderEditDocument Captured;
        public bool Throw;
        public override string Description => "staging isolation test";
        public override bool TryExecute(BistroBuilderEditDocument d, out BistroBuilderEditChangeSet c, out string e)
        {
            Captured=d; c=new BistroBuilderEditChangeSet(); e=string.Empty;
            if(Throw) { d.walls.Clear(); throw new InvalidOperationException("injected exception"); }
            return true;
        }
        public override bool TryUndo(BistroBuilderEditDocument d,out string e) { e=string.Empty; return true; }
        public override bool TryRedo(BistroBuilderEditDocument d,out string e) { e=string.Empty; return true; }
    }
    private static void AtomicExceptionAndAliases()
    {
        var s=Session(Wall("base",0,0,1,0));var before=s.Draft.ComputeFingerprint();
        Check(!s.TryExecute(new BistroBuilderAtomicEditCommand("throws",new StagingCaptureCommand{Throw=true}),out _,out _),"exception converted to rejection");
        Check(s.Draft.ComputeFingerprint()==before && !s.CanUndo,"exception isolation");
        var capture=new StagingCaptureCommand();
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("capture",new BistroBuilderCreateWallCommand(Wall("new",0,2,1,2)),capture),out _,out _),"successful composite");
        before=s.Draft.ComputeFingerprint();capture.Captured.walls.Clear();
        Check(s.Draft.ComputeFingerprint()==before,"staging not aliased to Draft");
    }
    private static void CompositeCascade()
    {
        var s=Session(Wall("host",0,0,4,0));
        s.TryExecute(new BistroBuilderCreateOpeningCommand(new BistroBuilderOpeningRecord{openingId=new BistroBuilderEditId("door"),hostWallId=new BistroBuilderEditId("host")}),out _,out _);
        var before=s.Draft.ComputeFingerprint();
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("replace",new BistroBuilderDeleteWallCommand(new BistroBuilderEditId("host")),
            new BistroBuilderCreateWallCommand(Wall("other",0,2,4,2))),out _,out _),"cascade composite");
        Check(s.Draft.openings.Count==0 && s.Draft.FindWall(new BistroBuilderEditId("host"))==null,"cascade");
        Check(s.TryUndo(out _) && s.Draft.ComputeFingerprint()==before,"restored host and opening IDs");
    }
    private static void CacheIsolationAndClosure()
    {
        var s=Session(Wall("a",0,0,4,0));var q=Cache(s);
        q.CaptureWall(new BistroBuilderEditId("a")).axisStart=new Vector2(100,100);
        Check(q.PickWall(new Vector2(1,0),0.1f).IsValid,"returned wall clone isolated");
        s.Cancel();q.Refresh(s);
        Check(!q.PickWall(new Vector2(1,0),0.1f).IsValid && !q.Matches(s),"cancel invalidates cache");
    }
    private static void SnapInvalidAndReset()
    {
        var s=Session(Wall("a",0,0,4,0));var q=Cache(s);var snap=new ArchitectureSnapService();
        Check(snap.Resolve(q,Vector2.zero,NoGrid()).IsSnapped,"capture");
        Check(!snap.Resolve(q,Vector2.zero,NoGrid(),new Vector2(float.NaN,0)).IsSnapped,"nonfinite anchor");
        s.TryExecute(new BistroBuilderDeleteWallCommand(new BistroBuilderEditId("a")),out _,out _);q.Refresh(s);
        Check(!snap.Resolve(q,Vector2.zero,NoGrid()).IsSnapped,"deleted target cannot retain snap");
        Check(!snap.Resolve(q,Vector2.zero,new SnapSettings{ReleaseDistance=0.01f}).IsSnapped,"invalid capture/release");
    }
    private static void ConnectedOpeningValidation()
    {
        var s=Session(Wall("host",0,0,4,0),Wall("side",0,0,0,2));
        s.TryExecute(new BistroBuilderCreateOpeningCommand(new BistroBuilderOpeningRecord{openingId=new BistroBuilderEditId("door"),hostWallId=new BistroBuilderEditId("host"),width=2}),out _,out _);
        var before=s.Draft.ComputeFingerprint();var g=new ConstructionGesture();
        Check(g.BeginJunction(s,Cache(s),Vector2.zero,"default",out _) && g.Update(new Vector2(3,0)),"light preview");
        Check(!g.Confirm(s.TryExecute,out var error) && error.Contains("OPENING"),"final host constraint rejects shortened wall");
        Check(s.Draft.ComputeFingerprint()==before,"rejected connected move atomic");
    }
    private static void RectangleVariationRegression()
    {
        var random=new System.Random(18);
        for(int i=0;i<32;i++)
        {
            float x=random.Next(-20,20),y=random.Next(-20,20),w=random.Next(2,10),h=random.Next(2,10);
            var s=Session();var g=new ConstructionGesture();
            Check(g.BeginRectangle(s,Cache(s),new Vector2(x+w,y+h),Wall("template",0,0,1,0),definitions,"zone.kitchen",1,out _) &&
                g.Update(new Vector2(x,y)) && g.Confirm(s.TryExecute,out _),"varied rectangle");
            Check(s.Draft.rooms.Count==1,"one derived room");
            var after=s.Draft.ComputeFingerprint();
            Check(s.TryUndo(out _) && !s.CanUndo && s.Draft.walls.Count==0,"one inverse");
            Check(s.TryRedo(out _) && s.Draft.ComputeFingerprint()==after,"stable restoration");
        }
    }
    private static void PreviewAllocationBudget()
    {
        var s=RoomSession();var q=Cache(s);var g=new ConstructionGesture();var snap=new ArchitectureSnapService();var settings=new SnapSettings();
        Check(g.BeginWall(s,q,new Vector2(8,8),Wall("t",0,0,1,0),out _),"begin");
        for(int i=0;i<100;i++)g.Update(snap.Resolve(q,new Vector2(10+(i%10)*0.1f,10),settings).Point);
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++)g.Update(snap.Resolve(q,new Vector2(10+(i%10)*0.1f,10),settings).Point);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
        report?.Invoke("PREVIEW_ALLOCATION_BYTES_10000=" + allocated);
        Check(allocated<=512000,"preview allocation budget (51.2 bytes/update)");
        Check(q.RebuildCount==1 && s.DraftRevision==0,"no topology rebuild or Draft write");
    }
#if UNITY_EDITOR
    public static void RunRegressionBatch()
    {
        try
        {
            RunInEditor();
            BistroBuilderEditBlock18CoreSelfTest.RunFromMenu();
            BistroBuilderEditRuntimeLifecycleSelfTest.RunFromMenu();
            string result=BistroBuilderEditFinance18MSelfTest.Run(out _,out int failed);
            Debug.Log(result);
            if(failed!=0)throw new InvalidOperationException("Finance regression failed");
            UnityEditor.EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); UnityEditor.EditorApplication.Exit(1); }
    }
#endif
}
