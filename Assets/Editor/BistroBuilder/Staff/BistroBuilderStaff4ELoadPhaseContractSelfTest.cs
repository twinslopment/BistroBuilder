using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4E — Gate de contrato para el orden de fases Save/Load de Personal.
///
/// La persistencia de Personal comparte el pipeline canónico con service.runtime.
/// Un cambio aparentemente inocente en LoadOrder/PrepareOrder/ApplyOrder/
/// FinalizeOrder puede provocar que se reconstruyan EmployeeId, bindings o
/// mercado en una fase incorrecta y dejar referencias runtime inconsistentes.
///
/// Este gate no guarda/carga partidas, no instala componentes persistentes y
/// no crea otra autoridad de SaveGame. Instancia providers temporalmente solo
/// para verificar su contrato público y los destruye en la misma ejecución.
/// </summary>
public static class BistroBuilderStaff4ELoadPhaseContractSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E - Gate contrato fases SaveLoad",
        false,
        3244)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E / Contrato fases",
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
        log.AppendLine("=== BISTRO BUILDER — 4E / CONTRATO FASES SAVELOAD ===");

        GameObject host = null;
        try
        {
            host = new GameObject("BB_Staff4E_LoadPhaseContract_Temporary")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var state = host.AddComponent<BistroBuilderStaffStateSaveSectionProvider>();
            var recruitment = host.AddComponent<BistroBuilderStaffRecruitmentSaveSectionProvider>();
            var session = host.AddComponent<BistroBuilderStaffSessionSaveSectionProvider>();

            Check(
                state.LoadOrder == 420 &&
                state.PrepareOrder == 8850 &&
                state.ApplyOrder == 400 &&
                state.FinalizeOrder == 10500,
                "staff.state conserva Load 420 / Prepare 8850 / Apply 400 / Finalize 10500.",
                ref passed, ref failed, log);

            Check(
                recruitment.LoadOrder == 430 &&
                recruitment.PrepareOrder == 8900 &&
                recruitment.ApplyOrder == 425 &&
                recruitment.FinalizeOrder == 10600,
                "staff.recruitment conserva Load 430 / Prepare 8900 / Apply 425 / Finalize 10600.",
                ref passed, ref failed, log);

            Check(
                session.LoadOrder == 550 &&
                session.PrepareOrder == 8950 &&
                session.ApplyOrder == 550 &&
                session.FinalizeOrder == 10950,
                "staff.session.runtime conserva Load 550 / Prepare 8950 / Apply 550 / Finalize 10950.",
                ref passed, ref failed, log);

            Check(
                session.PrepareOrder > recruitment.PrepareOrder &&
                recruitment.PrepareOrder > state.PrepareOrder,
                "Prepare mantiene sesión -> mercado -> plantilla al ordenar de mayor a menor.",
                ref passed, ref failed, log);

            Check(
                state.ApplyOrder < recruitment.ApplyOrder &&
                recruitment.ApplyOrder < session.ApplyOrder,
                "Apply reconstruye plantilla antes de mercado y binding runtime.",
                ref passed, ref failed, log);

            Check(
                state.FinalizeOrder < recruitment.FinalizeOrder &&
                recruitment.FinalizeOrder < session.FinalizeOrder,
                "Finalize conserva la progresión staff.state -> recruitment -> session.",
                ref passed, ref failed, log);

            Check(
                !state.IsRequired && !recruitment.IsRequired && !session.IsRequired,
                "Las tres secciones siguen siendo opcionales para compatibilidad pre-4E.",
                ref passed, ref failed, log);

            Check(
                state.SerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId &&
                recruitment.SerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId &&
                session.SerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId,
                "Las tres secciones conservan el serializador JSON canónico.",
                ref passed, ref failed, log);

            Check(
                state.StateType == typeof(BistroBuilderStaffSnapshot) &&
                recruitment.StateType == typeof(BistroBuilderStaffRecruitmentSnapshot) &&
                session.StateType == typeof(BistroBuilderStaffSessionSnapshot),
                "Cada provider conserva el tipo de snapshot de su dominio.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffStateSaveSectionProvider.StableSectionVersion > 0 &&
                BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionVersion > 0 &&
                BistroBuilderStaffSessionSaveSectionProvider.StableSectionVersion > 0,
                "Las tres secciones mantienen una versión de esquema explícita positiva.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine(
                "[FALLO] Excepción al inspeccionar providers 4E: " +
                exception.GetType().Name + " — " + exception.Message);
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos.");
        log.AppendLine(
            "Este gate protege el contrato de orden; no sustituye Save/Load real en Unity.");

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
