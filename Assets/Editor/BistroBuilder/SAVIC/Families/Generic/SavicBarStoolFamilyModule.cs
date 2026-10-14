using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicBarStoolFamilyModule : ISavicModelFamilyModule
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicGenericPlaceablePublisher publisher;
        internal SavicBarStoolFamilyModule(SavicStorageLayout layout, SavicManifestRepository manifests)
        { this.layout = layout; this.manifests = manifests; publisher = new SavicGenericPlaceablePublisher(layout, manifests); }
        public string TypeId => "BarStool";
        public SavicModelFamilyProcessingOutcome Process(SavicManifest manifest, GameObject source)
            => ProcessInternal(manifest, source, false);
        internal SavicModelFamilyProcessingOutcome PrepareCandidate(SavicManifest manifest, GameObject source)
            => ProcessInternal(manifest, source, true);
        private SavicModelFamilyProcessingOutcome ProcessInternal(SavicManifest manifest, GameObject source, bool prepareOnly)
        {
            if (!SavicBarStoolAuthoringPlanner.TryPlan(manifest, source, SavicBarStoolAuthoringPlanner.GetOrCreateProfile(),
                out var physical, out string error)) return Review(manifest, error, "BAR_STOOL_AUTHORING_REVIEW");
            manifest.barStool = physical;
            if (!SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest, out var common, out _, out error, layout))
                return Review(manifest, error, "BAR_STOOL_PLACEABLE_REVIEW");
            manifest.genericPlaceable = common;
            SavicManifestMutations.UpsertValidation(manifest, "Authoring.BarStool", "PASS", "INFO",
                "Measured seat normalized to the canonical profile. " + physical.facingEvidence, SavicBarStoolAuthoringPlanner.Version);
            SavicManifestMutations.UpsertArtifact(manifest, "authoring.bar_stool.profile", SavicBarStoolAuthoringPlanner.ProfilePath,
                "savic.bar-stool-planner", SavicBarStoolAuthoringPlanner.Version, physical.profileFingerprint);
            manifests.Save(manifest);
            bool candidate = prepareOnly || !SavicBarStoolRuntimeAcceptance.Matches(manifest, layout);
            var result = publisher.Publish(manifest, source, candidate);
            if (!result.Succeeded) return Review(manifest, result.Message, "BAR_STOOL_CANDIDATE_FAILED");
            return candidate ? Review(manifest,
                "Candidate prepared. Current creation/association/navigation/leases/seated Animation/repeated SaveGame/cleanup/Console acceptance is required.",
                "BAR_STOOL_RUNTIME_ACCEPTANCE_PENDING") : SavicModelFamilyProcessingOutcome.Success(result.Message);
        }
        public SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(SavicManifest manifest, GameObject source) => Process(manifest, source);
        private SavicModelFamilyProcessingOutcome Review(SavicManifest manifest, string message, string code)
        {
            manifest.status = "NEEDS_REVIEW";
            SavicManifestMutations.UpsertValidation(manifest, "Publication.BarStoolAcceptance", "REVIEW", "WARNING", message, SavicBarStoolRuntimeAcceptance.Version);
            manifests.Save(manifest); return SavicModelFamilyProcessingOutcome.Failure(message, code);
        }
    }
}
