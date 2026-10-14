using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicOverheadEquipmentRuntimeAcceptanceRecord
    {
        public string verifierVersion = string.Empty, sourceHash = string.Empty, planFingerprint = string.Empty;
        public string prefabDependencyHash = string.Empty, reportRelativePath = string.Empty, reportHash = string.Empty;
        public string verifiedUtc = string.Empty, catalogMode = string.Empty;
        public bool creationPassed, provisionalIsolationPassed, nativeBindingPassed, areaCapabilityPassed, routePassed,
            leasePassed, saveLoadPassed, repeatedLoadPassed, cleanupPassed, consoleClean;
    }
    internal static class SavicOverheadEquipmentRuntimeAcceptance
    {
        internal const string Version = "1.0.0";
        internal static bool Required(SavicManifest m) => m?.genericPlaceable?.integrationMode == SavicOverheadEquipmentFunctionAdapter.Mode;
        internal static bool Matches(SavicManifest m, SavicStorageLayout layout)
        {
            if (!Required(m)) return true;
            var p = m.overheadEquipmentRuntime;
            if (p == null || p.verifierVersion != Version || !p.creationPassed || !p.provisionalIsolationPassed || !p.nativeBindingPassed ||
                !p.areaCapabilityPassed || !p.routePassed || !p.leasePassed || !p.saveLoadPassed || !p.repeatedLoadPassed ||
                !p.cleanupPassed || !p.consoleClean || (p.catalogMode != "candidate" && p.catalogMode != "main") ||
                string.IsNullOrWhiteSpace(p.verifiedUtc) || p.sourceHash != m.source?.sourceHash ||
                p.planFingerprint != m.overheadEquipment?.inputFingerprint || !SavicOverheadEquipmentFunctionAdapter.PlanMatches(m) ||
                p.reportRelativePath != layout.ToProjectRelativePath(Path.Combine(layout.LogsRoot,
                    "savic-overhead-candidate-" + m.savicId + "-runtime-playtest.txt"))) return false;
            string report = layout.FromProjectRelativePath(p.reportRelativePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath);
            return File.Exists(report) && SavicHashService.ComputeSha256(report) == p.reportHash && prefab != null &&
                new SavicOverheadEquipmentFunctionAdapter().Validate(prefab, m, out _) &&
                AssetDatabase.GetAssetDependencyHash(m.genericPlaceable.prefabAssetPath).ToString() == p.prefabDependencyHash;
        }
    }
}
