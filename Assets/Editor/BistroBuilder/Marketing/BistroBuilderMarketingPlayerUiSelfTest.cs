using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate aislado de contratos y política de la UI jugable de Marketing.
/// </summary>
public static class BistroBuilderMarketingPlayerUiSelfTest
{
    [MenuItem("Tools/Bistro Builder/Marketing/UI jugable - Autotest", false, 7260)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();

        BistroBuilderMarketingCampaignDefinition flyer = FindDefinition(
            seed,
            "marketing.local.flyers");
        BistroBuilderMarketingSnapshot empty =
            BistroBuilderMarketingEngine.CreateEmptySnapshot();
        BistroBuilderMarketingSnapshot active = null;
        bool created = flyer != null && BistroBuilderMarketingEngine.TryCreateCampaign(
            empty,
            flyer,
            4,
            string.Empty,
            "marketing_test_ui_1",
            "marketing_test_ui_expense_1",
            out active,
            out _);
        Check(
            created && active != null && active.campaigns.Count == 1,
            "El motor puede crear una campaña válida para la UI.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingSnapshot cancelledState = null;
        bool cancelled = created && BistroBuilderMarketingEngine.TryCancelCampaign(
            active,
            "marketing_test_ui_1",
            4,
            out cancelledState,
            out _);
        Check(
            cancelled && cancelledState != null && cancelledState.campaigns.Count == 0 &&
            active.campaigns.Count == 1,
            "Cancelar retira la campaña sin mutar el snapshot origen.",
            ref passed, ref failed, lines);
        BistroBuilderMarketingSnapshot ignoredState;
        Check(
            created && !BistroBuilderMarketingEngine.TryCancelCampaign(
                active,
                "marketing_missing_ui",
                4,
                out ignoredState,
                out _),
            "Cancelar una instancia inexistente se rechaza.",
            ref passed, ref failed, lines);
        Check(
            created && !BistroBuilderMarketingEngine.TryCancelCampaign(
                active,
                "marketing_test_ui_1",
                7,
                out ignoredState,
                out _),
            "Una campaña vencida no puede cancelarse como activa.",
            ref passed, ref failed, lines);

        string effects = flyer != null
            ? BistroBuilderMarketingPlayerFacade.BuildEffectsSummary(flyer.modifiers)
            : string.Empty;
        Check(
            effects.Contains("Demanda +6 %") && effects.Contains("Presión operativa +2 %"),
            "Presentation resume los efectos de campaña con magnitudes legibles.",
            ref passed, ref failed, lines);

        Check(
            typeof(BistroBuilderMarketingPlayerFacade).IsSubclassOf(typeof(MonoBehaviour)) &&
            typeof(BistroBuilderMarketingPlayerScreen).IsSubclassOf(typeof(MonoBehaviour)) &&
            typeof(BistroBuilderMarketingPlayerRowView).IsSubclassOf(typeof(MonoBehaviour)),
            "Fachada, pantalla y fila pertenecen a Presentation Unity.",
            ref passed, ref failed, lines);
        Check(
            !typeof(BistroBuilderMarketingPlayerUiSnapshot).IsSubclassOf(typeof(UnityEngine.Object)) &&
            !typeof(BistroBuilderMarketingPlayerCampaignRow).IsSubclassOf(typeof(UnityEngine.Object)),
            "Los modelos de UI son DTO de lectura y no Unity Objects autoritativos.",
            ref passed, ref failed, lines);
        Check(
            ScreenHasOnlyFacadeAsBusinessAuthority(),
            "La pantalla depende de la fachada y no de autoridades de dominio.",
            ref passed, ref failed, lines);
        Check(
            !ContainsForbiddenAuthority(typeof(BistroBuilderMarketingPlayerFacade)),
            "La fachada no referencia SaveGameService ni FinanceService directamente.",
            ref passed, ref failed, lines);

        CheckMethod("TryBuildSnapshot", typeof(BistroBuilderMarketingPlayerFacade),
            ref passed, ref failed, lines);
        CheckMethod("TryStartCampaign", typeof(BistroBuilderMarketingPlayerFacade),
            ref passed, ref failed, lines);
        CheckMethod("TryCancelCampaign", typeof(BistroBuilderMarketingPlayerFacade),
            ref passed, ref failed, lines);
        CheckMethod("Show", typeof(BistroBuilderMarketingPlayerScreen),
            ref passed, ref failed, lines);
        CheckMethod("Refresh", typeof(BistroBuilderMarketingPlayerScreen),
            ref passed, ref failed, lines);
        CheckMethod("StartSelectedCampaign", typeof(BistroBuilderMarketingPlayerScreen),
            ref passed, ref failed, lines);
        CheckMethod("CancelSelectedActive", typeof(BistroBuilderMarketingPlayerScreen),
            ref passed, ref failed, lines);

        bool guestOk = BistroBuilderMarketingGuestRelationsSelfTest.Run(
            out _, out int guestFailed, out _);
        Check(
            guestOk && guestFailed == 0,
            "El autotest GuestRelations permanece verde.",
            ref passed, ref failed, lines);
        bool pressureOk = BistroBuilderMarketingOperationalPressureSelfTest.Run(
            out _, out int pressureFailed, out _);
        Check(
            pressureOk && pressureFailed == 0,
            "El autotest OperationalPressure permanece verde.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — MARKETING / UI JUGABLE AUTOTEST ===\n" +
                 "Resultado: " + passed + " OK / " + failed + " fallos.\n" +
                 string.Join("\n", lines);
        return failed == 0;
    }

    private static BistroBuilderMarketingCampaignDefinition FindDefinition(
        List<BistroBuilderMarketingCampaignDefinition> definitions,
        string campaignId)
    {
        if (definitions == null) return null;
        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderMarketingCampaignDefinition definition = definitions[index];
            if (definition != null && string.Equals(
                    definition.campaignId,
                    campaignId,
                    StringComparison.Ordinal))
                return definition;
        }
        return null;
    }

    private static bool ScreenHasOnlyFacadeAsBusinessAuthority()
    {
        FieldInfo[] fields = typeof(BistroBuilderMarketingPlayerScreen).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        for (int index = 0; index < fields.Length; index++)
        {
            Type type = fields[index].FieldType;
            if (type == typeof(BistroBuilderMarketingService) ||
                type == typeof(BistroBuilderGuestRelationsService) ||
                type == typeof(BistroBuilderFinanceService) ||
                type == typeof(BistroBuilderSaveGameService))
                return false;
        }
        return true;
    }

    private static bool ContainsForbiddenAuthority(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        for (int index = 0; index < fields.Length; index++)
        {
            Type fieldType = fields[index].FieldType;
            string name = fieldType.FullName ?? fieldType.Name;
            if (name.IndexOf("SaveGameService", StringComparison.Ordinal) >= 0 ||
                name.IndexOf("FinanceService", StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static void CheckMethod(
        string methodName,
        Type type,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        bool exists = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public) != null;
        Check(exists,
            "Contrato público disponible: " + methodName + ".",
            ref passed, ref failed, lines);
    }
    private static void Check(
        bool condition,
        string description,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + description);
        }
        else
        {
            failed++;
            lines.Add("[ERROR] " + description);
        }
    }
}
