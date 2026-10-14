using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderGalleryVivaQaDriver
{
    private IEnumerator VerifyCatalogTransforms(Transform view, RestaurantPlaceableItemDefinition item)
    {
        var interaction = FindFirstObjectByType<RestaurantEditInteractionController>();
        Vector3 baseline = item.DimensionsCentimeters;
        Check(interaction.CanConfigureInspectorDimensions(item), "Published table has real dimensions and editable policy");
        var image = view.Find("Preview/Image").GetComponent<Image>();
        Check(image.rectTransform.pivot == new Vector2(.5f, .5f) &&
            Mathf.Abs(image.rectTransform.anchoredPosition.x - 143) < .1f, "Main preview preserves aspect around the actual frame center");
        var width = view.Find("Width").GetComponent<TMP_InputField>();
        var height = view.Find("Height").GetComponent<TMP_InputField>();
        var depth = view.Find("Depth").GetComponent<TMP_InputField>();
        Check(width.interactable && height.interactable && depth.interactable, "All dimension inputs are genuinely interactive");
        ClickThroughRaycaster(width.transform.Find("Presets").GetComponent<Button>());
        yield return null;
        ClickThroughRaycaster(view.Find("NumericPresets/Value3").GetComponent<Button>());
        Check(Mathf.Abs(interaction.GetInspectorDimensions(item).x - baseline.x * 1.25f) < .01f,
            "Dimension dropdown receives pointer raycast and configures real placement draft");
        ClickThroughRaycaster(view.Find("DimensionRatio").GetComponent<Button>());
        width.onEndEdit.Invoke((baseline.x * 1.5f).ToString());
        Vector3 linked = interaction.GetInspectorDimensions(item);
        Check(Mathf.Abs(linked.x - baseline.x * 1.5f) < .01f && Mathf.Abs(linked.y - baseline.y * 1.2f) < .01f &&
            Mathf.Abs(linked.z - baseline.z * 1.2f) < .01f, "Chain control changes dimensions proportionally");
        ClickThroughRaycaster(view.Find("DimensionRatio").GetComponent<Button>());
        depth.onEndEdit.Invoke((baseline.z * .75f).ToString());
        Check(Mathf.Abs(interaction.GetInspectorDimensions(item).z - baseline.z * .75f) < .01f &&
            Mathf.Abs(interaction.GetInspectorDimensions(item).x - baseline.x * 1.5f) < .01f, "Unlocked dimensions are independently editable");
        var configured = interaction.GetInspectorDimensions(item);
        width.onEndEdit.Invoke("-2");
        Check((interaction.GetInspectorDimensions(item) - configured).sqrMagnitude < .001f && width.text != "-2", "Negative dimension is rejected and input restores authoritative value");
        Check(!interaction.TrySetInspectorDimensions(item, null, baseline * 100) &&
            !interaction.TrySetInspectorDimensions(item, null, new Vector3(float.NaN, 20, 20)), "Invalid or extreme dimensions never reach placement");
        string[] axes = { "X", "Y", "Z" }; float[] angles = { 15, 45, 25 };
        for (int i = 0; i < 3; i++)
        {
            var field = view.Find("Rotation" + axes[i]).GetComponent<TMP_InputField>();
            Check(field.interactable, "Rotation " + axes[i] + " has an active native numeric field");
            field.onEndEdit.Invoke(angles[i].ToString());
        }
        var rotation = interaction.GetInspectorRotation(item);
        Check(Quaternion.Angle(Quaternion.Euler(rotation), Quaternion.Euler(15, 45, 25)) < .01f,
            "All three rotation fields configure the exact requested orientation");
        Check(interaction.TryBeginPlaceableCreation(item), "Configured catalog item enters canonical provisional creation");
        var creation = FindFirstObjectByType<RestaurantPlaceableCreationService>();
        var provisional = creation.ActiveProvisionalPlaceable;
        Check(provisional != null && (interaction.GetInspectorDimensions(item) - configured).sqrMagnitude < .01f &&
            Quaternion.Angle(provisional.transform.rotation, Quaternion.Euler(rotation)) < .01f,
            "Creation consumes dimensions and XYZ rotation, rather than resetting them");
        var footprint = provisional.GetComponent<RestaurantPlacementFootprint>().BuildCurrentShape();
        foreach (var renderer in provisional.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds b = renderer.bounds;
            Check(footprint.Center.x - footprint.HalfWidth <= b.min.x + .01f &&
                footprint.Center.x + footprint.HalfWidth >= b.max.x - .01f &&
                footprint.Center.z - footprint.HalfDepth <= b.min.z + .01f &&
                footprint.Center.z + footprint.HalfDepth >= b.max.z - .01f,
                "Tilted candidate footprint conservatively encloses real geometry: " + renderer.name);
        }
        var modified = configured; modified.y *= .8f;
        Check(interaction.TrySetInspectorDimensions(item, null, modified) &&
            (interaction.GetInspectorDimensions(item) - modified).sqrMagnitude < .01f,
            "Dimension edit during placement changes the actual provisional instance");
        Check(interaction.CancelActivePlacement(), "Cancel destroys configured provisional placement safely");
        interaction.TrySetInspectorDimensions(item, null, baseline);
        interaction.TrySetInspectorRotation(item, null, Vector3.zero);
        yield return null;
        Check(creation.ActiveProvisionalPlaceable == null, "Canceled draft leaves no placed instance");
    }

    private IEnumerator VerifyWorldTransformsAndPersistence()
    {
        var interaction = FindFirstObjectByType<RestaurantEditInteractionController>();
        var registry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        var validation = FindFirstObjectByType<RestaurantPlacementValidationService>();
        var selection = FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>();
        interaction.ClearSelection();
        var target = registry.RegisteredPlaceables.Where(p => p != null && p.ItemDefinition != null &&
            interaction.CanConfigureInspectorDimensions(p.ItemDefinition) && p.ItemDefinition.EditableDefinition.CanRotate &&
            !p.TryGetComponent<RestaurantTable>(out _) && !p.TryGetComponent<RestaurantSeat>(out _) &&
            validation.ValidateCurrentPlacement(p.GetComponent<RestaurantAreaMember>()).IsValid).FirstOrDefault();
        if (target == null)
        {
            var definition = FindFirstObjectByType<RestaurantPlaceableCatalogService>().AvailableItems.First(p =>
                interaction.CanConfigureInspectorDimensions(p) && p.EditableDefinition.CanRotate &&
                !p.Prefab.TryGetComponent<RestaurantTable>(out _) && !p.Prefab.TryGetComponent<RestaurantSeat>(out _));
            var creation = FindFirstObjectByType<RestaurantPlaceableCreationService>();
            Check(creation.TryBeginCreation(definition, Vector3.zero, Quaternion.identity, null, out target, out _), "Canonical nonfunctional fixture creation starts");
            bool valid = false;
            for (int x = -20; x <= 20 && !valid; x++) for (int z = -20; z <= 20 && !valid; z++)
            {
                interaction.PlacementTransactionService.TryPreviewPlacement(new Vector3(x * .5f, target.transform.position.y, z * .5f), Quaternion.identity, out var result, out _);
                valid = result.IsValid;
            }
            Check(valid && creation.TryCommitActiveCreation(out _), "Fixture is actually committed at a valid position through BBSIS");
        }
        Check(target != null && interaction.TrySelectPlaceable(target), "Real single selection opens editable inspector");
        selection.RefreshSelection();
        for (int i = 0; i < 12; i++) yield return null;
        var panel = FindFirstObjectByType<RestaurantPlaceableCatalogPanel>();
        var view = panel.GetComponentInParent<Canvas>().transform.Find("BB_EditorV2_SelectionInspector/ReferenceDetails");
        var width = view.Find("Width").GetComponent<TMP_InputField>();
        var item = target.ItemDefinition; Vector3 original = interaction.GetInspectorDimensions(item, target);
        var originalScale = target.transform.localScale; var originalRotation = target.transform.rotation;
        var history = FindFirstObjectByType<BistroBuilderEditorV2GlobalHistory>(); int historyCount = history.UndoCount;
        width.onEndEdit.Invoke((original.x * .6f).ToString());
        Check(Mathf.Abs(interaction.GetInspectorDimensions(item, target).x - original.x * .6f) < .01f &&
            history.UndoCount == historyCount + 1 && !interaction.HasActivePlacement,
            "Selected dimension edit commits one canonical history command without starting pointer drag");
        Check(history.TryUndo(out _) && (target.transform.localScale - originalScale).sqrMagnitude < .001f, "Global Undo restores exact original dimensions");
        Check(history.TryRedo(out _) && Mathf.Abs(interaction.GetInspectorDimensions(item, target).x - original.x * .6f) < .01f,
            "Global Redo restores resized geometry");
        Check(interaction.TrySetInspectorDimensions(item, target, original * .6f), "Shrink all dimensions through real transaction");
        float[] angles = { 10, 25, 10 }; string[] axes = { "X", "Y", "Z" };
        for (int i = 0; i < 3; i++)
        {
            var field = view.Find("Rotation" + axes[i]).GetComponent<TMP_InputField>();
            field.onEndEdit.Invoke(angles[i].ToString()); yield return null;
        }
        Check(Quaternion.Angle(target.transform.rotation, Quaternion.Euler(10, 25, 10)) < .01f,
            "Selected XYZ rotation fields commit actual world orientation");
        var finalScale = target.transform.localScale; var finalRotation = target.transform.rotation;
        var countBefore = history.UndoCount;
        Check(!interaction.TrySetInspectorDimensions(item, target, original * 100) && history.UndoCount == countBefore &&
            (target.transform.localScale - finalScale).sqrMagnitude < .001f, "Rejected world edit creates no history and leaves exact geometry intact");
        Check(history.TryUndo(out _) && Quaternion.Angle(target.transform.rotation, finalRotation) > 1, "World rotation participates in global Undo");
        Check(history.TryRedo(out _) && Quaternion.Angle(target.transform.rotation, finalRotation) < .01f, "World rotation participates in global Redo");
        string id = target.InstanceId;
        interaction.ClearSelection();
        var provider = FindFirstObjectByType<RestaurantStructureSaveSectionProvider>();
        var capture = new BistroBuilderSaveCaptureContext(988);
        var routine = provider.CaptureState(capture); while (routine.MoveNext()) yield return routine.Current;
        Check(!capture.HasFailed && capture.State is RestaurantStructureSaveData, "Canonical persistence captures real resized and rotated world");
        var data = (RestaurantStructureSaveData)capture.State;
        string path = "Logs/GalleryViva/r8_structure_roundtrip.json";
        File.WriteAllText(path, JsonUtility.ToJson(data));
        var loaded = JsonUtility.FromJson<RestaurantStructureSaveData>(File.ReadAllText(path));
        Check(provider.ValidateState(loaded, out _), "Actual JSON file round trip validates through canonical structure provider");
        var context = new BistroBuilderSaveLoadContext(988, false, 64);
        routine = provider.PrepareForLoad(context); while (routine.MoveNext()) yield return routine.Current;
        routine = provider.ApplyState(loaded, context); while (routine.MoveNext()) yield return routine.Current;
        provider.FinalizeLoad(context);
        Check(!context.HasFailed && registry.TryGetByInstanceId(id, out target) &&
            (target.transform.localScale - finalScale).sqrMagnitude < .001f && Quaternion.Angle(target.transform.rotation, finalRotation) < .01f,
            "Canonical reload reconstructs exact dimensions and XYZ orientation with same instance identity");
        Check(registry.RegisteredPlaceables.Count == data.placeables.Count &&
            registry.RegisteredPlaceables.Select(p => p.InstanceId).Distinct().Count() == data.placeables.Count,
            "Round trip duplicates no placed objects or IDs");
    }
}
