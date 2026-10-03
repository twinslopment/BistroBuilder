using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCompoundBodyGeometrySelfTest
    {
        public static void VerifyClosureAndAuditFromCommandLine()
        {
            SavicV1ClosureGate.RunFromMenu();
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] COMPOUND BODY CLOSURE AND INVENTORY - PASS: closure gate and fresh canonical inventory completed.");
        }

        public static void RunFromCommandLine()
        {
            List<GameObject> roots = new List<GameObject>();
            Mesh triangleMesh = null;
            try
            {
                GameObject open = MakeU(); roots.Add(open);
                SavicCompoundBodyGeometryRecord body = Analyze(open);
                Require(body.usable && body.hasAccessibleInterior && body.boxes.Count >= 3 &&
                    body.openingDirectionMetric == Vector3.back && body.interiorClearanceMeters > 1f && body.triangleCount == 36,
                    "Open U body lost its measured interior/access: " + body.evidence);
                CheckDecomposition(body, SavicModelAnalyzer.Analyze(open, SavicModelAnalysisMode.GenericStatic));
                GameObject closed = Object.Instantiate(open); roots.Add(closed);
                AddBox(closed, new Vector3(3f, 1f, 0.5f), new Vector3(0f, 0.5f, -1.75f));
                SavicCompoundBodyGeometryRecord enclosed = Analyze(closed);
                Require(enclosed.usable && !enclosed.hasAccessibleInterior,
                    "An enclosed hole was mistaken for accessible service space.");
                GameObject solid = new GameObject("Solid body"); roots.Add(solid);
                AddBox(solid, new Vector3(4f, 1f, 4f), new Vector3(0f, 0.5f, 0f));
                SavicCompoundBodyGeometryRecord solidBody = Analyze(solid);
                Require(solidBody.usable && !solidBody.hasAccessibleInterior && solidBody.boxes.Count == 1,
                    "A solid box invented an interior concavity.");
                GameObject metric = Object.Instantiate(open); roots.Add(metric);
                foreach (Transform child in metric.transform) { child.localPosition *= 100; child.localScale *= 100; }
                metric.transform.localScale = Vector3.one * 0.01f;
                metric.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                metric.transform.localPosition = new Vector3(21f, 12f, -30f);
                SavicCompoundBodyGeometryRecord converted = Analyze(metric);
                Require(converted.usable && converted.hasAccessibleInterior && converted.openingDirectionMetric == Vector3.left &&
                    Math.Abs(converted.interiorClearanceMeters - body.interiorClearanceMeters) < body.cellSizeMeters * 2f,
                    "Importer units/rotation or root translation changed the compound interior.");
                Require(JsonUtility.ToJson(body) == JsonUtility.ToJson(Analyze(open)), "Compound analysis is not repeatable.");
                SavicModelAnalysisRecord invalid = SavicModelAnalyzer.Analyze(open, SavicModelAnalysisMode.GenericStatic);
                invalid.widthMeters = float.NaN;
                Require(!SavicCompoundBodyGeometryAnalyzer.Analyze(open, invalid).usable, "Invalid bounds were accepted.");
                invalid = SavicModelAnalyzer.Analyze(open, SavicModelAnalysisMode.GenericStatic);
                invalid.triangleCount = 4000001;
                Require(!SavicCompoundBodyGeometryAnalyzer.Analyze(open, invalid).usable, "Unbounded triangle input was accepted.");
                GameObject diagonal = new GameObject("Diagonal projection"); roots.Add(diagonal);
                triangleMesh = new Mesh { vertices = new[] { Vector3.zero, new Vector3(4f, 0.1f, 0f), new Vector3(0f, 0f, 4f) },
                    triangles = new[] { 0, 1, 2 } };
                triangleMesh.RecalculateBounds();
                diagonal.AddComponent<MeshFilter>().sharedMesh = triangleMesh;
                diagonal.AddComponent<MeshRenderer>();
                SavicCompoundBodyGeometryRecord projected = Analyze(diagonal);
                Require(projected.usable && projected.occupiedCells < projected.columns * projected.rows * 0.65f &&
                    projected.occupiedCells > projected.columns * projected.rows * 0.45f,
                    "Triangle projection filled its bounding box or lost occupied surface.");
                Debug.Log("[SAVIC] COMPOUND BODY GEOMETRY SELF-TEST - PASS: open U, enclosed/solid negatives, exact raster decomposition, " +
                    "triangle projection, metric units/rotation, repeatability and budget. Geometry alone does not authorize publication.");
            }
            finally
            {
                foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
                if (triangleMesh != null) Object.DestroyImmediate(triangleMesh);
            }
        }

        public static void VerifyRealBarFromCommandLine()
        {
            RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            Report report = new Report { generatedUtc = DateTime.UtcNow.ToString("O") };
            foreach (SavicManifest manifest in context.Manifests.GetAll().Where(candidate => candidate?.status == "NEEDS_REVIEW" &&
                         candidate.classification?.type == "Unknown"))
            {
                string subject = SavicProviderMetadataService.ResolveSemanticName(manifest, context.Layout);
                if (!System.Text.RegularExpressions.Regex.IsMatch(subject, @"\bbar\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                string archive = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
                Require(context.Layout.ToProjectRelativePath(archive) == manifest.source.archivedRelativePath &&
                    HasHash(archive, manifest.source.sourceHash) && HasHash(mirror, manifest.source.sourceHash),
                    "Bar original or imported mirror failed SHA-256 verification.");
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                Require(source != null, "Verified bar is unavailable in Unity.");
                SavicModelAnalysisRecord model = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
                SavicCompoundBodyGeometryRecord geometry = SavicCompoundBodyGeometryAnalyzer.Analyze(source, model);
                if (geometry.usable) CheckDecomposition(geometry, model);
                report.entries.Add(new Entry { savicId = manifest.savicId, sourceHash = manifest.source.sourceHash,
                    subject = subject, sourceSizeMeters = new Vector3(model.widthMeters, model.heightMeters, model.depthMeters), geometry = geometry });
                if (geometry.occupancy != null) WriteGrid(geometry, Path.Combine(context.Layout.LogsRoot, "bar-compound-source-geometry-" + manifest.savicId + ".png"));
                Debug.Log("[SAVIC] Verified original bar compound geometry: " + manifest.savicId + ", " + geometry.evidence);
            }
            File.WriteAllText(Path.Combine(context.Layout.LogsRoot, "bar-compound-real-geometry.json"), JsonUtility.ToJson(report, true));
            Require(report.entries.Count > 0 && report.entries.All(entry => entry.geometry.usable && entry.geometry.hasAccessibleInterior),
                "Verified bar geometry lacks a bounded accessible compound body; inspect bar-compound-real-geometry.json.");
            Debug.Log("[SAVIC] REAL BAR COMPOUND GEOMETRY - PASS: " + report.entries.Count +
                " SHA-256 verified source(s), bounded body and accessible concavity measured; lifecycle preserved, functional authoring pending.");
        }

        private static void CheckDecomposition(SavicCompoundBodyGeometryRecord record, SavicModelAnalysisRecord model)
        {
            float minX = model.boundsCenterX - model.widthMeters * 0.5f;
            float minZ = model.boundsCenterZ - model.depthMeters * 0.5f;
            for (int z = 0; z < record.rows; z++)
            for (int x = 0; x < record.columns; x++)
            {
                Vector2 point = new Vector2(minX + Mathf.Min(model.widthMeters - 0.00001f, (x + 0.5f) * record.cellSizeMeters),
                    minZ + Mathf.Min(model.depthMeters - 0.00001f, (z + 0.5f) * record.cellSizeMeters));
                bool covered = record.boxes.Any(box => Mathf.Abs(point.x - box.centerMetric.x) < box.sizeMetric.x * 0.5f + 0.000001f &&
                    Mathf.Abs(point.y - box.centerMetric.z) < box.sizeMetric.y * 0.5f + 0.000001f);
                Require(covered == record.occupancy[z * record.columns + x], "Box decomposition changed the conservative occupied-cell union.");
            }
        }
        private static void WriteGrid(SavicCompoundBodyGeometryRecord record, string path)
        {
            Texture2D grid = new Texture2D(record.columns, record.rows, TextureFormat.RGBA32, false);
            try
            {
                for (int z = 0; z < record.rows; z++)
                for (int x = 0; x < record.columns; x++)
                    grid.SetPixel(x, z, record.occupancy[z * record.columns + x] ? Color.gray : Color.black);
                grid.Apply(); File.WriteAllBytes(path, grid.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(grid); }
        }
        private static SavicCompoundBodyGeometryRecord Analyze(GameObject root) =>
            SavicCompoundBodyGeometryAnalyzer.Analyze(root, SavicModelAnalyzer.Analyze(root, SavicModelAnalysisMode.GenericStatic));
        private static GameObject MakeU()
        {
            GameObject root = new GameObject("Synthetic open U body");
            AddBox(root, new Vector3(0.5f, 1f, 4f), new Vector3(-1.75f, 0.5f, 0f));
            AddBox(root, new Vector3(0.5f, 1f, 4f), new Vector3(1.75f, 0.5f, 0f));
            AddBox(root, new Vector3(3f, 1f, 0.5f), new Vector3(0f, 0.5f, 1.75f));
            return root;
        }
        private static void AddBox(GameObject root, Vector3 size, Vector3 position)
        {
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false); child.transform.localScale = size; child.transform.localPosition = position;
        }
        private static bool HasHash(string path, string hash) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), hash, StringComparison.OrdinalIgnoreCase);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [Serializable] private sealed class Report { public string generatedUtc; public List<Entry> entries = new List<Entry>(); }
        [Serializable] private sealed class Entry
        { public string savicId, sourceHash, subject; public Vector3 sourceSizeMeters; public SavicCompoundBodyGeometryRecord geometry; }
    }
}
