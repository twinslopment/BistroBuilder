using UnityEngine;

/// <summary>
/// Bloqueo ligero para HUDs IMGUI runtime que no participan en EventSystem.
/// Evita que un clic de interfaz atraviese hasta las herramientas de edición.
/// </summary>
public static class BistroBuilderRuntimePointerUiGuard
{
    private static Rect blockedGuiRect;
    private static int lastFrame = -10;

    public static void PublishBlockedGuiRect(Rect rect)
    {
        blockedGuiRect = rect;
        lastFrame = Time.frameCount;
    }

    public static bool IsPointerBlocked(Vector2 screenPosition)
    {
        if (Time.frameCount - lastFrame > 1) return false;
        Vector2 guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return blockedGuiRect.Contains(guiPoint);
    }
}
