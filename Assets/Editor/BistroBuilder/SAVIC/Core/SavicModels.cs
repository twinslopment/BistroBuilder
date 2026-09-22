using System;
using System.Collections.Generic;

namespace BistroBuilder.Editor.Savic
{
    internal enum SavicSourceKind
    {
        Unknown = 0,
        Model3D = 1,
        Image = 2,
        StructuredData = 3
    }

    internal enum SavicJobState
    {
        Waiting = 0,
        Hashing = 1,
        Ingested = 2,
        DuplicateExact = 3,
        FailedSource = 4,
        Quarantined = 5
    }

    [Serializable]
    internal sealed class SavicManifest
    {
        public int schemaVersion = SavicVersion.ManifestSchemaVersion;
        public string savicVersion = SavicVersion.ProductVersion;
        public string pipelineVersion = SavicVersion.PipelineVersion;
        public string savicId = string.Empty;
        public string canonicalContentId = string.Empty;
        public string status = "INGESTED";
        public string family = "Unknown";
        public string type = "Unknown";
        public string category = "Unknown";
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public SavicSourceRecord source = new SavicSourceRecord();
        public SavicModelAnalysisRecord model3D = new SavicModelAnalysisRecord();
        public List<SavicDecisionRecord> decisions = new List<SavicDecisionRecord>();
        public List<SavicArtifactRecord> artifacts = new List<SavicArtifactRecord>();
        public List<SavicValidationRecord> validations = new List<SavicValidationRecord>();
        public List<SavicOverrideRecord> developerOverrides = new List<SavicOverrideRecord>();
    }

    [Serializable]
    internal sealed class SavicSourceRecord
    {
        public string sourceHash = string.Empty;
        public string originalFileName = string.Empty;
        public string extension = string.Empty;
        public string sourceKind = SavicSourceKind.Unknown.ToString();
        public string archivedRelativePath = string.Empty;
        public long byteLength;
        public long originalLastWriteUtcTicks;
        public string ingestedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicModelAnalysisRecord
    {
        public bool analyzed;
        public string analyzerVersion = string.Empty;
        public bool hasUsableBounds;
        public float boundsCenterX;
        public float boundsCenterY;
        public float boundsCenterZ;
        public float widthMeters;
        public float heightMeters;
        public float depthMeters;
        public int rendererCount;
        public int meshInstanceCount;
        public int uniqueMeshCount;
        public long vertexCount;
        public long triangleCount;
        public int materialSlotCount;
        public int uniqueMaterialCount;
        public int missingMaterialSlots;
        public bool hasSkinnedMeshes;
        public bool hasNegativeScale;
        public string analyzedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicDecisionRecord
    {
        public string key = string.Empty;
        public string value = string.Empty;
        public string confidence = "UNKNOWN";
        public string evidence = string.Empty;
        public string ruleId = string.Empty;
    }

    [Serializable]
    internal sealed class SavicArtifactRecord
    {
        public string role = string.Empty;
        public string projectRelativePath = string.Empty;
        public string builderId = string.Empty;
        public string builderVersion = string.Empty;
    }

    [Serializable]
    internal sealed class SavicValidationRecord
    {
        public string validationId = string.Empty;
        public string result = string.Empty;
        public string severity = string.Empty;
        public string message = string.Empty;
        public string validatorVersion = string.Empty;
    }

    [Serializable]
    internal sealed class SavicOverrideRecord
    {
        public string field = string.Empty;
        public string value = string.Empty;
        public string reason = string.Empty;
        public string createdUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicQueueSnapshot
    {
        public int schemaVersion = SavicVersion.QueueSchemaVersion;
        public string savedUtc = string.Empty;
        public List<SavicJobRecord> jobs = new List<SavicJobRecord>();
    }

    [Serializable]
    internal sealed class SavicJobRecord
    {
        public string jobId = string.Empty;
        public string state = SavicJobState.Waiting.ToString();
        public string sourceHash = string.Empty;
        public string originalFileName = string.Empty;
        public string archivedRelativePath = string.Empty;
        public string manifestSavicId = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public int attempts;
        public string message = string.Empty;
    }

    internal readonly struct SavicIntakeOutcome
    {
        internal SavicIntakeOutcome(
            bool succeeded,
            bool duplicateExact,
            string sourceHash,
            string manifestSavicId,
            string message)
        {
            Succeeded = succeeded;
            DuplicateExact = duplicateExact;
            SourceHash = sourceHash ?? string.Empty;
            ManifestSavicId = manifestSavicId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool DuplicateExact { get; }
        internal string SourceHash { get; }
        internal string ManifestSavicId { get; }
        internal string Message { get; }
    }
}
