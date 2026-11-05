using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comprobacion visual/funcional reproducible de E5.5 en Play Mode.
/// Menu: Bistro Builder > Pruebas > Validar barra inferior E5.5.
/// </summary>
public static class BistroBuilderBottomE55SmokeCheck
{
    [MenuItem("Bistro Builder/Pruebas/Validar barra inferior E5.5")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("E5.5: abre la escena de juego y entra en Play Mode antes de validar.");
            return;
        }
        BistroBuilderBottomE55Hud hud =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderBottomE55Hud>(
                FindObjectsInactive.Include);
        Need(hud != null, "No existe el componente E5.5.");
        Need(hud.ValidateConfiguration(out string error), error);

        RectTransform root = hud.transform.Find(BistroBuilderBottomE55Hud.VisualRootName) as RectTransform;
        Need(root != null, "No se encuentra la superficie E5.5.");
        Transform content = root.Find("ContenidoE55");
        Need(content != null && content.childCount == 4,
            "La barra debe contener exactamente cuatro modulos.");
        string[] sections = { "CajaE55", "SatisfaccionE55", "ClimaE55", "VelocidadE55" };
        foreach (string section in sections)
            Need(content.Find(section) != null, "Falta " + section);

        Canvas.ForceUpdateCanvases();
        Need(root.rect.width > 600f && root.rect.height > 32f,
            "Las dimensiones de E5.5 son incompatibles con el viewport.");

        GameClock clock = UnityEngine.Object.FindFirstObjectByType<GameClock>(
            FindObjectsInactive.Include);
        Need(clock != null, "Falta GameClock.");
        Transform speed = content.Find("VelocidadE55");
        Button pause = speed.Find("Pausa")?.GetComponent<Button>();
        Button one = speed.Find("Speed1")?.GetComponent<Button>();
        Button two = speed.Find("Speed2")?.GetComponent<Button>();
        Button three = speed.Find("Speed3")?.GetComponent<Button>();
        Need(pause != null && one != null && two != null && three != null,
            "Los cuatro botones de tiempo no estan operativos.");

        if (!clock.IsRuntimeSuspended)
        {
            float originalSpeed = clock.SpeedMultiplier;
            bool originalPaused = clock.IsPaused;
            try
            {
                two.onClick.Invoke();
                Need(Mathf.Approximately(clock.SpeedMultiplier, 2f), "No funciona 2x.");
                three.onClick.Invoke();
                Need(Mathf.Approximately(clock.SpeedMultiplier, 3f), "No funciona 3x.");
                one.onClick.Invoke();
                Need(Mathf.Approximately(clock.SpeedMultiplier, 1f), "No funciona 1x.");
                pause.onClick.Invoke();
                Need(clock.IsPaused != originalPaused, "No cambia la pausa.");
                pause.onClick.Invoke();
                Need(clock.IsPaused == originalPaused, "No reanuda correctamente.");
            }
            finally
            {
                clock.SetSpeedMultiplier(originalSpeed);
                clock.SetPaused(originalPaused);
            }
        }

        Canvas canvas = hud.GetComponentInParent<Canvas>();
        Transform legacy = canvas != null ? canvas.transform.Find("BB_368B_TimeControlsDock") : null;
        if (legacy != null)
        {
            CanvasGroup group = legacy.GetComponent<CanvasGroup>();
            Need(group != null && group.alpha < 0.01f && !group.blocksRaycasts,
                "El control de tiempo 368B antiguo queda duplicado o intercepta clicks.");
        }
        Debug.Log("BISTRO BUILDER E5.5: PASS estructura, cuatro modulos y controles GameClock. " +
            "Pendientes de inspeccion manual: valores reales de Caja, Satisfaccion, Clima y capturas 1920/1280.");
    }

    private static void Need(bool ok, string error)
    {
        if (!ok) throw new InvalidOperationException("E5.5: " + error);
    }
}
