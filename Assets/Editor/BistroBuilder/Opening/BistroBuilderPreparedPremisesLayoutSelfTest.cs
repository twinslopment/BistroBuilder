using System;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderPreparedPremisesLayoutSelfTest
{
    [MenuItem("Tools/Bistro Builder/Opening/Test Prepared Premises Layout", false, 16006)]
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 0.2f, 20f));

        Check(
            BistroBuilderPreparedPremisesLayout.TryBuild(
                BistroBuilderStartingPremisesProfile.Empty,
                bounds,
                new Vector3(-4f, 0f, -9f),
                out BistroBuilderEditDocument empty,
                out string emptyError),
            "Empty layout failed: " + emptyError);

        Check(empty.walls.Count == 0, "Empty must not author walls.");
        Check(empty.openings.Count == 0, "Empty must not author openings.");
        Check(empty.zones.Count == 0, "Empty must not author functional zones.");

        Check(
            BistroBuilderPreparedPremisesLayout.TryBuild(
                BistroBuilderStartingPremisesProfile.Essentials,
                bounds,
                new Vector3(-4f, 0f, -9f),
                out BistroBuilderEditDocument essentials,
                out string essentialsError),
            "Essentials layout failed: " + essentialsError);

        Check(essentials.walls.Count == 2, "Essentials requires kitchen divider + bathroom partition.");
        Check(essentials.openings.Count == 2, "Prepared layout requires two real door openings.");
        Check(essentials.zones.Count == 3, "Prepared layout requires dining, kitchen and bathroom zones.");
        Check(HasZone(essentials, "zone.dining"), "Prepared layout missing dining zone.");
        Check(HasZone(essentials, "zone.kitchen"), "Prepared layout missing kitchen zone.");
        Check(HasZone(essentials, "zone.bathroom"), "Prepared layout missing bathroom zone.");

        for (int i = 0; i < essentials.walls.Count; i++)
            Check(essentials.walls[i] != null && essentials.walls[i].Length >= 2f,
                "Prepared wall is invalid or too short.");

        for (int i = 0; i < essentials.openings.Count; i++)
        {
            BistroBuilderOpeningRecord opening = essentials.openings[i];
            Check(opening != null && essentials.FindWall(opening.hostWallId) != null,
                "Prepared opening is not hosted by a prepared wall.");
            Check(opening.width >= 0.9f && opening.height >= 2f,
                "Prepared doorway is not usable.");
        }

        Check(
            BistroBuilderPreparedPremisesLayout.TryBuild(
                BistroBuilderStartingPremisesProfile.FinishingTouches,
                bounds,
                new Vector3(-4f, 0f, -9f),
                out BistroBuilderEditDocument finishing,
                out string finishingError),
            "FinishingTouches layout failed: " + finishingError);

        Check(finishing.walls.Count == essentials.walls.Count,
            "Prepared profiles must share the same safe architectural shell.");
        Check(finishing.ComputeFingerprint() != empty.ComputeFingerprint(),
            "Prepared profile fingerprint must differ from Empty.");

        Check(
            BistroBuilderPreparedPremisesLayout.TryBuild(
                BistroBuilderStartingPremisesProfile.Essentials,
                bounds,
                new Vector3(-4f, 0f, -9f),
                out BistroBuilderEditDocument essentialsAgain,
                out _),
            "Second deterministic build failed.");

        Check(
            essentials.ComputeFingerprint() == essentialsAgain.ComputeFingerprint(),
            "Prepared architecture must be deterministic.");

        Debug.Log(
            "=== BISTRO BUILDER - PREPARED PREMISES LAYOUT ===\n" +
            "[PASS] " + checks +
            " checks. Empty stays architecturally blank; prepared profiles get deterministic " +
            "dining/kitchen/bathroom zoning, real partitions and hosted door openings.");
    }

    private static bool HasZone(
        BistroBuilderEditDocument document,
        string id)
    {
        for (int i = 0; i < document.zones.Count; i++)
        {
            BistroBuilderFunctionalZoneRecord zone = document.zones[i];
            if (zone != null &&
                string.Equals(zone.zoneDefinitionId, id, StringComparison.Ordinal))
                return zone.explicitRegion != null && zone.explicitRegion.Count >= 3;
        }
        return false;
    }
}
