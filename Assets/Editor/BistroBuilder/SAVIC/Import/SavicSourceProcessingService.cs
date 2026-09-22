using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicSourceProcessingOutcome
    {
        internal SavicSourceProcessingOutcome(
            bool succeeded,
            string status,
            string message,
            SavicManifest manifest)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            Manifest = manifest;
        }

        internal bool Succeeded { get; }
        internal string Status { get; }
        internal string Message { get; }
        internal SavicManifest Manifest { get; }
    }

    internal sealed class SavicSourceProcessingService
    {
        private const string SourceMirrorRole = "unity.source_mirror";
        private const string ImportValidationId = "Source.UnityImport";
        private const string ArchiveValidationId = "Source.ArchiveIntegrity";
        private const string BoundsValidationId = "Geometry.ValidBounds";
        private const string MeshValidationId = "Geometry.MeshData";
        private const string GeometryProfileValidationId =
            "Geometry.StructuralProfile";
        private const string MaterialValidationId = "Materials.ReferenceIntegrity";
        private const string MaterialAppearanceValidationId =
            "Materials.AppearanceData";
        private const string MaterialSemanticValidationId =
            "Materials.SemanticProfile";

        private readonly SavicManifestRepository manifests;
        private readonly SavicTablePublisher tablePublisher;
        private readonly List<ISavicSourceImportAdapter> adapters =
            new List<ISavicSourceImportAdapter>();

        internal SavicSourceProcessingService(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));

            tablePublisher =
                new SavicTablePublisher(
                    layout,
                    this.manifests);

            adapters.Add(
                new SavicUnityModelSourceImportAdapter(layout));
        }

        internal SavicSourceProcessingOutcome ProcessBySavicId(
            string savicId)
        {
            if (!manifests.TryGetBySavicId(
                    savicId,
                    out SavicManifest manifest))
            {
                return new SavicSourceProcessingOutcome(
                    false,
                    "NOT_FOUND",
                    "No SAVIC manifest exists for the requested identity.",
                    null);
            }

            return Process(manifest);
        }

        internal SavicSourceProcessingOutcome Process(
            SavicManifest manifest)
        {
            if (manifest?.source == null)
            {
                return new SavicSourceProcessingOutcome(
                    false,
                    "INVALID_MANIFEST",
                    "Manifest or source record is missing.",
                    manifest);
            }

            SavicManifest previousPublishedSnapshot =
                IsPublished(
                    manifest)
                    ? CloneManifest(manifest)
                    : null;

            if (!string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.Model3D.ToString(),
                    StringComparison.Ordinal))
            {
                return new SavicSourceProcessingOutcome(
                    false,
                    "UNSUPPORTED_SOURCE_KIND",
                    "This processing service currently accepts only 3D models.",
                    manifest);
            }

            ISavicSourceImportAdapter adapter =
                ResolveAdapter(manifest);

            if (adapter == null)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    "No compatible source import adapter is available.");

                return ReturnFailure(
                    previousPublishedSnapshot,
                    manifest,
                    "No compatible source import adapter is available.");
            }

            SavicSourceImportResult import =
                adapter.Import(manifest);

            if (!import.Succeeded)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    import.Message);

                return ReturnFailure(
                    previousPublishedSnapshot,
                    manifest,
                    import.Message);
            }

            SavicManifestMutations.UpsertArtifact(
                manifest,
                SourceMirrorRole,
                import.AssetPath,
                SavicUnityModelSourceImportAdapter.BuilderId,
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            SavicManifestMutations.UpsertValidation(
                manifest,
                ArchiveValidationId,
                "PASS",
                "INFO",
                "Archived source passed SHA-256 integrity validation.",
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            SavicManifestMutations.UpsertValidation(
                manifest,
                ImportValidationId,
                "PASS",
                "INFO",
                import.Message,
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            if (import.MainObject is not GameObject root)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    "Imported model main object is not a GameObject.");

                return ReturnFailure(
                    previousPublishedSnapshot,
                    manifest,
                    "Imported model main object is not a GameObject.");
            }

            try
            {
                SavicModelAnalysisRecord analysis =
                    SavicModelAnalyzer.Analyze(root);

                manifest.model3D = analysis;

                bool meshValid =
                    analysis.uniqueMeshCount > 0 &&
                    analysis.vertexCount > 0;

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    BoundsValidationId,
                    analysis.hasUsableBounds ? "PASS" : "FAIL",
                    analysis.hasUsableBounds ? "INFO" : "ERROR",
                    analysis.hasUsableBounds
                        ? "Model bounds are finite and usable."
                        : "Model bounds are missing, degenerate or non-finite.",
                    SavicModelAnalyzer.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    MeshValidationId,
                    meshValid ? "PASS" : "FAIL",
                    meshValid ? "INFO" : "ERROR",
                    meshValid
                        ? "Model contains usable mesh geometry."
                        : "Model contains no usable mesh geometry.",
                    SavicModelAnalyzer.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    MaterialValidationId,
                    analysis.missingMaterialSlots == 0
                        ? "PASS"
                        : "WARNING",
                    analysis.missingMaterialSlots == 0
                        ? "INFO"
                        : "WARNING",
                    analysis.missingMaterialSlots == 0
                        ? "All renderer material slots have references."
                        : analysis.missingMaterialSlots +
                          " renderer material slot(s) are missing a material.",
                    SavicModelAnalyzer.Version);

                bool geometryProfileUsable =
                    analysis.geometry != null &&
                    analysis.geometry.analyzed &&
                    analysis.geometry.usable;

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    GeometryProfileValidationId,
                    geometryProfileUsable
                        ? "PASS"
                        : "WARNING",
                    geometryProfileUsable
                        ? "INFO"
                        : "WARNING",
                    geometryProfileUsable
                        ? "Mesh surface distribution analyzed successfully: " +
                          analysis.geometry.evidence
                        : "Mesh is usable but no reliable structural surface profile could be derived.",
                    SavicGeometryProfileAnalyzer.Version);

                bool appearanceDataPresent =
                    !string.Equals(
                        analysis.appearanceDataCompleteness,
                        "NONE",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        analysis.appearanceDataCompleteness,
                        "UNKNOWN",
                        StringComparison.Ordinal);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    MaterialAppearanceValidationId,
                    appearanceDataPresent
                        ? "PASS"
                        : "WARNING",
                    appearanceDataPresent
                        ? "INFO"
                        : "WARNING",
                    appearanceDataPresent
                        ? "Source appearance data detected: " +
                          analysis.appearanceDataCompleteness +
                          "."
                        : "Source contains no texture, vertex-color or non-default base-color appearance data; SAVIC will keep this explicit instead of inventing visual material detail.",
                    SavicModelAnalyzer.Version);

                bool analysisValid =
                    analysis.hasUsableBounds &&
                    meshValid;

                if (!analysisValid)
                {
                    manifest.status = "NEEDS_REVIEW";
                    manifests.Save(manifest);

                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        "Model imported but geometry requires review.");
                }

                SavicClassificationRecord classification =
                    SavicContentClassifier.Classify(manifest);

                manifest.classification = classification;
                manifest.family = classification.family;
                manifest.type = classification.type;
                manifest.category = classification.category;

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "content.type",
                    classification.type,
                    classification.confidence,
                    classification.evidence,
                    "content.classification.v2");

                SavicResolvedMaterialSemantic resolvedMaterial =
                    SavicMaterialSemanticResolver.Resolve(
                        manifest);

                manifest.materialSemantic =
                    new SavicMaterialSemanticResolutionRecord
                    {
                        resolved =
                            resolvedMaterial.IsKnown,
                        resolverVersion =
                            SavicMaterialSemanticResolver.Version,
                        semantic =
                            resolvedMaterial.Semantic,
                        confidence =
                            resolvedMaterial.Confidence,
                        score =
                            resolvedMaterial.Score,
                        source =
                            resolvedMaterial.Source,
                        evidence =
                            resolvedMaterial.Evidence,
                        resolvedUtc =
                            DateTime.UtcNow.ToString("O")
                    };

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "material.semantic",
                    resolvedMaterial.Semantic,
                    resolvedMaterial.Confidence,
                    resolvedMaterial.Evidence,
                    "material.semantic.resolve.v1");

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    MaterialSemanticValidationId,
                    resolvedMaterial.IsKnown
                        ? "PASS"
                        : "WARNING",
                    resolvedMaterial.IsKnown
                        ? "INFO"
                        : "WARNING",
                    resolvedMaterial.IsKnown
                        ? "Material semantic resolved as " +
                          resolvedMaterial.Semantic +
                          " from " +
                          resolvedMaterial.Source +
                          " with " +
                          resolvedMaterial.Confidence +
                          " confidence."
                        : "Material semantic remains Unknown: " +
                          resolvedMaterial.Evidence,
                    SavicMaterialSemanticResolver.Version);

                bool classifiedAsTable =
                    string.Equals(
                        classification.type,
                        "Table",
                        StringComparison.Ordinal);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Classification.ContentType",
                    classifiedAsTable ? "PASS" : "REVIEW",
                    classifiedAsTable ? "INFO" : "WARNING",
                    classifiedAsTable
                        ? "Model classified as Table with sufficient confidence."
                        : "Automatic classification is not strong enough for publication.",
                    SavicContentClassifier.Version);

                if (!classifiedAsTable)
                {
                    manifest.status = "NEEDS_REVIEW";
                    manifests.Save(manifest);

                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        "Model analyzed but content type requires review.");
                }

                if (!SavicTableAuthoringPlanner.TryPlan(
                        manifest,
                        out SavicTableAuthoringRecord plan,
                        out string planRejection))
                {
                    manifest.tableAuthoring = plan;
                    manifest.status = "NEEDS_REVIEW";

                    SavicManifestMutations.UpsertValidation(
                        manifest,
                        "Authoring.TablePlan",
                        "REVIEW",
                        "WARNING",
                        planRejection,
                        SavicTableAuthoringPlanner.Version);

                    manifests.Save(manifest);

                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        planRejection);
                }

                manifest.tableAuthoring = plan;
                manifest.status = "PLANNED";

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Authoring.TablePlan",
                    "PASS",
                    "INFO",
                    plan.planReason,
                    SavicTableAuthoringPlanner.Version);

                manifests.Save(manifest);

                SavicTablePublicationOutcome publication =
                    tablePublisher.Publish(
                        manifest,
                        root);

                if (!publication.Succeeded)
                {
                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        publication.Message);
                }

                return new SavicSourceProcessingOutcome(
                    true,
                    manifest.status,
                    publication.Message,
                    manifest);
            }
            catch (Exception exception)
            {
                RecordFailure(
                    manifest,
                    "Pipeline.Model3D",
                    "ERROR",
                    "Model processing failed: " + exception.Message);

                return ReturnFailure(
                    previousPublishedSnapshot,
                    manifest,
                    "Model processing failed: " + exception.Message);
            }
        }

        private SavicSourceProcessingOutcome ReturnFailure(
            SavicManifest previousPublishedSnapshot,
            SavicManifest currentManifest,
            string message)
        {
            if (previousPublishedSnapshot != null)
            {
                try
                {
                    manifests.Save(
                        previousPublishedSnapshot);

                    Debug.LogWarning(
                        "[SAVIC] Reprocessing failed safely; previous " +
                        "published version was preserved. " +
                        message);

                    return new SavicSourceProcessingOutcome(
                        false,
                        "PUBLISHED",
                        message +
                        " Previous published version preserved.",
                        previousPublishedSnapshot);
                }
                catch (Exception restoreException)
                {
                    Debug.LogError(
                        "[SAVIC] Failed to restore previous published " +
                        "manifest after reprocessing failure: " +
                        restoreException);
                }
            }

            return new SavicSourceProcessingOutcome(
                false,
                currentManifest?.status ?? "FAILED_PROCESSING",
                message,
                currentManifest);
        }

        private static bool IsPublished(
            SavicManifest manifest)
        {
            return manifest != null &&
                   string.Equals(
                       manifest.status,
                       "PUBLISHED",
                       StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(
                       manifest.canonicalContentId);
        }

        private static SavicManifest CloneManifest(
            SavicManifest manifest)
        {
            if (manifest == null)
                return null;

            string json =
                JsonUtility.ToJson(
                    manifest,
                    false);

            return JsonUtility.FromJson<SavicManifest>(
                json);
        }

        private ISavicSourceImportAdapter ResolveAdapter(
            SavicManifest manifest)
        {
            for (int index = 0; index < adapters.Count; index++)
            {
                ISavicSourceImportAdapter adapter = adapters[index];
                if (adapter != null && adapter.CanImport(manifest))
                    return adapter;
            }

            return null;
        }

        private void RecordFailure(
            SavicManifest manifest,
            string validationId,
            string severity,
            string message)
        {
            SavicManifestMutations.UpsertValidation(
                manifest,
                validationId,
                "FAIL",
                severity,
                message,
                SavicVersion.ProductVersion);

            manifest.status = "FAILED_PROCESSING";
            manifests.Save(manifest);
        }

    }
}
