using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BistroBuilderEditSectionsPlayTest
{
    const string Key = "BB.EditSections.Test";
    static int stage, section;
    static double next;
    static string failure;
    static RestaurantPlaceableCatalogPanel catalog;
    static readonly string[] Buttons = { "EditBuild", "EditSurfaces", "EditWalls", "EditDecor", "EditLighting", "EditServices", "EditOther" };
    static BistroBuilderEditSectionsPlayTest() { EditorApplication.playModeStateChanged += State; }
    public static void RunOpeningAndJoinsAndBuild(){SessionState.SetBool(Key+".OpeningOnly",true);RunBatchAndBuild();}
    public static void RunWallActionsAndBuild(){SessionState.SetBool(Key+".WallsOnly",true);RunBatchAndBuild();}
    public static void RunBatchAndBuild(){SessionState.SetBool(Key+".Build",true);RunBatch();}
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = section = 0; failure = null; next = EditorApplication.timeSinceStartup + 6;
            Application.runInBackground = true; EditorApplication.update += Tick; Application.logMessageReceived += Log;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            bool passed=SessionState.GetBool(Key+".Pass",false);
            if(passed&&SessionState.GetBool(Key+".Build",false)){SessionState.SetBool(Key+".Build",false);try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BistroBuilderEditBlock18CoreSelfTest.RunFromMenu();BistroBuilderPlaytestBuild.BuildWindowsPlaytestFromCommandLine();}catch(Exception e){Debug.LogException(e);passed=false;}}
            EditorApplication.Exit(passed?0:1);
        }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = message;
    }
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static Button B(string name) { var go = GameObject.Find(name); Check(go != null, "Missing " + name); return go.GetComponent<Button>(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        try
        {
            Check(failure == null, failure);
            if (stage == 0)
            {
                BistroBuilderOpeningAndWallJoinsPlayTest.Menu();
                Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                var shell = Object.FindFirstObjectByType<BistroBuilderUiShell>();
                shell.EnsureShell();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(BistroBuilderUiShell).GetMethod("CloseCurrentManagementScreen", flags).Invoke(shell, null);
                typeof(BistroBuilderUiShell).GetMethod("CloseSimpleManagementScreens", flags).Invoke(shell, new object[]{ null });
                Object.FindFirstObjectByType<RestaurantEditInteractionController>().TryEnterEditMode();
                Check(Object.FindFirstObjectByType<RestaurantEditModeService>().IsEditModeActive, "Enter edit mode");
                typeof(BistroBuilderUiShell).GetMethod("RefreshEditModeChrome", flags).Invoke(shell, new object[]{true, false});
                catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogPanel>();
                stage++; return;
            }
            if (stage == 1) {
                if(SessionState.GetBool(Key+".OpeningOnly",false)){SessionState.SetBool(Key+".OpeningOnly",false);BistroBuilderOpeningAndWallJoinsPlayTest.Joins();Finish(true,"Opening UI and continuous wall joints.");return;}
                if(SessionState.GetBool(Key+".WallsOnly",false)){SessionState.SetBool(Key+".WallsOnly",false);BistroBuilderWallActionsPlayTest.Run();BistroBuilderWallCrossingPlayTest.Run();Finish(true,"Focused wall actions and crossing validation.");return;}
                if(section==0){var data=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();Check(data.antialiasing==UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing,"Edit geometry antialiasing");foreach(var key in new[]{"EditDelete","EditRotate","EditDuplicate"}){var button=B(key);var ink=button.GetComponentInChildren<TMP_Text>().color;Check(ink.a>.99f&&ink.r<.5f,"Readable action: "+key);}}
                B(Buttons[section]).onClick.Invoke(); stage++; return;
            }
            if (stage == 2)
            {
                var expected = (RestaurantEditCatalogSection)section;
                Check(catalog.CurrentSection == expected, "Section routing " + expected);
                var labels = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
                Check(labels.Any(t => t.text == catalog.SectionTitle), "Visible section title " + expected);
                if (RestaurantEditCatalogSections.IsArchitecture(expected))
                {
                    Check(catalog.GetComponent<RestaurantArchitectureCatalogPanel>().IsVisible, "Architecture catalogue visible");
                    var cards = GameObject.Find("BB_ArchitectureCatalog").GetComponentsInChildren<Image>().Where(i => i.name == "Preview");
                    Check(cards.Any() && cards.All(i => i.sprite != null), "Real architecture previews");
                    if (expected == RestaurantEditCatalogSection.Walls)
                    {
                        var panel = catalog.GetComponent<RestaurantArchitectureCatalogPanel>();
                        panel.SelectById("door");
                        Check(Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().Mode == BistroBuilderConstructionRuntimeMode.Door, "Door routing");
                        panel.SelectById("window");
                        Check(Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>().Mode == BistroBuilderConstructionRuntimeMode.Window, "Window routing");
                        panel.SelectById("wall-interior");
                    }
                }
                else if (expected == RestaurantEditCatalogSection.Decoration)
                {
                    var plant = Resources.FindObjectsOfTypeAll<RestaurantPlaceableItemDefinition>().First(d => d.Category == RestaurantPlaceableItemCategory.Decoration);
                    Check(RestaurantEditCatalogSections.Matches(expected, 1001, plant), "Plant subcategory metadata");
                    Check(!RestaurantEditCatalogSections.Matches(expected, 1002, plant), "Pictures excludes plant");
                    catalog.GetComponent<RestaurantPlaceableInspectorPanel>().ShowForDefinition(plant);
                }
                else if (expected == RestaurantEditCatalogSection.Build)
                {
                    var chair = Resources.FindObjectsOfTypeAll<RestaurantPlaceableItemDefinition>().First(d => d.Category == RestaurantPlaceableItemCategory.Seating);
                    catalog.GetComponent<RestaurantPlaceableInspectorPanel>().ShowForDefinition(chair);
                }
                stage++; return;
            }
            if (stage == 3)
            {
                foreach(var icon in Object.FindObjectsByType<BistroBuilderEditChromeIcon>(FindObjectsSortMode.None))Check(icon.GetComponent<CanvasRenderer>()!=null&&icon.canvasRenderer.GetMesh()!=null&&icon.canvasRenderer.GetMesh().vertexCount>0,"Rendered icon mesh: "+icon.transform.parent.name);
                BistroBuilderOptionsPlayTest.Capture("EditSection_" + (RestaurantEditCatalogSection)section + "_1920.png", 1920, 1080);
                foreach(var icon in Object.FindObjectsByType<BistroBuilderEditChromeIcon>(FindObjectsSortMode.None))Check(icon.GetComponent<CanvasRenderer>()!=null&&icon.canvasRenderer.GetMesh()!=null&&icon.canvasRenderer.GetMesh().vertexCount>0,"Rendered icon mesh: "+icon.transform.parent.name);
                BistroBuilderOptionsPlayTest.Capture("EditSection_" + (RestaurantEditCatalogSection)section + "_1280.png", 1280, 720);
                if (++section < Buttons.Length) { stage = 1; return; }
                BistroBuilderOptionsPlayTest.Capture("EditSections_Ultrawide.png", 3440, 1440);
                catalog.SelectSection(RestaurantEditCatalogSection.Walls); stage++; return;
            }
            if (stage == 4)
            {
                var root = GameObject.Find("BB_ArchitectureCatalog");
                root.transform.Find("Close").GetComponent<Button>().onClick.Invoke(); stage++; return;
            }
            if (stage == 5)
            {
                Check(!catalog.GetComponent<RestaurantArchitectureCatalogPanel>().IsVisible, "Close catalogue");
                catalog.SelectSection(RestaurantEditCatalogSection.Walls); stage++; return;
            }
            if (stage == 6)
            {
                Check(catalog.GetComponent<RestaurantArchitectureCatalogPanel>().IsVisible, "Reopen catalogue");
                Object.FindFirstObjectByType<BistroBuilderUiDesignSystem>().ApplyAllNow(true);
                BistroBuilder.UI.Iconography.BBIconographyRuntime.DecorateAll(); stage++; return;
            }
            if (stage == 7) { BistroBuilderWallActionsPlayTest.Run(); BistroBuilderWallCrossingPlayTest.Run(); BistroBuilderOpeningAndWallJoinsPlayTest.Joins(); Finish(true, "Seven routes/titles; real previews; door/window tools; plant subcategories; close/reopen; 1920/1280/ultrawide captures; theme isolation; wall rotation and orthogonal construction."); }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/EditSectionsTest.txt", (pass ? "PASS " : "FAIL ") + message);
        Debug.Log("BB_EDIT_SECTIONS_" + (pass ? "PASS " : "FAIL ") + message);
        SessionState.SetBool(Key + ".Pass", pass); EditorApplication.isPlaying = false;
    }
}
