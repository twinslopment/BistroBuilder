using TMPro;
using UnityEngine;

public enum BistroBuilderUiStyleRole
{
    Auto = 0,
    Background = 1,
    Surface = 2,
    SurfaceElevated = 3,
    Overlay = 4,
    Panel = 5,
    Card = 6,
    PrimaryButton = 10,
    SecondaryButton = 11,
    ContextualButton = 12,
    TertiaryButton = 12,
    DestructiveButton = 13,
    NavButton = 14,
    Tab = 15,
    Field = 20,
    Row = 21,
    BadgeNeutral = 22,
    BadgeInfo = 23,
    BadgeSuccess = 24,
    BadgeAttention = 25,
    BadgeCritical = 26,
    ProgressTrack = 27,
    ProgressSuccess = 28,
    ProgressAttention = 29,

    // 30-34 are serialized by the original 21A contract.
    Title = 30,
    Heading = 31,
    Body = 32,
    Caption = 33,
    Kpi = 34,
    ProgressInfo = 35,
    ProgressCritical = 36,
    Subheading = 37,
    Label = 38,

    StatusSuccess = 40,
    StatusAttention = 41,
    StatusCritical = 42,
    StatusInfo = 43,
    StatusDisabled = 44,
    BottomDock = 50,
    Toast = 51
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Style Tag")]
public sealed class BistroBuilderUiStyleTag : MonoBehaviour
{    [SerializeField] private BistroBuilderUiStyleRole role = BistroBuilderUiStyleRole.Auto;
    [SerializeField] private bool preserveGraphicColor;
    [SerializeField] private bool preserveFontSize;
    [SerializeField] private bool preserveFont;

    public BistroBuilderUiStyleRole Role => role;
    public bool PreserveGraphicColor => preserveGraphicColor;
    public bool PreserveFontSize => preserveFontSize;
    public bool PreserveFont => preserveFont;

    public void Configure(
        BistroBuilderUiStyleRole newRole,
        bool keepGraphicColor = false,
        bool keepFontSize = false,
        bool keepFont = false)
    {
        role = newRole;
        preserveGraphicColor = keepGraphicColor;
        preserveFontSize = keepFontSize;
        preserveFont = keepFont;
    }
}

/// <summary>
/// Tema intercambiable. Recoleta se inyectará aquí cuando exista el asset/licencia.
/// </summary>
[CreateAssetMenu(fileName = "BistroBuilderUiTheme", menuName = "Bistro Builder/UI/Theme")]
public sealed class BistroBuilderUiTheme : ScriptableObject
{    [Header("Tipografía")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;

    [Header("Paleta")]
    [SerializeField] private Color background = BistroBuilderUiTokens.Background;
    [SerializeField] private Color surface1 = BistroBuilderUiTokens.Surface1;
    [SerializeField] private Color surface2 = BistroBuilderUiTokens.Surface2;
    [SerializeField] private Color surfaceElevated = BistroBuilderUiTokens.SurfaceElevated;
    [SerializeField] private Color textPrimary = BistroBuilderUiTokens.TextPrimary;
    [SerializeField] private Color textSecondary = BistroBuilderUiTokens.TextSecondary;
    [SerializeField] private Color textMuted = BistroBuilderUiTokens.TextMuted;
    [SerializeField] private Color primary = BistroBuilderUiTokens.Primary;
    [SerializeField] private Color warmAccent = BistroBuilderUiTokens.WarmAccent;
    [SerializeField] private Color success = BistroBuilderUiTokens.Success;
    [SerializeField] private Color attention = BistroBuilderUiTokens.Attention;
    [SerializeField] private Color critical = BistroBuilderUiTokens.Critical;
    [SerializeField] private Color info = BistroBuilderUiTokens.Info;

    public TMP_FontAsset TitleFont => titleFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public Color Background => background;
    public Color Surface1 => surface1;
    public Color Surface2 => surface2;
    public Color SurfaceElevated => surfaceElevated;    public Color TextPrimary => textPrimary;
    public Color TextSecondary => textSecondary;
    public Color TextMuted => textMuted;
    public Color Primary => primary;
    public Color WarmAccent => warmAccent;
    public Color Success => success;
    public Color Attention => attention;
    public Color Critical => critical;
    public Color Info => info;
}
