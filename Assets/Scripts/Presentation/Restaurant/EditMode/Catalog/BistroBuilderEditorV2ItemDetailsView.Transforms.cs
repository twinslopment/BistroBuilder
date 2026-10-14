using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderEditorV2ItemDetailsView
{
    private Action<Vector3> resize, orient;
    private Func<Vector3> readDimensions, readRotation;
    private Func<string> readMessage;
    private Vector3 displayedDimensions, displayedRotation;
    private TMP_Text transformStatus;
    private RectTransform numericMenu;
    private Button ratio;
    private bool ratioLocked;

    private TMP_InputField NumericField(string name, float x, float y, float w, bool dimension, int axis)
    {
        var surface = BistroBuilderGalleryViva.Surface(name, root, x, y, w, 28);
        var input = surface.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = surface;
        float textX = dimension ? 8 : 5, textWidth = dimension ? w - 40 : 38;
        var viewport = BistroBuilderGalleryViva.Node("TextArea", input.transform, textX, 0, textWidth, 28);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = gallery.Label("Value", viewport, "", 0, 0, textWidth, 28, 13);
        value.richText = false;
        input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)value;
        input.fontAsset = value.font; input.pointSize = 13; input.characterLimit = 8;
        input.lineType = TMP_InputField.LineType.SingleLine; input.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
        input.selectionColor = new Color32(232, 181, 79, 100);
        gallery.Label("Unit", input.transform, dimension ? "cm" : "°", dimension ? w - 34 : w - 20, 0, dimension ? 17 : 7, 28, 11, false, BistroBuilderGalleryVivaArt.Muted);
        var arrow = gallery.TextButton("Presets", input.transform, "", w - (dimension ? 18 : 14), 0, dimension ? 18 : 14, 28,
            () => ToggleNumericMenu(input, dimension, axis), 12, false);
        BistroBuilderGalleryViva.Symbol("Arrow", arrow.transform, 4, 10, 9, 7, BistroBuilderGalleryVivaSymbol.Shape.Down);
        input.onEndEdit.AddListener(text => SetNumericValue(dimension, axis, text));
        return input;
    }

    private TMP_InputField DimensionField(string name, string label, float y, int axis)
    {
        gallery.Label(name + "Label", root, label, 27, y, 94, 29, 14);
        return NumericField(name, 125, y, 132, true, axis);
    }

    private TMP_InputField RotationField(string axis, float x, int component)
    {
        gallery.Label(axis + "Axis", root, axis, x, 458, 17, 29, 12);
        return NumericField("Rotation" + axis, x + 21, 458, 64, false, component);
    }

    public void RefreshTransformValues()
    {
        Vector3 dims = readDimensions != null ? readDimensions() : definition != null ? definition.DimensionsCentimeters : Vector3.zero;
        Vector3 angles = readRotation != null ? readRotation() : displayedRotation;
        if (dims != displayedDimensions || width.text.Length == 0)
        {
            displayedDimensions = dims; SetField(width, dims.x); SetField(depth, dims.z); SetField(height, dims.y);
        }
        if (angles != displayedRotation || rx.text.Length == 0)
        {
            displayedRotation = angles; SetField(rx, Mathf.Repeat(angles.x, 360));
            SetField(ry, Mathf.Repeat(angles.y, 360)); SetField(rz, Mathf.Repeat(angles.z, 360));
        }
        bool canResize = resize != null, canRotate = orient != null;
        SetAvailability(width, canResize); SetAvailability(depth, canResize); SetAvailability(height, canResize);
        SetAvailability(rx, canRotate); SetAvailability(ry, canRotate); SetAvailability(rz, canRotate);
        ratio.interactable = canResize;
        ratio.GetComponent<Image>().sprite = BistroBuilderGalleryVivaArt.Surface(ratioLocked && canResize);
        if (transformStatus != null && readMessage != null) transformStatus.text = readMessage() ?? "";
    }

    private static void SetAvailability(TMP_InputField field, bool value)
    {
        field.interactable = value;
        field.transform.Find("Presets").GetComponent<Button>().interactable = value;
    }

    private static void SetField(TMP_InputField field, float value)
    {
        if (!field.isFocused) field.SetTextWithoutNotify(value > 0 ? value.ToString("0.#", CultureInfo.CurrentCulture) : "0");
    }

    private void SetNumericValue(bool dimension, int axis, string text)
    {
        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            float.IsNaN(value) || float.IsInfinity(value))
        {
            if (transformStatus != null) transformStatus.text = "Introduce un número válido.";
            RestoreNumericValues(); return;
        }
        if (dimension && resize != null)
        {
            var next = readDimensions != null ? readDimensions() : displayedDimensions;
            if (ratioLocked && next[axis] > 0) next *= value / next[axis]; else next[axis] = value;
            resize(next);
        }
        else if (!dimension && orient != null)
        {
            var next = readRotation != null ? readRotation() : displayedRotation; next[axis] = value; orient(next);
        }
        RestoreNumericValues();
    }

    private void RestoreNumericValues()
    {
        displayedDimensions = displayedRotation = new Vector3(float.MinValue, 0, 0);
        RefreshTransformValues();
        // EndEdit can run before TMP releases focus; always restore the canonical
        // value after a rejected request rather than retaining typed invalid text.
        var dims = readDimensions != null ? readDimensions() : definition != null ? definition.DimensionsCentimeters : Vector3.zero;
        var angles = readRotation != null ? readRotation() : Vector3.zero;
        width.SetTextWithoutNotify(dims.x.ToString("0.#")); depth.SetTextWithoutNotify(dims.z.ToString("0.#")); height.SetTextWithoutNotify(dims.y.ToString("0.#"));
        rx.SetTextWithoutNotify(Mathf.Repeat(angles.x, 360).ToString("0.#")); ry.SetTextWithoutNotify(Mathf.Repeat(angles.y, 360).ToString("0.#")); rz.SetTextWithoutNotify(Mathf.Repeat(angles.z, 360).ToString("0.#"));
    }

    private void ToggleNumericMenu(TMP_InputField field, bool dimension, int axis)
    {
        if (definition == null || !field.interactable) return;
        if (numericMenu != null) { CloseNumericMenu(); return; }
        float baseline = dimension ? definition.DimensionsCentimeters[axis] : 0;
        float[] values = dimension ? new[] { baseline * .5f, baseline * .75f, baseline, baseline * 1.25f, baseline * 1.5f }
            : new[] { 0f, 45f, 90f, 180f, 270f };
        var fieldRect = (RectTransform)field.transform;
        float x = Mathf.Min(fieldRect.anchoredPosition.x, 176);
        float y = -fieldRect.anchoredPosition.y + 29;
        numericMenu = BistroBuilderGalleryViva.Surface("NumericPresets", root, x, y, 132, 147).rectTransform;
        numericMenu.SetAsLastSibling();
        for (int i = 0; i < values.Length; i++)
        {
            float choice = values[i];
            gallery.TextButton("Value" + i, numericMenu, choice.ToString("0.#") + (dimension ? " cm" : "°"), 4, 4 + i * 28, 124, 27,
                () => { CloseNumericMenu(); SetNumericValue(dimension, axis, choice.ToString(CultureInfo.InvariantCulture)); }, 13, false);
        }
    }

    public void CloseNumericMenu()
    {
        if (numericMenu == null) return;
        numericMenu.gameObject.SetActive(false); UnityEngine.Object.Destroy(numericMenu.gameObject); numericMenu = null;
    }

    private static void CenterPicture(Image picture)
    {
        var rect = picture.rectTransform; Vector2 size = rect.sizeDelta;
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
    }
}
