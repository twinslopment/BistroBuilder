using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BB18MCandidateVerification
{
    public const string Candidate = "Assets/Scenes/__BB18M_RecoveryCandidate.unity";
    public static void Install()
    {
        try {
            var scene=EditorSceneManager.OpenScene(Candidate,OpenSceneMode.Single);
            var catalog=UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
            if(catalog==null) throw new Exception("Missing save definition catalog");
            if(!catalog.TryGetDefinition("table_basic_4",out _)) {
                var definition=AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>("Assets/Data/Restaurant/EditMode/PlaceableItems/PlaceableItemDefinition_TableBasic4.asset");
                if(definition==null || !definition.HasValidPrefab) throw new Exception("Recovered table_basic_4 asset is invalid");
                var serialized=new SerializedObject(catalog);
                var list=serialized.FindProperty("definitions");
                int index=list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue=definition;
                serialized.ApplyModifiedPropertiesWithoutUndo(); catalog.RebuildIndex();
            }
            typeof(BistroBuilderEditBlock18Installer).GetMethod("InstallScene",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{scene});
            if(!BistroBuilderEditBlock18SceneValidator.ValidateScene(scene,Candidate)) throw new Exception(BistroBuilderEditBlock18SceneValidator.LastReport);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,Candidate,false)) throw new Exception("Could not save candidate installation");
            Debug.Log(BistroBuilderEditBlock18SceneValidator.LastReport);
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void RunNavigation() { UseCandidate(); BistroBuilderNavigation17PlayModeSelfTest.RunFromCommandLine(); }
    public static void RunSpatial() { UseCandidate(); BistroBuilderBBSISPhase3PlayModeSelfTest.RunFromCommandLine(); }
    public static void RunActiveService() { UseCandidate(); BistroBuilderAdvancedOrders11SaveLoadPlayModeSelfTest.RunFromCommandLine(); }
    public static void RunRegression()
    {
        var report=new System.Text.StringBuilder("BB18M candidate regression " + DateTime.UtcNow.ToString("O") + "\n");
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            BistroBuilderEditBlock18CoreSelfTest.RunFromMenu();
            report.AppendLine(System.IO.File.ReadAllText("EditBlock18CoreSelfTestReport.txt"));
            BistroBuilderEditRuntimeLifecycleSelfTest.RunFromMenu();
            report.AppendLine(System.IO.File.ReadAllText("EditRuntimeLifecycleSelfTestReport.txt"));
            report.AppendLine(BistroBuilderEditFinance18MSelfTest.Run(out int financePassed,out int financeFailed));
            if(financeFailed!=0) throw new Exception("Finance regression failed");
            var scene=EditorSceneManager.OpenScene(Candidate,OpenSceneMode.Single);
            BistroBuilderBBSISPhase3SelfTest.Run();
            report.AppendLine(BistroBuilderBBSISPhase3SelfTest.LastReport);
            if(BistroBuilderBBSISPhase3SelfTest.LastFailed!=0) throw new Exception("BBSIS regression failed");
            if(!BistroBuilderEditBlock18SceneValidator.ValidateScene(scene,Candidate)) throw new Exception(BistroBuilderEditBlock18SceneValidator.LastReport);
            report.AppendLine(BistroBuilderEditBlock18SceneValidator.LastReport);
            report.AppendLine("[PASS] Targeted regression suite on recovery candidate. Canonical scene unchanged.");
            System.IO.File.WriteAllText("EditBlock18CandidateRegressionReport.txt",report.ToString());
            Debug.Log(report.ToString()); EditorApplication.Exit(0);
        } catch(Exception e) {
            report.AppendLine("[FAIL] "+e); System.IO.File.WriteAllText("EditBlock18CandidateRegressionReport.txt",report.ToString());
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    static void UseCandidate()
    {
        var previous=EditorSceneManager.playModeStartScene;
        EditorApplication.quitting += () => EditorSceneManager.playModeStartScene=previous;
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Candidate);
        Debug.Log("Validation explicitly targets recovery candidate: "+Candidate);
    }
}
