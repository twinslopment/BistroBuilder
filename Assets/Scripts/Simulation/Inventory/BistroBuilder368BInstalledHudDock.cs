using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marcador y contrato de la banda compacta de tiempo/velocidad 368B.
///
/// La jerarquía visual se instala en la escena para que no dependa de código
/// de Editor durante el build. Este componente permite validarla sin buscar
/// objetos por nombres frágiles.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/368B Installed HUD Dock")]
public sealed class BistroBuilder368BInstalledHudDock : MonoBehaviour
{
    [SerializeField]
    private string installedRevision = "368B";

    [SerializeField]
    private RectTransform dockRect;

    [SerializeField]
    private Image background;

    [SerializeField]
    private ClockDisplay clockDisplay;

    [SerializeField]
    private TMP_Text clockText;

    [SerializeField]
    private PauseButtonController pauseButton;

    [SerializeField]
    private SpeedButtonController[] speedButtons =
        new SpeedButtonController[0];

    public string InstalledRevision => installedRevision;

    public RectTransform DockRect => dockRect;

    public ClockDisplay ClockDisplay => clockDisplay;

    public TMP_Text ClockText => clockText;

    public PauseButtonController PauseButton => pauseButton;

    public SpeedButtonController[] SpeedButtons => speedButtons;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (installedRevision != "368B")
        {
            error = "La revisión del HUD instalado no es 368B.";
            return false;
        }

        if (dockRect == null || dockRect != (transform as RectTransform))
        {
            error = "El HUD 368B no tiene un RectTransform coherente.";
            return false;
        }

        if (background == null ||
            GetComponent<HorizontalLayoutGroup>() == null)
        {
            error = "El HUD 368B no tiene fondo o layout horizontal.";
            return false;
        }

        if (clockDisplay == null ||
            clockText == null ||
            clockDisplay.transform.parent != transform)
        {
            error = "El reloj no está integrado en el dock 368B.";
            return false;
        }

        if (pauseButton == null || pauseButton.transform.parent != transform)
        {
            error = "El control de pausa no está integrado en el dock 368B.";
            return false;
        }

        if (speedButtons == null || speedButtons.Length != 3)
        {
            error = "El dock 368B necesita exactamente tres velocidades.";
            return false;
        }

        for (int index = 0; index < speedButtons.Length; index++)
        {
            if (speedButtons[index] == null ||
                speedButtons[index].transform.parent != transform)
            {
                error = "Una velocidad no está integrada en el dock 368B.";
                return false;
            }
        }

        if (dockRect.anchorMin != new Vector2(1f, 0f) ||
            dockRect.anchorMax != new Vector2(1f, 0f) ||
            dockRect.pivot != new Vector2(1f, 0f))
        {
            error = "El dock 368B no está anclado abajo a la derecha.";
            return false;
        }

        return true;
    }
}
