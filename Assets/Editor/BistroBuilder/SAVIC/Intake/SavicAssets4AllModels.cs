using System;
using System.Collections.Generic;

namespace BistroBuilder.Editor.Savic
{
    [Serializable] internal sealed class SavicAssets4AllDelivery
    {
        public string schemaId;
        public int schemaVersion;
        public string generatorVersion;
        public string assetUuid;
        public int revision;
        public string parentFingerprint;
        public string fingerprint;
        public string displayName;
        public string profile;
        public string modelSha256;
        public string manifestSha256;
        public string partGraphSha256;
        public int graphRevision;
        public int authorityGeneration;
        public string workGeometryHash;
        public string coordinateSystem;
        public SavicAssets4AllPart[] parts;
    }
    [Serializable] internal sealed class SavicAssets4AllPart
    {
        public string partKey;
        public string membershipDigest;
        public string role;
        public float confidence;
        public string provenance;
        public SavicAssets4AllNode[] nodes;
    }
    [Serializable] internal sealed class SavicAssets4AllNode
    {
        public string nodeName;
        public long triangleCount;
        public string sourceUid;
    }
    [Serializable] internal sealed class SavicAssets4AllRecord
    {
        public string packageRelativePath = "";
        public string deliveryHash = "";
        public SavicAssets4AllDelivery delivery;
        public string semanticName = "";
        public SavicClassificationRecord boundClassification;
        public List<SavicOverrideRecord> materialBaseline = new List<SavicOverrideRecord>();
    }
    [Serializable] internal sealed class SavicAssets4AllRevision
    {
        public SavicSourceRecord source;
        public SavicAssets4AllRecord assets4All;
    }
    [Serializable] internal sealed class SavicAssets4AllReceipt
    {
        public string assetUuid;
        public int revision;
        public string fingerprint;
        public string savicId;
        public string canonicalContentId;
        public string state;
        public string owner;
        public string reason;
        public string updatedUtc;
    }
    [Serializable] internal sealed class SavicAssets4AllJournal
    {
        public string fingerprint;
        public string savicId;
        public string previousManifestJson;
        public List<string> assetPaths = new List<string>();
    }
    [Serializable] internal sealed class SavicAssets4AllGraph { public SavicAssets4AllGraphNode[] nodes; public string workGeometryHash; }
    [Serializable] internal sealed class SavicAssets4AllGraphNode { public string partKey; public SavicAssets4AllMembership membership; public SavicAssets4AllGeometry geometry; }
    [Serializable] internal sealed class SavicAssets4AllMembership { public string membershipDigest; }
    [Serializable] internal sealed class SavicAssets4AllGeometry { public float areaFraction; }
    [Serializable] internal sealed class SavicAssets4AllGlb { public SavicAssets4AllGlbNode[] nodes; public SavicAssets4AllGlbMesh[] meshes; public SavicAssets4AllGlbAccessor[] accessors; }
    [Serializable] internal sealed class SavicAssets4AllGlbNode { public string name; public int mesh = -1; }
    [Serializable] internal sealed class SavicAssets4AllGlbMesh { public SavicAssets4AllGlbPrimitive[] primitives; }
    [Serializable] internal sealed class SavicAssets4AllGlbPrimitive { public int mode = 4; public int indices = -1; }
    [Serializable] internal sealed class SavicAssets4AllGlbAccessor { public long count; }
}
