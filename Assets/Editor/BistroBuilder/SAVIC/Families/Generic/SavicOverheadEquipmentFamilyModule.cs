using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    // This is the specialized passive case of the registered KitchenEquipment
    // family, not a second family registration with a competing type ID.
    internal sealed class SavicOverheadEquipmentFamilyModule
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicGenericPlaceablePublisher publisher;
        internal SavicOverheadEquipmentFamilyModule(SavicStorageLayout layout, SavicManifestRepository manifests)
        { this.layout = layout; this.manifests = manifests; publisher = new SavicGenericPlaceablePublisher(layout, manifests); }
        internal SavicModelFamilyProcessingOutcome Process(SavicManifest manifest, GameObject source)
        {
            if (!SavicOverheadEquipmentAuthoringPlanner.TryPlan(manifest, source, SavicOverheadEquipmentAuthoringPlanner.GetOrCreateProfile(),
                    out var physical, out string error, layout)) return Review(manifest, error, "OVERHEAD_AUTHORING_REVIEW");
            manifest.overheadEquipment = physical;
            if (!SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest, out var common, out _, out error, layout))
                return Review(manifest, error, "OVERHEAD_COMMON_PLAN_REVIEW");
            manifest.genericPlaceable = common;
            SavicManifestMutations.UpsertValidation(manifest, "Authoring.OverheadEquipment", "PASS", "INFO", physical.evidence,
                SavicOverheadEquipmentAuthoringPlanner.Version);
            SavicManifestMutations.UpsertArtifact(manifest, "authoring.overhead.profile", SavicOverheadEquipmentAuthoringPlanner.ProfilePath,
                "savic.overhead-planner", SavicOverheadEquipmentAuthoringPlanner.Version, physical.profileFingerprint);
            manifests.Save(manifest);
            bool candidate = !SavicOverheadEquipmentRuntimeAcceptance.Matches(manifest, layout);
            var outcome = publisher.Publish(manifest, source, candidate);
            if (!outcome.Succeeded) return Review(manifest, outcome.Message, "OVERHEAD_CANDIDATE_FAILED");
            if (candidate) return Review(manifest, "Elevated passive candidate prepared; native creation/claims/routes/repeated SaveGame/cleanup acceptance required.",
                "OVERHEAD_RUNTIME_ACCEPTANCE_PENDING");
            SavicManifestMutations.UpsertValidation(manifest, "Publication.OverheadAcceptance", "PASS", "INFO",
                "Current elevated passive candidate passed canonical runtime acceptance and publication.", SavicOverheadEquipmentRuntimeAcceptance.Version);
            manifests.Save(manifest); return SavicModelFamilyProcessingOutcome.Success(outcome.Message);
        }
        private SavicModelFamilyProcessingOutcome Review(SavicManifest manifest, string error, string reason)
        {
            manifest.status = "NEEDS_REVIEW";
            SavicManifestMutations.UpsertValidation(manifest, "Publication.OverheadAcceptance", "REVIEW", "WARNING", error,
                SavicOverheadEquipmentRuntimeAcceptance.Version);
            manifests.Save(manifest); return SavicModelFamilyProcessingOutcome.Failure(error, reason);
        }
    }
}
