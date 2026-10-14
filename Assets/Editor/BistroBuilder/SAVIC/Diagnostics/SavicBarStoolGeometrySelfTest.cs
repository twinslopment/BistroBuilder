using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarStoolGeometrySelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Bar Stool Geometry Self-Test", false, 139)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            List<GameObject> roots = new List<GameObject>();
            try
            {
                GameObject backless = MakeStool(false, true); roots.Add(backless);
                SavicBarStoolGeometryRecord first = Analyze(backless);
                Require(first.usable && Math.Abs(first.seatHeightMeters - 0.8f) < 0.015f,
                    "Backless stool top was not found above its foot brace: " + first.evidence);
                GameObject backed = MakeStool(true, true); roots.Add(backed);
                SavicBarStoolGeometryRecord second = Analyze(backed);
                Require(second.usable && Math.Abs(second.seatHeightMeters - 0.8f) < 0.015f,
                    "Backed stool seat was confused with the narrow back top: " + second.evidence);
                Require(first.facingUsable && !first.hasBackrest && first.facingMetric == Vector3.forward &&
                    second.facingUsable && second.hasBackrest && Vector3.Dot(second.facingMetric, Vector3.forward) > 0.99f,
                    "Backless authoring convention or measured backed orientation was incorrect.");
                GameObject ambiguous = Object.Instantiate(backed); roots.Add(ambiguous);
                AddPart(ambiguous, new Vector3(0.50f, 0.40f, 0.03f), new Vector3(0, 1f, 0.235f));
                Require(Analyze(ambiguous).usable && !Analyze(ambiguous).facingUsable, "Symmetric upper structure fabricated a front.");
                VerifyPhysicalPlan(backed, backless, ambiguous);
                GameObject cushion = Object.Instantiate(backless); roots.Add(cushion);
                AddPart(cushion, new Vector3(0.23f, 0.01f, 0.23f), new Vector3(0, 0.835f, 0));
                SavicBarStoolGeometryRecord broad = Analyze(cushion);
                Require(broad.usable && Math.Abs(broad.seatHeightMeters - 0.8f) < 0.015f && broad.seatSizeMetric.z > 0.45f,
                    "A narrow cushion apex replaced the broad seating surface.");
                GameObject footrest = MakeStool(false, false); roots.Add(footrest);
                Require(!Analyze(footrest).usable, "Narrow foot brace was accepted as a seat.");
                GameObject unsupported = new GameObject("Unsupported seat"); roots.Add(unsupported);
                AddPart(unsupported, new Vector3(0.5f, 0.04f, 0.5f), new Vector3(0, 0.78f, 0));
                Require(!Analyze(unsupported).usable, "Unsupported isolated seat surface was accepted.");
                GameObject metric = Object.Instantiate(backed); roots.Add(metric);
                foreach (Transform child in metric.transform)
                { child.localPosition *= 100; child.localScale *= 100; }
                metric.transform.localScale = Vector3.one * 0.01f;
                metric.transform.localRotation = Quaternion.Euler(0, 90, 0);
                metric.transform.localPosition = new Vector3(23, 12, -30);
                SavicBarStoolGeometryRecord converted = Analyze(metric);
                Require(converted.usable && Math.Abs(converted.seatHeightMeters - second.seatHeightMeters) < 0.001f,
                    "Importer units or root placement changed stool measurements.");
                Require(converted.facingUsable && Vector3.Dot(converted.facingMetric, Vector3.right) > 0.99f,
                    "Importer rotation did not rotate the measured front.");
                Require(JsonUtility.ToJson(Analyze(backed)) == JsonUtility.ToJson(second), "Geometry analysis is not repeatable.");
                SavicModelAnalysisRecord invalid = SavicModelAnalyzer.Analyze(backed);
                invalid.heightMeters = float.NaN;
                Require(!SavicBarStoolGeometryAnalyzer.Analyze(backed, invalid).usable, "Invalid bounds were accepted.");
                Debug.Log("[SAVIC] BAR STOOL GEOMETRY SELF-TEST - PASS: backed/backless seat, cushion apex, foot brace and unsupported negatives, " +
                    "importer units, repeatability and invalid bounds. Geometry evidence does not authorize publication.");
            }
            finally { foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root); }
        }

        public static void VerifyRealFromCommandLine()
        {
            RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            Report report = new Report { generatedUtc = DateTime.UtcNow.ToString("O") };
            foreach (SavicManifest manifest in context.Manifests.GetAll().Where(candidate =>
                         candidate?.classification?.type == "BarStool" && candidate.status == "NEEDS_REVIEW"))
            {
                string archive = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
                Require(context.Layout.ToProjectRelativePath(archive) == manifest.source.archivedRelativePath &&
                    HasHash(archive, manifest.source.sourceHash) && HasHash(mirror, manifest.source.sourceHash),
                    "Bar stool original or imported mirror failed SHA-256 verification.");
                string subject = SavicProviderMetadataService.ResolveSemanticName(manifest, context.Layout);
                Require(subject.ToLowerInvariant().Contains("bar") && subject.ToLowerInvariant().Contains("stool"),
                    "Bar stool lacks explicit verified source identity.");
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                Require(source != null, "Imported bar stool is unavailable.");
                // Recompute model measurements from the verified source; stale derived fields are not authority.
                SavicModelAnalysisRecord model = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
                SavicBarStoolGeometryRecord geometry = SavicBarStoolGeometryAnalyzer.Analyze(source, model);
                var probe = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(manifest));
                probe.model3D = model;
                Require(SavicBarStoolAuthoringPlanner.TryPlan(probe, source, SavicBarStoolAuthoringPlanner.GetOrCreateProfile(),
                    out var physical, out string error), error);
                Require(SavicBarStoolAuthoringPlanner.TryPlan(probe, source, SavicBarStoolAuthoringPlanner.GetOrCreateProfile(),
                    out var repeated, out error) && repeated.inputFingerprint == physical.inputFingerprint,
                    "Real physical stool planning is not repeatable: " + error);
                VerifyRealVisual(source, model, physical);
                probe.barStool = physical;
                probe.genericPlaceable.requiresFunctionalAdapter = true;
                probe.genericPlaceable.integrationMode = SavicBarStoolFunctionAdapter.Mode;
                VerifyRealFunction(source, model, probe);
                report.entries.Add(new Entry
                {
                    savicId = manifest.savicId, sourceHash = manifest.source.sourceHash, subject = subject,
                    sourceSizeMeters = new Vector3(model.widthMeters, model.heightMeters, model.depthMeters), geometry = geometry, physicalPlan = physical
                });
                Debug.Log("[SAVIC] Verified original stool geometry: " + manifest.savicId + ", " + geometry.evidence + " " + geometry.facingEvidence);
                Debug.Log("[SAVIC] Real stool physical plan: seat=" + physical.seatHeightMeters + "m, scale=" + physical.uniformScale +
                    ", final=" + physical.finalSizeMeters + ", source-facing=" + physical.sourceFacingMetric + ", yaw=" + physical.visualYawDegrees);
            }
            File.WriteAllText(Path.Combine(context.Layout.LogsRoot, "bar-stool-real-geometry.json"), JsonUtility.ToJson(report, true));
            Require(report.entries.Count > 0 && report.entries.All(entry => entry.geometry.usable && entry.geometry.facingUsable),
                "One or more verified stools lack safe seating geometry; inspect bar-stool-real-geometry.json.");
            Debug.Log("[SAVIC] REAL BAR STOOL GEOMETRY - PASS: " + report.entries.Count +
                " verified originals measured. Publication and lifecycle states preserved; functional seating remains pending.");
        }

        private static void VerifyPhysicalPlan(GameObject backed, GameObject backless, GameObject ambiguous)
        {
            var profile = ScriptableObject.CreateInstance<SavicBarStoolProfile>();
            try
            {
                foreach (GameObject source in new[] { backed, backless })
                {
                    var manifest = PhysicalManifest(source);
                    Require(SavicBarStoolAuthoringPlanner.TryPlan(manifest, source, profile, out var plan, out string error), error);
                    Require(Math.Abs(plan.seatLocalPosition.y - profile.seatHeightMeters) < 0.001f &&
                        Math.Abs(plan.uniformScale - profile.seatHeightMeters / 0.8f) < 0.001f,
                        "Physical profile did not normalize the seat uniformly.");
                    string fingerprint = plan.inputFingerprint;
                    plan.seatLocalPosition.x += 0.01f;
                    Require(SavicBarStoolAuthoringPlanner.ComputeFingerprint(plan) != fingerprint, "Seat point changed without invalidating authoring.");
                    profile.counterHeightMeters = 1.1f;
                    Require(SavicBarStoolAuthoringPlanner.TryPlan(manifest, source, profile, out var revised, out error) &&
                        revised.inputFingerprint != fingerprint, "Changed bar profile did not invalidate authoring.");
                    profile.counterHeightMeters = 1.05f;
                    manifest.model3D.widthMeters += 0.02f;
                    Require(!SavicBarStoolAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _), "Stale stool geometry accepted.");
                }
                Require(!SavicBarStoolAuthoringPlanner.TryPlan(PhysicalManifest(ambiguous), ambiguous, profile, out _, out _),
                    "Ambiguous stool front was published in the plan.");
                profile.seatHeightMeters = float.NaN;
                Require(!SavicBarStoolAuthoringPlanner.TryPlan(PhysicalManifest(backed), backed, profile, out _, out _), "Invalid stool physical profile accepted.");
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static void VerifyRealVisual(GameObject source, SavicModelAnalysisRecord model, SavicBarStoolAuthoringRecord plan)
        {
            GameObject root = new GameObject("SAVIC Real Stool Normalization Probe");
            try
            {
                Transform visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                var imported = Object.Instantiate(source, visual, false);
                SavicGenericPlaceablePublisher.NormalizeSourceVisual(imported.transform, model);
                visual.localScale = Vector3.one * plan.uniformScale;
                visual.localRotation = Quaternion.Euler(0, plan.visualYawDegrees, 0);
                var bounds = root.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Require(Math.Abs(bounds.min.y) < 0.001f && Math.Abs(bounds.size.y - plan.finalSizeMeters.y) < 0.001f &&
                    bounds.size.x <= plan.finalSizeMeters.x + 0.001f && bounds.size.z <= plan.finalSizeMeters.z + 0.001f &&
                    Math.Abs(plan.seatLocalPosition.y - plan.seatHeightMeters) < 0.001f &&
                    Vector3.Dot(visual.localRotation * plan.sourceFacingMetric, Vector3.forward) > 0.999f,
                    "Actual normalized renderer, floor, seat point or front differs from the physical plan.");
                Debug.Log("[SAVIC] REAL STOOL NORMALIZED VISUAL - PASS: renderer inside physical envelope, floor minY=0, seat point height=" +
                    plan.seatLocalPosition.y + "m and measured/authored front aligned to +Z. Runtime seating remains pending.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyRealFunction(GameObject source, SavicModelAnalysisRecord model, SavicManifest manifest)
        {
            GameObject root = new GameObject("SAVIC Real Stool Function Authoring Probe");
            try
            {
                root.AddComponent<RestaurantPlaceableObject>();
                Transform visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                var imported = Object.Instantiate(source, visual, false);
                SavicGenericPlaceablePublisher.NormalizeSourceVisual(imported.transform, model);
                var adapter = new SavicBarStoolFunctionAdapter();
                adapter.Apply(root, manifest);
                Require(adapter.Validate(root, manifest, out string error), error);
                var binding = root.GetComponent<BistroBuilderBarSeatBinding>();
                Require(binding != null && !binding.IsSpatialLifecycleActive && binding.AttachedSpot == null &&
                    binding.Occupant == null && !root.GetComponent<BistroBuilderSpatialSubject>().IsRegistrationEligible,
                    "An authored but provisional stool acquired native occupancy or spatial eligibility.");
                adapter.Apply(root, manifest);
                Require(adapter.Validate(root, manifest, out error) && root.GetComponentsInChildren<Collider>(true).Length == 1 &&
                    root.GetComponents<BistroBuilderBarSeatBinding>().Length == 1, "Stool function authoring was not idempotent: " + error);
                Vector3 original = binding.ApproachFrame.localPosition;
                binding.ApproachFrame.localPosition = Vector3.zero;
                Require(!adapter.Validate(root, manifest, out _), "Function validator ignored a blocked approach.");
                binding.ApproachFrame.localPosition = original;
                var collider = root.GetComponent<BoxCollider>(); collider.size += new Vector3(0.02f, 0, 0);
                Require(!adapter.Validate(root, manifest, out _), "Function validator ignored changed physical geometry.");
                collider.size = manifest.barStool.finalSizeMeters;
                Require(adapter.Validate(root, manifest, out error), error);
                SavicRealBarSeatNativeProbe.Verify(root, manifest);
                Debug.Log("[SAVIC] REAL STOOL FUNCTION AUTHORING - PASS: normalized source, one body/collider, native seat/floor approach, " +
                    "provisional isolation, idempotence and tamper negatives. No publication, customer animation or persistence asserted.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static SavicManifest PhysicalManifest(GameObject source) => new SavicManifest {
            source = new SavicSourceRecord { sourceHash = new string('a', 64), sourceKind = SavicSourceKind.Model3D.ToString(), originalFileName = "bar_stool.glb" },
            classification = new SavicClassificationRecord { type = "BarStool", score = 0.95f },
            model3D = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic) };

        private static SavicBarStoolGeometryRecord Analyze(GameObject root) =>
            SavicBarStoolGeometryAnalyzer.Analyze(root, SavicModelAnalyzer.Analyze(root, SavicModelAnalysisMode.GenericStatic));
        private static GameObject MakeStool(bool back, bool seat)
        {
            GameObject root = new GameObject("Synthetic stool");
            foreach (float x in new[] { -0.20f, 0.20f })
                foreach (float z in new[] { -0.20f, 0.20f })
                    AddPart(root, new Vector3(0.035f, 0.78f, 0.035f), new Vector3(x, 0.39f, z));
            AddPart(root, new Vector3(0.43f, 0.03f, 0.025f), new Vector3(0, 0.60f, -0.20f));
            if (seat) AddPart(root, new Vector3(0.50f, 0.04f, 0.50f), new Vector3(0, 0.78f, 0));
            if (back) AddPart(root, new Vector3(0.50f, 0.40f, 0.03f), new Vector3(0, 1f, -0.235f));
            return root;
        }
        private static void AddPart(GameObject root, Vector3 size, Vector3 position)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(root.transform, false);
            part.transform.localScale = size; part.transform.localPosition = position;
        }
        private static bool HasHash(string path, string hash) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), hash, StringComparison.OrdinalIgnoreCase);
        private static void Require(bool success, string error) { if (!success) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        { public string generatedUtc; public List<Entry> entries = new List<Entry>(); }
        [Serializable] private sealed class Entry
        { public string savicId, sourceHash, subject; public Vector3 sourceSizeMeters; public SavicBarStoolGeometryRecord geometry; public SavicBarStoolAuthoringRecord physicalPlan; }
    }
}
