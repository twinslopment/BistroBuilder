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
            SavicManifest manifest,
            SavicProcessingDiagnostics diagnostics = null)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            Manifest = manifest;
            Diagnostics =
                diagnostics ??
                new SavicProcessingDiagnostics();
        }

        internal bool Succeeded { get; }
        internal string Status { get; }
        internal string Message { get; }
        internal SavicManifest Manifest { get; }
        internal SavicProcessingDiagnostics Diagnostics { get; }
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
        private const string SemanticPartsValidationId =
            "Geometry.SemanticParts";
        private const string MaterialValidationId = "Materials.ReferenceIntegrity";
        private const string MaterialAppearanceValidationId =
            "Materials.AppearanceData";
        private const string MaterialSemanticValidationId =
            "Materials.SemanticProfile";

        private readonly SavicManifestRepository manifests;
        private readonly SavicModelFamilyRegistry familyRegistry;
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

            familyRegistry =
                new SavicModelFamilyRegistry(
                    new SavicTableFamilyModule(
                        layout,
                        this.manifests),
                    new SavicChairFamilyModule(
                        layout,
                        this.manifests),
                    new SavicGenericPlaceableFamilyModule(
                        "Decoration",
                        layout,
                        this.manifests),
                    new SavicGenericPlaceableFamilyModule(
                        "KitchenEquipment",
                        layout,
                        this.manifests),
                    new SavicGenericPlaceableFamilyModule(
                        "ServiceEquipment",
                        layout,
                        this.manifests));

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

        internal SavicSourceProcessingOutcome
            MaterializeSourceMirrorBySavicId(
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

            if (manifest?.source == null)
            {
                return new SavicSourceProcessingOutcome(
                    false,
                    "INVALID_MANIFEST",
                    "Manifest or source record is missing.",
                    manifest);
            }

            SavicProcessingTrace trace =
                new SavicProcessingTrace();

            ISavicSourceImportAdapter adapter =
                ResolveAdapter(
                    manifest);

            if (adapter == null)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    "No compatible source import adapter is available.");

                return ReturnFailure(
                    null,
                    manifest,
                    "No compatible source import adapter is available.",
                    "NO_IMPORT_ADAPTER",
                    "MATERIALIZE_SOURCE_MIRROR",
                    trace);
            }

            SavicSourceImportResult materialized =
                trace.Measure(
                    "MATERIALIZE_SOURCE_MIRROR",
                    () =>
                        adapter.Materialize(
                            manifest),
                    result =>
                        result.Succeeded,
                    result =>
                        result.Message);

            if (!materialized.Succeeded)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    materialized.Message);

                return ReturnFailure(
                    null,
                    manifest,
                    materialized.Message,
                    "SOURCE_MATERIALIZATION_FAILED",
                    "MATERIALIZE_SOURCE_MIRROR",
                    trace);
            }

            SavicManifestMutations.UpsertArtifact(
                manifest,
                SourceMirrorRole,
                materialized.AssetPath,
                SavicUnityModelSourceImportAdapter.BuilderId,
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            SavicManifestMutations.UpsertValidation(
                manifest,
                ArchiveValidationId,
                "PASS",
                "INFO",
                "Archived source passed integrity validation while materializing the Unity mirror.",
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            manifest.status =
                "SOURCE_MATERIALIZED";

            manifests.Save(
                manifest);

            return new SavicSourceProcessingOutcome(
                true,
                "SOURCE_MATERIALIZED",
                materialized.Message,
                manifest,
                trace.Finish(
                    "SOURCE_MATERIALIZED",
                    "MATERIALIZE_SOURCE_MIRROR",
                    materialized.Message));
        }

        internal SavicSourceProcessingOutcome
            PrepareSourceImportBySavicId(
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

            if (manifest?.source == null)
            {
                return new SavicSourceProcessingOutcome(
                    false,
                    "INVALID_MANIFEST",
                    "Manifest or source record is missing.",
                    manifest);
            }

            SavicProcessingTrace trace =
                new SavicProcessingTrace();

            ISavicSourceImportAdapter adapter =
                ResolveAdapter(
                    manifest);

            if (adapter == null)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    "No compatible source import adapter is available.");

                return ReturnFailure(
                    null,
                    manifest,
                    "No compatible source import adapter is available.",
                    "NO_IMPORT_ADAPTER",
                    "PREPARE_IMPORT_SOURCE",
                    trace);
            }

            SavicSourceImportResult import =
                trace.Measure(
                    "PREPARE_IMPORT_SOURCE",
                    () =>
                        adapter.ImportPrepared(
                            manifest),
                    result =>
                        result.Succeeded,
                    result =>
                        result.Message);

            if (!import.Succeeded)
            {
                RecordFailure(
                    manifest,
                    ImportValidationId,
                    "ERROR",
                    import.Message);

                return ReturnFailure(
                    null,
                    manifest,
                    import.Message,
                    "SOURCE_IMPORT_FAILED",
                    "PREPARE_IMPORT_SOURCE",
                    trace);
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
                "Prepared Unity source mirror is available for processing.",
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            SavicManifestMutations.UpsertValidation(
                manifest,
                ImportValidationId,
                "PASS",
                "INFO",
                import.Message,
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            manifest.status =
                "SOURCE_READY";

            manifests.Save(
                manifest);

            return new SavicSourceProcessingOutcome(
                true,
                "SOURCE_PREPARED",
                import.Message,
                manifest,
                trace.Finish(
                    "SOURCE_PREPARED",
                    "PREPARE_IMPORT_SOURCE",
                    import.Message));
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

            SavicProcessingTrace trace =
                new SavicProcessingTrace();

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
                    manifest,
                    trace.Finish(
                        "UNSUPPORTED_SOURCE_KIND",
                        "PRECHECK",
                        "Source kind is not eligible for the 3D pipeline."));
            }

            if (SavicGenericPlaceableAuthoringPlanner
                .TryResolvePreImportReview(
                    manifest.source.originalFileName,
                    out string routedType,
                    out string routedCategory,
                    out string routedReasonCode,
                    out string routedMessage))
            {
                SavicClassificationRecord routedClassification =
                    new SavicClassificationRecord
                    {
                        classified = true,
                        classifierVersion =
                            SavicContentClassifier.Version,
                        family = "Placeable",
                        type = routedType,
                        category = routedCategory,
                        confidence = "HIGH",
                        score = 0.95f,
                        explicitTypeToken = true,
                        nameBacked = true,
                        geometryBacked = false,
                        evidence = routedMessage,
                        classifiedUtc =
                            DateTime.UtcNow.ToString("O")
                    };

                manifest.classification =
                    routedClassification;

                manifest.family =
                    routedClassification.family;

                manifest.type =
                    routedClassification.type;

                manifest.category =
                    routedClassification.category;

                manifest.status =
                    "NEEDS_REVIEW";

                trace.RecordDecision(
                    "PREIMPORT_ROUTE",
                    routedMessage);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "content.type",
                    routedClassification.type,
                    routedClassification.confidence,
                    routedClassification.evidence,
                    "content.classification.preimport.v1");

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Pipeline.PreImportRouting",
                    "REVIEW",
                    "WARNING",
                    routedMessage,
                    SavicGenericPlaceableAuthoringPlanner.Version);

                manifests.Save(
                    manifest);

                return new SavicSourceProcessingOutcome(
                    false,
                    manifest.status,
                    routedMessage,
                    manifest,
                    trace.Finish(
                        routedReasonCode,
                        "PREIMPORT_ROUTE",
                        routedMessage));
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
                    "No compatible source import adapter is available.",
                    "NO_IMPORT_ADAPTER",
                    "IMPORT_SOURCE",
                    trace);
            }

            SavicSourceImportResult import =
                trace.Measure(
                    "IMPORT_SOURCE",
                    () => adapter.Import(manifest),
                    result => result.Succeeded,
                    result => result.Message);

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
                    import.Message,
                    "SOURCE_IMPORT_FAILED",
                    "IMPORT_SOURCE",
                    trace);
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
                    "Imported model main object is not a GameObject.",
                    "INVALID_IMPORTED_OBJECT",
                    "IMPORT_SOURCE",
                    trace);
            }

            try
            {
                bool genericStaticFastPath =
                    SavicGenericPlaceableAuthoringPlanner
                        .IsHighConfidenceStaticGenericCandidate(
                            manifest.source.originalFileName);

                SavicModelAnalysisMode analysisMode =
                    genericStaticFastPath
                        ? SavicModelAnalysisMode.GenericStatic
                        : SavicModelAnalysisMode.Full;

                SavicModelAnalysisRecord analysis =
                    trace.Measure(
                        "ANALYZE_GEOMETRY",
                        () =>
                            SavicModelAnalyzer.Analyze(
                                root,
                                analysisMode),
                        null,
                        _ =>
                            analysisMode ==
                            SavicModelAnalysisMode.GenericStatic
                                ? "GenericStatic fast path: detailed table/chair geometry profiling skipped."
                                : "Full furniture geometry analysis.");

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
                        "Model imported but geometry requires review.",
                        "GEOMETRY_REQUIRES_REVIEW",
                        "ANALYZE_GEOMETRY",
                        trace);
                }

                SavicIncrementalPlan incrementalPlan =
                    trace.Measure(
                        "INCREMENTAL_PLAN",
                        () =>
                            SavicIncrementalInvalidationService.Evaluate(
                                previousPublishedSnapshot,
                                manifest,
                                analysis));

                manifest.incremental =
                    SavicIncrementalInvalidationService.Stamp(
                        previousPublishedSnapshot?.incremental,
                        incrementalPlan);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Pipeline.IncrementalInvalidation",
                    "PASS",
                    "INFO",
                    incrementalPlan.Action +
                    ": " +
                    incrementalPlan.Reason,
                    SavicIncrementalInvalidationService.Version);

                bool canReuseClassification =
                    incrementalPlan.GeometryReusable &&
                    previousPublishedSnapshot?.classification != null &&
                    previousPublishedSnapshot.classification.classified &&
                    string.Equals(
                        previousPublishedSnapshot.classification.classifierVersion,
                        SavicContentClassifier.Version,
                        StringComparison.Ordinal);

                SavicClassificationRecord classification;

                if (canReuseClassification)
                {
                    classification =
                        previousPublishedSnapshot.classification;

                    trace.RecordReuse(
                        "CLASSIFICATION",
                        "Classification reused from compatible published baseline.");
                }
                else
                {
                    classification =
                        trace.Measure(
                            "CLASSIFICATION",
                            () => SavicContentClassifier.Classify(manifest));
                }

                manifest.classification = classification;
                manifest.family = classification.family;
                manifest.type = classification.type;
                manifest.category = classification.category;

                bool canReuseSemanticParts =
                    incrementalPlan.GeometryReusable &&
                    previousPublishedSnapshot?.model3D?.semanticParts != null &&
                    previousPublishedSnapshot.model3D.semanticParts.analyzed &&
                    string.Equals(
                        previousPublishedSnapshot.model3D.semanticParts.analyzerVersion,
                        SavicSemanticPartAnalyzer.Version,
                        StringComparison.Ordinal);

                SavicSemanticPartAnalysisRecord semanticParts;

                if (canReuseSemanticParts)
                {
                    semanticParts =
                        previousPublishedSnapshot.model3D.semanticParts;

                    trace.RecordReuse(
                        "SEMANTIC_PARTS",
                        "Semantic-part analysis reused from compatible published baseline.");
                }
                else
                {
                    semanticParts =
                        trace.Measure(
                            "SEMANTIC_PARTS",
                            () => SavicSemanticPartAnalyzer.Analyze(
                                root,
                                analysis,
                                classification));
                }

                analysis.semanticParts =
                    semanticParts;

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    SemanticPartsValidationId,
                    semanticParts.automationReady
                        ? "PASS"
                        : semanticParts.analyzed
                            ? "REVIEW"
                            : "WARNING",
                    semanticParts.automationReady
                        ? "INFO"
                        : "WARNING",
                    semanticParts.evidence,
                    SavicSemanticPartAnalyzer.Version);

                RecordSemanticPartDecisions(
                    manifest,
                    semanticParts);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "content.type",
                    classification.type,
                    classification.confidence,
                    classification.evidence,
                    "content.classification.v2");

                bool canReuseMaterialSemantic =
                    incrementalPlan.IsExactReuse &&
                    previousPublishedSnapshot?.materialSemantic != null &&
                    string.Equals(
                        previousPublishedSnapshot.materialSemantic.resolverVersion,
                        SavicMaterialSemanticResolver.Version,
                        StringComparison.Ordinal);

                SavicResolvedMaterialSemantic resolvedMaterial;

                if (canReuseMaterialSemantic)
                {
                    SavicMaterialSemanticResolutionRecord previousMaterial =
                        previousPublishedSnapshot.materialSemantic;

                    resolvedMaterial =
                        new SavicResolvedMaterialSemantic(
                            previousMaterial.semantic,
                            previousMaterial.confidence,
                            previousMaterial.score,
                            previousMaterial.evidence,
                            previousMaterial.source);

                    manifest.materialSemantic =
                        previousMaterial;

                    trace.RecordReuse(
                        "MATERIAL_SEMANTIC",
                        "Material semantic reused from compatible published baseline.");
                }
                else
                {
                    resolvedMaterial =
                        trace.Measure(
                            "MATERIAL_SEMANTIC",
                            () =>
                                SavicMaterialSemanticResolver.Resolve(
                                    manifest));

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
                }

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

                bool supportedForAutomaticPublication =
                    familyRegistry.TryResolve(
                        classification.type,
                        out ISavicModelFamilyModule familyModule);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Classification.ContentType",
                    supportedForAutomaticPublication
                        ? "PASS"
                        : "REVIEW",
                    supportedForAutomaticPublication
                        ? "INFO"
                        : "WARNING",
                    supportedForAutomaticPublication
                        ? "Model classified as " +
                          classification.type +
                          " and matched a registered SAVIC family module."
                        : "Automatic classification has no registered V1 publication family module.",
                    SavicContentClassifier.Version);

                if (!supportedForAutomaticPublication)
                {
                    manifest.status =
                        "NEEDS_REVIEW";

                    manifests.Save(
                        manifest);

                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        "Model analyzed but content type has no automatic publication module.",
                        "UNSUPPORTED_PUBLICATION_FAMILY",
                        "CLASSIFICATION",
                        trace);
                }

                SavicModelFamilyProcessingOutcome familyOutcome =
                    trace.Measure(
                        "FAMILY_PUBLICATION",
                        () =>
                            incrementalPlan.IsAppearanceOnly
                                ? familyModule.ProcessAppearanceOnly(
                                    manifest,
                                    root)
                                : familyModule.Process(
                                    manifest,
                                    root),
                        result => result.Succeeded,
                        result => result.Message);

                if (!familyOutcome.Succeeded)
                {
                    return ReturnFailure(
                        previousPublishedSnapshot,
                        manifest,
                        familyOutcome.Message,
                        familyOutcome.ReasonCode,
                        "FAMILY_PUBLICATION",
                        trace);
                }

                return new SavicSourceProcessingOutcome(
                    true,
                    manifest.status,
                    familyOutcome.Message,
                    manifest,
                    trace.Finish(
                        "PUBLISHED",
                        "FAMILY_PUBLICATION",
                        familyOutcome.Message));
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
                    "Model processing failed: " + exception.Message,
                    "PIPELINE_EXCEPTION",
                    trace.LastStageId,
                    trace);
            }
        }

        private static void RecordSemanticPartDecisions(
            SavicManifest manifest,
            SavicSemanticPartAnalysisRecord semanticParts)
        {
            if (manifest == null ||
                semanticParts == null)
            {
                return;
            }

            if (semanticParts.parts != null)
            {
                for (int index = 0;
                     index < semanticParts.parts.Count;
                     index++)
                {
                    SavicSemanticPartRecord part =
                        semanticParts.parts[index];

                    if (part == null ||
                        string.IsNullOrWhiteSpace(part.partId))
                    {
                        continue;
                    }

                    SavicManifestMutations.UpsertDecision(
                        manifest,
                        "semantic.part." +
                        part.partId,
                        part.role,
                        part.confidence,
                        part.evidence,
                        "semantic.parts.v1");
                }
            }

            SavicSupportPatternRecord support =
                semanticParts.supportPattern;

            if (support != null &&
                support.analyzed)
            {
                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "semantic.support.pattern",
                    support.mode,
                    support.confidence,
                    support.evidence,
                    "semantic.parts.v1");
            }
        }

        private SavicSourceProcessingOutcome ReturnFailure(
            SavicManifest previousPublishedSnapshot,
            SavicManifest currentManifest,
            string message,
            string reasonCode,
            string primaryStage,
            SavicProcessingTrace trace)
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
                        previousPublishedSnapshot,
                        trace?.Finish(
                            reasonCode,
                            primaryStage,
                            message) ??
                        new SavicProcessingDiagnostics());
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
                currentManifest,
                trace?.Finish(
                    reasonCode,
                    primaryStage,
                    message) ??
                new SavicProcessingDiagnostics());
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
