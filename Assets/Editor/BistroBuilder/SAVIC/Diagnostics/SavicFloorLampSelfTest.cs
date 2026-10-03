using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicFloorLampSelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Floor Lamp Self-Test", false, 137)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            GameObject source = null, box = null, authored = null, imported = null;
            SavicFloorLampProfile profile = ScriptableObject.CreateInstance<SavicFloorLampProfile>();
            try
            {
                source = new GameObject("Test Floor Lamp");
                AddPart(source, new Vector3(0.4f, 0.05f, 0.4f), new Vector3(0, 0.025f, 0));
                AddPart(source, new Vector3(0.025f, 1.45f, 0.025f), new Vector3(0, 0.75f, 0));
                AddPart(source, new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0, 1.6f, 0));
                SavicManifest manifest = MakeManifest(source, "contemporary_floor_lamp.glb");
                Require(SavicFloorLampAuthoringPlanner.TryPlan(manifest, source, profile,
                    out SavicFloorLampAuthoringRecord plan, out string error), error);
                Require(plan.emitterLocalPosition.y > manifest.model3D.heightMeters * 0.75f &&
                    plan.emitterLocalPosition.y < manifest.model3D.heightMeters,
                    "Emitter was not derived from the measured shade volume.");
                Require(SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest, out SavicGenericPlaceableAuthoringRecord floor,
                    out _, out error) && floor.category == "Lighting" && floor.requiresFunctionalAdapter &&
                    floor.integrationMode == SavicFloorLampFunctionAdapter.Mode && string.IsNullOrEmpty(floor.requiredAreaCapabilityId),
                    "Floor lamp did not retain its lighting, floor-placement and function contracts: " + error);
                manifest.floorLamp = plan;
                manifest.genericPlaceable = floor;
                authored = new GameObject("Test Authored Lamp");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Transform emitter = authored.transform.Find(SavicFloorLampFunctionAdapter.EmitterName);
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Require(authored.transform.Find(SavicFloorLampFunctionAdapter.EmitterName) == emitter &&
                    authored.GetComponentsInChildren<Light>(true).Length == 1 &&
                    SavicPlaceableFunctionAdapters.Validate(authored, manifest, out error), "Light authoring is not idempotent: " + error);
                Light light = authored.GetComponentInChildren<Light>();
                light.range += 0.2f;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Wrong light range was accepted.");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                authored.AddComponent<Light>();
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Duplicate emitter was accepted.");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                float validIntensity = plan.intensity;
                plan.intensity += 0.2f;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Changed authoring data retained a valid fingerprint.");
                plan.intensity = validIntensity;

                box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.localScale = new Vector3(0.4f, 1.8f, 0.4f);
                SavicManifest falseLamp = MakeManifest(box, "floor_lamp.glb");
                Require(!SavicFloorLampAuthoringPlanner.TryPlan(falseLamp, box, profile, out _, out _),
                    "A solid box was accepted as a floor lamp from its name alone.");
                manifest.source.originalFileName = "wall_lamp.glb";
                Require(!SavicFloorLampAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _),
                    "A wall lamp was accepted for floor placement.");
                manifest.source.originalFileName = "contemporary_floor_lamp.glb";
                profile.intensity = float.NaN;
                Require(!SavicFloorLampAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _),
                    "An invalid lighting profile was accepted.");
                profile.intensity = validIntensity;
                imported = Object.Instantiate(source);
                foreach (Transform child in imported.transform)
                {
                    child.localPosition *= 100;
                    child.localScale *= 100;
                }
                imported.transform.localScale = Vector3.one * 0.01f;
                imported.transform.localRotation = Quaternion.Euler(0, 90, 0);
                SavicManifest metric = MakeManifest(imported, "metric_floor_lamp.glb");
                Require(SavicFloorLampAuthoringPlanner.TryPlan(metric, imported, profile, out _, out error), error);
                SavicGenericPlaceablePublisher.NormalizeSourceVisual(imported.transform, metric.model3D);
                Bounds authoredBounds = imported.GetComponentsInChildren<Renderer>().Select(renderer => renderer.bounds)
                    .Aggregate((left, right) => { left.Encapsulate(right); return left; });
                Require(Math.Abs(authoredBounds.size.y - metric.model3D.heightMeters) < 0.001f &&
                    Math.Abs(imported.transform.localScale.x - 0.01f) < 0.000001f,
                    "Floor publication cancelled the importer's metric unit conversion.");
                double delta = VerifyLightEmission(manifest);
                Debug.Log("[SAVIC] FLOOR LAMP SELF-TEST - PASS: geometry bands, emitter position, lighting category, functional gate, " +
                    "idempotence, altered settings, duplicate light, box/wall/profile negatives; rendered illumination delta=" + delta.ToString("0.######") + ".");
            }
            finally
            {
                if (source != null) Object.DestroyImmediate(source);
                if (box != null) Object.DestroyImmediate(box);
                if (authored != null) Object.DestroyImmediate(authored);
                if (imported != null) Object.DestroyImmediate(imported);
                Object.DestroyImmediate(profile);
            }
        }

        public static void VerifyRegressionAndReconcileFromCommandLine()
        {
            SavicV1ClosureGate.RunFromMenu();
            VerifyRealAndReconcileFromCommandLine();
        }

        public static void VerifyRealAndReconcileFromCommandLine()
        {
            RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            int eligible = 0;
            foreach (SavicManifest manifest in context.Manifests.GetAll().Where(candidate =>
                         candidate != null && candidate.status == "NEEDS_REVIEW" && candidate.classification?.type == "FloorLamp"))
            {
                string archive = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
                Require(context.Layout.ToProjectRelativePath(archive) == manifest.source.archivedRelativePath &&
                    HasHash(archive, manifest.source.sourceHash) && HasHash(mirror, manifest.source.sourceHash),
                    "Real floor-lamp original or Unity mirror failed SHA-256 verification.");
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                Require(source != null, "Real floor-lamp Unity source could not be loaded.");
                Require(SavicFloorLampAuthoringPlanner.TryPlan(manifest, source,
                    SavicFloorLampAuthoringPlanner.GetOrCreateProfile(), out SavicFloorLampAuthoringRecord plan,
                    out string error, context.Layout), "Real floor-lamp planning failed.");
                // Use an isolated manifest clone for emission; the canonical queue owns publication.
                SavicManifest probe = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(manifest));
                probe.floorLamp = plan;
                Require(SavicGenericPlaceableAuthoringPlanner.TryPlan(probe, out SavicGenericPlaceableAuthoringRecord floor,
                    out _, out error, context.Layout), error);
                probe.genericPlaceable = floor;
                double delta = VerifyLightEmission(probe, context.Layout.LogsRoot);
                Debug.Log("[SAVIC] Verified real floor-lamp plan: " + manifest.savicId + ", emitter=" + plan.emitterLocalPosition +
                    ", rendered illumination delta=" + delta.ToString("0.######") + ".");
                eligible++;
            }
            Require(eligible > 0, "No verified floor-lamp review was available for this completion test.");
            SavicAutonomousClassificationAudit.ReconcileAndAuditFromCommandLine();
            Require(!context.Manifests.GetAll().Any(candidate => candidate?.classification?.type == "FloorLamp" && candidate.status != "PUBLISHED"),
                "A verified floor lamp was not published by the canonical queue.");
        }

        internal static double VerifyLightEmission(SavicManifest manifest, string outputRoot = null)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Floor-lamp emission requires a graphics device.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject lamp = null, floor = null, cameraNode = null;
            Material material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                lamp = new GameObject("Emission Test Lamp");
                SceneManager.MoveGameObjectToScene(lamp, scene);
                SavicPlaceableFunctionAdapters.Apply(lamp, manifest);
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.localScale = new Vector3(3, 0.03f, 3);
                floor.transform.position = new Vector3(0, -0.015f, 0);
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                Require(shader != null && shader.isSupported, "Emission receiver shader is unavailable.");
                material = new Material(shader);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0);
                floor.GetComponent<Renderer>().sharedMaterial = material;
                cameraNode = new GameObject("Emission Test Camera");
                SceneManager.MoveGameObjectToScene(cameraNode, scene);
                Camera camera = cameraNode.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1.8f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(3, 2, -3);
                camera.transform.LookAt(Vector3.zero);
                target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
                Light emitter = lamp.GetComponentInChildren<Light>();
                emitter.enabled = false;
                double dark = CaptureMean(camera, target, pixels);
                if (outputRoot != null) File.WriteAllBytes(Path.Combine(outputRoot, "floor-lamp-emission-off.png"), pixels.EncodeToPNG());
                emitter.enabled = true;
                double lit = CaptureMean(camera, target, pixels);
                if (outputRoot != null) File.WriteAllBytes(Path.Combine(outputRoot, "floor-lamp-emission-on.png"), pixels.EncodeToPNG());
                Require(lit - dark > 0.001, "Configured native light did not visibly illuminate the receiver: delta=" + (lit - dark));
                return lit - dark;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraNode != null) Object.DestroyImmediate(cameraNode);
                if (lamp != null) Object.DestroyImmediate(lamp);
                if (floor != null) Object.DestroyImmediate(floor);
                if (material != null) Object.DestroyImmediate(material);
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static double CaptureMean(Camera camera, RenderTexture target, Texture2D pixels)
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels.GetPixels().Average(color => (double)color.grayscale);
        }

        private static void AddPart(GameObject root, Vector3 scale, Vector3 position)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(root.transform, false);
            part.transform.localScale = scale;
            part.transform.localPosition = position;
        }

        private static SavicManifest MakeManifest(GameObject root, string name)
        {
            SavicManifest manifest = new SavicManifest
            {
                savicId = Guid.NewGuid().ToString("N"),
                source = new SavicSourceRecord { originalFileName = name, sourceHash = new string('a', 64), sourceKind = SavicSourceKind.Model3D.ToString() },
                model3D = SavicModelAnalyzer.Analyze(root)
            };
            manifest.classification = SavicContentClassifier.Classify(manifest);
            return manifest;
        }

        private static bool HasHash(string path, string hash) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), hash, StringComparison.OrdinalIgnoreCase);
        private static void Require(bool success, string error) { if (!success) throw new InvalidOperationException(error); }
    }
}
