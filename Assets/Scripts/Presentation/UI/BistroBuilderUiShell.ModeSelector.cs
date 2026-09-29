using UnityEngine;

public sealed partial class BistroBuilderUiShell
{
    public const string ModeSelectorName =
        "BB_UIUX_ModeSelector";

    private RectTransform modeSelectorRoot;

    /// <summary>
    /// Compatibilidad de escena para el selector flotante heredado.
    /// La navegación normal y la barra de edición son ya las únicas
    /// autoridades visibles para entrar y salir de Modo Edición.
    /// </summary>
    private void EnsureModeSelector()
    {
        if (shellRoot == null)
            return;

        Transform existing =
            shellRoot.Find(
                ModeSelectorName);

        modeSelectorRoot =
            existing as RectTransform;

        if (modeSelectorRoot != null)
        {
            modeSelectorRoot.gameObject.SetActive(
                false);
        }
    }

    private void RefreshModeSelector(
        bool editing,
        bool managing)
    {
        EnsureModeSelector();

        if (modeSelectorRoot != null &&
            modeSelectorRoot.gameObject.activeSelf)
        {
            modeSelectorRoot.gameObject.SetActive(
                false);
        }
    }
}
