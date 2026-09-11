using System;
using System.Collections.Generic;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Pure behavioral tests against the real Block 18 model, topology,
/// commands and history. No scene, materializer, SaveGame or spatial services.</summary>
public static partial class BistroBuilderConstructionAuthoringSelfTest
{
    private static int assertions; private static Action<string> report;
    private static ConstructionDefinitionCatalog definitions;
    private static BistroBuilderWallRecord Wall(string id, float ax, float ay, float bx, float by) =>
        new BistroBuilderWallRecord { wallId = new BistroBuilderEditId(id), axisStart = new Vector2(ax,ay), axisEnd = new Vector2(bx,by) };
    private static BistroBuilderEditSession Session(params BistroBuilderWallRecord[] walls)
    {
        var doc = new BistroBuilderEditDocument { documentId = "construction-test" };
        doc.walls.AddRange(walls); return new BistroBuilderEditSession(doc);
    }
    private static ArchitectureQueryCache Cache(BistroBuilderEditSession session)
    { var q = new ArchitectureQueryCache(); q.Refresh(session); return q; }
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Near(float a, float b, string message) => Check(Math.Abs(a-b) < 0.0001f, message);
    private static void SamePoint(Vector2 a, Vector2 b, string message) => Check((a-b).sqrMagnitude < 0.000001f, message);
    private static SnapSettings NoGrid() => new SnapSettings { GridEnabled = false, AxisEnabled = false };

    public static int RunAll(ConstructionDefinitionCatalog catalog, Action<string> log)
    {
        definitions = catalog; assertions = 0; report = log;
        var cases = new KeyValuePair<string, Action>[]
        {
            Case("atomic exceptions and alias isolation", AtomicExceptionAndAliases),
            Case("composite cascade preserves IDs", CompositeCascade),
            Case("query clone and closed-session isolation", CacheIsolationAndClosure),
            Case("invalid snap and deleted target reset", SnapInvalidAndReset),
            Case("connected move respects host opening constraints", ConnectedOpeningValidation),
            Case("rectangle variation and room ID regression", RectangleVariationRegression),
            Case("bounded allocations during pointer motion", PreviewAllocationBudget),

            Case("rectangle one Undo, stable rooms and zones", RectangleHistory),
            Case("wall confirmation and preview isolation", WallGesture),
            Case("rectangle failure leaves Draft and history intact", FailedRectangle),
            Case("atomic child failure is isolated", AtomicFailure),
            Case("atomic Undo/Redo failure is isolated", AtomicInverseFailure),
            Case("new command clears Redo", BranchingHistory),
            Case("wall room opening picking and selection", Picking),
            Case("selection invalidation and refresh cache", SelectionLifecycle),
            Case("endpoint and wall projection snapping", PointSnap),
            Case("intersection and build-plane isolation", IntersectionSnap),
            Case("axis and configurable grid snapping", AxisGrid),
            Case("snap ranking independent of wall order", Determinism),
            Case("snap capture release hysteresis", Hysteresis),
            Case("dimensions and provisional geometry", Dimensions),
            Case("perpendicular wall move preserves neighbours and openings", MoveWall),
            Case("corner movement preserves incident wall IDs", MoveCorner),
            Case("T junction constrained host movement", MoveTJunction),
            Case("crossing and hosted endpoint detachment rejected", Detachment),
            Case("door/window host coordinates and height bounds", Openings),
            Case("opening gesture Undo/Redo preserves IDs", OpeningHistory),
            Case("all gesture cancellations preserve Draft", Cancellation),
            Case("stale revision and mutation reject confirmation", Stale),
            Case("definition IDs originate in canonical tariffs", DefinitionIds),
            Case("invalid inputs and no-op gestures", InvalidInputs),
            Case("pointer loop does not rebuild or mutate Draft", PointerLoop)
        };
        int failed = 0;
        foreach (var test in cases)
        {
            try { test.Value(); log?.Invoke("PASS " + test.Key); }
            catch (Exception e) { failed++; log?.Invoke("FAIL " + test.Key + ": " + e.Message); }
        }
        log?.Invoke("Construction Authoring: " + (cases.Length-failed) + "/" + cases.Length + " scenarios, " + assertions + " assertions, " + failed + " failures.");
        if (failed != 0) throw new InvalidOperationException("Construction Authoring failures: " + failed);
        return assertions;
    }
    private static KeyValuePair<string, Action> Case(string name, Action action) => new KeyValuePair<string, Action>(name, action);

    private static void RectangleHistory()
    {
        var s = Session(); var q = Cache(s); var g = new ConstructionGesture();
        string before = s.Draft.ComputeFingerprint();
        Check(g.BeginRectangle(s,q,Vector2.zero,Wall("template",0,0,1,0),definitions,"zone.dining",1f,out _),"begin");
        Check(g.Update(new Vector2(4,3)),"preview");
        Check(s.Draft.ComputeFingerprint()==before && !s.CanUndo,"preview isolated");
        Check(g.Confirm(s.TryExecute,out _),"confirm");
        Check(s.Draft.walls.Count==4 && s.Draft.rooms.Count==1 && s.Draft.zones.Count==1,"rectangle/room/zone");
        Check(s.DraftRevision==1,"one command revision");
        Check(s.Draft.zones[0].zoneDefinitionId=="zone.dining","canonical ID");
        string after = s.Draft.ComputeFingerprint(); var roomId=s.Draft.rooms[0].roomId;
        Check(s.TryUndo(out _),"undo");
        Check(s.Draft.walls.Count==0 && s.Draft.rooms.Count==0 && s.Draft.zones.Count==0 && !s.CanUndo,"one undo restores all");
        Check(s.TryRedo(out _),"redo");
        Check(s.Draft.ComputeFingerprint()==after && s.Draft.rooms[0].roomId==roomId,"same IDs and contents");
        Check(s.TryUndo(out _) && !s.CanUndo,"still one undo");
    }
    private static void WallGesture()
    {
        var s=Session(); var g=new ConstructionGesture(); var q=Cache(s); string baseline=s.Baseline.ComputeFingerprint();
        Check(g.BeginWall(s,q,Vector2.zero,Wall("t",0,0,1,0),out _),"begin");
        for(int i=1;i<=50;i++) Check(g.Update(new Vector2(i*0.1f,1)),"update");
        Check(s.Draft.walls.Count==0 && s.DraftRevision==0,"no Draft changes");
        int calls=0;
        ConstructionCommandExecutor execute=(IBistroBuilderEditCommand c,out BistroBuilderEditChangeSet change,out string error) =>
        { calls++; return s.TryExecute(c,out change,out error); };
        Check(g.Confirm(execute,out _) && calls==1,"exactly one dispatch");
        Check(!g.Confirm(execute,out _) && calls==1,"no double dispatch");
        Check(s.Baseline.ComputeFingerprint()==baseline,"baseline isolated");
        var id=s.Draft.walls[0].wallId;
        Check(s.TryUndo(out _) && s.TryRedo(out _) && s.Draft.walls[0].wallId==id,"wall ID preserved");
    }
    private static void FailedRectangle()
    {
        var s=Session(Wall("existing",0,0,4,0)); var q=Cache(s); var g=new ConstructionGesture();
        var before=s.Draft.ComputeFingerprint();
        Check(g.BeginRectangle(s,q,Vector2.zero,Wall("t",0,0,1,0),definitions,"zone.dining",1,out _) && g.Update(new Vector2(4,3)),"preview");
        Check(!g.Confirm(s.TryExecute,out var error) && error.Contains("OVERLAP"),"reject overlap");
        Check(before==s.Draft.ComputeFingerprint() && !s.CanUndo && !s.CanRedo && s.DraftRevision==0,"atomic rejection");
    }
    private sealed class FailCommand : BistroBuilderEditCommandBase
    {
        private readonly bool executeFails, undoFails, redoFails;
        public FailCommand(bool executeFails, bool undoFails=false, bool redoFails=false)
        { this.executeFails=executeFails; this.undoFails=undoFails; this.redoFails=redoFails; }
        public override string Description => "failure injection";
        public override bool TryExecute(BistroBuilderEditDocument d,out BistroBuilderEditChangeSet c,out string e)
        { c=new BistroBuilderEditChangeSet(); e="injected"; if(executeFails)d.walls.Clear(); return !executeFails; }
        public override bool TryUndo(BistroBuilderEditDocument d,out string e)
        { e="injected inverse"; if(undoFails)d.walls.Clear(); return !undoFails; }
        public override bool TryRedo(BistroBuilderEditDocument d,out string e)
        { e="injected redo"; if(redoFails)d.walls.Clear(); return !redoFails; }
    }
    private static void AtomicFailure()
    {
        var s=Session(Wall("base",0,0,1,0)); var before=s.Draft.ComputeFingerprint();
        var command=new BistroBuilderAtomicEditCommand("fail",new BistroBuilderCreateWallCommand(Wall("new",0,1,1,1)),new FailCommand(true));
        Check(!s.TryExecute(command,out _,out _),"reject");
        Check(s.Draft.ComputeFingerprint()==before && !s.CanUndo && !s.CanRedo,"no mutation");
    }
    private static void AtomicInverseFailure()
    {
        var s=Session();
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("fail undo",new FailCommand(false,true),new BistroBuilderCreateWallCommand(Wall("a",0,0,1,0))),out _,out _),"execute");
        var before=s.Draft.ComputeFingerprint(); long revision=s.DraftRevision;
        Check(!s.TryUndo(out _) && before==s.Draft.ComputeFingerprint() && s.DraftRevision==revision && s.CanUndo,"undo rollback");
        s=Session();
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("fail redo",new BistroBuilderCreateWallCommand(Wall("a",0,0,1,0)),new FailCommand(false,false,true)),out _,out _),"execute");
        Check(s.TryUndo(out _),"undo");
        before=s.Draft.ComputeFingerprint(); revision=s.DraftRevision;
        Check(!s.TryRedo(out _) && before==s.Draft.ComputeFingerprint() && s.DraftRevision==revision && s.CanRedo,"redo rollback");
    }
    private static void BranchingHistory()
    {
        var s=Session();
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("a",new BistroBuilderCreateWallCommand(Wall("a",0,0,1,0))),out _,out _) && s.TryUndo(out _),"undo");
        Check(s.TryExecute(new BistroBuilderAtomicEditCommand("b",new BistroBuilderCreateWallCommand(Wall("b",0,1,1,1))),out _,out _),"branch");
        Check(!s.CanRedo && s.Draft.walls[0].wallId.Value=="b","redo cleared");
    }
    private static BistroBuilderEditSession RoomSession()
    {
        return Session(Wall("a",0,0,4,0),Wall("b",4,0,4,3),Wall("c",4,3,0,3),Wall("d",0,3,0,0));
    }
    private static void Picking()
    {
        var s=RoomSession();
        Check(s.TryExecute(new BistroBuilderCreateOpeningCommand(new BistroBuilderOpeningRecord { openingId=new BistroBuilderEditId("door"),hostWallId=new BistroBuilderEditId("a"),axisPosition01=0.5f }),out _,out _),"opening fixture");
        var q=Cache(s);
        Check(q.PickWall(new Vector2(0.5f,0.05f),0.1f).Id.Value=="a","wall");
        Check(q.PickRoom(new Vector2(2,1)).Id==s.Draft.rooms[0].roomId,"room ID from canonical polygon");
        Check(q.Pick(new Vector2(2,0.05f),0.1f).Kind==EntityKind.Opening,"opening priority");
        Check(!q.Pick(new Vector2(20,20),0.1f).IsValid,"outside");
        var selection=new ArchitectureSelection();
        Check(selection.Select(q,q.PickOpening(new Vector2(2,0),0.1f)) && selection.Id.Value=="door","opening selection");
        Check(selection.Select(q,q.PickRoom(new Vector2(1,1))),"room selection");
        Check(selection.Select(q,q.PickWall(new Vector2(0.5f,0),0.1f)) && selection.Id.Value=="a","wall selection");
    }
    private static void SelectionLifecycle()
    {
        var s=Session(Wall("a",0,0,2,0)); var q=Cache(s); var selection=new ArchitectureSelection();
        selection.Select(q,q.PickWall(new Vector2(1,0),0.1f));
        Check(!q.Refresh(s) && q.RebuildCount==1,"same revision cached");
        Check(s.TryExecute(new BistroBuilderDeleteWallCommand(new BistroBuilderEditId("a")),out _,out _),"delete");
        Check(q.Refresh(s) && !selection.Reconcile(q),"deleted selection cleared");
        Check(s.TryUndo(out _) && q.Refresh(s),"undo refresh");
        selection.Select(q,q.PickWall(new Vector2(1,0),0.1f));
        var other=Session(Wall("a",0,0,2,0)); q.Refresh(other);
        Check(!selection.Reconcile(q),"selection cannot cross sessions");
    }
    private static void PointSnap()
    {
        var q=Cache(Session(Wall("a",0,0,4,0)));var snap=new ArchitectureSnapService();var settings=NoGrid();
        var result=snap.Resolve(q,new Vector2(0.02f,0.03f),settings);
        Check(result.Kind==SnapKind.Endpoint,"endpoint priority");SamePoint(result.Point,Vector2.zero,"endpoint");
        snap.Reset();result=snap.Resolve(q,new Vector2(2,0.05f),settings);
        Check(result.Kind==SnapKind.Wall,"wall projection");SamePoint(result.Point,new Vector2(2,0),"projected");
        Check(!snap.Resolve(q,new Vector2(10,10),settings).IsSnapped,"release");
    }
    private static void IntersectionSnap()
    {
        var other=Wall("upstairs",5,5,7,5);other.buildPlaneId="upper";
        var q=Cache(Session(Wall("x",-2,0,2,0),Wall("y",0,-2,0,2),other));var snap=new ArchitectureSnapService();
        var r=snap.Resolve(q,new Vector2(0.01f,0.01f),NoGrid());
        Check(r.Kind==SnapKind.Intersection,"derived X");SamePoint(r.Point,Vector2.zero,"intersection");
        Check(!snap.Resolve(q,new Vector2(5,5),NoGrid()).IsSnapped,"other plane excluded");
        Check(snap.Resolve(q,new Vector2(5,5),NoGrid(),null,"upper").IsSnapped,"active plane");
    }
    private static void AxisGrid()
    {
        var q=Cache(Session());var snap=new ArchitectureSnapService();var settings=new SnapSettings{GridEnabled=false};
        Check(snap.Resolve(q,new Vector2(3,0.05f),settings,Vector2.zero).Kind==SnapKind.Horizontal,"horizontal");
        snap.Reset();Check(snap.Resolve(q,new Vector2(0.05f,3),settings,Vector2.zero).Kind==SnapKind.Vertical,"vertical");
        settings.AxisEnabled=false;settings.GridEnabled=true;settings.GridSize=0.5f;settings.GridOrigin=new Vector2(0.1f,0.1f);
        var r=snap.Resolve(q,new Vector2(0.57f,1.08f),settings);
        Check(r.Kind==SnapKind.Grid,"grid");SamePoint(r.Point,new Vector2(0.6f,1.1f),"grid origin/size");
        settings.GridEnabled=false;Check(!snap.Resolve(q,new Vector2(0.57f,1.08f),settings).IsSnapped,"disabled");
    }
    private static void Determinism()
    {
        var a=Wall("a",-1,0,-1,1);var b=Wall("b",1,0,1,1);
        var settings=NoGrid();settings.CaptureDistance=2;settings.ReleaseDistance=3;
        var p=new Vector2(0,0);var r1=new ArchitectureSnapService().Resolve(Cache(Session(a,b)),p,settings);
        var r2=new ArchitectureSnapService().Resolve(Cache(Session(b,a)),p,settings);
        Check(r1.Kind==r2.Kind && r1.EntityId==r2.EntityId && r1.Point==r2.Point,"stable ordering");
    }
    private static void Hysteresis()
    {
        var q=Cache(Session(Wall("a",0,0,-1,0),Wall("b",0.3f,0,1,0)));var snap=new ArchitectureSnapService();var settings=NoGrid();
        var first=snap.Resolve(q,new Vector2(0.13f,0.08f),settings);
        Check(first.EntityId.Value=="a" && first.Kind==SnapKind.Endpoint,"initial target");
        var near=snap.Resolve(q,new Vector2(0.155f,0.08f),settings);
        Check(near.EntityId.Value=="a","small rank change retained");
        var far=snap.Resolve(q,new Vector2(0.29f,0.01f),settings);
        Check(far.EntityId.Value=="b","stronger target switches");
        Check(!snap.Resolve(q,new Vector2(8,8),settings).IsSnapped,"outside release");
    }
    private static void Dimensions()
    {
        var d=new ConstructionDimensions(Vector2.zero,new Vector2(3,4),true);
        Near(d.Length,5,"length");Near(d.Area,12,"area");Near(d.Perimeter,14,"perimeter");
        var output=new List<WallPose>();
        Check(ConstructionGeometry.TryRectangle(new Vector2(4,3),Vector2.zero,1,output,out _) && output.Count==4,"reverse rectangle");
        SamePoint(output[0].Start,Vector2.zero,"normalized bounds");
        Check(!ConstructionGeometry.TryWall(Vector2.zero,new Vector2(0.001f,0),out _,out _),"minimum wall");
        SamePoint(ConstructionGeometry.PerpendicularDisplacement(Vector2.zero,new Vector2(3,0),new Vector2(8,2)),new Vector2(0,2),"normal");
        var diagonal=ConstructionGeometry.PerpendicularDisplacement(Vector2.zero,new Vector2(1,1),new Vector2(2,0));
        Near(Vector2.Dot(diagonal,new Vector2(1,1)),0,"diagonal normal");
    }
    private static void MoveWall()
    {
        var s=RoomSession();s.TryExecute(new BistroBuilderCreateOpeningCommand(new BistroBuilderOpeningRecord{openingId=new BistroBuilderEditId("o"),hostWallId=new BistroBuilderEditId("a")}),out _,out _);
        var before=s.Draft.ComputeFingerprint();var g=new ConstructionGesture();
        Check(g.BeginMoveWall(s,Cache(s),new BistroBuilderEditId("a"),Vector2.zero,out _) && g.Update(new Vector2(6,-1)),"preview");
        Check(g.PreviewWalls.Count==3,"selected and neighbours");
        Check(g.Confirm(s.TryExecute,out _),"confirm");
        SamePoint(s.Draft.FindWall(new BistroBuilderEditId("a")).axisStart,new Vector2(0,-1),"perpendicular only");
        SamePoint(s.Draft.FindWall(new BistroBuilderEditId("d")).axisEnd,new Vector2(0,-1),"connected neighbour");
        Check(s.Draft.openings[0].openingId.Value=="o" && s.Draft.openings[0].hostWallId.Value=="a","opening preserved");
        var after=s.Draft.ComputeFingerprint();Check(s.TryUndo(out _) && s.Draft.ComputeFingerprint()==before,"one undo");
        Check(s.TryRedo(out _) && s.Draft.ComputeFingerprint()==after,"redo");
    }
    private static void MoveCorner()
    {
        var s=RoomSession();var g=new ConstructionGesture();var before=s.Draft.ComputeFingerprint();
        Check(g.BeginJunction(s,Cache(s),Vector2.zero,"default",out _) && g.Update(new Vector2(-1,-1)),"preview");
        Check(g.PreviewWalls.Count==2 && g.Confirm(s.TryExecute,out _),"two walls one command");
        Check(s.Draft.walls.Count==4 && s.Draft.rooms.Count==1,"no split IDs");
        SamePoint(s.Draft.FindWall(new BistroBuilderEditId("a")).axisStart,s.Draft.FindWall(new BistroBuilderEditId("d")).axisEnd,"connected");
        var after=s.Draft.ComputeFingerprint();
        Check(s.TryUndo(out _) && s.Draft.ComputeFingerprint()==before,"undo");
        Check(s.TryRedo(out _) && s.Draft.ComputeFingerprint()==after,"redo IDs");
    }
    private static void MoveTJunction()
    {
        var s=Session(Wall("host",0,0,4,0),Wall("stem",2,0,2,2));var g=new ConstructionGesture();
        Check(g.BeginJunction(s,Cache(s),new Vector2(2,0),"default",out _),"begin");
        Check(!g.Update(new Vector2(2,1)) && g.Diagnostic.Contains("STRAIGHT_HOST"),"off host rejected");
        Check(g.Update(new Vector2(3,0)) && g.Confirm(s.TryExecute,out _),"slide on host");
        Check(s.Draft.walls.Count==2,"no artificial split");
        SamePoint(s.Draft.FindWall(new BistroBuilderEditId("stem")).axisStart,new Vector2(3,0),"stem follows");
        SamePoint(s.Draft.FindWall(new BistroBuilderEditId("host")).axisEnd,new Vector2(4,0),"host unchanged");
    }
    private static void Detachment()
    {
        var output=new List<WallPose>();var q=Cache(Session(Wall("host",0,0,4,0),Wall("stem",2,0,3,2)));
        Check(!ConstructionGeometry.TryMoveWall(q,new BistroBuilderEditId("stem"),new Vector2(0,1),output,out _),"hosted endpoint cannot detach");
        q=Cache(Session(Wall("a",-1,0,1,0),Wall("b",0,-1,0,1)));
        Check(!ConstructionGeometry.TryMoveWall(q,new BistroBuilderEditId("a"),new Vector2(0,2),output,out _),"crossing retained");
    }
    private static void Openings()
    {
        var host=Wall("a",0,0,4,0);
        Check(ConstructionGeometry.TryOpening(host,new Vector2(2,3),1,0,2.1f,out var t,out var center,out _),"door");
        Near(t,0.5f,"longitudinal position");SamePoint(center,new Vector2(2,0),"host center");
        Check(ConstructionGeometry.TryOpening(host,new Vector2(-10,0),1,1,1,out t,out _,out _) && t>0,"window edge margin");
        Check(!ConstructionGeometry.TryOpening(host,Vector2.zero,1,2,2,out _,out _,out _),"window height");
        Check(!ConstructionGeometry.TryOpening(host,Vector2.zero,5,0,2,out _,out _,out _),"width");
    }
    private static void OpeningHistory()
    {
        foreach(var type in new[]{"door","window"})
        {
            var s=Session(Wall("host",0,0,4,0));var g=new ConstructionGesture();
            var template=new BistroBuilderOpeningRecord{openingType=type,width=1,bottomElevation=type=="window"?1:0,height=1.5f};
            Check(g.BeginOpening(s,Cache(s),new BistroBuilderEditId("host"),template,out _) && g.Update(new Vector2(2,2)),"begin/update");
            Check(s.Draft.openings.Count==0 && g.Confirm(s.TryExecute,out _),"confirm only");
            var after=s.Draft.ComputeFingerprint();
            Check(s.TryUndo(out _) && s.Draft.openings.Count==0 && !s.CanUndo,"undo opening");
            Check(s.TryRedo(out _) && s.Draft.ComputeFingerprint()==after,"redo IDs");
            g=new ConstructionGesture();
            Check(g.BeginOpening(s,Cache(s),new BistroBuilderEditId("host"),template,out _) && g.Update(new Vector2(2,0)),"overlap preview");
            Check(!g.Confirm(s.TryExecute,out _) && s.Draft.ComputeFingerprint()==after,"overlap rejected atomically");
        }
    }
    private static void Cancellation()
    {
        for(int kind=0;kind<5;kind++)
        {
            var s=RoomSession();var q=Cache(s);var g=new ConstructionGesture();bool began;
            if(kind==0)began=g.BeginWall(s,q,new Vector2(8,8),Wall("t",0,0,1,0),out _);
            else if(kind==1)began=g.BeginRectangle(s,q,new Vector2(8,8),Wall("t",0,0,1,0),definitions,"zone.dining",1,out _);
            else if(kind==2)began=g.BeginMoveWall(s,q,new BistroBuilderEditId("a"),Vector2.zero,out _);
            else if(kind==3)began=g.BeginJunction(s,q,Vector2.zero,"default",out _);
            else began=g.BeginOpening(s,q,new BistroBuilderEditId("a"),new BistroBuilderOpeningRecord(),out _);
            var before=s.Draft.ComputeFingerprint();Check(began,"begin cancel case");
            g.Update(new Vector2(10,10));g.Cancel();
            Check(g.State==ConstructionGestureState.Cancelled && g.PreviewWalls.Count==0,"preview cleared");
            Check(s.Draft.ComputeFingerprint()==before && !s.CanUndo && s.DraftRevision==0,"cancel isolated");
            Check(!g.Confirm(s.TryExecute,out _),"cancel cannot confirm");
        }
    }
    private static void Stale()
    {
        var s=Session();var q=Cache(s);var g=new ConstructionGesture();
        g.BeginWall(s,q,Vector2.zero,Wall("t",0,0,1,0),out _);g.Update(new Vector2(2,0));
        s.TryExecute(new BistroBuilderCreateWallCommand(Wall("other",0,1,2,1)),out _,out _);
        var before=s.Draft.ComputeFingerprint();
        Check(!g.Confirm(s.TryExecute,out _) && before==s.Draft.ComputeFingerprint(),"revision stale");
        g.Cancel();q.Refresh(s);g.BeginWall(s,q,new Vector2(0,2),Wall("t",0,0,1,0),out _);g.Update(new Vector2(2,2));
        s.Draft.walls[0].height=4;before=s.Draft.ComputeFingerprint();
        Check(!g.Confirm(s.TryExecute,out _) && before==s.Draft.ComputeFingerprint(),"same revision mutation stale");
    }
    private static void DefinitionIds()
    {
        Check(definitions.ContainsZone("zone.dining") && definitions.ContainsZone("zone.kitchen") &&
            definitions.ContainsZone("zone.bathroom"),"published IDs");
        Check(!definitions.ContainsZone("dining") && !definitions.ContainsZone("kitchen") &&
            !definitions.ContainsZone("bathroom"),"no alias translation");
        var s=Session();var g=new ConstructionGesture();
        Check(g.BeginRectangle(s,Cache(s),Vector2.zero,Wall("t",0,0,1,0),definitions,
            "zone.bathroom",1,out _),"published bathroom available");
        g.Cancel();
    }
    private static void InvalidInputs()
    {
        var s=Session(Wall("a",0,0,4,0));var q=Cache(s);var g=new ConstructionGesture();
        Check(g.BeginMoveWall(s,q,new BistroBuilderEditId("a"),Vector2.zero,out _) && !g.Update(new Vector2(3,0)),"tangential no-op");
        Check(!g.Update(new Vector2(float.NaN,0)),"nonfinite pointer");g.Cancel();
        Check(!new ArchitectureSnapService().Resolve(q,Vector2.zero,new SnapSettings{GridSize=0}).IsSnapped,"invalid grid");
        var template=Wall("t",0,0,1,0);template.thickness=-1;
        Check(!g.BeginWall(s,q,Vector2.zero,template,out _),"invalid thickness");
    }
    private static void PointerLoop()
    {
        var s=RoomSession();var q=Cache(s);var g=new ConstructionGesture();var snap=new ArchitectureSnapService();var settings=new SnapSettings();
        Check(g.BeginWall(s,q,new Vector2(8,8),Wall("t",0,0,1,0),out _),"begin");
        string draft=s.Draft.ComputeFingerprint(), baseline=s.Baseline.ComputeFingerprint();
        for(int i=0;i<10000;i++)g.Update(snap.Resolve(q,new Vector2(10+(i%10)*0.1f,10),settings).Point);
        Check(q.RebuildCount==1 && s.DraftRevision==0 && !s.CanUndo,"no rebuild or commands");
        Check(s.Draft.ComputeFingerprint()==draft && s.Baseline.ComputeFingerprint()==baseline,"no Draft/baseline mutation");
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Bistro Builder/Edit Mode/18N Construction Authoring Pure Tests")]
    public static void RunInEditor()
    {
        var table=AssetDatabase.LoadAssetAtPath<BistroBuilderEditFinanceTariffTable>("Assets/Resources/BistroBuilder/Finance/BB_EditMode_PlaytestTariffs.asset");
        RunAll(ConstructionCatalogAdapter.FromTariffs(table), message=>Debug.Log(message));
    }
    public static void RunBatch()
    {
        try { RunInEditor(); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
#endif
}
