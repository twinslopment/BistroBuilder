using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real Play Mode smoke/regression of Gallery V2 and B8 Inspector.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2GalleryRuntimeSelfTest
{
    private const string Stage = "BB.EditorV2Gallery.Stage";
    private const string Result = "BB.EditorV2Gallery.Result";
    private const string Report = "EditorV2_GalleryInspector_Runtime_Report.txt";

    static BistroBuilderEditorV2GalleryRuntimeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/Gallery and Inspector Runtime")]
    public static void RunFromMenu() => StartTest(false);

    public static void RunFromCommandLine() => StartTest(true);

    private static void StartTest(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode ya está activo.");
        File.WriteAllText(Path.GetFullPath(Report), "Gallery/Inspector starting\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage, cli ? "running_cli" : "running_menu");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__GalleryInspectorV2_QA")
                .AddComponent<BistroBuilderEditorV2GalleryDriver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("_cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }

    public static void Finish(bool ok, string text)
    {
        File.WriteAllText(Path.GetFullPath(Report),
            (ok ? "PASS\n" : "FAIL\n") + text + "\n");
        SessionState.SetBool(Result, ok);
        SessionState.SetString(Stage,
            SessionState.GetString(Stage, "running_menu").EndsWith("_cli",
                StringComparison.Ordinal) ? "done_cli" : "done_menu");
        if (ok) Debug.Log("[EditorV2 Gallery Inspector] PASS " + text);
        else Debug.LogError("[EditorV2 Gallery Inspector] FAIL " + text);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2GalleryDriver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int i = 0; i < 16; i++) yield return null;
        string result = "";
        IEnumerator routine = Verify(s => result = s);
        while (true)
        {
            bool hasStep = false;
            object step = null;
            Exception failure = null;
            try
            {
                hasStep = routine.MoveNext();
                if (hasStep) step = routine.Current;
            }
            catch (Exception error)
            {
                failure = error;
            }
            if (failure != null)
            {
                BistroBuilderEditorV2GalleryRuntimeSelfTest.Finish(
                    false, failure.ToString());
                yield break;
            }
            if (!hasStep) break;
            yield return step;
        }
        BistroBuilderEditorV2GalleryRuntimeSelfTest.Finish(true, result);
    }

    private IEnumerator Verify(Action<string> finish)
    {
        var mode = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogPanel>(
            FindObjectsInactive.Include);
        var inspector = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionInspector>(FindObjectsInactive.Include);
        var legacy = Object.FindFirstObjectByType<RestaurantPlaceableInspectorPanel>(
            FindObjectsInactive.Include);
        var b8 = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);

        Assert(mode != null && catalog != null && inspector != null &&
               legacy != null && b8 != null && registry != null,
            "faltan componentes canónicos en la escena");

        if (!mode.IsEditModeActive &&
            !mode.TryEnterEditMode(out _, out string reason))
            throw new InvalidOperationException("Entrar edición: " + reason);
        catalog.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 4; i++) yield return null;

        Assert(catalog.GetComponents<BistroBuilderEditorV2SelectionInspector>().Length == 1,
            "no duplicar inspector B8");
        Assert(catalog.GetComponents<RestaurantPlaceableCatalogPreviewSkin>().Length == 1,
            "piel preview única");
        Assert(catalog.GetComponents<RestaurantPlaceableCatalogApprovedSkin>().Length == 1,
            "piel approved única");
        var content = catalog.transform.Find("CatalogContent");
        Assert(content != null, "Catálogo original");
        var title = content.Find("Header/PreviewTitle")?.GetComponent<TMP_Text>();
        Assert(title != null && title.text == "Catálogo Galería Viva",
            "Título Galería Viva: " + (title != null ? title.text : "NULL"));
        var recoleta = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        Assert(recoleta != null && title.font == recoleta,
            "Título oficial Recoleta aplicado al catálogo");
        var background = content.GetComponent<Image>();
        Assert(background != null && background.color.r > background.color.g &&
            background.color.g > background.color.b,
            "Crema cálido, sin oliva");
        catalog.SelectSection(RestaurantEditCatalogSection.Surfaces);
        for (int i = 0; i < 3; i++) yield return null;
        Assert(title.text.StartsWith("Taller ·", StringComparison.Ordinal),
            "Taller de superficies");
        catalog.SelectSection(RestaurantEditCatalogSection.Build);
        for (int i = 0; i < 2; i++) yield return null;

        var members = new List<BistroBuilderEditorV2Selection>();
        var definitions = new List<RestaurantPlaceableItemDefinition>();
        foreach (RestaurantPlaceableObject item in registry.RegisteredPlaceables)
        {
            if (item == null || !item.HasInstanceId || item.ItemDefinition == null)
                continue;
            members.Add(new BistroBuilderEditorV2Selection
            {
                family = BistroBuilderEditorV2ToolFamily.Furniture,
                kind = BistroBuilderEditorV2SelectionKind.Furniture,
                stableId = item.InstanceId,
                displayName = item.DisplayName,
                capabilities = BistroBuilderEditorV2SelectionCapability.Inspect,
                persistentIdentity = true
            });
            definitions.Add(item.ItemDefinition);
            if (members.Count == 2) break;
        }
        Assert(members.Count == 2, "dos artículos reales necesarios");
        legacy.ShowForDefinition(definitions[0]);
        var oldInspector = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "BB_UIUX_PlaceableInspector");
        Assert(oldInspector != null && oldInspector.gameObject.activeSelf,
            "ficha del catálogo accesible antes de seleccionar mundo");
        Assert(b8.ReplaceSelectionSet(members, members[0].stableId,
            out string replaceError), "B8 selection: " + replaceError);
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(inspector.IsShowing && inspector.VisibleSelectionCount == 2,
            "inspector reconoce conjunto real");
        Assert(!oldInspector.gameObject.activeSelf,
            "ficha antigua se oculta al seleccionar muebles del mundo");
        var root = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "BB_EditorV2_SelectionInspector");
        Assert(root != null, "raíz inspector V2");
        var count = root.Find("Panel/SelectionCount")?.GetComponent<TMP_Text>();
        Assert(count != null && count.text.Contains("2 artículos"),
            "contador muestra selección real");

        inspector.ExpandInspector();
        inspector.SetViewGroup(true);
        Assert(root.Find("Panel/ScrollArea/Viewport/Pages/GroupDetails")
            .gameObject.activeSelf, "vista Conjunto");
        var rows = root.Find("Panel/ScrollArea/Viewport/Pages/GroupDetails/Members");
        Assert(rows != null && rows.childCount == 2,
            "dos filas de miembros reales");
        Button switchButton = null;
        foreach (Transform row in rows)
        {
            var rowText = row.Find("MemberLabel")?.GetComponent<TMP_Text>();
            if (rowText != null &&
                !rowText.text.StartsWith("Principal", StringComparison.Ordinal))
                switchButton = row.GetComponent<Button>();
        }
        Assert(switchButton != null, "fila secundaria accesible");
        switchButton.onClick.Invoke();
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(b8.PrimarySelection.stableId == members[1].stableId,
            "botón de fila cambia el principal real B8");

        inspector.SetViewGroup(false);
        var label = root.Find("Panel/Heading/SelectedTitle")?.GetComponent<TMP_Text>();
        Assert(label != null && label.text == members[1].displayName,
            "datos del nuevo principal");
        inspector.CollapseInspector();
        Assert(!inspector.IsExpanded &&
            root.Find("InspectorRail").gameObject.activeSelf,
            "plegado conserva grupo");

        // Don't mutate world: only the B8 read model has been exercised.
        b8.ClearSelectionSet();
        inspector.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Assert(!inspector.IsShowing || !inspector.IsExpanded,
            "deselección no deja panel sobre el restaurante");

        finish("Galería Viva y Taller conectados; pieles únicas; marfil/latón; " +
            "2 muebles reales de registry; selección B8, Principal/Conjunto, " +
            "cambio de principal, plegado y deselección. Sin duplicar autoridad.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Gallery V2 QA: " + message);
    }
}
