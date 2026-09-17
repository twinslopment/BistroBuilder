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
            "Acción primaria y crítico usan semánticas distintas", ref passed, ref failed, log);
        Check(!BistroBuilderUiTokens.Success.Equals(BistroBuilderUiTokens.Attention) &&
            !BistroBuilderUiTokens.Attention.Equals(BistroBuilderUiTokens.Critical),
            "Estados Normal/Atención/Crítico son cromáticamente distinguibles",
            ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.FontBody >= 14f && BistroBuilderUiTokens.FontCaption >= 12f,
            "Legibilidad mínima de cuerpo/caption preservada", ref passed, ref failed, log);

        Check(BistroBuilderUiTokens.Space16 * 2f == BistroBuilderUiTokens.Space32,
            "Escala de spacing sigue base 4 px", ref passed, ref failed, log);
        Check(BistroBuilderUiTokens.PressScale > 0.95f && BistroBuilderUiTokens.PressScale < 1f,
            "Feedback de pulsación es sutil, no decorativo", ref passed, ref failed, log);

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
