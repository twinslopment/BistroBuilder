using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicAssets4AllService
    {
        internal const string Version = "1.0.0";
        private static readonly string[] PackageFiles = { "model.glb", "asset4all.json", "partgraph.json", "delivery.json" };
        private static readonly string[] ProtectedFields = { "displayName", "description", "purchasePrice", "resaleBasisPoints", "removalCost", "demolitionBasisPoints", "disposalMode" };
        private static bool busy;
        private static double nextScan;

        internal static SavicAssets4AllDelivery VerifyPackage(string folder)
        {
            folder = Path.GetFullPath(folder);
            foreach (string name in PackageFiles)
            {
                string path = Path.Combine(folder, name);
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("MODEL: incomplete delivery or linked file: " + name);
            }
            SavicAssets4AllDelivery d = SavicAtomicFile.ReadJson<SavicAssets4AllDelivery>(Path.Combine(folder, "delivery.json"));
            if (d == null || d.schemaId != "assets4all.savic-delivery" || d.schemaVersion != 1 ||
                !Guid.TryParseExact(d.assetUuid, "N", out _) || d.revision < 1 ||
                d.coordinateSystem != "GLTF2_METERS_Y_UP" || !IsHash(d.fingerprint) || !IsHash(d.workGeometryHash) ||
                !IsHash(d.modelSha256) || !IsHash(d.manifestSha256) || !IsHash(d.partGraphSha256) ||
                (d.revision == 1 ? !string.IsNullOrEmpty(d.parentFingerprint) : !IsHash(d.parentFingerprint)))
                throw new InvalidOperationException("MODEL: unsupported or invalid delivery contract.");
            if (SavicHashService.ComputeSha256(Path.Combine(folder, "model.glb")) != d.modelSha256 ||
                SavicHashService.ComputeSha256(Path.Combine(folder, "asset4all.json")) != d.manifestSha256 ||
                SavicHashService.ComputeSha256(Path.Combine(folder, "partgraph.json")) != d.partGraphSha256)
                throw new InvalidOperationException("MODEL: delivery file hash mismatch.");
            string fingerprint = SavicHashService.ComputeSha256Text("{\"asset4all.json\":\"" + d.manifestSha256 +
                "\",\"model.glb\":\"" + d.modelSha256 + "\",\"partgraph.json\":\"" + d.partGraphSha256 + "\"}");
            if (fingerprint != d.fingerprint || d.parts == null || d.parts.Length == 0)
                throw new InvalidOperationException("MODEL: invalid delivery fingerprint or missing parts.");
            HashSet<string> keys = new HashSet<string>(), names = new HashSet<string>();
            foreach (SavicAssets4AllPart p in d.parts)
            {
                if (p == null || string.IsNullOrEmpty(p.partKey) || !keys.Add(p.partKey) || !IsHash(p.membershipDigest) ||
                    !float.IsFinite(p.confidence) || p.confidence < 0 || p.confidence > 1 || p.nodes == null || p.nodes.Length == 0)
                    throw new InvalidOperationException("MODEL: invalid or duplicate part membership.");
                foreach (SavicAssets4AllNode n in p.nodes)
                    if (n == null || string.IsNullOrEmpty(n.nodeName) || !names.Add(n.nodeName) || n.triangleCount <= 0 ||
                        !Guid.TryParseExact(n.sourceUid, "N", out _))
                        throw new InvalidOperationException("MODEL: invalid or duplicate exported surface.");
            }
            VerifySurfaces(folder, d);
            return d;
        }

        private static void VerifySurfaces(string folder, SavicAssets4AllDelivery delivery)
        {
            var graph = SavicAtomicFile.ReadJson<SavicAssets4AllGraph>(Path.Combine(folder, "partgraph.json"));
            if (graph?.nodes == null || graph.nodes.Length != delivery.parts.Length || graph.workGeometryHash != delivery.workGeometryHash)
                throw new InvalidOperationException("MODEL: PartGraph identity or physical hash differs from delivery.");
            foreach (var part in delivery.parts)
            {
                var matching = graph.nodes.Where(n => n.partKey == part.partKey).ToArray();
                if (matching.Length != 1 || matching[0].membership?.membershipDigest != part.membershipDigest)
                    throw new InvalidOperationException("MODEL: PartGraph membership differs from delivery.");
            }
            using var stream = File.OpenRead(Path.Combine(folder, "model.glb"));
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadUInt32() != 0x46546c67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != stream.Length)
                throw new InvalidOperationException("MODEL: invalid GLB header.");
            uint length = reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x4e4f534a || length > 16 * 1024 * 1024 || length > stream.Length - stream.Position)
                throw new InvalidOperationException("MODEL: invalid GLB JSON chunk.");
            var glb = JsonUtility.FromJson<SavicAssets4AllGlb>(Encoding.UTF8.GetString(reader.ReadBytes((int)length)));
            var expected = delivery.parts.SelectMany(p => p.nodes).ToDictionary(n => n.nodeName);
            if (glb?.nodes == null || glb.meshes == null || glb.accessors == null)
                throw new InvalidOperationException("MODEL: missing GLB surfaces.");
            foreach (var node in glb.nodes.Where(n => n.mesh >= 0))
            {
                if (!expected.TryGetValue(node.name, out var surface) || node.mesh >= glb.meshes.Length)
                    throw new InvalidOperationException("MODEL: unexpected GLB surface.");
                long count = 0;
                foreach (var primitive in glb.meshes[node.mesh].primitives)
                {
                    if (primitive.mode != 4 || primitive.indices < 0 || primitive.indices >= glb.accessors.Length ||
                        glb.accessors[primitive.indices].count % 3 != 0)
                        throw new InvalidOperationException("MODEL: invalid GLB triangles.");
                    count += glb.accessors[primitive.indices].count / 3;
                }
                if (count != surface.triangleCount) throw new InvalidOperationException("MODEL: GLB triangle membership differs from delivery.");
                expected.Remove(node.name);
            }
            if (expected.Count != 0) throw new InvalidOperationException("MODEL: missing GLB part surface.");
        }

        internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        internal static bool TryReadVerified(SavicManifest manifest, SavicStorageLayout layout, out SavicAssets4AllDelivery delivery)
        {
            delivery = null;
            SavicAssets4AllRecord record = manifest?.assets4All;
            if (record?.delivery == null) return false;
            string prefix = "ContentSource/Assets4All/";
            if (!record.packageRelativePath.StartsWith(prefix, StringComparison.Ordinal) || record.packageRelativePath.Contains(".."))
                throw new InvalidOperationException("Invalid Assets4ALL archive path.");
            string folder = layout.FromProjectRelativePath(record.packageRelativePath);
            delivery = VerifyPackage(folder);
            if (SavicHashService.ComputeSha256(Path.Combine(folder, "delivery.json")) != record.deliveryHash ||
                JsonUtility.ToJson(delivery) != JsonUtility.ToJson(record.delivery) ||
                delivery.modelSha256 != manifest.source.sourceHash)
                throw new InvalidOperationException("Assets4ALL evidence no longer matches the canonical source.");
            return true;
        }

        internal static SavicAssets4AllReceipt Import(string folder, SavicEditorContext context, string bindSavicId = null)
        {
            if (busy) throw new InvalidOperationException("Another Assets4ALL delivery is being processed.");
            busy = true;
            string journalFolder = null;
            SavicAssets4AllDelivery d = null;
            SavicAssets4AllReceipt receipt = new SavicAssets4AllReceipt();
            bool committed = false;
            try
            {
                Recover(context.Layout, context.Manifests);
                d = VerifyPackage(folder);
                receipt.assetUuid = d.assetUuid; receipt.revision = d.revision; receipt.fingerprint = d.fingerprint;
                SavicManifest previous = context.Manifests.GetAll().SingleOrDefault(m => m.assets4All?.delivery?.assetUuid == d.assetUuid);
                if (!string.IsNullOrWhiteSpace(bindSavicId))
                {
                    if (previous != null && previous.savicId != bindSavicId)
                        throw new InvalidOperationException("GAME: delivery identity is already bound to another asset.");
                    if (!context.Manifests.TryGetBySavicId(bindSavicId, out previous) || previous.status != "PUBLISHED" ||
                        (previous.assets4All?.delivery != null && previous.assets4All.delivery.assetUuid != d.assetUuid))
                        throw new InvalidOperationException("GAME: select a published, unbound SAVIC asset.");
                }
                if (previous?.assets4All?.delivery != null)
                {
                    SavicAssets4AllDelivery old = previous.assets4All.delivery;
                    if (old.revision == d.revision && old.fingerprint == d.fingerprint)
                    {
                        receipt.state = "UNCHANGED"; receipt.savicId = previous.savicId;
                        receipt.canonicalContentId = previous.canonicalContentId;
                        return WriteReceipt(context.Layout, receipt);
                    }
                    if (d.revision != old.revision + 1 || d.parentFingerprint != old.fingerprint)
                        throw new InvalidOperationException("MODEL: stale, conflicting or missing source revision.");
                }
                else if (d.revision != 1)
                    throw new InvalidOperationException("MODEL: import revision 1 before later revisions.");
                if (context.Manifests.TryGetBySourceHash(d.modelSha256, out SavicManifest hashOwner) &&
                    hashOwner.savicId != previous?.savicId)
                    throw new InvalidOperationException("GAME: identical GLB already belongs to another identity; bind it explicitly.");
                if (context.Manifests.TryGetHistoricalSource(d.modelSha256, out hashOwner) && hashOwner.savicId != previous?.savicId)
                    throw new InvalidOperationException("GAME: identical GLB belongs to another asset's source history; bind it explicitly.");
                string archivedPackage = Path.Combine(context.Layout.ContentSourceRoot, "Assets4All", d.assetUuid,
                    "r" + d.revision + "_" + d.fingerprint);
                Directory.CreateDirectory(archivedPackage);
                foreach (string name in PackageFiles)
                {
                    string dest = Path.Combine(archivedPackage, name);
                    if (File.Exists(dest) && SavicHashService.ComputeSha256(dest) != SavicHashService.ComputeSha256(Path.Combine(folder, name)))
                        throw new InvalidOperationException("MODEL: immutable archived revision conflicts.");
                    if (!File.Exists(dest)) File.Copy(Path.Combine(folder, name), dest);
                }
                VerifyPackage(archivedPackage);
                SavicManifest candidate = previous == null ? new SavicManifest { savicId = Guid.NewGuid().ToString("N"),
                    createdUtc = DateTime.UtcNow.ToString("O") } : Clone(previous);
                if (previous != null)
                {
                    candidate.sourceRevisions ??= new List<SavicSourceRecord>();
                    candidate.sourceRevisions.Add(Clone(previous.source));
                    candidate.assets4AllRevisions ??= new List<SavicAssets4AllRevision>();
                    candidate.assets4AllRevisions.Add(new SavicAssets4AllRevision { source = Clone(previous.source), assets4All = Clone(previous.assets4All) });
                }
                candidate.assets4All = new SavicAssets4AllRecord { packageRelativePath = context.Layout.ToProjectRelativePath(archivedPackage),
                    deliveryHash = SavicHashService.ComputeSha256(Path.Combine(archivedPackage, "delivery.json")), delivery = d,
                    semanticName = previous == null ? d.displayName : SavicProviderMetadataService.ResolveSemanticName(previous, context.Layout),
                    boundClassification = previous?.classification?.classified == true ? Clone(previous.classification) : null };
                string fileName = previous?.source.originalFileName ?? (d.assetUuid + ".glb");
                string archive = context.Layout.GetArchivedSourcePath(d.modelSha256, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(archive));
                if (!File.Exists(archive)) File.Copy(Path.Combine(archivedPackage, "model.glb"), archive);
                if (SavicHashService.ComputeSha256(archive) != d.modelSha256) throw new InvalidOperationException("MODEL: canonical GLB archive is corrupt.");
                candidate.source = new SavicSourceRecord { sourceHash = d.modelSha256, originalFileName = fileName, extension = ".glb",
                    sourceKind = "Model3D", archivedRelativePath = context.Layout.ToProjectRelativePath(archive),
                    byteLength = new FileInfo(archive).Length, ingestedUtc = DateTime.UtcNow.ToString("O") };
                candidate.status = "INGESTED";
                CaptureOverrides(previous, candidate, d);
                string candidateRoot = Path.Combine(context.Layout.SavicRoot, "Candidates", "Assets4All", d.assetUuid, "r" + d.revision);
                // An interrupted candidate is evidence, not a new baseline. Keep earlier attempts.
                candidateRoot = Path.Combine(candidateRoot, Guid.NewGuid().ToString("N"));
                SavicManifestRepository candidates = new SavicManifestRepository(context.Layout, candidateRoot);
                candidates.Save(candidate);
                journalFolder = PrepareJournal(context.Layout, previous, candidate.savicId, d.fingerprint);
                SavicSourceProcessingOutcome outcome = new SavicSourceProcessingService(context.Layout, candidates).Process(candidate);
                if (!outcome.Succeeded || candidate.status != "PUBLISHED")
                    throw new InvalidOperationException("GAME: " + outcome.Message);
                ApplyOverrides(candidate);
                candidate.assets4All.materialBaseline = MaterialValues(candidate);
                context.Manifests.CommitAssets4AllRevision(candidate, previous?.source.sourceHash ?? "");
                committed = true;
                FinishJournal(journalFolder);
                journalFolder = null;
                receipt.state = "PUBLISHED"; receipt.savicId = candidate.savicId; receipt.canonicalContentId = candidate.canonicalContentId;
                receipt.reason = previous == null ? "Asset añadido desde Assets4ALL." : "Asset actualizado; identidad y ajustes conservados.";
                return WriteReceipt(context.Layout, receipt);
            }
            catch (Exception exception)
            {
                if (journalFolder != null && !committed) RestoreJournal(context.Layout, context.Manifests, journalFolder);
                if (committed)
                {
                    receipt.state = "PUBLISHED"; receipt.owner = "GAME";
                    receipt.reason = "Publication committed; receipt/journal finalization requires recovery: " + exception.Message;
                    return receipt;
                }
                receipt.state = "NEEDS_REVIEW";
                receipt.owner = exception.Message.StartsWith("MODEL:", StringComparison.Ordinal) ? "MODEL" : "GAME";
                receipt.reason = exception.Message;
                WriteReceipt(context.Layout, receipt);
                return receipt;
            }
            finally { busy = false; }
        }

        internal static SavicAssets4AllReceipt WriteReceipt(SavicStorageLayout layout, SavicAssets4AllReceipt receipt)
        {
            receipt.updatedUtc = DateTime.UtcNow.ToString("O");
            string name = IsUuid(receipt.assetUuid) ? receipt.assetUuid + "_r" + receipt.revision : "invalid-delivery";
            SavicAtomicFile.WriteJson(Path.Combine(layout.SavicRoot, "Receipts", "Assets4All", name + ".json"), receipt);
            return receipt;
        }

        private static bool IsUuid(string value) => Guid.TryParseExact(value, "N", out _);
        private static T Clone<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        internal static void Tick(SavicEditorContext context)
        {
            if (busy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                context.Intake.IsBusy || context.Jobs.IsPaused || EditorApplication.timeSinceStartup < nextScan) return;
            nextScan = EditorApplication.timeSinceStartup + 5;
            string root = Path.Combine(context.Layout.InboxRoot, "Deliveries");
            if (!Directory.Exists(root)) return;
            foreach (string folder in Directory.GetDirectories(root).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (Path.GetFileName(folder).StartsWith(".") || !File.Exists(Path.Combine(folder, "delivery.json"))) continue;
                SavicAssets4AllDelivery d;
                try { d = VerifyPackage(folder); } catch { continue; }
                string receiptPath = Path.Combine(context.Layout.SavicRoot, "Receipts", "Assets4All", d.assetUuid + "_r" + d.revision + ".json");
                SavicAssets4AllReceipt receipt = SavicAtomicFile.ReadJson<SavicAssets4AllReceipt>(receiptPath);
                if (receipt?.fingerprint == d.fingerprint) continue;
                Import(folder, context);
                break;
            }
        }

        internal static void CaptureOverrides(SavicManifest previous, SavicManifest candidate, SavicAssets4AllDelivery d)
        {
            if (previous == null) return;
            SavicArtifactRecord item = previous.artifacts.FirstOrDefault(a => a.role == "catalog.item_definition");
            UnityEngine.Object obj = item == null ? null : AssetDatabase.LoadMainAssetAtPath(item.projectRelativePath);
            if (obj != null)
            {
                SerializedObject serialized = new SerializedObject(obj);
                foreach (string field in ProtectedFields)
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (property == null) continue;
                    string value = property.propertyType == SerializedPropertyType.String ? property.stringValue : property.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    SetOverride(candidate, field, value);
                }
            }
            var baseline = previous.assets4All?.materialBaseline;
            if (baseline != null)
                foreach (SavicOverrideRecord material in MaterialValues(previous))
                    if (baseline.Any(b => b.field == material.field && b.value != material.value))
                        SetOverride(candidate, material.field, material.value);
            HashSet<string> incomingNodes = new HashSet<string>(d.parts.SelectMany(p => p.nodes).Select(n => n.nodeName));
            foreach (SavicOverrideRecord o in candidate.developerOverrides)
                if (o.field.StartsWith("material:") && !incomingNodes.Contains(o.field.Split(':')[1]))
                    throw new InvalidOperationException("MODEL: protected finish lost its part; resolve part lineage before updating: " + o.field);
        }

        private static void SetOverride(SavicManifest candidate, string field, string value)
        {
            candidate.developerOverrides.RemoveAll(o => o.field == field);
            candidate.developerOverrides.Add(new SavicOverrideRecord { field = field, value = value,
                reason = "Conservar ajuste del contenido publicado al actualizar desde Assets4ALL", createdUtc = DateTime.UtcNow.ToString("O") });
        }

        internal static List<SavicOverrideRecord> MaterialValues(SavicManifest manifest)
        {
            List<SavicOverrideRecord> result = new List<SavicOverrideRecord>();
            string path = manifest.artifacts.FirstOrDefault(a => a.role.EndsWith(".prefab", StringComparison.Ordinal))?.projectRelativePath;
            GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return result;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material material = renderer.sharedMaterials[i];
                    if (renderer.name.StartsWith("A4A_") && material != null)
                        result.Add(new SavicOverrideRecord { field = "material:" + renderer.name + ":" + i,
                            value = GlobalObjectId.GetGlobalObjectIdSlow(material).ToString() });
                }
            return result;
        }

        internal static void ApplyOverrides(SavicManifest manifest)
        {
            string path = manifest.artifacts.FirstOrDefault(a => a.role == "catalog.item_definition")?.projectRelativePath;
            UnityEngine.Object obj = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null)
            {
                SerializedObject serialized = new SerializedObject(obj);
                foreach (SavicOverrideRecord o in manifest.developerOverrides.Where(o => ProtectedFields.Contains(o.field)))
                {
                    SerializedProperty property = serialized.FindProperty(o.field);
                    if (property == null) throw new InvalidOperationException("GAME: protected catalog field is missing: " + o.field);
                    if (property.propertyType == SerializedPropertyType.String) property.stringValue = o.value;
                    else property.intValue = int.Parse(o.value, System.Globalization.CultureInfo.InvariantCulture);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
            }
            var materials = manifest.developerOverrides.Where(o => o.field.StartsWith("material:")).ToArray();
            if (materials.Length != 0)
            {
                path = manifest.artifacts.First(a => a.role.EndsWith(".prefab", StringComparison.Ordinal)).projectRelativePath;
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (SavicOverrideRecord o in materials)
                    {
                        string[] tokens = o.field.Split(':');
                        Renderer renderer = prefab.GetComponentsInChildren<Renderer>(true).SingleOrDefault(r => r.name == tokens[1]);
                        if (renderer == null || !GlobalObjectId.TryParse(o.value, out GlobalObjectId id) ||
                            !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is Material material))
                            throw new InvalidOperationException("GAME: protected material binding is missing.");
                        Material[] values = renderer.sharedMaterials;
                        int index = int.Parse(tokens[2]);
                        if (index >= values.Length) throw new InvalidOperationException("MODEL: protected material slot no longer exists.");
                        values[index] = material; renderer.sharedMaterials = values;
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
        }

        private static string PrepareJournal(SavicStorageLayout layout, SavicManifest previous, string savicId, string fingerprint)
        {
            string folder = Path.Combine(layout.SavicRoot, "Transactions", "Assets4All", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            SavicAssets4AllJournal journal = new SavicAssets4AllJournal { savicId = savicId, fingerprint = fingerprint,
                previousManifestJson = previous == null ? "" : JsonUtility.ToJson(previous) };
            string published = Path.Combine(layout.ProjectRoot, "Assets", "Generated", "BistroBuilder", "SAVIC", "Published");
            if (Directory.Exists(published))
                journal.assetPaths.AddRange(Directory.GetFiles(published, "*", SearchOption.AllDirectories).Select(layout.ToProjectRelativePath));
            // Existing publishers own the catalog; the journal protects those same assets.
            foreach (string catalog in Directory.GetFiles(Path.Combine(layout.ProjectRoot, "Assets"), "*Catalog*.asset", SearchOption.AllDirectories))
            {
                journal.assetPaths.Add(layout.ToProjectRelativePath(catalog));
                if (File.Exists(catalog + ".meta")) journal.assetPaths.Add(layout.ToProjectRelativePath(catalog + ".meta"));
            }
            journal.assetPaths = journal.assetPaths.Distinct().ToList();
            foreach (string relative in journal.assetPaths)
            {
                string backup = Path.Combine(folder, "assets", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(layout.FromProjectRelativePath(relative), backup);
            }
            SavicAtomicFile.WriteJson(Path.Combine(folder, "journal.json"), journal);
            return folder;
        }

        private static void FinishJournal(string folder) => File.WriteAllText(Path.Combine(folder, "complete"), "COMMITTED", new UTF8Encoding(false));

        internal static void Recover(SavicStorageLayout layout, SavicManifestRepository repository)
        {
            string root = Path.Combine(layout.SavicRoot, "Transactions", "Assets4All");
            if (!Directory.Exists(root)) return;
            foreach (string folder in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Combine(folder, "journal.json")) || File.Exists(Path.Combine(folder, "complete"))) continue;
                var journal = SavicAtomicFile.ReadJson<SavicAssets4AllJournal>(Path.Combine(folder, "journal.json"));
                if (repository.TryGetBySavicId(journal.savicId, out SavicManifest current) && current.status == "PUBLISHED" &&
                    current.assets4All?.delivery?.fingerprint == journal.fingerprint) FinishJournal(folder);
                else RestoreJournal(layout, repository, folder);
            }
        }

        private static void RestoreJournal(SavicStorageLayout layout, SavicManifestRepository repository, string folder)
        {
            var journal = SavicAtomicFile.ReadJson<SavicAssets4AllJournal>(Path.Combine(folder, "journal.json"));
            if (journal == null) return;
            string published = Path.Combine(layout.ProjectRoot, "Assets", "Generated", "BistroBuilder", "SAVIC", "Published");
            HashSet<string> baseline = new HashSet<string>(journal.assetPaths);
            if (Directory.Exists(published))
                foreach (string path in Directory.GetFiles(published, "*", SearchOption.AllDirectories))
                    if (!baseline.Contains(layout.ToProjectRelativePath(path))) File.Delete(path);
            foreach (string relative in journal.assetPaths)
            {
                if (!relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.Contains(".."))
                    throw new InvalidOperationException("Invalid transaction backup path.");
                File.Copy(Path.Combine(folder, "assets", relative), layout.FromProjectRelativePath(relative), true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            repository.Reload();
            File.WriteAllText(Path.Combine(folder, "complete"), "ROLLED_BACK", new UTF8Encoding(false));
        }
    }
}
