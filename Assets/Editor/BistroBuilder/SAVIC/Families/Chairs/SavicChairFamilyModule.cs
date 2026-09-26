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

            bool semanticReady =
                semantic != null &&
                semantic.analyzed &&
                semantic.automationReady;

            SavicManifestMutations.UpsertValidation(
                manifest,
                "Analysis.ChairSemantic",
                semanticReady
                    ? "PASS"
                    : "REVIEW",
                semanticReady
                    ? "INFO"
                    : "WARNING",
                semanticReady
                    ? "Detailed chair semantic parts are automation-ready."
                    : "Detailed chair semantic parts are not automation-ready; publication may continue using geometry-backed functional authoring and colliders.",
                SavicChairSemanticPartAnalyzer.Version);

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
