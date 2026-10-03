using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.Editor.Savic
{
    internal interface ISavicPlaceableFunctionAdapter
    {
        string IntegrationMode { get; }
        void Apply(GameObject root, SavicManifest manifest);
        bool Validate(GameObject root, SavicManifest manifest, out string error);
        string Fingerprint(SavicManifest manifest);
    }

    internal static class SavicPlaceableFunctionAdapters
    {
        private static readonly ISavicPlaceableFunctionAdapter[] Registered = {
            new SavicFloorLampFunctionAdapter(), new SavicBarCounterFunctionAdapter(), new SavicBarStoolFunctionAdapter(),
            new SavicOverheadEquipmentFunctionAdapter() };

        internal static ISavicPlaceableFunctionAdapter Resolve(SavicManifest manifest)
        {
            if (manifest?.genericPlaceable?.requiresFunctionalAdapter != true) return null;
            foreach (ISavicPlaceableFunctionAdapter adapter in Registered)
                if (adapter.IntegrationMode == manifest.genericPlaceable.integrationMode) return adapter;
            throw new InvalidOperationException("Required placeable function adapter is not registered.");
        }

        internal static void Apply(GameObject root, SavicManifest manifest) => Resolve(manifest)?.Apply(root, manifest);

        internal static bool Validate(GameObject root, SavicManifest manifest, out string error)
        {
            error = string.Empty;
            ISavicPlaceableFunctionAdapter adapter = Resolve(manifest);
            return adapter == null || adapter.Validate(root, manifest, out error);
        }
    }

    internal sealed class SavicFloorLampFunctionAdapter : ISavicPlaceableFunctionAdapter
    {
        internal const string Mode = "FLOOR_LIGHT";
        internal const string EmitterName = "SAVIC_LightEmitter";
        public string IntegrationMode => Mode;
        public string Fingerprint(SavicManifest manifest) => manifest.floorLamp?.inputFingerprint ?? string.Empty;

        public void Apply(GameObject root, SavicManifest manifest)
        {
            SavicFloorLampAuthoringRecord plan = manifest.floorLamp;
            if (!PlanMatches(manifest)) throw new InvalidOperationException("Verified floor-lamp authoring plan is absent or stale.");
            foreach (Light sourceLight in root.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(sourceLight);
            Transform emitter = root.transform.Find(EmitterName);
            if (emitter == null)
            {
                GameObject node = new GameObject(EmitterName);
                SceneManager.MoveGameObjectToScene(node, root.scene);
                node.transform.SetParent(root.transform, false);
                emitter = node.transform;
            }
            emitter.localPosition = plan.emitterLocalPosition;
            emitter.localRotation = Quaternion.identity;
            emitter.localScale = Vector3.one;
            Light light = emitter.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.intensity = plan.intensity;
            light.range = plan.rangeMeters;
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = plan.colorTemperatureKelvin;
            light.shadows = LightShadows.None;
            light.enabled = true;
        }

        public bool Validate(GameObject root, SavicManifest manifest, out string error)
        {
            error = "Floor-lamp emitter does not match its verified authoring plan.";
            if (root == null || !PlanMatches(manifest)) return false;
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            SavicFloorLampAuthoringRecord plan = manifest.floorLamp;
            if (lights.Length != 1) return false;
            Light light = lights[0];
            if (light.transform.parent != root.transform || light.name != EmitterName || !light.enabled ||
                !light.gameObject.activeSelf || light.type != LightType.Point ||
                light.lightmapBakeType != LightmapBakeType.Realtime || light.shadows != LightShadows.None ||
                (light.transform.localPosition - plan.emitterLocalPosition).sqrMagnitude > 0.000001f ||
                Math.Abs(light.intensity - plan.intensity) > 0.0001f || Math.Abs(light.range - plan.rangeMeters) > 0.0001f ||
                !light.useColorTemperature || Math.Abs(light.colorTemperature - plan.colorTemperatureKelvin) > 0.1f) return false;
            error = string.Empty;
            return true;
        }

        private static bool PlanMatches(SavicManifest manifest) => manifest?.floorLamp?.planned == true &&
            manifest.classification?.type == "FloorLamp" && manifest.floorLamp.plannerVersion == SavicFloorLampAuthoringPlanner.Version &&
            !string.IsNullOrWhiteSpace(manifest.floorLamp.inputFingerprint) &&
            manifest.floorLamp.inputFingerprint == SavicFloorLampAuthoringPlanner.ComputeFingerprint(manifest.floorLamp) &&
            manifest.floorLamp.sourceHash == manifest.source?.sourceHash &&
            manifest.floorLamp.providerMetadataHash == manifest.source?.providerMetadataHash;
    }
}
