using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicConstructionFamilyModule :
        ISavicModelFamilyModule
    {
        private readonly string typeId;
        private readonly SavicManifestRepository manifests;
        private readonly SavicConstructionPublisher publisher;

        internal SavicConstructionFamilyModule(
            string typeId,
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("Construction type id is required.", nameof(typeId));

            this.typeId = typeId;
            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
            publisher =
                new SavicConstructionPublisher(
                    layout,
                    this.manifests);
        }

        public string TypeId => typeId;

        public SavicModelFamilyProcessingOutcome Process(
            SavicManifest manifest,
            GameObject sourceModel)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (sourceModel == null)
                throw new ArgumentNullException(nameof(sourceModel));

            bool planned =
                SavicConstructionAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicConstructionAuthoringRecord plan,
                    out string reasonCode,
                    out string planningError);

            manifest.construction = plan;

            if (!planned)
            {
                manifest.status = "NEEDS_REVIEW";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Construction.AuthoringPlan",
                    "REVIEW",
                    "WARNING",
                    planningError,
                    SavicConstructionAuthoringPlanner.Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "construction.role",
                    plan?.role ?? "UNSET",
                    "UNKNOWN",
                    planningError,
                    "construction.plan.v1");

                manifests.Save(manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    planningError,
                    string.IsNullOrWhiteSpace(reasonCode)
                        ? "CONSTRUCTION_REVIEW"
                        : reasonCode);
            }

            SavicManifestMutations.UpsertValidation(
                manifest,
                "Construction.AuthoringPlan",
                "PASS",
                "INFO",
                plan.planReason,
                SavicConstructionAuthoringPlanner.Version);

            SavicManifestMutations.UpsertDecision(
                manifest,
                "construction.role",
                plan.role,
                "HIGH",
                plan.planReason,
                "construction.plan.v1");

            SavicConstructionPublicationOutcome publication =
                publisher.Publish(
                    manifest,
                    sourceModel);

            return publication.Succeeded
                ? SavicModelFamilyProcessingOutcome.Success(publication.Message)
                : SavicModelFamilyProcessingOutcome.Failure(
                    publication.Message,
                    "CONSTRUCTION_PUBLICATION_FAILED");
        }

        public SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModel)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (sourceModel == null)
                throw new ArgumentNullException(nameof(sourceModel));

            bool planned =
                SavicConstructionAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicConstructionAuthoringRecord plan,
                    out string reasonCode,
                    out string planningError);

            manifest.construction = plan;

            if (!planned)
            {
                manifest.status = "NEEDS_REVIEW";
                manifests.Save(manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    planningError,
                    string.IsNullOrWhiteSpace(reasonCode)
                        ? "CONSTRUCTION_REVIEW"
                        : reasonCode);
            }

            SavicConstructionPublicationOutcome publication =
                publisher.RefreshAppearanceOnly(
                    manifest,
                    sourceModel);

            return publication.Succeeded
                ? SavicModelFamilyProcessingOutcome.Success(publication.Message)
                : SavicModelFamilyProcessingOutcome.Failure(
                    publication.Message,
                    "CONSTRUCTION_PUBLICATION_FAILED");
        }
    }
}
