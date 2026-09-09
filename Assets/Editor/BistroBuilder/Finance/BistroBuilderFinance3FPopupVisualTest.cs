using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Demostración exclusivamente visual de los popups monetarios de 3F.
///
/// No publica movimientos financieros ni modifica el restaurante.
/// La prueba runtime 3F cubre la integración económica real; esta demo
/// permite al jugador validar tamaño, color, posición y animación.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderFinance3FPopupVisualTest
{
    private const string ArmedKey = "BB.Finance.3F.PopupVisual.Armed";
    private const string ResultKey = "BB.Finance.3F.PopupVisual.Result";
    private const double StartupTimeoutSeconds = 15d;
    private const double FirstDelaySeconds = 1.5d;
    private const double StepDelaySeconds = 2.2d;
    private const double FinishDelaySeconds = 2.2d;

    private static double startupDeadline;
    private static double nextStepAt;
    private static int step;
    private static BistroBuilderMoneyPopupService popupService;
    private static Camera targetCamera;
    private static Vector3 anchorPosition;

    static BistroBuilderFinance3FPopupVisualTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3F - Demo visual popups",
        false,
        3054)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "La demo visual de popups ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "Sal de Play Mode antes de iniciar la demo visual.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManagerBridge.TrySaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "No se pudieron guardar las escenas antes de la demo visual.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            startupDeadline =
                EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            nextStepAt =
                EditorApplication.timeSinceStartup + FirstDelaySeconds;
            step = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ArmedKey, false);
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string result = SessionState.GetString(ResultKey, string.Empty);
        if (string.IsNullOrWhiteSpace(result))
        {
            return;
        }

        SessionState.EraseString(ResultKey);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3F",
            result,
            "Aceptar");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (popupService == null)
        {
            popupService =
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderMoneyPopupService>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (popupService == null || targetCamera == null)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Finish(
                    "DEMO VISUAL 3F NO INICIADA\n\n" +
                    "No se encontró el servicio de popups o la cámara principal.");
            }
            return;
        }

        if (step == 0)
        {
            anchorPosition = ResolveVisibleAnchor(targetCamera);
        }

        if (EditorApplication.timeSinceStartup < nextStepAt)
        {
            return;
        }

        switch (step)
        {
            case 0:
                popupService.Show(-50000L, anchorPosition);
                step = 1;
                nextStepAt =
                    EditorApplication.timeSinceStartup + StepDelaySeconds;
                break;

            case 1:
                popupService.Show(25000L, anchorPosition);
                step = 2;
                nextStepAt =
                    EditorApplication.timeSinceStartup + StepDelaySeconds;
                break;

            case 2:
                popupService.Show(-7500L, anchorPosition);
                step = 3;
                nextStepAt =
                    EditorApplication.timeSinceStartup + FinishDelaySeconds;
                break;

            default:
                Finish(
                    "DEMO VISUAL 3F COMPLETADA\n\n" +
                    "Deberías haber visto, desde el mismo punto del restaurante:\n" +
                    "-500 €  (compra)\n" +
                    "+250 €  (venta)\n" +
                    "-75 €   (demolición)\n\n" +
                    "Valida visualmente tamaño, legibilidad, color, subida y fade.");
                break;
        }
    }

    private static Vector3 ResolveVisibleAnchor(Camera camera)
    {
        RestaurantPlaceableObject[] placeables =
            UnityEngine.Object.FindObjectsByType<RestaurantPlaceableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        float bestDistanceFromCenter = float.MaxValue;
        Vector3 bestAnchor = default;
        bool found = false;

        for (int index = 0; index < placeables.Length; index++)
        {
            RestaurantPlaceableObject placeable = placeables[index];
            if (placeable == null || !placeable.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 worldPoint = ResolveObjectTop(placeable.gameObject);
            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0f ||
                viewport.x < 0.12f || viewport.x > 0.88f ||
                viewport.y < 0.12f || viewport.y > 0.88f)
            {
                continue;
            }

            float dx = viewport.x - 0.5f;
            float dy = viewport.y - 0.5f;
            float score = dx * dx + dy * dy;
            if (score >= bestDistanceFromCenter)
            {
                continue;
            }

            bestDistanceFromCenter = score;
            bestAnchor = worldPoint;
            found = true;
        }

        if (found)
        {
            return bestAnchor;
        }

        float fallbackDistance = Mathf.Max(4f, camera.nearClipPlane + 3f);
        return camera.ViewportToWorldPoint(
            new Vector3(0.5f, 0.55f, fallbackDistance));
    }

    private static Vector3 ResolveObjectTop(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return target.transform.position + Vector3.up * 1.0f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
        }

        return new Vector3(
            bounds.center.x,
            bounds.max.y + 0.18f,
            bounds.center.z);
    }

    private static void Finish(string message)
    {
        EditorApplication.update -= Tick;
        SessionState.SetString(ResultKey, message);
        EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Puente mínimo para mantener las dependencias de UnityEditor concentradas
    /// y permitir que el flujo de la demo permanezca fácil de leer.
    /// </summary>
    private static class EditorSceneManagerBridge
    {
        public static bool TrySaveOpenScenes()
        {
            return UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }
    }
}
