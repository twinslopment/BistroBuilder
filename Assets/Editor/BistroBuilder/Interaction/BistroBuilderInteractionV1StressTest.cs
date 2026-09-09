using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderInteractionV1StressTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Stress + determinism replay")]
    public static void Run()
    {
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BB INTERACTION v1 - STRESS / DETERMINISM\n");
        void Check(bool value, string label)
        {
            if (value) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        GameObject root = new GameObject("__BBInteractionStress__");
        BistroBuilderInteractionService service = root.AddComponent<BistroBuilderInteractionService>();
        service.SetEditorSimulationTime(0d);
        try
        {
            List<BistroBuilderInteractionAcquisitionRequest> requests = BuildRequests();
            for (int i = 0; i < requests.Count; i++)
                service.SubmitAcquisition(requests[i]);
            service.ResolveArbitrationEpoch();
            Dictionary<string, string> first = CaptureWinners(service, 100);
            Check(first.Count == 100 && service.TaskClaimCount == 100,
                "500 contendientes / 100 recursos -> 100 owners exactos");
            Check(service.ValidateRuntimeInvariants(out _),
                "Stress mantiene exclusividad e invariantes");

            service.ResetTransientRuntimeStateAfterLoad();
            for (int i = requests.Count - 1; i >= 0; i--)
                service.SubmitAcquisition(requests[i]);
            service.ResolveArbitrationEpoch();
            Dictionary<string, string> second = CaptureWinners(service, 100);
            Check(DictionaryEqual(first, second),
                "Replay inverso produce exactamente los mismos owners");

            int invalidated = service.InvalidateHolder(
                "actor:0000", BistroBuilderInteractionReasonCode.HolderInvalidated);
            Check(invalidated >= 0 && service.ValidateRuntimeInvariants(out _),
                "Invalidación masiva de holder no deja grants huérfanos");

            BistroBuilderInteractionCanonicalSnapshot snapshot =
                service.CaptureCanonicalSnapshot();
            Check(service.ValidateCanonicalSnapshot(snapshot, out _),
                "Snapshot de stress es canónico");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    private static List<BistroBuilderInteractionAcquisitionRequest> BuildRequests()
    {
        var requests = new List<BistroBuilderInteractionAcquisitionRequest>(500);
        var random = new System.Random(1729);
        for (int resource = 0; resource < 100; resource++)
        for (int contender = 0; contender < 5; contender++)
        {
            string holder = "actor:" + (resource * 5 + contender).ToString("D4");
            requests.Add(new BistroBuilderInteractionAcquisitionRequest
            {
                requestId = "stress:" + resource.ToString("D3") + ":" + contender,
                grantKind = BistroBuilderInteractionGrantKind.TaskClaim,
                holderId = holder,
                interactionId = "stress.claim",
                taskPriorityClass = random.Next(0, 5),
                candidates = new List<BistroBuilderInteractionCandidate>
                {
                    new BistroBuilderInteractionCandidate
                    {
                        resourceId = "resource:" + resource.ToString("D3"),
                        suitability = random.Next(0, 100),
                        travelCostHint = random.Next(0, 1000)
                    }
                }
            });
        }
        return requests;
    }
    private static Dictionary<string, string> CaptureWinners(
        BistroBuilderInteractionService service,
        int resourceCount)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < resourceCount; i++)
        {
            string resource = "resource:" + i.ToString("D3");
            var grants = service.GetGrantsForResource(resource);
            if (grants.Count == 1)
                result[resource] = grants[0].holderId;
        }
        return result;
    }

    private static bool DictionaryEqual(
        Dictionary<string, string> first,
        Dictionary<string, string> second)
    {
        if (first.Count != second.Count) return false;
        foreach (KeyValuePair<string, string> pair in first)
            if (!second.TryGetValue(pair.Key, out string value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
                return false;
        return true;
    }

    public static void RunFromCommandLine()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
