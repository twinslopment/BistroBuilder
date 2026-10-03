using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicBarCounterRuntimeAcceptanceRecord
    {
        public string verifierVersion = string.Empty;
        public string planFingerprint = string.Empty;
        public string prefabDependencyHash = string.Empty;
        public string sourceHash = string.Empty;
        public string reportRelativePath = string.Empty;
        public string reportHash = string.Empty;
        public string verifiedUtc = string.Empty;
        public bool creationPassed, nativeBindingPassed, routePassed, leasePassed, saveLoadPassed, cleanupPassed, consoleClean;
    }

    internal static class SavicBarCounterRuntimeAcceptance
    {
        internal const string Version = "1.1.0";
        internal static bool Required(SavicManifest manifest) =>
            manifest?.genericPlaceable?.integrationMode == SavicBarCounterFunctionAdapter.Mode;

        internal static bool Matches(SavicManifest manifest, SavicStorageLayout layout)
        {
            if (!Required(manifest)) return true;
            SavicBarCounterRuntimeAcceptanceRecord proof = manifest.barCounterRuntime;
            if (proof == null || proof.verifierVersion != Version || !proof.creationPassed || !proof.nativeBindingPassed ||
                !proof.routePassed || !proof.leasePassed || !proof.saveLoadPassed || !proof.cleanupPassed || !proof.consoleClean ||
                proof.planFingerprint != manifest.barCounter.inputFingerprint || proof.sourceHash != manifest.source.sourceHash ||
                !SavicBarCounterFunctionAdapter.PlanMatches(manifest) ||
                proof.reportRelativePath != layout.ToProjectRelativePath(Path.Combine(layout.LogsRoot,
                    "savic-barcounter-candidate-" + manifest.savicId + "-runtime-playtest.txt"))) return false;
            string path = layout.FromProjectRelativePath(proof.reportRelativePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.genericPlaceable.prefabAssetPath);
            return File.Exists(path) && SavicHashService.ComputeSha256(path) == proof.reportHash &&
                prefab != null && new SavicBarCounterFunctionAdapter().Validate(prefab, manifest, out _) &&
                AssetDatabase.GetAssetDependencyHash(manifest.genericPlaceable.prefabAssetPath).ToString() == proof.prefabDependencyHash;
        }
    }
}
