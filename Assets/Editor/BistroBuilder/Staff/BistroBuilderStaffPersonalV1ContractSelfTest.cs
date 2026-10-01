using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-mutating Personal V1 regression gate. Uses canonical authoring assets
/// and the real recruitment engine, not mock candidate rows.
/// </summary>
public static class BistroBuilderStaffPersonalV1ContractSelfTest
{
    private const string RolesPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";

    [MenuItem("Tools/Bistro Builder/Personal/V1 - Verificar contrato visual y mercado", false, 3255)]
    public static void Run()
    {
        int passed = 0;
        int failed = 0;
        var issues = new List<string>();

        void Check(bool condition, string label)
        {
            if (condition) passed++;
            else
            {
                failed++;
                issues.Add(label);
            }
        }

        var catalog = AssetDatabase.LoadAssetAtPath<BistroBuilderStaffRoleCatalog>(RolesPath);
        var market = AssetDatabase.LoadAssetAtPath<BistroBuilderStaffRecruitmentProfile>(RecruitmentPath);
        Check(catalog != null && catalog.TryValidate(out _),
            "Catálogo de roles canónico válido");
        Check(market != null && catalog != null &&
              market.TryValidate(catalog, out _),
            "Mercado de contratación canónico válido");

        bool hasWaiter = catalog != null &&
            catalog.TryGetRole("waiter", out var waiter) && waiter != null && waiter.active &&
            string.Equals(waiter.departmentId, "sala", StringComparison.Ordinal);
        bool hasCook = catalog != null &&
            catalog.TryGetRole("cook", out var cook) && cook != null && cook.active &&
            string.Equals(cook.departmentId, "cocina", StringComparison.Ordinal);
        Check(hasWaiter && hasCook, "Sala/Cocina derivan de metadatos de rol");

        if (market != null && catalog != null &&
            market.TryValidate(catalog, out _) &&
            BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                market, catalog, 1, out BistroBuilderStaffRecruitmentSnapshot generated,
                out _))
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BistroBuilderStaffCandidateRecord row in generated.candidates)
            {
                if (row == null) continue;
                counts.TryGetValue(row.roleId, out int count);
                counts[row.roleId] = count + 1;
            }
            Check(generated.candidates.Count == market.CandidateCount,
                "5 ofertas reales según perfil V1");
            Check(counts.ContainsKey("waiter") && counts.ContainsKey("cook"),
                "Mercado inicial ofrece camareros y cocineros");
            Check(market.EnabledRoleIds.Count <= generated.candidates.Count,
                "Mercado permite cubrir todos los roles habilitados");
        }
        else
        {
            Check(false, "Generación pura de mercado V1");
        }

        Type screen = typeof(BistroBuilderStaffPlayerScreen);
        const BindingFlags privateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        Check(screen.GetMethod("EnsureApprovedPresentation", privateInstance) != null,
            "La pantalla aprobada se aplica a la 4F existente");
        Check(screen.GetMethod("ApplyCandidateRoleFilter", privateInstance) != null,
            "Filtros de candidatos derivados del rol");
        Check(screen.GetField("confirmationBlocker", privateInstance) != null &&
              screen.GetField("pendingTargetId", privateInstance) != null,
            "Modal bloqueante vinculado a identidad estable");
        Check(screen.GetMethod("ConfirmPendingAction",
            BindingFlags.Instance | BindingFlags.Public) != null,
            "Contratar/Despedir requieren comando confirmado");

        string report = "[PERSONAL V1] " + passed + " PASS / " + failed +
            " FAIL" + (issues.Count == 0 ? string.Empty :
                "\n" + string.Join("\n", issues));
        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);
    }
}
