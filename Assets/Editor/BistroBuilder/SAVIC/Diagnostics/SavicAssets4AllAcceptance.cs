using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicAssets4AllAcceptance
    {
        private const string CabinetId = "8a5c37cab8eb4366ab675afa66af65ad";
        private const string ManualName = "Armario revisado · ajuste SAVIC";
        private const string MaterialPath = "Assets/Generated/BistroBuilder/SAVIC/AcceptanceProtectedFinish.mat";
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static string Arg(string key)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
        public static void RunFromCommandLine()
        {
            var context = SavicEditorContext.Instance;
            string phase = Arg("-assets4AllPhase"), root = Arg("-assets4AllDeliveries");
            int errors = 0;
            Application.LogCallback callback = (message, trace, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            Application.logMessageReceived += callback;
            try
            {
                Require(context.Manifests.TryGetBySavicId(CabinetId, out var old), "Existing cabinet missing.");
                string canonical = old.canonicalContentId;
                string itemPath = old.artifacts.First(a => a.role == "catalog.item_definition").projectRelativePath;
                string itemGuid = AssetDatabase.AssetPathToGUID(itemPath);
                int count = context.Manifests.GetAll().Count;
                string[] packages = Directory.GetDirectories(root).Where(p => !Path.GetFileName(p).StartsWith(".") && File.Exists(Path.Combine(p, "delivery.json")))
                    .OrderBy(p => SavicAssets4AllService.VerifyPackage(p).revision).ToArray();
                Require(packages.Length >= 2, "Expected real Blender revision deliveries.");
                if (phase == "first")
                {
                    var item = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(itemPath));
                    item.FindProperty("displayName").stringValue = ManualName;
                    item.FindProperty("purchasePrice").intValue = 4321;
                    item.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
                    var receipt = SavicAssets4AllService.Import(packages[0], context, CabinetId);
                    Require(receipt.state == "PUBLISHED", receipt.reason);
                    context.Manifests.TryGetBySavicId(CabinetId, out var current);
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    material.color = new Color(0.7f, 0.05f, 0.08f, 1);
                    AssetDatabase.CreateAsset(material, MaterialPath);
                    string prefabPath = current.artifacts.First(a => a.role.EndsWith(".prefab", StringComparison.Ordinal)).projectRelativePath;
                    var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        var renderer = prefab.GetComponentsInChildren<Renderer>(true).First(r => r.name.StartsWith("A4A_"));
                        var values = renderer.sharedMaterials; values[0] = material; renderer.sharedMaterials = values;
                        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                    AssetDatabase.SaveAssets();
                }
                else if (phase == "update")
                {
                    Require(old.assets4All?.delivery?.revision == 1, "Revision 1 did not survive editor restart.");
                    var receipt = SavicAssets4AllService.Import(packages[1], context);
                    Require(receipt.state == "PUBLISHED", receipt.reason);
                    for (int revision = 2; revision < packages.Length; revision++)
                    { receipt = SavicAssets4AllService.Import(packages[revision], context); Require(receipt.state == "PUBLISHED", receipt.reason); }
                    Require(SavicAssets4AllService.Import(packages.Last(), context).state == "UNCHANGED", "Duplicate delivery was not idempotent.");
                    Require(SavicAssets4AllService.Import(packages[0], context).state == "NEEDS_REVIEW", "Stale revision accepted.");
                    string broken = Path.Combine(root, ".corrupt-test"); Directory.CreateDirectory(broken);
                    foreach (string file in new[] { "model.glb", "asset4all.json", "partgraph.json", "delivery.json" }) File.Copy(Path.Combine(packages[1], file), Path.Combine(broken, file), true);
                    using (var stream = new FileStream(Path.Combine(broken, "model.glb"), FileMode.Append)) stream.WriteByte(0);
                    Require(SavicAssets4AllService.Import(broken, context).state == "NEEDS_REVIEW", "Corrupt GLB accepted.");
                    context.Manifests.TryGetBySavicId(CabinetId, out var current);
                    Require(context.SourceProcessing.Process(current).Succeeded, "Ordinary reprocess failed.");
                }
                else if (phase == "advance")
                {
                    foreach (string package in packages.Where(p => SavicAssets4AllService.VerifyPackage(p).revision > old.assets4All.delivery.revision))
                    { var receipt = SavicAssets4AllService.Import(package, context); Require(receipt.state == "PUBLISHED", receipt.reason); }
                    Require(SavicAssets4AllService.Import(packages.Last(), context).state == "UNCHANGED", "Latest revision did not retain identity.");
                }
                else if (phase == "restart") Require(context.SourceProcessing.Process(old).Succeeded, "Reprocess after restart failed.");
                else throw new InvalidOperationException("Unknown test phase.");
                context.Manifests.Reload();
                context.Manifests.TryGetBySavicId(CabinetId, out var result);
                Require(result.status == "PUBLISHED" && result.canonicalContentId == canonical && result.savicId == CabinetId, "Canonical identity changed.");
                Require(AssetDatabase.AssetPathToGUID(itemPath) == itemGuid, "Catalog GUID changed.");
                Require(context.Manifests.GetAll().Count == count, "A duplicate catalog identity was created.");
                var updated = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(itemPath));
                Require(updated.FindProperty("displayName").stringValue == ManualName && updated.FindProperty("purchasePrice").intValue == 4321, "Manual catalog overrides lost.");
                Require(result.model3D.semanticParts.parts.Select(p => p.partId).SequenceEqual(result.assets4All.delivery.parts.Select(p => p.partKey)), "Assets4ALL part IDs were replaced.");
                var finish = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                var published = AssetDatabase.LoadAssetAtPath<GameObject>(result.genericPlaceable.prefabAssetPath);
                Require(published.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Contains(finish)), "Manual finish lost.");
                if (phase != "first")
                {
                    Require(result.assets4All.delivery.revision == packages.Length, "Latest revision missing after restart.");
                    Require(result.sourceRevisions.Count == packages.Length, "Prior source provenance not retained.");
                    Require(context.Manifests.TryGetHistoricalSource(result.sourceRevisions[0].sourceHash, out var historical) && historical.savicId == CabinetId, "Historical duplicate lost identity.");
                    var direct = context.Intake.IngestExternalCopySynchronously(Path.Combine(packages[0], "model.glb"));
                    Require(direct.DuplicateExact && direct.ManifestSavicId == CabinetId, "Historical GLB entered as a new article.");
                }
                Require(errors == 0, "Console emitted errors during acceptance.");
                File.WriteAllText(Path.Combine(root, "unity-" + phase + "-result.json"), JsonUtility.ToJson(new Result { status = "PASS", phase = phase,
                    savicId = result.savicId, canonicalContentId = result.canonicalContentId, revision = result.assets4All.delivery.revision,
                    manifestCount = count, partCount = result.assets4All.delivery.parts.Length, sourceHash = result.source.sourceHash }, true));
                Debug.Log("[Assets4ALL→SAVIC] " + phase + " PASS: identity, parts, catalog fields and material preserved.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(root, "unity-" + phase + "-FAIL.txt"), e.ToString()); Debug.LogError(e); EditorApplication.Exit(1); }
            finally { Application.logMessageReceived -= callback; }
        }
        [Serializable] private class Result { public string status, phase, savicId, canonicalContentId, sourceHash; public int revision, manifestCount, partCount; }
        public static void RunRollbackFromCommandLine()
        {
            string root = Arg("-assets4AllDeliveries"); var context = SavicEditorContext.Instance;
            try
            {
                Require(context.Manifests.TryGetBySavicId(CabinetId, out var current), "Cabinet missing.");
                string originalHash = current.source.sourceHash;
                string package = context.Layout.FromProjectRelativePath(current.assets4All.packageRelativePath);
                string attempt = Path.Combine(root, ".rollback-test"); Directory.CreateDirectory(attempt);
                foreach (string name in new[] { "model.glb", "asset4all.json", "partgraph.json", "delivery.json" }) File.Copy(Path.Combine(package, name), Path.Combine(attempt, name), true);
                var delivery = SavicAssets4AllService.VerifyPackage(attempt); delivery.revision++; delivery.parentFingerprint = delivery.fingerprint;
                SavicAtomicFile.WriteJson(Path.Combine(attempt, "delivery.json"), delivery);
                var paths = current.artifacts.Select(a => a.projectRelativePath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(context.Layout.FromProjectRelativePath(p))).Distinct().ToArray();
                var before = paths.ToDictionary(p => p, p => SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(p)));
                string field = "material:" + delivery.parts[0].nodes[0].nodeName + ":999";
                current.developerOverrides.Add(new SavicOverrideRecord { field = field, value = GlobalObjectId.GetGlobalObjectIdSlow(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)).ToString(), reason = "Diagnostic rollback fault" });
                context.Manifests.Save(current);
                var receipt = SavicAssets4AllService.Import(attempt, context);
                Require(receipt.state == "NEEDS_REVIEW", "Invalid protected material slot was accepted.");
                context.Manifests.Reload(); context.Manifests.TryGetBySavicId(CabinetId, out current);
                Require(current.source.sourceHash == originalHash && current.assets4All.delivery.revision == delivery.revision - 1, "Failed candidate replaced canonical source.");
                Require(before.All(pair => SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(pair.Key)) == pair.Value), "Publication rollback did not restore original bytes.");
                current.developerOverrides.RemoveAll(o => o.field == field); context.Manifests.Save(current);
                // Simulate a crash after publication started but before manifest commit.
                var method = typeof(SavicAssets4AllService).GetMethod("PrepareJournal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                string journal = (string)method.Invoke(null, new object[] { context.Layout, current, CabinetId, new string('f', 64) });
                string prefab = current.genericPlaceable.prefabAssetPath;
                File.AppendAllText(context.Layout.FromProjectRelativePath(prefab), "\n# interrupted candidate write\n");
                SavicAssets4AllService.Recover(context.Layout, context.Manifests);
                Require(File.Exists(Path.Combine(journal, "complete")) && before.All(pair => SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(pair.Key)) == pair.Value), "Crash recovery failed.");
                File.WriteAllText(Path.Combine(root, "unity-rollback-result.txt"), "PASS: rejected post-publication fault, prior source identity retained, published bytes restored, interrupted journal recovered.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(root, "unity-rollback-FAIL.txt"), e.ToString()); EditorApplication.Exit(1); }
        }
    }
}
