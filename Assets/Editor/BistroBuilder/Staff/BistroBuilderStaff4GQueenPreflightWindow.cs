using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 4G — Preflight del Queen Test real de Personal.
///
/// No altera la partida ni sustituye el Queen flow final. Comprueba, en Play
/// Mode, que todas las autoridades 4A–4F y la Presentation jugable completa
/// están presentes y coherentes antes de una prueba reversible Save/Load.
/// </summary>
public sealed class BistroBuilderStaff4GQueenPreflightWindow : EditorWindow
{
    private Vector2 scroll;
    private string report =
        "Entra en Play Mode y ejecuta el preflight antes del Queen Test real.";
    private MessageType reportType = MessageType.Info;

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Queen Test preflight",
        false,
        3260)]
    private static void Open()
    {
        GetWindow<BistroBuilderStaff4GQueenPreflightWindow>(
            "Queen Test 4G");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "4G — QUEEN TEST REAL DE PERSONAL",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Este preflight no modifica empleados ni slots. Verifica que " +
            "Staff, contratación, desarrollo, binding, persistencia y UI " +
            "están listos para ejecutar después el Queen flow reversible.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("EJECUTAR PREFLIGHT 4G", GUILayout.Height(36f)))
            {
                RunPreflight();
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void RunPreflight()
    {
        var lines = new List<string>();
        int passed = 0;
        int failed = 0;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Finish(lines, 0, 1, "No hay escena activa cargada.");
            return;
        }

        BistroBuilderSaveGameService save =
            Unique<BistroBuilderSaveGameService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffService staff =
            Unique<BistroBuilderStaffService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffRecruitmentService recruitment =
            Unique<BistroBuilderStaffRecruitmentService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffDevelopmentService development =
            Unique<BistroBuilderStaffDevelopmentService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffSessionService session =
            Unique<BistroBuilderStaffSessionService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffPlayerFacade facade =
            Unique<BistroBuilderStaffPlayerFacade>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffPlayerScreen screen =
            Unique<BistroBuilderStaffPlayerScreen>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffPlayerTrainingPanel trainingPanel =
            Unique<BistroBuilderStaffPlayerTrainingPanel>(scene, lines, ref passed, ref failed);

        if (save != null)
        {
            save.RefreshExtensions();
            Check(
                save.HasProvider(BistroBuilderStaffStateSaveSectionProvider.StableSectionId),
                "Save registra staff.state.",
                lines, ref passed, ref failed);
            Check(
                save.HasProvider(
                    BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId),
                "Save registra staff.recruitment.",
                lines, ref passed, ref failed);
            Check(
                save.HasProvider(BistroBuilderStaffSessionSaveSectionProvider.StableSectionId),
                "Save registra staff.session.runtime.",
                lines, ref passed, ref failed);
        }

        if (staff != null)
        {
            CheckConfiguration(
                staff.ValidateConfiguration,
                "StaffService",
                lines, ref passed, ref failed);
            Check(
                staff.CreateSnapshot() != null,
                "staff.state expone snapshot canónico.",
                lines, ref passed, ref failed);
        }

        if (recruitment != null)
        {
            CheckConfiguration(
                recruitment.ValidateConfiguration,
                "RecruitmentService",
                lines, ref passed, ref failed);
            bool marketReady = recruitment.EnsureMarketReady(out string marketError);
            Check(
                marketReady && recruitment.CandidateCount > 0,
                marketReady
                    ? "Mercado de candidatos disponible con " +
                      recruitment.CandidateCount + " ofertas."
                    : "Mercado de candidatos no disponible: " + marketError,
                lines, ref passed, ref failed);
        }

        if (development != null)
        {
            CheckConfiguration(
                development.ValidateConfiguration,
                "DevelopmentService",
                lines, ref passed, ref failed);
        }

        if (session != null)
        {
            CheckConfiguration(
                session.ValidateConfiguration,
                "StaffSessionService",
                lines, ref passed, ref failed);
            Check(
                session.CreateSessionSnapshot() != null,
                "staff.session.runtime expone snapshot para Save/Load.",
                lines, ref passed, ref failed);
        }

        if (facade != null)
        {
            CheckConfiguration(
                facade.ValidateConfiguration,
                "StaffPlayerFacade",
                lines, ref passed, ref failed);
            bool snapshotOk = facade.TryBuildSnapshot(
                out BistroBuilderStaffPlayerUiSnapshot uiSnapshot,
                out string uiError);
            Check(
                snapshotOk && uiSnapshot != null,
                snapshotOk
                    ? "Presentation reconstruye snapshot sin asumir autoridad."
                    : "Presentation no puede construir snapshot: " + uiError,
                lines, ref passed, ref failed);
        }

        if (screen != null)
        {
            CheckConfiguration(
                screen.ValidateConfiguration,
                "StaffPlayerScreen",
                lines, ref passed, ref failed);
        }

        if (trainingPanel != null)
        {
            CheckConfiguration(
                trainingPanel.ValidateConfiguration,
                "StaffPlayerTrainingPanel",
                lines, ref passed, ref failed);
            Check(
                trainingPanel.TrainingOptionCount > 0,
                "La UI de formación expone opciones derivadas del perfil canónico.",
                lines, ref passed, ref failed);
        }

        Check(
            Object.FindObjectsByType<Waiter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length > 0,
            "Existen agentes Waiter operativos reales para probar binding 4D.",
            lines, ref passed, ref failed);

        Finish(lines, passed, failed, null);
    }

    private delegate bool ConfigurationValidator(out string error);

    private static void CheckConfiguration(
        ConfigurationValidator validator,
        string name,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        bool ok = validator(out string error);
        Check(
            ok,
            ok ? name + " válido." : name + " inválido: " + error,
            lines,
            ref passed,
            ref failed);
    }

    private static T Unique<T>(
        Scene scene,
        List<string> lines,
        ref int passed,
        ref int failed)
        where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var sceneValues = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                sceneValues.Add(all[index]);
            }
        }

        bool ok = sceneValues.Count == 1;
        Check(
            ok,
            ok
                ? "Existe una única autoridad/componente " + typeof(T).Name + "."
                : typeof(T).Name + " debe existir una vez; hay " + sceneValues.Count + ".",
            lines,
            ref passed,
            ref failed);
        return ok ? sceneValues[0] : null;
    }

    private static void Check(
        bool condition,
        string text,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[ERROR] " + text);
        }
    }

    private void Finish(
        List<string> lines,
        int passed,
        int failed,
        string immediateFailure)
    {
        if (!string.IsNullOrWhiteSpace(immediateFailure))
        {
            lines.Add("[ERROR] " + immediateFailure);
        }

        report = "4G — PREFLIGHT QUEEN TEST\n" +
                 string.Join("\n", lines) +
                 "\n\nResultado: " + passed + " OK / " + failed + " errores.";
        reportType = failed == 0 ? MessageType.Info : MessageType.Error;
        Repaint();

        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);
    }
}
