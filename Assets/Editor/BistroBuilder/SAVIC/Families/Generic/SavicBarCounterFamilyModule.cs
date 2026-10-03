using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicBarCounterFamilyModule : ISavicModelFamilyModule
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicGenericPlaceablePublisher publisher;
        internal SavicBarCounterFamilyModule(SavicStorageLayout layout, SavicManifestRepository manifests)
        { this.layout = layout; this.manifests = manifests; publisher = new SavicGenericPlaceablePublisher(layout, manifests); }
        public string TypeId => "BarCounter";
        public SavicModelFamilyProcessingOutcome Process(SavicManifest manifest, GameObject source)
        {
            if (!SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, SavicBarCounterAuthoringPlanner.GetOrCreateProfile(),
                    out var physical, out string error, layout)) return Review(manifest, error, "BAR_COUNTER_AUTHORING_REVIEW");
            manifest.barCounter = physical;
            if (!SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest, out var common, out _, out error, layout))
                return Review(manifest, error, "BAR_COUNTER_PLACEABLE_REVIEW");
            manifest.genericPlaceable = common;
            SavicManifestMutations.UpsertValidation(manifest, "Authoring.BarCounter", "PASS", "INFO", physical.evidence, SavicBarCounterAuthoringPlanner.Version);
            SavicManifestMutations.UpsertArtifact(manifest, "authoring.bar_counter.profile", SavicBarCounterAuthoringPlanner.ProfilePath,
                "savic.bar-counter-planner", SavicBarCounterAuthoringPlanner.Version, physical.profileFingerprint);
            manifests.Save(manifest);
            bool candidate = !SavicBarCounterRuntimeAcceptance.Matches(manifest, layout);
            var outcome = publisher.Publish(manifest, source, candidate);
            if (!outcome.Succeeded) return Review(manifest, outcome.Message, "BAR_COUNTER_CANDIDATE_FAILED");
            if (!candidate)
            {
                SavicManifestMutations.UpsertValidation(manifest, "Publication.BarCounterAcceptance", "PASS", "INFO",
                    "Current candidate passed canonical runtime acceptance and publication.", SavicBarCounterRuntimeAcceptance.Version);
                manifests.Save(manifest);
            }
            return candidate ? Review(manifest, "Candidate prepared; canonical creation/service/navigation/SaveGame acceptance is required before catalog publication.",
                "BAR_COUNTER_RUNTIME_ACCEPTANCE_PENDING") : SavicModelFamilyProcessingOutcome.Success(outcome.Message);
        }
        public SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(SavicManifest manifest, GameObject source) => Process(manifest, source);
        private SavicModelFamilyProcessingOutcome Review(SavicManifest manifest, string message, string reason)
        {
            manifest.status = "NEEDS_REVIEW";
            SavicManifestMutations.UpsertValidation(manifest, "Publication.BarCounterAcceptance", "REVIEW", "WARNING", message, SavicBarCounterRuntimeAcceptance.Version);
            manifests.Save(manifest); return SavicModelFamilyProcessingOutcome.Failure(message, reason);
        }
    }
}
