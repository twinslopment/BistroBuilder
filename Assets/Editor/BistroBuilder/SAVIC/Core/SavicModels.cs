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
        Quarantined = 5,
        Processing = 6,
        NeedsReview = 7,
        FailedProcessing = 8,
        Done = 9,
        Cancelled = 10
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
        public SavicIncrementalStateRecord incremental =
            new SavicIncrementalStateRecord();
        public SavicTableAuthoringRecord tableAuthoring =
            new SavicTableAuthoringRecord();
        public SavicTableColliderAuthoringRecord tableColliders =
            new SavicTableColliderAuthoringRecord();
        public SavicTableSpatialReadinessRecord tableSpatial =
            new SavicTableSpatialReadinessRecord();
        public SavicTableNavigationReadinessRecord tableNavigation =
            new SavicTableNavigationReadinessRecord();
        public SavicTablePersistenceReadinessRecord tablePersistence =
            new SavicTablePersistenceReadinessRecord();
        public SavicChairAuthoringRecord chairAuthoring =
            new SavicChairAuthoringRecord();
        public SavicChairColliderAuthoringRecord chairColliders =
            new SavicChairColliderAuthoringRecord();
        public SavicChairSpatialReadinessRecord chairSpatial =
            new SavicChairSpatialReadinessRecord();
        public SavicChairNavigationReadinessRecord chairNavigation =
            new SavicChairNavigationReadinessRecord();
        public SavicChairPersistenceReadinessRecord chairPersistence =
            new SavicChairPersistenceReadinessRecord();
        public SavicGenericPlaceableAuthoringRecord genericPlaceable =
            new SavicGenericPlaceableAuthoringRecord();
        public SavicGenericPlaceableReadinessRecord genericPlaceableReadiness =
            new SavicGenericPlaceableReadinessRecord();
        public List<SavicDecisionRecord> decisions = new List<SavicDecisionRecord>();
        public List<SavicArtifactRecord> artifacts = new List<SavicArtifactRecord>();
        public List<SavicValidationRecord> validations = new List<SavicValidationRecord>();
        public List<SavicOverrideRecord> developerOverrides = new List<SavicOverrideRecord>();
    }

    [Serializable]
    internal sealed class SavicIncrementalStateRecord
    {
        public string evaluatorVersion = string.Empty;
        public string geometryFingerprint = string.Empty;
        public string appearanceFingerprint = string.Empty;
        public string lastAction = "UNSET";
        public string reason = string.Empty;
        public bool reusedGeometry;
        public bool reusedSemanticParts;
        public bool reusedClassification;
        public bool reusedColliders;
        public bool appearanceOnlyRefresh;
        public int reuseCount;
        public int fullRebuildCount;
        public int appearanceRefreshCount;
        public string evaluatedUtc = string.Empty;
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
        public SavicChairGeometryProfileRecord chairGeometry =
            new SavicChairGeometryProfileRecord();
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
    internal sealed class SavicChairGeometryProfileRecord
    {
        public bool analyzed;
        public string analyzerVersion = string.Empty;
        public bool usable;
        public long sourceTriangleCount;
        public long sampledTriangleCount;
        public long invalidTriangleCount;
        public long degenerateTriangleCount;
        public int maximumSamplingStride = 1;
        public float seatHeight01;
        public float seatHeightMeters;
        public float seatUpwardAreaRatio;
        public float seatProjectedCoverage;
        public float upperVerticalAreaRatio;
        public float upperVerticalCentroidX01 = 0.5f;
        public float upperVerticalCentroidZ01 = 0.5f;
        public string backAxis = "UNKNOWN";
        public string backSide = "UNKNOWN";
        public float backEdgeBias;
        public float frontDirectionLocalX;
        public float frontDirectionLocalZ;
        public float lowerSupportAreaRatio;
        public float confidenceScore;
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
        public List<SavicSupportZoneRecord> zones =
            new List<SavicSupportZoneRecord>();
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicSupportZoneRecord
    {
        public string zoneId = string.Empty;
        public int pointCount;
        public float normalizedCenterX;
        public float normalizedCenterZ;
        public float normalizedSizeX;
        public float normalizedSizeZ;
        public float confidenceScore;
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicTableColliderAuthoringRecord
    {
        public bool generated;
        public string builderVersion = string.Empty;
        public string strategy = "UNSET";
        public int colliderCount;
        public int supportColliderCount;
        public bool semanticBacked;
        public string evidence = string.Empty;
        public string generatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicTableSpatialReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public string contractAssetPath = string.Empty;
        public string contractId = string.Empty;
        public string familyId = string.Empty;
        public string configurationId = string.Empty;
        public int seatBayPortCount;
        public int emittedSeatBayCount;
        public bool runtimeBindingValidated;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicTableNavigationReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public bool footprintBlocksNavigation;
        public float footprintWidthMeters;
        public float footprintDepthMeters;
        public float defaultAgentRadiusMeters;
        public float requiredEndpointClearanceMeters;
        public float customerEndpointClearanceMeters;
        public float waiterEndpointClearanceMeters;
        public int solidColliderCount;
        public bool usesCanonicalFootprintTopology;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicTablePersistenceReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public string sourceCatalogAssetPath = string.Empty;
        public string canonicalContentId = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string prefabAssetPath = string.Empty;
        public bool catalogResolvable;
        public bool prefabResolvable;
        public bool functionalTableIdValid;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicChairAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty;
        public float uniformScale = 1f;
        public float visualYawDegrees;
        public float finalWidthMeters;
        public float finalHeightMeters;
        public float finalDepthMeters;
        public float finalSeatHeightMeters;
        public float sourceFrontLocalX;
        public float sourceFrontLocalZ;
        public float canonicalFrontLocalX;
        public float canonicalFrontLocalZ = 1f;
        public int suggestedPurchasePriceEuro;
        public bool scaleCorrectionApplied;
        public string templatePrefabAssetPath = string.Empty;
        public string seatUseProfileAssetPath = string.Empty;
        public string editableDefinitionAssetPath = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string planReason = string.Empty;
        public string plannedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicChairColliderAuthoringRecord
    {
        public bool generated;
        public string builderVersion = string.Empty;
        public string strategy = "UNSET";
        public int colliderCount;
        public int seatColliderCount;
        public int backColliderCount;
        public int supportColliderCount;
        public int armColliderCount;
        public bool semanticBacked;
        public string evidence = string.Empty;
        public string generatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicChairSpatialReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public string contractAssetPath = string.Empty;
        public string contractId = string.Empty;
        public string familyId = string.Empty;
        public string configurationId = string.Empty;
        public bool runtimeBindingValidated;
        public int staticVolumeCount;
        public int operationalVolumeCount;
        public int dynamicVolumeCount;
        public int semanticVolumeCount;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicChairNavigationReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public bool footprintBlocksNavigation;
        public float footprintWidthMeters;
        public float footprintDepthMeters;
        public float seatHeightMeters;
        public float approachDistanceMeters;
        public float approachRadiusMeters;
        public int solidColliderCount;
        public bool canonicalFrontPositiveZ;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicChairPersistenceReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public string sourceCatalogAssetPath = string.Empty;
        public string canonicalContentId = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string prefabAssetPath = string.Empty;
        public bool catalogResolvable;
        public bool prefabResolvable;
        public bool seatComponentValid;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicGenericPlaceableAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty;
        public string placementMode = "UNSET";
        public string category = "Other";
        public float finalWidthMeters;
        public float finalHeightMeters;
        public float finalDepthMeters;
        public float rotationStepDegrees = 90f;
        public float minimumClearanceMeters;
        public int suggestedPurchasePriceEuro;
        public bool requiresFunctionalAdapter;
        public string prefabAssetPath = string.Empty;
        public string editableDefinitionAssetPath = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string planReason = string.Empty;
        public string plannedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class SavicGenericPlaceableReadinessRecord
    {
        public bool validated;
        public string validatorVersion = string.Empty;
        public bool floorPlacementReady;
        public bool colliderReady;
        public bool footprintReady;
        public bool catalogResolvable;
        public bool persistenceReady;
        public bool navigationReady;
        public bool spatialContractRequired;
        public string prefabAssetPath = string.Empty;
        public string itemDefinitionAssetPath = string.Empty;
        public string evidence = string.Empty;
        public string validatedUtc = string.Empty;
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
        public bool paused;
        public int schedulerGeneration;
        public int recoveredJobs;
        public string lastRecoveryUtc = string.Empty;
        public List<SavicJobRecord> jobs = new List<SavicJobRecord>();
    }

    [Serializable]
    internal sealed class SavicProcessingStageRecord
    {
        public string stageId = string.Empty;
        public string result = string.Empty;
        public long durationMilliseconds;
        public string detail = string.Empty;
    }

    [Serializable]
    internal sealed class SavicProcessingDiagnostics
    {
        public string traceVersion = string.Empty;
        public string reasonCode = string.Empty;
        public string primaryStage = string.Empty;
        public string summary = string.Empty;
        public long totalMilliseconds;
        public string completedUtc = string.Empty;
        public List<SavicProcessingStageRecord> stages =
            new List<SavicProcessingStageRecord>();
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
        public bool batchEligible;
        public bool cancelRequested;
        public string checkpoint = string.Empty;
        public string processingStartedUtc = string.Empty;
        public string completedUtc = string.Empty;
        public long lastDurationMilliseconds;
        public long maximumAtomicDurationMilliseconds;
        public bool sourcePrepared;
        public string outcomeStatus = string.Empty;
        public string reasonCode = string.Empty;
        public string primaryStage = string.Empty;
        public List<SavicProcessingStageRecord> stageTimings =
            new List<SavicProcessingStageRecord>();
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
