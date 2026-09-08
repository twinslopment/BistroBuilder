using System;

public static class BistroBuilderNewGame16SelfTest
{
    public static bool Run(out int passed, out int failed, out string report)
    {
        int ok = 0, bad = 0;
        var lines = new System.Collections.Generic.List<string>();
        Action<bool, string> check = (value, label) =>
        {
            if (value) { ok++; lines.Add("OK - " + label); }
            else { bad++; lines.Add("FAIL - " + label); }
        };

        var state = new BistroBuilderNewGameStateSnapshot
        {
            revision = 2,
            phase = BistroBuilderNewGamePhase.ReadyToOpen,
            setupCompleted = true,
            briefingAcknowledged = true,
            restaurantName = "Bistro Test",
            premisesProfile = BistroBuilderStartingPremisesProfile.Balanced,
            initialOpeningHour = 12,
            initialClosingHour = 15,
            initialSaveSlot = 0
        };
        check(BistroBuilderNewGameEngine.TryValidateSnapshot(state, out _), "Estado inicial coherente aceptado");
        var clone = state.DeepClone(); clone.restaurantName = "Otro";
        check(state.restaurantName != clone.restaurantName, "Snapshot se clona sin compartir identidad");
        var invalid = state.DeepClone(); invalid.restaurantName = string.Empty;
        check(!BistroBuilderNewGameEngine.TryValidateSnapshot(invalid, out _), "Configuracion completada exige identidad");
        invalid = state.DeepClone(); invalid.firstOpeningCompleted = true; invalid.setupCompleted = false;
        check(!BistroBuilderNewGameEngine.TryValidateSnapshot(invalid, out _), "Primera apertura exige setup");
        invalid = state.DeepClone(); invalid.firstServiceStarted = true; invalid.firstOpeningCompleted = false;
        check(!BistroBuilderNewGameEngine.TryValidateSnapshot(invalid, out _), "Primer servicio exige apertura");
        invalid = state.DeepClone(); invalid.transitionedToNormalPlay = true; invalid.firstServiceStarted = false;
        check(!BistroBuilderNewGameEngine.TryValidateSnapshot(invalid, out _), "Juego normal exige primer servicio");
        invalid = state.DeepClone(); invalid.initialOpeningHour = invalid.initialClosingHour;
        check(!BistroBuilderNewGameEngine.TryValidateSnapshot(invalid, out _), "Horario inicial no puede tener duracion cero");

        var preflight = new BistroBuilderOpeningPreflightReport();
        BistroBuilderNewGameEngine.AddCheck(preflight, "a", "Entrada", BistroBuilderOpeningCheckLevel.Passed, "ok");
        BistroBuilderNewGameEngine.AddCheck(preflight, "b", "Banos", BistroBuilderOpeningCheckLevel.Warning, "aviso");
        check(preflight.CanOpen, "Avisos no bloquean apertura");
        check(preflight.passedCount == 1 && preflight.warningCount == 1, "Preflight contabiliza OK y avisos");
        BistroBuilderNewGameEngine.AddCheck(preflight, "c", "Stock", BistroBuilderOpeningCheckLevel.Blocker, "falta");
        check(!preflight.CanOpen && preflight.blockerCount == 1, "Bloqueo impide apertura");
        var copy = preflight.DeepClone(); copy.checks[0].message = "otro";
        check(preflight.checks[0].message != copy.checks[0].message, "Preflight clona profundamente");
        string briefing = BistroBuilderNewGameEngine.BuildBriefing("Bistro Test",
            BistroBuilderStartingPremisesProfile.Balanced, preflight, 12, 15);
        check(briefing.Contains("Bistro Test"), "Briefing incluye identidad");
        check(briefing.Contains("12:00") && briefing.Contains("15:00"), "Briefing incluye horario");
        check(briefing.Contains("requisitos imprescindibles"), "Briefing explica bloqueos");
        var clean = new BistroBuilderOpeningPreflightReport();
        BistroBuilderNewGameEngine.AddCheck(clean, "a", "Entrada", BistroBuilderOpeningCheckLevel.Passed, "ok");
        check(BistroBuilderNewGameEngine.BuildBriefing("B", BistroBuilderStartingPremisesProfile.Compact,
            clean, 12, 15).Contains("listo para abrir"), "Briefing positivo reconoce apertura posible");
        check(new BistroBuilderNewGameStateSnapshot().phase == BistroBuilderNewGamePhase.StartMenu,
            "Estado nuevo empieza en menu inicial");
        check(new BistroBuilderOpeningPreflightReport().CanOpen, "Preflight vacio no inventa bloqueos");

        passed = ok; failed = bad;
        report = "BLOQUE 16 - AUTOTEST\n" + string.Join("\n", lines) +
                 "\nResultado: " + ok + " OK / " + bad + " fallos.";
        return bad == 0;
    }
}
