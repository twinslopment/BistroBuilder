using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Editor V2 chrome integration smoke test in the real restaurant scene.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2ChromeRuntimeSelfTest
{
    private const string Stage = "BB.EditorV2Chrome.Stage";
    private const string Result = "BB.EditorV2Chrome.Result";
    private const string Report = "EditorV2_UI_RuntimeSmoke_Report.txt";

    static BistroBuilderEditorV2ChromeRuntimeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/V2 Chrome Runtime Smoke")]
    public static void RunFromMenu() => Run(false);

    public static void RunFromCommandLine() => Run(true);

    private static void Run(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El editor ya está ejecutando Play Mode.");
        File.WriteAllText(Path.GetFullPath(Report), "UI chrome smoke: starting\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage, batch ? "start_cli" : "start_menu");
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange next)
    {
        string stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0)
            return;

        if (next == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__BB_UIChromeV2_Smoke")
                .AddComponent<BistroBuilderEditorV2ChromeSmokeDriver>();

        if (next == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("_cli", StringComparison.Ordinal))
                EditorApplication.Exit(
                    SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }

    public static void Finish(bool pass, string details)
    {
        File.WriteAllText(Path.GetFullPath(Report),
            "UI Chrome PlayMode: " + (pass ? "PASS\n" : "FAIL\n") +
            details + "\n");
        SessionState.SetBool(Result, pass);
        string stage = SessionState.GetString(Stage, "start_menu");
        SessionState.SetString(Stage,
            stage.EndsWith("_cli", StringComparison.Ordinal)
                ? "end_cli" : "end_menu");
        if (pass) Debug.Log("[UI Chrome V2] PASS " + details);
        else Debug.LogError("[UI Chrome V2] FAIL " + details);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2ChromeSmokeDriver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int i = 0; i < 12; i++)
            yield return null;

        try
        {
            string result = Verify();
            BistroBuilderEditorV2ChromeRuntimeSelfTest.Finish(true, result);
        }
        catch (Exception ex)
        {
            BistroBuilderEditorV2ChromeRuntimeSelfTest.Finish(
                false, ex.ToString());
        }
    }

    private static string Verify()
    {
        var shell = Object.FindFirstObjectByType<BistroBuilderUiShell>(
            FindObjectsInactive.Include);
        var editMode = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var selection = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include);
        if (shell == null || editMode == null || selection == null)
            throw new InvalidOperationException(
                "No se encuentran las autoridades o el UI shell.");

        if (!editMode.IsEditModeActive &&
            !editMode.TryEnterEditMode(out _, out string error))
            throw new InvalidOperationException(
                "No se pudo entrar en edición: " + error);

        // The live shell reconciles its chrome on its scheduled refresh.
        var root = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var top = root.FirstOrDefault(t =>
            t.name == BistroBuilderUiShell.EditModeTopBarName);
        var bottom = root.FirstOrDefault(t =>
            t.name == BistroBuilderUiShell.EditModeBottomBarName);
        if (top == null || bottom == null)
            throw new InvalidOperationException("No se construyeron las barras.");

        Require(top.Find("OfficialLogo") != null, "Logo oficial");
        Require(top.Find("EditorV2ModeIdentity") != null, "Placa de edición");
        Require(top.Find("EditUndo") != null, "Deshacer");
        Require(top.Find("EditRedo") != null, "Rehacer");
        Require(top.Find("EditApply") != null, "Aplicar");
        Require(top.Find("EditDiscard") != null, "Descartar");
        Require(bottom.Find("ModeTools/Buttons/EditSelect") != null,
            "Seleccionar");
        Require(bottom.Find("ModeTools/Buttons/EditBuild") != null,
            "Colocar");
        Require(bottom.Find("ModeTools/Buttons/EditWalls") != null,
            "Construir");
        Require(bottom.Find("ModeTools/Buttons/EditSurfaces") != null,
            "Superficies");
        Require(bottom.Find("EditSelectionTools/Buttons/EditDelete") != null,
            "Eliminar");
        Require(bottom.Find("EditViewTools/Buttons/EditGrid") != null,
            "Cuadrícula");

        var dialog = root.FirstOrDefault(t =>
            t.name == "BB_EditorV2_ConfirmReform");
        Require(dialog != null && !dialog.gameObject.activeSelf,
            "Confirmación cerrada inicialmente");

        float topWidth = RequiredWidth(top);
        float bottomWidth = RequiredWidth(bottom);
        Require(topWidth <= 1280 - 24, "Topbar 1280: " + topWidth);
        Require(bottomWidth <= 1280 - 24, "Bottombar 1280: " + bottomWidth);
        Require(topWidth <= 1920 - 24, "Topbar 1920");
        Require(bottomWidth <= 1920 - 24, "Bottombar 1920");

        var snap = bottom.Find("EditViewTools/Buttons/EditSnap")
            .GetComponent<Button>();
        var views = bottom.Find("EditViewTools/Buttons/EditViews")
            .GetComponent<Button>();
        // Invalid or unconnected operations must be visibly unavailable.
        Require(!snap.interactable && !views.interactable,
            "No-op tools stay disabled");

        var catalog = Object.FindFirstObjectByType<
            RestaurantPlaceableCatalogPanel>(FindObjectsInactive.Include);
        Require(catalog != null, "Catálogo real disponible");
        bottom.Find("ModeTools/Buttons/EditSurfaces")
            .GetComponent<Button>().onClick.Invoke();
        Require(catalog.CurrentSection == RestaurantEditCatalogSection.Surfaces,
            "Superficies cambia el catálogo real");
        bottom.Find("ModeTools/Buttons/EditBuild")
            .GetComponent<Button>().onClick.Invoke();
        Require(catalog.CurrentSection == RestaurantEditCatalogSection.Build,
            "Colocar restaura el catálogo de muebles");
        top.Find("EditDiscard").GetComponent<Button>().onClick.Invoke();
        Require(!dialog.gameObject.activeSelf,
            "No hay confirmación destructiva sin cambios");

        return "Barras y logo creados, 13 controles requeridos, " +
            "confirmación inicialmente cerrada, ambas resoluciones sin " +
            "desbordamiento por anchura, controles no conectados deshabilitados." +
            " Top=" + topWidth + " px, bottom=" + bottomWidth + " px.";
    }

    private static float RequiredWidth(RectTransform rect)
    {
        var layout = rect.GetComponent<HorizontalLayoutGroup>();
        float total = layout != null ?
            layout.padding.left + layout.padding.right +
            Mathf.Max(0, rect.childCount - 1) * layout.spacing : 0f;
        foreach (Transform child in rect)
        {
            var item = child.GetComponent<LayoutElement>();
            if (item != null)
                total += item.minWidth;
        }
        return total;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("QA: " + message);
    }
}
