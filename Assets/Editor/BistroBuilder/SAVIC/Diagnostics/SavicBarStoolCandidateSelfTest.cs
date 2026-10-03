using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarStoolCandidateSelfTest
    {
        public static void PrepareRealCandidatesFromCommandLine()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SavicAutonomousClassificationAudit.ReconcileAndAuditFromCommandLine();
            var context = SavicEditorContext.Instance;
            var stools = context.Manifests.GetAll().Where(m => m?.type == "BarStool").ToArray();
            Require(stools.Length == 3, "Expected three canonical real stool candidates.");
            foreach (var m in stools)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath);
                Require(m.status == "NEEDS_REVIEW" && m.barStool.planned && m.genericPlaceable.planned &&
                    prefab != null && new SavicBarStoolFunctionAdapter().Validate(prefab, m, out _) &&
                    m.genericPlaceable.category == "Seating" && !m.genericPlaceableReadiness.validated &&
                    !m.genericPlaceableReadiness.catalogResolvable && !SavicBarStoolRuntimeAcceptance.Matches(m, context.Layout),
                    "Canonical source did not prepare a safe unaccepted native stool candidate: " + m.savicId);
                Require(!context.Jobs.Jobs.Any(j => j.manifestSavicId == m.savicId && j.reasonCode == "PUBLISHED"),
                    "Unaccepted stool bypassed runtime publication gate.");
            }
            BistroBuilderAnimationV1SelfTest.Run();
            Require(BistroBuilderAnimationV1SelfTest.LastFailed == 0, BistroBuilderAnimationV1SelfTest.LastReport);
            BistroBuilderAdvancedCustomers10GSelfTest.RunFromCommandLine();
            SavicBarSeatBindingSelfTest.VerifyNativeAndCanonicalFromCommandLine();
            Debug.Log("[SAVIC] REAL BAR STOOL CANONICAL CANDIDATES - PASS: three original-derived Seating candidates with native function and persistent bar requirement, " +
                "none published without seated Animation and repeated Play Mode SaveGame acceptance; installed Animation/Customers/native regressions passed.");
        }
        private static void Require(bool ok, string error) { if (!ok) throw new InvalidOperationException(error); }
    }
}
