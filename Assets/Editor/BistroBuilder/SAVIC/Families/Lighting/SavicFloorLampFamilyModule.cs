using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicFloorLampFamilyModule : ISavicModelFamilyModule
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicGenericPlaceablePublisher publisher;
        internal SavicFloorLampFamilyModule(SavicStorageLayout layout, SavicManifestRepository manifests)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
            publisher = new SavicGenericPlaceablePublisher(layout, manifests);
        }

        public string TypeId => "FloorLamp";

        public SavicModelFamilyProcessingOutcome Process(SavicManifest manifest, GameObject sourceModel)
        {
            SavicFloorLampProfile profile = SavicFloorLampAuthoringPlanner.GetOrCreateProfile();
            if (!SavicFloorLampAuthoringPlanner.TryPlan(manifest, sourceModel, profile,
                    out SavicFloorLampAuthoringRecord lightPlan, out string error, layout) ||
                !SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest,
                    out SavicGenericPlaceableAuthoringRecord placeablePlan, out _, out error, layout))
            {
                manifest.status = "NEEDS_REVIEW";
                SavicManifestMutations.UpsertValidation(manifest, "Authoring.FloorLamp", "REVIEW", "WARNING",
                    error, SavicFloorLampAuthoringPlanner.Version);
                manifests.Save(manifest);
                return SavicModelFamilyProcessingOutcome.Failure(error, "FLOOR_LAMP_AUTHORING_REVIEW");
            }
            manifest.floorLamp = lightPlan;
            manifest.genericPlaceable = placeablePlan;
            SavicManifestMutations.UpsertValidation(manifest, "Authoring.FloorLamp", "PASS", "INFO",
                lightPlan.evidence, SavicFloorLampAuthoringPlanner.Version);
            SavicManifestMutations.UpsertArtifact(manifest, "authoring.floor_lamp.profile",
                SavicFloorLampAuthoringPlanner.ProfilePath, "savic.floor-lamp-planner",
                SavicFloorLampAuthoringPlanner.Version, lightPlan.profileFingerprint);
            manifests.Save(manifest);
            SavicGenericPlaceablePublicationOutcome outcome = publisher.Publish(manifest, sourceModel);
            return outcome.Succeeded ? SavicModelFamilyProcessingOutcome.Success(outcome.Message)
                : SavicModelFamilyProcessingOutcome.Failure(outcome.Message, "FLOOR_LAMP_PUBLICATION_FAILED");
        }

        public SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(SavicManifest manifest, GameObject sourceModel) =>
            Process(manifest, sourceModel);
    }
}
