using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicGenericPlaceableFamilyModule :
        ISavicModelFamilyModule
    {
        private readonly string typeId;
        private readonly SavicManifestRepository manifests;
        private readonly SavicGenericPlaceablePublisher publisher;

        internal SavicGenericPlaceableFamilyModule(
            string typeId,
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("Type id is required.", nameof(typeId));

            this.typeId = typeId;
            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));

            publisher =
                new SavicGenericPlaceablePublisher(
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
                SavicGenericPlaceableAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicGenericPlaceableAuthoringRecord plan,
                    out string reasonCode,
                    out string planningError);

            manifest.genericPlaceable =
                plan;

            if (!planned)
            {
                manifest.status =
                    "NEEDS_REVIEW";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "GenericPlaceable.AuthoringPlan",
                    "REVIEW",
                    "WARNING",
                    planningError,
                    SavicGenericPlaceableAuthoringPlanner.Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "generic.placement",
                    plan?.placementMode ?? "UNSET",
                    "UNKNOWN",
                    planningError,
                    "generic.placeable.plan.v1");

                manifests.Save(
                    manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    planningError,
                    string.IsNullOrWhiteSpace(reasonCode)
                        ? "GENERIC_PLACEABLE_REVIEW"
                        : reasonCode);
            }

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.AuthoringPlan",
                "PASS",
                "INFO",
                plan.planReason,
                SavicGenericPlaceableAuthoringPlanner.Version);

            SavicManifestMutations.UpsertDecision(
                manifest,
                "generic.placement",
                plan.placementMode,
                "HIGH",
                plan.planReason,
                "generic.placeable.plan.v1");

            SavicGenericPlaceablePublicationOutcome publication =
                publisher.Publish(
                    manifest,
                    sourceModel);

            return publication.Succeeded
                ? SavicModelFamilyProcessingOutcome.Success(
                    publication.Message)
                : SavicModelFamilyProcessingOutcome.Failure(
                    publication.Message);
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
                SavicGenericPlaceableAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicGenericPlaceableAuthoringRecord plan,
                    out string reasonCode,
                    out string planningError);

            manifest.genericPlaceable =
                plan;

            if (!planned)
            {
                manifest.status =
                    "NEEDS_REVIEW";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "GenericPlaceable.AuthoringPlan",
                    "REVIEW",
                    "WARNING",
                    planningError,
                    SavicGenericPlaceableAuthoringPlanner.Version);

                manifests.Save(
                    manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    planningError,
                    string.IsNullOrWhiteSpace(reasonCode)
                        ? "GENERIC_PLACEABLE_REVIEW"
                        : reasonCode);
            }

            SavicGenericPlaceablePublicationOutcome publication =
                publisher.RefreshAppearanceOnly(
                    manifest,
                    sourceModel);

            return publication.Succeeded
                ? SavicModelFamilyProcessingOutcome.Success(
                    publication.Message)
                : SavicModelFamilyProcessingOutcome.Failure(
                    publication.Message);
        }
    }
}
