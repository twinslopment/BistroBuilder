using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicTableFamilyModule :
        ISavicModelFamilyModule
    {
        private readonly SavicManifestRepository manifests;
        private readonly SavicTablePublisher publisher;

        internal SavicTableFamilyModule(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));

            publisher =
                new SavicTablePublisher(
                    layout,
                    this.manifests);
        }

        public string TypeId =>
            "Table";

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
                    "Table classification passed, but semantic part structure requires review.");
            }

            if (!SavicTableAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicTableAuthoringRecord plan,
                    out string rejection))
            {
                manifest.tableAuthoring =
                    plan;

                manifest.status =
                    "NEEDS_REVIEW";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Authoring.TablePlan",
                    "REVIEW",
                    "WARNING",
                    rejection,
                    SavicTableAuthoringPlanner.Version);

                manifests.Save(
                    manifest);

                return SavicModelFamilyProcessingOutcome.Failure(
                    rejection);
            }

            manifest.tableAuthoring =
                plan;

            manifest.status =
                "PLANNED";

            SavicManifestMutations.UpsertValidation(
                manifest,
                "Authoring.TablePlan",
                "PASS",
                "INFO",
                plan.planReason,
                SavicTableAuthoringPlanner.Version);

            manifests.Save(
                manifest);

            SavicTablePublicationOutcome publication =
                publisher.Publish(
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
