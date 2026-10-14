using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicChairFamilyModule :
        ISavicModelFamilyModule
    {
        private readonly SavicManifestRepository manifests;
        private readonly SavicChairPublisher publisher;

        internal SavicChairFamilyModule(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));

            publisher =
                new SavicChairPublisher(
                    layout,
                    this.manifests);
        }

        public string TypeId =>
            "Chair";

        public SavicModelFamilyProcessingOutcome Process(
            SavicManifest manifest,
            GameObject sourceModel)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModel == null)
                throw new ArgumentNullException(nameof(sourceModel));

            SavicSemanticPartAnalysisRecord semantic =
                manifest.model3D?.semanticParts;

            if (semantic == null ||
                !semantic.automationReady)
            {
                manifest.status =
                    "NEEDS_REVIEW";

                manifests.Save(
                    manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    "Chair classification passed, but semantic part structure requires review.",
                    "CHAIR_SEMANTIC_REVIEW");
            }

            if (!SavicChairAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicChairAuthoringRecord plan,
                    out string rejection))
            {
                manifest.chairAuthoring =
                    plan;

                manifest.status =
                    "NEEDS_REVIEW";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Authoring.ChairPlan",
                    "REVIEW",
                    "WARNING",
                    rejection,
                    SavicChairAuthoringPlanner.Version);

                manifests.Save(
                    manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    rejection,
                    "CHAIR_AUTHORING_REVIEW");
            }

            manifest.chairAuthoring =
                plan;

            manifest.status =
                "PLANNED";

            SavicManifestMutations.UpsertValidation(
                manifest,
                "Authoring.ChairPlan",
                "PASS",
                "INFO",
                plan.planReason,
                SavicChairAuthoringPlanner.Version);

            manifests.Save(
                manifest);

            SavicChairPublicationOutcome publication =
                publisher.Publish(
                    manifest,
                    sourceModel);

            return publication.Succeeded
                ? SavicModelFamilyProcessingOutcome.Success(
                    publication.Message)
                : SavicModelFamilyProcessingOutcome.Failure(
                    publication.Message,
                    "CHAIR_PUBLICATION_FAILED");
        }
        public SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModel)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModel == null)
                throw new ArgumentNullException(nameof(sourceModel));

            SavicChairPublicationOutcome publication =
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
