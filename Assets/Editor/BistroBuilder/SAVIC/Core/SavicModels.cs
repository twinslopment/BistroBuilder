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
        public SavicClassificationRecord classification =
            new SavicClassificationRecord();
        public SavicMaterialSemanticResolutionRecord materialSemantic =
            new SavicMaterialSemanticResolutionRecord();
        public SavicTableAuthoringRecord tableAuthoring =
            new SavicTableAuthoringRecord();
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
        public int texturedMaterialCount;
        public bool hasVertexColors;
        public bool hasUv0;
        public bool hasNonDefaultBaseColor;
        public string appearanceDataCompleteness = "UNKNOWN";
        public bool hasSkinnedMeshes;
        public bool hasNegativeScale;
        public SavicGeometryProfileRecord geometry =
            new SavicGeometryProfileRecord();
        public SavicSemanticPartAnalysisRecord semanticParts =
            new SavicSemanticPartAnalysisRecord();
        public string dominantMaterialSemantic = "Unknown";
        public string dominantMaterialConfidence = "UNKNOWN";
        public List<SavicMaterialAnalysisRecord> materials =
            new List<SavicMaterialAnalysisRecord>();
        public string analyzedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicGeometryProfileRecord
    {
        public bool analyzed;
        public string analyzerVersion = string.Empty;
        public bool usable;
        public int meshInstanceCount;
        public long sourceTriangleCount;
        public long sampledTriangleCount;
        public long invalidTriangleCount;
        public long degenerateTriangleCount;
        public int maximumSamplingStride = 1;
        public float estimatedSurfaceAreaSquareMeters;
        public float upwardFacingAreaRatio;
        public float horizontalAreaRatio;
        public float verticalAreaRatio;
        public float upperBandAreaRatio;
        public float lowerBandAreaRatio;
        public float surfaceAreaCentroidHeight01;
        public float upperUpwardProjectedCoverage;
        public float lowerHorizontalProjectedCoverage;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicSemanticPartAnalysisRecord
    {
        public bool analyzed;
        public string analyzerVersion = string.Empty;
        public bool automationReady;
        public int rawRegionCount;
        public string regionDetailMode = "FULL";
        public bool rawRegionDetailTruncated;
        public int semanticPartCount;
        public float semanticCoverage;
        public float unresolvedAreaRatio;
        public SavicSupportPatternRecord supportPattern =
            new SavicSupportPatternRecord();
        public List<SavicSemanticPartRecord> parts =
            new List<SavicSemanticPartRecord>();
        public List<SavicPartRelationRecord> relations =
            new List<SavicPartRelationRecord>();
        public string evidence = string.Empty;
        public string analyzedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicSemanticPartRecord
    {
        public string partId = string.Empty;
        public string role = "Unresolved";
        public string confidence = "UNKNOWN";
        public float confidenceScore;
        public bool syntheticZone;
        public bool movableCandidate;
        public int sourceRegionCount;
        public string sourceRegionKeys = string.Empty;
        public long triangleCount;
        public float areaFraction;
        public float centerX;
        public float centerY;
        public float centerZ;
        public float sizeX;
        public float sizeY;
        public float sizeZ;
        public float normalizedCenterX;
        public float normalizedCenterY;
        public float normalizedCenterZ;
        public float normalizedSizeX;
        public float normalizedSizeY;
        public float normalizedSizeZ;
        public float meanAbsoluteNormalX;
        public float meanAbsoluteNormalY;
        public float meanAbsoluteNormalZ;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicPartRelationRecord
    {
        public string sourcePartId = string.Empty;
        public string targetPartId = string.Empty;
        public string relation = string.Empty;
        public float confidence;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicSupportPatternRecord
    {
        public bool analyzed;
        public string mode = "UNASSESSED";
        public string confidence = "UNKNOWN";
        public float confidenceScore;
        public int zoneCount;
        public float supportSpanXRatio;
        public float supportSpanZRatio;
        public float supportPolygonAreaRatio;
        public bool centerSupported;
        public float broadBaseAreaRatio;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicMaterialAnalysisRecord
    {
        public string materialName = string.Empty;
        public string shaderName = string.Empty;
        public string semantic = "Unknown";
        public string semanticConfidence = "UNKNOWN";
        public float semanticScore;
        public float metallic;
        public float smoothness;
        public float baseColorR = 1f;
        public float baseColorG = 1f;
        public float baseColorB = 1f;
        public float baseColorA = 1f;
        public bool transparent;
        public int textureCount;
        public string textureNames = string.Empty;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicClassificationRecord
    {
        public bool classified;
        public string classifierVersion = string.Empty;
        public string family = "Unknown";
        public string type = "Unknown";
        public string category = "Unknown";
        public string confidence = "UNKNOWN";
        public float score;
        public bool explicitTypeToken;
        public bool nameBacked;
        public bool geometryBacked;
        public string evidence = string.Empty;
        public string classifiedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicMaterialSemanticResolutionRecord
    {
        public bool resolved;
        public string resolverVersion = string.Empty;
        public string semantic = "Unknown";
        public string confidence = "UNKNOWN";
        public float score;
        public string source = "NONE";
        public string evidence = string.Empty;
        public string resolvedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicTableAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty;
        public float uniformScale = 1f;
        public float visualYawDegrees;
        public float finalWidthMeters;
        public float finalHeightMeters;
        public float finalDepthMeters;
        public int capacity;
        public int suggestedPurchasePriceEuro;
        public bool scaleCorrectionApplied;
        public string templatePrefabAssetPath = string.Empty;
        public string seatingDefinitionAssetPath = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string planReason = string.Empty;
        public string plannedUtc = string.Empty;
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
        public string inputFingerprint = string.Empty;
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
