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
        private const string MaterialValidationId = "Materials.ReferenceIntegrity";

        private readonly SavicManifestRepository manifests;
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

                return new SavicSourceProcessingOutcome(
                    false,
                    manifest.status,
                    "No compatible source import adapter is available.",
                    manifest);
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

                return new SavicSourceProcessingOutcome(
                    false,
                    manifest.status,
                    import.Message,
                    manifest);
            }

            UpsertArtifact(
                manifest,
                SourceMirrorRole,
                import.AssetPath,
                SavicUnityModelSourceImportAdapter.BuilderId,
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            UpsertValidation(
                manifest,
                ArchiveValidationId,
                "PASS",
                "INFO",
                "Archived source passed SHA-256 integrity validation.",
                SavicUnityModelSourceImportAdapter.BuilderVersion);

            UpsertValidation(
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

                return new SavicSourceProcessingOutcome(
                    false,
                    manifest.status,
                    "Imported model main object is not a GameObject.",
                    manifest);
            }

            try
            {
                SavicModelAnalysisRecord analysis =
                    SavicModelAnalyzer.Analyze(root);

                manifest.model3D = analysis;

                bool meshValid =
                    analysis.uniqueMeshCount > 0 &&
                    analysis.vertexCount > 0;

                UpsertValidation(
                    manifest,
                    BoundsValidationId,
                    analysis.hasUsableBounds ? "PASS" : "FAIL",
                    analysis.hasUsableBounds ? "INFO" : "ERROR",
                    analysis.hasUsableBounds
                        ? "Model bounds are finite and usable."
                        : "Model bounds are missing, degenerate or non-finite.",
                    SavicModelAnalyzer.Version);

                UpsertValidation(
                    manifest,
                    MeshValidationId,
                    meshValid ? "PASS" : "FAIL",
                    meshValid ? "INFO" : "ERROR",
                    meshValid
                        ? "Model contains usable mesh geometry."
                        : "Model contains no usable mesh geometry.",
                    SavicModelAnalyzer.Version);

                UpsertValidation(
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

                bool analysisValid =
                    analysis.hasUsableBounds &&
                    meshValid;

                manifest.status =
                    analysisValid
                        ? "ANALYZED"
                        : "NEEDS_REVIEW";

                manifests.Save(manifest);

                return new SavicSourceProcessingOutcome(
                    analysisValid,
                    manifest.status,
                    analysisValid
                        ? "Model imported and analyzed."
                        : "Model imported but requires review.",
                    manifest);
            }
            catch (Exception exception)
            {
                RecordFailure(
                    manifest,
                    "Geometry.Analysis",
                    "ERROR",
                    "Model analysis failed: " + exception.Message);

                return new SavicSourceProcessingOutcome(
                    false,
                    manifest.status,
                    "Model analysis failed: " + exception.Message,
                    manifest);
            }
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
            UpsertValidation(
                manifest,
                validationId,
                "FAIL",
                severity,
                message,
                SavicVersion.ProductVersion);

            manifest.status = "FAILED_PROCESSING";
            manifests.Save(manifest);
        }

        private static void UpsertArtifact(
            SavicManifest manifest,
            string role,
            string projectRelativePath,
            string builderId,
            string builderVersion)
        {
            manifest.artifacts ??=
                new List<SavicArtifactRecord>();

            SavicArtifactRecord record = null;

            for (int index = 0;
                 index < manifest.artifacts.Count;
                 index++)
            {
                SavicArtifactRecord candidate =
                    manifest.artifacts[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.role,
                        role,
                        StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            if (record == null)
            {
                record = new SavicArtifactRecord
                {
                    role = role
                };

                manifest.artifacts.Add(record);
            }

            record.projectRelativePath =
                projectRelativePath ?? string.Empty;
            record.builderId =
                builderId ?? string.Empty;
            record.builderVersion =
                builderVersion ?? string.Empty;
        }

        private static void UpsertValidation(
            SavicManifest manifest,
            string validationId,
            string result,
            string severity,
            string message,
            string validatorVersion)
        {
            manifest.validations ??=
                new List<SavicValidationRecord>();

            SavicValidationRecord record = null;

            for (int index = 0;
                 index < manifest.validations.Count;
                 index++)
            {
                SavicValidationRecord candidate =
                    manifest.validations[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.validationId,
                        validationId,
                        StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            if (record == null)
            {
                record = new SavicValidationRecord
                {
                    validationId = validationId
                };

                manifest.validations.Add(record);
            }

            record.result = result ?? string.Empty;
            record.severity = severity ?? string.Empty;
            record.message = message ?? string.Empty;
            record.validatorVersion =
                validatorVersion ?? string.Empty;
        }
    }
}
