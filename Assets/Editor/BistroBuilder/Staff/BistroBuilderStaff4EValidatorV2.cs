using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gate estructural 4E v2. Comprueba que las tres secciones de Personal
/// reutilizan el SaveGame universal y que su orden relativo conserva
/// staff.state -> staff.recruitment -> service.runtime -> staff.session.runtime.
/// No ejecuta Play Mode ni modifica la escena.
/// </summary>
public static class BistroBuilderStaff4EValidatorV2
{
    public sealed class Result
    {
        public int correct;
        public int warnings;
        public int errors;
        public readonly List<string> lines = new List<string>();

        public void Pass(string text) { correct++; lines.Add("[OK] " + text); }
        public void Warn(string text) { warnings++; lines.Add("[AVISO] " + text); }
        public void Fail(string text) { errors++; lines.Add("[ERROR] " + text); }

        public string BuildReport()
        {
            return "4E v2 — Validación estructural\n" +
                   string.Join("\n", lines) +
                   "\nResultado: " + correct + " OK / " + warnings +
                   " avisos / " + errors + " errores";
        }
    }

    [MenuItem("Tools/Bistro Builder/Personal/4E v2 - Validar persistencia", false, 3241)]
    private static void ValidateMenu()
    {
        Result result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E v2",
            result.correct + " OK / " + result.warnings + " avisos / " +
            result.errors + " errores",
            "Aceptar");
    }

    public static Result ValidateCurrentScene()
    {
        var result = new Result();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Fail("No hay una escena activa cargada.");
            return result;
        }

        BistroBuilderSaveGameService save = RequireUnique<BistroBuilderSaveGameService>(scene, result);
        BistroBuilderStaffService staff = RequireUnique<BistroBuilderStaffService>(scene, result);
        BistroBuilderStaffRecruitmentService recruitment = RequireUnique<BistroBuilderStaffRecruitmentService>(scene, result);
        BistroBuilderStaffSessionService session = RequireUnique<BistroBuilderStaffSessionService>(scene, result);
        RestaurantServiceStateService serviceState = RequireUnique<RestaurantServiceStateService>(scene, result);

        BistroBuilderStaffStateSaveSectionProvider stateProvider =
            RequireUnique<BistroBuilderStaffStateSaveSectionProvider>(scene, result);
        BistroBuilderStaffRecruitmentSaveSectionProvider recruitmentProvider =
            RequireUnique<BistroBuilderStaffRecruitmentSaveSectionProvider>(scene, result);
        BistroBuilderStaffSessionSaveSectionProvider sessionProvider =
            RequireUnique<BistroBuilderStaffSessionSaveSectionProvider>(scene, result);
        BistroBuilderActiveServiceSaveSectionProvider activeServiceProvider =
            RequireUnique<BistroBuilderActiveServiceSaveSectionProvider>(scene, result);

        if (save == null || staff == null || recruitment == null || session == null ||
            serviceState == null || stateProvider == null || recruitmentProvider == null ||
            sessionProvider == null || activeServiceProvider == null)
        {
            return result;
        }

        if (!stateProvider.ValidateConfiguration(out string stateError))
            result.Fail("staff.state inválido: " + stateError);
        else
            result.Pass("staff.state configurado.");

        if (!recruitmentProvider.ValidateConfiguration(out string recruitmentError))
            result.Fail("staff.recruitment inválido: " + recruitmentError);
        else
            result.Pass("staff.recruitment configurado.");

        if (!sessionProvider.ValidateConfiguration(out string sessionError))
            result.Fail("staff.session.runtime inválido: " + sessionError);
        else
            result.Pass("staff.session.runtime configurado.");

        if (stateProvider.SectionId == BistroBuilderStaffSnapshot.CurrentSchemaId &&
            recruitmentProvider.SectionId == BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId &&
            sessionProvider.SectionId == BistroBuilderStaffSessionSnapshot.CurrentSchemaId)
        {
            result.Pass("IDs de sección canónicos y separados.");
        }
        else
        {
            result.Fail("Algún provider no expone su SectionId canónico.");
        }

        if (stateProvider.ApplyOrder < recruitmentProvider.ApplyOrder &&
            recruitmentProvider.ApplyOrder < activeServiceProvider.ApplyOrder &&
            activeServiceProvider.ApplyOrder < sessionProvider.ApplyOrder)
        {
            result.Pass("Orden Apply correcto: Staff -> mercado -> servicio -> binding.");
        }
        else
        {
            result.Fail(
                "Orden Apply inseguro: state=" + stateProvider.ApplyOrder +
                ", recruitment=" + recruitmentProvider.ApplyOrder +
                ", service=" + activeServiceProvider.ApplyOrder +
                ", session=" + sessionProvider.ApplyOrder + ".");
        }

        if (activeServiceProvider.PrepareOrder < stateProvider.PrepareOrder &&
            stateProvider.PrepareOrder < recruitmentProvider.PrepareOrder &&
            recruitmentProvider.PrepareOrder < sessionProvider.PrepareOrder)
        {
            result.Pass("Orden Prepare conserva service.runtime como limpiador primero.");
        }
        else
        {
            result.Fail("Orden Prepare 4E no respeta la autoridad de service.runtime.");
        }

        save.RefreshExtensions();
        string[] ids =
        {
            BistroBuilderStaffStateSaveSectionProvider.StableSectionId,
            BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId,
            BistroBuilderStaffSessionSaveSectionProvider.StableSectionId
        };
        for (int index = 0; index < ids.Length; index++)
        {
            if (save.HasProvider(ids[index]))
                result.Pass("SaveGameService registra " + ids[index] + ".");
            else
                result.Fail("SaveGameService no registra " + ids[index] + ".");
        }

        if (save.ValidateConfiguration(out string saveError))
            result.Pass("SaveGameService continúa válido con las extensiones 4E.");
        else
            result.Fail("SaveGameService quedó inválido: " + saveError);

        return result;
    }

    private static T RequireUnique<T>(Scene scene, Result result) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var matches = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
                matches.Add(all[index]);
        }

        if (matches.Count != 1)
        {
            result.Fail("Se esperaba exactamente un " + typeof(T).Name +
                        " y hay " + matches.Count + ".");
            return null;
        }

        result.Pass("Existe un único " + typeof(T).Name + ".");
        return matches[0];
    }
}
