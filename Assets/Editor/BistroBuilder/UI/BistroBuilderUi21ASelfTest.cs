using System.Text;
using UnityEngine;

public static class BistroBuilderUi21ASelfTest
{
    public static bool Run(out string report)
    {
        int passed = 0;
        int failed = 0;
        StringBuilder log = new StringBuilder();
        log.AppendLine("BISTRO BUILDER — UI/UX 21A STATIC SELFTEST");

        Check(BistroBuilderUiTokens.ReferenceResolution == new Vector2(1920f, 1080f),
            "1920x1080 es la referencia responsive", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.MotionDropdownSeconds <= 0.20f,
            "Dropdown mantiene microinteracción <= 200 ms", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.MotionPressSeconds <= 0.12f,
            "Press feedback es inmediato", ref passed, ref failed, log);
        Check(!BistroBuilderUiTokens.Primary.Equals(BistroBuilderUiTokens.Critical),
            "Acción primaria y destructiva usan semánticas distintas", ref passed, ref failed, log);
        Check(!BistroBuilderUiTokens.Primary.Equals(BistroBuilderUiTokens.SecondaryAction),
            "Principal y secundario mantienen jerarquía visual distinta", ref passed, ref failed, log);
        Check(!BistroBuilderUiTokens.Success.Equals(BistroBuilderUiTokens.Attention) &&
            !BistroBuilderUiTokens.Attention.Equals(BistroBuilderUiTokens.Critical),
            "Estados Normal/Atención/Crítico son cromáticamente distinguibles",
            ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.FontH1 == 28f && BistroBuilderUiTokens.FontH2 == 20f &&
            BistroBuilderUiTokens.FontH3 == 16f,
            "Jerarquía H1/H2/H3 fijada en 28/20/16", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.FontBody == 14f && BistroBuilderUiTokens.FontSecondary == 13f &&
            BistroBuilderUiTokens.FontLabel == 12f,
            "Cuerpo/secundario/etiqueta fijados en 14/13/12", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.ControlStandard == 40f && BistroBuilderUiTokens.ControlPrimary == 46f,
            "Inputs y botones usan alturas canónicas", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.ProgressHeight <= 8f,
            "Barras de progreso permanecen finas", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.TabUnderline <= 3f,
            "Tabs usan selección inferior discreta", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.Space16 * 2f == BistroBuilderUiTokens.Space32,
            "Escala de spacing sigue base 4 px", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.PressScale > 0.95f && BistroBuilderUiTokens.PressScale < 1f,
            "Feedback de pulsación es sutil, no decorativo", ref passed, ref failed, log);
        Check(((Color)BistroBuilderUiTokens.BorderSubtle).a < 0.5f,
            "Bordes de panel permanecen sutiles", ref passed, ref failed, log);
        Check(((Color)BistroBuilderUiTokens.Shadow).a < 0.25f,
            "Sombras se reservan para profundidad suave", ref passed, ref failed, log);

        log.AppendLine($"RESULTADO: {passed} OK / {failed} fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static void Check(bool condition, string description,
        ref int passed, ref int failed, StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("OK  · " + description);
        }
        else
        {
            failed++;
            log.AppendLine("FAIL· " + description);
        }
    }
}
