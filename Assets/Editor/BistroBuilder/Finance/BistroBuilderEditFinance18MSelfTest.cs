using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditFinance18MSelfTest
{
    [MenuItem("Bistro Builder/Finance/Run 18M Tariff Self Test")]
    public static void RunMenu()
    {
        string report = Run(out int passed, out int failed);
        if (failed == 0) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            string report = Run(out _, out int failed);
            Debug.Log(report);
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
    public static string Run(out int passed, out int failed)
    {
        passed = 0; failed = 0;
        var report = new StringBuilder("18M Finance tariff tests (explicit test fixtures only)\n");
        var table = ScriptableObject.CreateInstance<BistroBuilderEditFinanceTariffTable>();
        var host = new GameObject("BB18M_FinanceTariffSelfTest");
        try
        {
            var gateway = host.AddComponent<BistroBuilderEditFinanceGateway>();
            Check(!gateway.ValidateTariffAuthority(out _), "Missing Finance authority stays blocked",
                ref passed, ref failed, report);
            Check(!table.ValidateConfiguration(out _), "Empty table is not a free construction policy",
                ref passed, ref failed, report);
            Configure(table, "fixture.door", BistroBuilderEditFinanceRateUnit.Quantity,
                BistroBuilderEditFinancePricedChanges.Added, 101L);
            Check(table.ValidateConfiguration(out _), "Explicit sourced, revisioned tariff is accepted",
                ref passed, ref failed, report);
            gateway.ConfigureTariffTable(table);
            Check(gateway.ValidateTariffAuthority(out _) && gateway.TariffTable == table,
                "Gateway binds the Finance-owned table", ref passed, ref failed, report);

            var proposal = Proposal("fixture.door", 2f, 3.5f, 0f);
            var priced = new List<BistroBuilderPricedEditEconomicLine>();
            Check(BistroBuilderEditFinancePricingPolicy.TryPrice(new BistroBuilderEditEconomicProposal(),
                null, priced, out _, out _, out _), "An unchanged document needs no invented economic tariff",
                ref passed, ref failed, report);
            bool ok = BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out long credit, out long debit, out _);
            Check(ok && debit == 202L && credit == 0L,
                "Quantity tariff charges two doors independently of their width", ref passed, ref failed, report);
            proposal.lines[0].definitionId = "mutated.after.quote";
            Check(priced.Count == 1 && priced[0].source.definitionId == "fixture.door",
                "Prepared price keeps its source identity after caller mutation", ref passed, ref failed, report);
            proposal.lines[0].definitionId = "fixture.door";
            proposal.lines[0].kind = BistroBuilderEditEconomicChangeKind.Modified;
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _) && credit == 0L && debit == 0L && priced.Count == 0,
                "Unpriced operation is blocked instead of receiving an implicit zero price", ref passed, ref failed, report);

            Configure(table, "fixture.wall", BistroBuilderEditFinanceRateUnit.Length,
                BistroBuilderEditFinancePricedChanges.All, 101L);
            proposal = Proposal("fixture.wall", 1f, 1.5f, 0f);
            Check(BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _) && debit == 152L,
                "Length tariffs round half cents away from zero", ref passed, ref failed, report);
            proposal.lines.Add(Proposal("missing.fixture", 1f, 1f, 0f).lines[0]);
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _) && credit == 0L && debit == 0L && priced.Count == 0,
                "Missing second tariff clears every partial price and total", ref passed, ref failed, report);
            proposal.lines.RemoveAt(1);
            proposal.lines[0].length = float.NaN;
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _), "NaN measurements fail closed", ref passed, ref failed, report);
            proposal.lines[0].length = -1f;
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _), "Negative measurements fail closed", ref passed, ref failed, report);
            proposal.lines[0].length = 0f;
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _), "Explicit length cannot silently fall back to quantity", ref passed, ref failed, report);
            proposal.lines[0].length = 1f;
            proposal.lines[0].kind = (BistroBuilderEditEconomicChangeKind)99;
            Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _), "Unknown economic change kind cannot price at zero", ref passed, ref failed, report);

            Configure(table, "fixture.finish", BistroBuilderEditFinanceRateUnit.Area,
                BistroBuilderEditFinancePricedChanges.Added, 40L);
            proposal = Proposal("fixture.finish", 1f, 3f, 10f);
            Check(BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                out credit, out debit, out _) && debit == 400L,
                "Area rate uses the authored unit", ref passed, ref failed, report);
            Configure(table, "fixture.finish", BistroBuilderEditFinanceRateUnit.Area,
                BistroBuilderEditFinancePricedChanges.Added, 0L);
            Check(table.ValidateConfiguration(out _) &&
                BistroBuilderEditFinancePricingPolicy.TryPrice(proposal, table.Rates, priced,
                    out credit, out debit, out _) && debit == 0L,
                "An explicitly authored zero remains distinct from a missing tariff", ref passed, ref failed, report);

            Configure(table, "*", BistroBuilderEditFinanceRateUnit.Quantity,
                BistroBuilderEditFinancePricedChanges.All, 1L);
            Check(!table.ValidateConfiguration(out _), "Published tariffs reject catch-all prices",
                ref passed, ref failed, report);
            Configure(table, "fixture.wall", BistroBuilderEditFinanceRateUnit.ProposalMeasure,
                BistroBuilderEditFinancePricedChanges.All, 1L);
            Check(!table.ValidateConfiguration(out _), "Published tariffs require an explicit unit",
                ref passed, ref failed, report);
            Configure(table, "fixture.wall", BistroBuilderEditFinanceRateUnit.Length,
                BistroBuilderEditFinancePricedChanges.None, 1L);
            Check(!table.ValidateConfiguration(out _), "Published tariffs require operation coverage",
                ref passed, ref failed, report);
            Configure(table, "fixture.wall", BistroBuilderEditFinanceRateUnit.Length,
                BistroBuilderEditFinancePricedChanges.All, 1L);
            var serialized = new SerializedObject(table);
            serialized.FindProperty("rates").arraySize = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(!table.ValidateConfiguration(out _), "Duplicate DefinitionIds cannot override one another",
                ref passed, ref failed, report);
        }
        catch (Exception exception)
        {
            failed++;
            report.AppendLine("[ERROR] " + exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(table);
        }
        report.AppendLine("18M Finance: " + passed + " OK / " + failed + " failures");
        return report.ToString();
    }

    private static BistroBuilderEditEconomicProposal Proposal(string id, float quantity, float length, float area)
    {
        var proposal = new BistroBuilderEditEconomicProposal { draftRevision = 7L };
        proposal.lines.Add(new BistroBuilderEditEconomicLine
        {
            definitionId = id, entityId = BistroBuilderEditId.NewId(), quantity = quantity,
            length = length, area = area, kind = BistroBuilderEditEconomicChangeKind.Added
        });
        return proposal;
    }

    private static void Configure(BistroBuilderEditFinanceTariffTable table, string id,
        BistroBuilderEditFinanceRateUnit unit, BistroBuilderEditFinancePricedChanges changes, long addedCents)
    {
        var serialized = new SerializedObject(table);
        serialized.FindProperty("sourceReference").stringValue = "18M synthetic self-test fixture; not production";
        serialized.FindProperty("tariffRevision").stringValue = "fixture-1";
        var rates = serialized.FindProperty("rates");
        rates.arraySize = 1;
        var rate = rates.GetArrayElementAtIndex(0);
        rate.FindPropertyRelative("definitionId").stringValue = id;
        rate.FindPropertyRelative("unit").intValue = (int)unit;
        rate.FindPropertyRelative("pricedChanges").intValue = (int)changes;
        rate.FindPropertyRelative("addedSignedCentsPerUnit").longValue = addedCents;
        rate.FindPropertyRelative("modifiedSignedCentsPerUnit").longValue = 0L;
        rate.FindPropertyRelative("removedSignedCentsPerUnit").longValue = 0L;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Check(bool condition, string label, ref int passed, ref int failed, StringBuilder report)
    {
        if (condition) passed++; else failed++;
        report.AppendLine((condition ? "[OK] " : "[ERROR] ") + label);
    }
}
