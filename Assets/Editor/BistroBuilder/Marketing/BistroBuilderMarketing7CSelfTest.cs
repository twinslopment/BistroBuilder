using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro de 7C: marketing.state y atribución de captación dentro de
/// service.runtime sobreviven a serialización sin acoplar clientes a Marketing.
/// </summary>
public static class BistroBuilderMarketing7CSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/7C - Autotest persistencia",
        false,
        7310)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static bool RunFromCommandLine()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        if (!ok) throw new InvalidOperationException(report);
        return ok;
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        bool sevenBOk = BistroBuilderMarketing7BSelfTest.Run(
            out int sevenBPassed,
            out int sevenBFailed,
            out _);
        Check(
            sevenBOk && sevenBFailed == 0 && sevenBPassed >= 14,
            "7C conserva íntegro el gate funcional de 7B.",
            ref passed, ref failed, lines);

        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();
        BistroBuilderMarketingSnapshot active =
            BistroBuilderMarketingEngine.CreateEmptySnapshot();
        bool activated = BistroBuilderMarketingEngine.TryCreateCampaign(
            active,
            Find(seed, "marketing.digital.online_reservations"),
            3,
            string.Empty,
            "marketing_7c_persist",
            "marketing.expense.7c_persist",
            out active,
            out _);
        Check(
            activated && active != null && active.campaigns.Count == 1,
            "Una campaña activa queda disponible para persistencia.",
            ref passed, ref failed, lines);

        var saveData = new BistroBuilderMarketingSaveData
        {
            state = active,
            reservationLeadDay = 3,
            reservationLeadsGeneratedForDay = 1
        };
        Check(
            saveData.TryValidate(out _),
            "marketing.state valida campaña activa y contador de leads.",
            ref passed, ref failed, lines);

        string json = JsonUtility.ToJson(saveData);
        BistroBuilderMarketingSaveData restored =
            JsonUtility.FromJson<BistroBuilderMarketingSaveData>(json);
        Check(
            restored != null && restored.TryValidate(out _) &&
            restored.state.campaigns.Count == 1 &&
            restored.reservationLeadDay == 3 &&
            restored.reservationLeadsGeneratedForDay == 1,
            "marketing.state sobrevive a un round-trip JSON exacto.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingSaveData broken = saveData.DeepClone();
        broken.reservationLeadsGeneratedForDay = 4;
        Check(
            !broken.TryValidate(out _),
            "7C rechaza más leads persistidos que el límite V1.",
            ref passed, ref failed, lines);

        var arrival = new BistroBuilderCustomerArrivalPlanSaveRecord
        {
            groupSize = 2,
            serviceMode = (int)BistroBuilderServiceMode.TableService,
            acquisition = new BistroBuilderCustomerAcquisitionProfile
            {
                segmentId = "planners",
                sourceSystemId = "marketing.runtime",
                sourceReferenceId = "marketing.demand.day3.rev1",
                marketingInfluenced = true
            }
        };
        Check(
            arrival.TryValidate(out _),
            "Una llegada futura conserva un perfil de captación válido.",
            ref passed, ref failed, lines);

        string arrivalJson = JsonUtility.ToJson(arrival);
        BistroBuilderCustomerArrivalPlanSaveRecord restoredArrival =
            JsonUtility.FromJson<BistroBuilderCustomerArrivalPlanSaveRecord>(
                arrivalJson);
        Check(
            restoredArrival != null && restoredArrival.TryValidate(out _) &&
            restoredArrival.acquisition.marketingInfluenced &&
            restoredArrival.acquisition.segmentId == "planners",
            "La atribución de una llegada pendiente sobrevive a JSON.",
            ref passed, ref failed, lines);

        var group = new BistroBuilderCustomerGroupSaveRecord
        {
            groupId = 7,
            groupSize = 2,
            state = (int)CustomerGroupState.WaitingForTable,
            requestedServiceMode = (int)BistroBuilderServiceMode.TableService,
            currentServiceMode = (int)BistroBuilderServiceMode.TableService,
            waitingTime = 4f,
            assignedTableId = 0,
            worldPosition = new BistroBuilderSaveVector3(Vector3.zero),
            worldRotation = new BistroBuilderSaveQuaternion(Quaternion.identity),
            acquisition = arrival.acquisition.DeepClone()
        };
        Check(
            group.TryValidate(out _),
            "Un CustomerGroup activo acepta atribución persistente genérica.",
            ref passed, ref failed, lines);

        string groupJson = JsonUtility.ToJson(group);
        BistroBuilderCustomerGroupSaveRecord restoredGroup =
            JsonUtility.FromJson<BistroBuilderCustomerGroupSaveRecord>(groupJson);
        Check(
            restoredGroup != null && restoredGroup.TryValidate(out _) &&
            restoredGroup.acquisition.marketingInfluenced &&
            restoredGroup.acquisition.sourceSystemId == "marketing.runtime",
            "Un grupo ya materializado conserva su origen tras JSON.",
            ref passed, ref failed, lines);

        restoredArrival.acquisition = null;
        Check(
            restoredArrival.TryValidate(out _) &&
            restoredArrival.acquisition != null &&
            !restoredArrival.acquisition.marketingInfluenced,
            "Un save anterior sin atribución migra de forma segura a baseline.",
            ref passed, ref failed, lines);

        Check(
            BistroBuilderMarketingSaveSectionProvider.StableSectionId ==
                "marketing.state" &&
            BistroBuilderMarketingSaveSectionProvider.StableSectionVersion == 1,
            "La sección 7C usa identidad y versión estables.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — 7C / PERSISTENCIA MARKETING ===\n" +
                 string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderMarketingCampaignDefinition Find(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> seed,
        string id)
    {
        for (int index = 0; index < seed.Count; index++)
            if (BistroBuilderMarketingEngine.NormalizeId(seed[index].campaignId) ==
                BistroBuilderMarketingEngine.NormalizeId(id))
                return seed[index];
        return null;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + text);
        }
    }
}
