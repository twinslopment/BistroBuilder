using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    // Part membership is supplied by Assets4ALL. Game family and publication
    // remain SAVIC decisions; uncertain roles never become gameplay authority.
    internal static class SavicAssets4AllEvidence
    {
        internal static SavicSemanticPartAnalysisRecord Analyze(SavicManifest manifest, SavicStorageLayout layout, GameObject root)
        {
            if (!SavicAssets4AllService.TryReadVerified(manifest, layout, out var delivery))
                throw new InvalidOperationException("MODEL: missing verified delivery.");
            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var expected = delivery.parts.SelectMany(p => p.nodes).ToArray();
            if (filters.Length != expected.Length || root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("MODEL: imported surface count differs from PartGraph delivery.");
            foreach (var node in expected)
            {
                var matches = filters.Where(f => f.name == node.nodeName).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("MODEL: imported part is missing or duplicated: " + node.nodeName);
                long triangles = 0;
                var mesh = matches[0].sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                        throw new InvalidOperationException("MODEL: imported part is not triangular.");
                    triangles += mesh.GetIndexCount(i) / 3;
                }
                if (triangles != node.triangleCount)
                    throw new InvalidOperationException("MODEL: imported triangle membership differs: " + node.nodeName);
            }
            var result = new SavicSemanticPartAnalysisRecord { analyzed = true, analyzerVersion = "Assets4ALL/" + SavicAssets4AllService.Version,
                analyzedUtc = DateTime.UtcNow.ToString("O"), rawRegionCount = expected.Length,
                semanticPartCount = delivery.parts.Length, automationReady = false, semanticCoverage = 0f,
                evidence = "Verified Assets4ALL part identities and complete exported surface membership. Roles remain hypotheses; gameplay validators retain authority." };
            var graph = SavicAtomicFile.ReadJson<SavicAssets4AllGraph>(System.IO.Path.Combine(layout.FromProjectRelativePath(manifest.assets4All.packageRelativePath), "partgraph.json"));
            foreach (var part in delivery.parts)
            {
                bool first = true;
                Bounds bounds = default;
                foreach (var node in part.nodes)
                {
                    var filter = filters.Single(f => f.name == node.nodeName);
                    var matrix = SavicMetricSpace.LocalToMetric(root.transform, filter.transform);
                    var local = filter.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 v = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                        if (first) { bounds = new Bounds(v, Vector3.zero); first = false; } else bounds.Encapsulate(v);
                    }
                }
                long count = part.nodes.Sum(n => n.triangleCount);
                result.parts.Add(new SavicSemanticPartRecord { partId = part.partKey, role = "Unresolved",
                    confidence = "UNKNOWN", confidenceScore = part.confidence, triangleCount = count,
                    sourceRegionCount = part.nodes.Length, sourceRegionKeys = string.Join(";", part.nodes.Select(n => n.nodeName)),
                    areaFraction = graph.nodes.Single(n => n.partKey == part.partKey).geometry?.areaFraction ?? 0f, centerX = bounds.center.x, centerY = bounds.center.y, centerZ = bounds.center.z,
                    sizeX = bounds.size.x, sizeY = bounds.size.y, sizeZ = bounds.size.z,
                    evidence = "Assets4ALL membership=" + part.membershipDigest + "; hypothesis=" + part.role + "; provider=" + part.provenance });
            }
            result.unresolvedAreaRatio = 1f;
            SavicManifestMutations.UpsertValidation(manifest, "Assets4ALL.Delivery", "PASS", "INFO",
                "Archive hashes, source identity, imported node identities and triangle counts verified.", SavicAssets4AllService.Version);
            return result;
        }
    }
}
