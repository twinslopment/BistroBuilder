using System;
using System.Linq;
using System.Reflection;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class BistroBuilderWallActionsPlayTest
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Check(bool ok, string text) { if (!ok) throw new Exception("Wall actions: " + text); }
    static bool Axis(BistroBuilderWallRecord wall) => wall.axisStart.x == wall.axisEnd.x || wall.axisStart.y == wall.axisEnd.y;

    public static void Run()
    {
        var tool = UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        var shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
        var coordinator = Field<BistroBuilderEditRuntimeCoordinator>(tool, "coordinator");
        tool.TryCancelDraft(out _);
        tool.SetMode(BistroBuilderConstructionRuntimeMode.Wall);
        Check(coordinator.TryBeginSession(out var beginError), "start construction session: "+beginError);
        Call(tool,"RefreshQueries");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            // Exercise the actual input-to-preview-to-confirm path with both modifiers held.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftAlt, Key.LeftShift));
            InputSystem.Update();
            Call(tool, "BeginWall", new Vector2(-6, -6));
            Call(tool, "ConfirmTwoClick", new Vector2(-2, -4.7f));
            var first = coordinator.Session.Draft.walls.Last().DeepClone();
            Check(first.axisStart == new Vector2(-6,-6) && first.axisEnd == new Vector2(-2,-6), "diagonal cursor creates horizontal wall with Alt+Shift");
            Call(tool, "ConfirmTwoClick", new Vector2(-0.7f,-2));
            var second = coordinator.Session.Draft.walls.Last();
            Check(second.axisStart == first.axisEnd && second.axisEnd == new Vector2(-2,-2), "continuous walls share the constrained endpoint");
            tool.CancelCurrentGesture();

            var opening = new BistroBuilderOpeningRecord { openingId=BistroBuilderEditId.NewId(), hostWallId=first.wallId,
                openingType="window", fillDefinitionId="window", axisPosition01=.35f, width=1f, height=1.2f, bottomElevation=1f };
            Check(coordinator.TryExecute(new BistroBuilderCreateOpeningCommand(opening),out _,out var error), "create hosted window: "+error);
            tool.SetMode(BistroBuilderConstructionRuntimeMode.Select);
            Call(tool,"RefreshQueries");
            Call(tool,"HandlePointerPressed",new Vector2(-3,-6));
            tool.CancelCurrentGesture();
            Check(tool.SelectedKind==EntityKind.Wall&&tool.SelectedId==first.wallId,"click selects wall through Edit tool");
            Call(shell,"RefreshEditModeChrome",true,false);
            var rotate=GameObject.Find("EditRotate").GetComponent<Button>();
            Check(rotate.interactable,"toolbar enables wall rotation");
            rotate.onClick.Invoke();
            var turned=coordinator.Session.Draft.FindWall(first.wallId);
            Check(turned.axisStart==new Vector2(-4,-8)&&turned.axisEnd==new Vector2(-4,-4),"toolbar rotates selected wall 90 degrees around center");
            var retained=coordinator.Session.Draft.openings.Single(o=>o.openingId==opening.openingId);
            Check(retained.hostWallId==first.wallId && retained.axisPosition01==.35f && retained.width==1f,"window retains host, offset and width");
            Check(tool.TryUndo(out error),"undo rotation: "+error);
            Check(coordinator.Session.Draft.FindWall(first.wallId).axisStart==first.axisStart,"undo restores wall");
            Check(tool.TryRedo(out error),"redo rotation: "+error);
            for(int i=0;i<3;i++) Check(tool.TryRotateArchitecture(out error),"subsequent rotation: "+error);
            turned=coordinator.Session.Draft.FindWall(first.wallId);
            Check(turned.axisStart==first.axisStart&&turned.axisEnd==first.axisEnd,"four turns restore exact original geometry");

            tool.SetMode(BistroBuilderConstructionRuntimeMode.WallModule);
            foreach(float angle in new[]{-135f,-45f,0f,31f,45f,91f,135f,225f,315f,719f})
            {
                tool.ConfigureModule(2.25f,angle);
                var start=new Vector2(4,4);
                var end=(Vector2)Call(tool,"ModuleEnd",start);
                Check((end.x==start.x||end.y==start.y)&&Mathf.Approximately(Vector2.Distance(start,end),2.25f),"module angle "+angle+" remains orthogonal at exact length");
            }
            tool.ConfigureModule(2,0);
            Call(shell,"RefreshEditModeChrome",true,false);
            Check(rotate.interactable,"toolbar enables module rotation");
            rotate.onClick.Invoke();
            Check(tool.ModuleAngle==90,"toolbar rotates module preview");
            Call(tool,"PlaceModule",new Vector2(4,4));
            var module=coordinator.Session.Draft.walls.Last();
            Check(Axis(module)&&module.axisEnd==module.axisStart+Vector2.up*2,"placed module matches rotated preview");
            tool.SetMode(BistroBuilderConstructionRuntimeMode.Select);
            Call(shell,"RefreshEditModeChrome",true,false);
            Check(!rotate.interactable,"no selection disables rotation");
            System.IO.File.WriteAllText("Logs/WallActionsTest.txt","PASS: Alt+Shift orthogonal drawing; chained endpoint; toolbar wall rotation; hosted window; undo/redo; four turns; 10 module angles; toolbar module preview and placement; disabled without selection.");
            Debug.Log("BB_WALL_ACTIONS_PASS");
        }
        finally { InputSystem.RemoveDevice(keyboard); tool.TryCancelDraft(out _); tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture); }
    }
}
