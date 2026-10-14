using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicBarStoolRuntimeAcceptanceRecord
    {
        public string verifierVersion = string.Empty;
        public string sourceHash = string.Empty;
        public string planFingerprint = string.Empty;
        public string prefabDependencyHash = string.Empty;
        public string customerPrefabDependencyHash = string.Empty;
        public string animationCatalogDependencyHash = string.Empty;
        public string reportRelativePath = string.Empty;
        public string reportHash = string.Empty;
        public string verifiedUtc = string.Empty;
        public bool creationPassed, associationPassed, routePassed, leasePassed, seatedAnimationPassed,
            saveLoadPassed, repeatedLoadPassed, cleanupPassed, consoleClean;
    }
    internal static class SavicBarStoolRuntimeAcceptance
    {
        internal const string Version = "1.0.0";
        internal const string CustomerPrefabPath = "Assets/Prefabs/Customers/CustomerGroupPrefab.prefab";
        internal const string AnimationCatalogPath = "Assets/Data/Animation/V1/BistroBuilderMotionRecipeCatalog.asset";
        internal static bool Required(SavicManifest m) => m?.genericPlaceable?.integrationMode == SavicBarStoolFunctionAdapter.Mode;
        internal static bool Matches(SavicManifest m, SavicStorageLayout layout)
        {
            if (!Required(m)) return true;
            var p = m.barStoolRuntime;
            if (p == null || p.verifierVersion != Version || !p.creationPassed || !p.associationPassed || !p.routePassed ||
                !p.leasePassed || !p.seatedAnimationPassed || !p.saveLoadPassed || !p.repeatedLoadPassed || !p.cleanupPassed || !p.consoleClean ||
                p.sourceHash != m.source?.sourceHash || p.planFingerprint != m.barStool?.inputFingerprint ||
                !SavicBarStoolFunctionAdapter.PlanMatches(m) ||
                p.reportRelativePath != layout.ToProjectRelativePath(Path.Combine(layout.LogsRoot,
                    "savic-barstool-candidate-" + m.savicId + "-runtime-playtest.txt"))) return false;
            string report = layout.FromProjectRelativePath(p.reportRelativePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath);
            var customers = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            var visuals = customers != null ? customers.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>() : null;
            return File.Exists(report) && SavicHashService.ComputeSha256(report) == p.reportHash &&
                prefab != null && new SavicBarStoolFunctionAdapter().Validate(prefab, m, out _) &&
                visuals?.HumanoidProfile != null && visuals.ValidateConfiguration(out _) &&
                AssetDatabase.GetAssetDependencyHash(m.genericPlaceable.prefabAssetPath).ToString() == p.prefabDependencyHash &&
                AssetDatabase.GetAssetDependencyHash(CustomerPrefabPath).ToString() == p.customerPrefabDependencyHash &&
                AssetDatabase.GetAssetDependencyHash(AnimationCatalogPath).ToString() == p.animationCatalogDependencyHash;
        }
    }
    internal static class SavicFunctionalRuntimeAcceptance
    {
        internal static bool Required(SavicManifest m) => SavicBarCounterRuntimeAcceptance.Required(m) || SavicBarStoolRuntimeAcceptance.Required(m) ||
            SavicOverheadEquipmentRuntimeAcceptance.Required(m);
        internal static bool Matches(SavicManifest m, SavicStorageLayout layout) =>
            SavicBarCounterRuntimeAcceptance.Matches(m, layout) && SavicBarStoolRuntimeAcceptance.Matches(m, layout) &&
            SavicOverheadEquipmentRuntimeAcceptance.Matches(m, layout);
    }
}
