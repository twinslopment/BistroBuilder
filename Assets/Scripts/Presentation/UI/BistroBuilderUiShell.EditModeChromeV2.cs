using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mode = BistroBuilderConstructionRuntimeMode;
using Symbol = BistroBuilderEditChromeSymbol;

/// <summary>
/// View-only Editor V2 chrome. Existing edit authorities own the actions:
/// Furniture, Construction, Surfaces, B5 renovation, B8 selection and history.
/// No game state is written by the view except through those actions.
/// </summary>
public sealed partial class BistroBuilderUiShell
{
    private const float EditCompactWidth = 1400f;
    private float lastEditChromeWidth = -1f;
    private TMP_Text editModeSelectionCountText;

    private void BuildEditTopChromeV2(RectTransform root)
    {
        var logo = ChromeBlock(root, "OfficialLogo", 144, 60);
        var tex = Resources.Load<Texture2D>(
            "BistroBuilder/UI/EditorV2/BB_Logo_Aprobado_Referencia");
        if (tex != null)
        {
            var image = NewUi("BistroBuilderOfficialLogo", logo)
                .AddComponent<RawImage>();
            image.texture = tex;
            image.raycastTarget = false;
            ChromeBox(image.rectTransform, 6, 2, 132, 56);
        }
        else
        {
            var fallback = ChromeText(logo, "BistroBuilderBrand",
                "BISTRO\nBUILDER", 19, EditChromeText);
            fallback.font = BistroBuilderTypography.Title;
            fallback.alignment = TextAlignmentOptions.Center;
            ChromeBox(fallback.rectTransform, 0, 2, 140, 55);
        }

        var badge = ChromeBlock(root, "EditorV2ModeIdentity", 160, 58);
        var badgeBg = badge.gameObject.AddComponent<Image>();
        badgeBg.sprite = EditChromeRoundedSprite();
        badgeBg.type = Image.Type.Sliced;
        badgeBg.color = new Color32(68, 44, 27, 255);
        ChromeIcon(badge, "EditingMark", Symbol.Pencil,
            new Color32(241, 198, 115, 255), new Vector2(28, 28), 32);
        var badgeTitle = ChromeText(badge, "Title", "EDITOR V2", 18,
            new Color32(255, 241, 211, 255));
        badgeTitle.font = BistroBuilderTypography.Title;
        ChromeBox(badgeTitle.rectTransform, 55, 7, 102, 29);
        var badgeSub = ChromeText(badge, "Subtitle", "MODO EDICIÓN", 10,
            new Color32(237, 196, 130, 255));
        ChromeBox(badgeSub.rectTransform, 55, 34, 103, 20);

        ChromeSpacer(root, "V2LeadSpacer");
        var venue = ChromeBlock(root, "V2Venue", 182, 51);
        var venueName = ChromeText(venue, "VenueName",
            "Restaurante", 17, EditChromeText);
        venueName.font = BistroBuilderTypography.Title;
        venueName.alignment = TextAlignmentOptions.Center;
        ChromeBox(venueName.rectTransform, 0, 1, 180, 28);
        editModeSelectionCountText = ChromeText(
            venue, "RenovationStatus", "Modo edición", 11, EditChromeMuted);
        editModeSelectionCountText.alignment = TextAlignmentOptions.Center;
        ChromeBox(editModeSelectionCountText.rectTransform, 0, 29, 180, 20);
        ChromeSpacer(root, "V2RightSpacer");

        ChromeButton(root, "EditUndo", Symbol.Undo, "", 44, 47,
            "Deshacer el último cambio", () =>
            {
                if (editChromeGlobalHistory != null)
                {
                    if (!editChromeGlobalHistory.TryUndo(out var error))
                        ChromeMessage(error);
                }
                else if (IsFurnitureTool())
                    editModeFurnitureController?.TryUndoLastPlacement();
                else
                    editModeConstructionTool?.TryUndo(out _);
            });
        ChromeButton(root, "EditRedo", Symbol.Redo, "", 44, 47,
            "Rehacer el último cambio", () =>
            {
                if (editChromeGlobalHistory != null)
                {
                    if (!editChromeGlobalHistory.TryRedo(out var error))
                        ChromeMessage(error);
                }
                else if (IsFurnitureTool())
                    editModeFurnitureController?.TryRedoLastPlacement();
                else
                    editModeConstructionTool?.TryRedo(out _);
            });

        var clock = ChromeBlock(root, "V2Clock", 82, 49);
        ChromeIcon(clock, "Sun", Symbol.Sun,
            new Color32(181, 118, 36, 255), new Vector2(18, 25), 23);
        editModeClockText = ChromeText(clock, "Time", "—", 12,
            EditChromeMuted);
        ChromeBox(editModeClockText.rectTransform, 33, 0, 49, 49);

        editModeMoneyText = ChromeText(root, "EditMoney", "—", 13,
            EditChromeText);
        ChromeWidth(editModeMoneyText.gameObject, 94, 46);

        ChromeButton(root, "EditDiscard", Symbol.Delete, "Descartar", 110,
            47, "Revisar antes de descartar toda la reforma",
            () => RequestEditV2Confirmation(EditV2Confirmation.Discard), true);
        ChromeButton(root, "EditApply", Symbol.Play, "Aplicar", 102, 47,
            "Aplicar la reforma completa solo si es válida",
            () => RequestEditV2Confirmation(EditV2Confirmation.Apply));
        ChromeButton(root, "EditPlay", Symbol.Home, "", 46, 47,
            "Volver al restaurante; se protegen los cambios pendientes",
            () => RequestEditV2Confirmation(EditV2Confirmation.Exit));
    }

    private void BuildEditBottomChromeV2(RectTransform root)
    {
        var mode = EditV2Group(root, "ModeTools", "MODO");
        ChromeButton(mode, "EditSelect", Symbol.Hand, "Seleccionar", 84, 64,
            "Seleccionar mobiliario o estructura",
            () =>
            {
                editModeFurnitureController?.CancelActivePlacement();
                editModeConstructionTool?.SetMode(Mode.Select);
            }, false, true);
        ChromeButton(mode, "EditBuild", Symbol.Chair, "Colocar", 82, 64,
            "Abrir Galería Viva para colocar mobiliario",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Build),
            false, true);
        ChromeButton(mode, "EditWalls", Symbol.Walls, "Construir", 86, 64,
            "Abrir Taller de construcción",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Walls),
            false, true);
        ChromeButton(mode, "EditSurfaces", Symbol.Surfaces, "Superficies", 92,
            64, "Abrir Taller de superficies",
            () => editModeCatalogPanel?.SelectSection(RestaurantEditCatalogSection.Surfaces),
            false, true);

        var edit = EditV2Group(root, "EditSelectionTools", "EDITAR");
        ChromeButton(edit, "EditMove", Symbol.Move, "Mover", 77, 64,
            "Mover la selección si es compatible",
            () => editModeFurnitureController?.TryBeginMoveSelected(),
            false, true);
        ChromeButton(edit, "EditRotate", Symbol.Rotate, "Girar", 77, 64,
            "Girar la selección", RotateChromeSelection, false, true);
        ChromeButton(edit, "EditDuplicate", Symbol.Duplicate, "Duplicar", 83,
            64, "Duplicar la selección", DuplicateChromeSelection, false, true);
        ChromeButton(edit, "EditDelete", Symbol.Delete, "Eliminar", 83, 64,
            "Eliminar objetos seleccionados con las reglas reales",
            DeleteChromeSelection, true, true);

        var help = EditV2Group(root, "EditViewTools", "AYUDAS");
        ChromeButton(help, "EditSnap", Symbol.Move, "Snapping", 88, 64,
            "Ajuste contextual; no sustituye la validación",
            null, false, true);
        ChromeButton(help, "EditGrid", Symbol.Grid, "Cuadrícula", 91, 64,
            "Mostrar u ocultar la cuadrícula",
            () =>
            {
                if (editChromeGrid != null)
                    editChromeGrid.enabled = !editChromeGrid.enabled;
            }, false, true);
        ChromeButton(help, "EditViews", Symbol.Terrain, "Vistas", 75, 64,
            "Utiliza la cámara actual del restaurante",
            null, false, true);

        // Safe before the first refresh: never expose invalid operations.
        foreach (string key in new[]
        {
            "EditMove", "EditRotate", "EditDuplicate", "EditDelete",
            "EditSnap", "EditViews", "EditApply", "EditDiscard",
            "EditUndo", "EditRedo"
        })
        {
            if (editChromeButtons.TryGetValue(key, out Button control))
                control.interactable = false;
        }
    }

    private Transform EditV2Group(Transform parent, string key, string title)
    {
        float width = key == "ModeTools" ? 377f :
            key == "EditSelectionTools" ? 346f : 280f;
        var group = ChromeBlock(parent, key, width, 85);
        var vertical = group.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 0;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandHeight = false;
        var caption = ChromeText(group, "Caption", title, 12, EditChromeText);
        caption.font = BistroBuilderTypography.Title;
        caption.alignment = TextAlignmentOptions.Center;
        ChromeWidth(caption.gameObject, width, 17);
        var row = NewUi("Buttons", group).GetComponent<RectTransform>();
        ChromeWidth(row.gameObject, width, 66);
        var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 2;
        horizontal.padding = new RectOffset(0, 0, 0, 0);
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        return row;
    }

    private void UpdateEditChromeResponsiveV2()
    {
        if (shellRoot == null || editModeTopBar == null ||
            editModeBottomBar == null)
            return;

        float screenWidth = shellRoot.rect.width;
        if (Mathf.Abs(screenWidth - lastEditChromeWidth) < 1f)
            return;
        lastEditChromeWidth = screenWidth;

        // Scale the content rather than reducing the clickable frame.
        // All widths derive from the container, never from desktop resolution.
        float available = Mathf.Max(860, screenWidth - 36f);
        float topScale = Mathf.Clamp(available / 1190f, .80f, 1f);
        float bottomScale = Mathf.Clamp(available / 1090f, .78f, 1f);
        editModeTopBar.localScale = new Vector3(topScale, 1f, 1f);
        editModeBottomBar.localScale = new Vector3(bottomScale, 1f, 1f);
    }
}
