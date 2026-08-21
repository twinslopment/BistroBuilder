using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4E que congela la identidad pública de las secciones SaveGame
/// de Personal. Evita una regresión especialmente peligrosa: renombrar a la vez
/// el provider y su snapshot puede hacer pasar tests internos y, aun así,
/// volver invisibles las secciones de partidas ya existentes.
///
/// Este test no escribe saves, no carga escenas y no crea otra autoridad de
/// persistencia. Solo protege los IDs canónicos ya publicados por 4E.
/// </summary>
public static class BistroBuilderStaff4EStableSectionIdentitySelfTest
{
    private const string ExpectedStaffStateSectionId = "staff.state";
    private const string ExpectedRecruitmentSectionId = "staff.recruitment";
    private const string ExpectedSessionSectionId = "staff.session.runtime";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4E - Gate identidad estable SaveGame",
        false,
        3243)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E / Identidad SaveGame",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4E / IDENTIDAD ESTABLE SAVEGAME ===");

        Check(
            BistroBuilderStaffStateSaveSectionProvider.StableSectionId ==
                ExpectedStaffStateSectionId,
            "staff.state conserva su ID público exacto.",
            ref passed, ref failed, log);

        Check(
            BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId ==
                ExpectedRecruitmentSectionId,
            "staff.recruitment conserva su ID público exacto.",
            ref passed, ref failed, log);

        Check(
            BistroBuilderStaffSessionSaveSectionProvider.StableSectionId ==
                ExpectedSessionSectionId,
            "staff.session.runtime conserva su ID público exacto.",
            ref passed, ref failed, log);

        Check(
            BistroBuilderStaffSnapshot.CurrentSchemaId == ExpectedStaffStateSectionId &&
            BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId ==
                ExpectedRecruitmentSectionId &&
            BistroBuilderStaffSessionSnapshot.CurrentSchemaId == ExpectedSessionSectionId,
            "Los snapshots de dominio conservan la misma identidad pública que sus providers.",
            ref passed, ref failed, log);

        Check(
            ExpectedStaffStateSectionId != ExpectedRecruitmentSectionId &&
            ExpectedStaffStateSectionId != ExpectedSessionSectionId &&
            ExpectedRecruitmentSectionId != ExpectedSessionSectionId,
            "Las tres identidades públicas siguen siendo distintas.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos.");
        log.AppendLine(
            "Este gate protege compatibilidad nominal; no sustituye un Save/Load real en Unity.");

        report = log.ToString();
        return failed == 0;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + text);
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
