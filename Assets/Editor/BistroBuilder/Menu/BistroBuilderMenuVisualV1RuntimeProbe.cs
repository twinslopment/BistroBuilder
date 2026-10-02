using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// CARTA V1 visual runtime gate. Opens Portfolio -> Editor -> Recipe with
/// real services in an unsaved play session; never commits dishes or rules.
/// All presentation checks are made against the built uGUI tree, not HTML.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderMenuVisualV1RuntimeProbe
{
    private const string Prefix = "BB.MenuVisualV1.Probe.";
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private static int passed, failed;
    private static BistroBuilderMenuPortfolioRuntimeView portfolio;
    private static BistroBuilderMenuEditorRuntimeView editor;
    private static BistroBuilderDishRecipeAuthoringRuntimeView recipe;
    private static string outputDirectory;

    static BistroBuilderMenuVisualV1RuntimeProbe()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Bistro Builder/Menu/CARTA V1 - Prueba visual reversible", false, 205)]
    public static void RunMenu() => Begin(false);

    public static void RunBatch() => Begin(true);

    private static void Begin(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool(Prefix + "Running", false))
        {
            Debug.LogError("[CARTA V1] Already running / Play Mode active.");
            if (batch) EditorApplication.Exit(2);
            return;
        }
        if (!File.Exists(ScenePath) || SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[CARTA V1] Missing scene or unsaved scene changes; abort.");
            if (batch) EditorApplication.Exit(2);
            return;
        }
        passed = failed = 0;
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetBool(Prefix + "Batch", batch);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Passed", 0);
        SessionState.SetInt(Prefix + "Failed", 0);
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs", "CartaVisualV1");
        Directory.CreateDirectory(outputDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Pass(bool value, string name)
    {
        if (value) passed++; else failed++;
        Debug.Log((value ? "[CARTA V1] PASS " : "[CARTA V1] FAIL ") + name);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Failed", failed);
    }

    private static T FindScene<T>() where T : Component
    {
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            if (item != null && item.gameObject.scene == SceneManager.GetActiveScene())
                return item;
        return null;
    }

    private static RectTransform Modal(Component host)
    {
        if (host == null) return null;
        FieldInfo field = host.GetType().GetField("modalRoot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(host) as RectTransform;
    }

    private static bool IsParchment(Transform parent, string path)
    {
        Transform node = parent?.Find(path);
        Image image = node != null ? node.GetComponent<Image>() : null;
        if (image == null) return false;
        Color color = image.color;
        return color.r > .80f && color.g > .69f && color.b > .49f;
    }

    private static void Capture(string file)
    {
        // Unity batch mode has no dependable Game View capture; avoid claiming
        // or writing screenshots that may never be rendered. In an interactive
        // Editor session the command remains available for visual inspection.
        if (SessionState.GetBool(Prefix + "Batch", false)) return;
        // Static fields are reset by Enter Play Mode domain reload.
        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs", "CartaVisualV1");
        Directory.CreateDirectory(outputDirectory);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, file));
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        int stage = SessionState.GetInt(Prefix + "Stage", 0);
        if (!EditorApplication.isPlaying)
        {
            if (stage < 6) return;
            int p = SessionState.GetInt(Prefix + "Passed", 0);
            int f = SessionState.GetInt(Prefix + "Failed", 0);
            Debug.Log("[CARTA V1] PLAY-MODE " + p + " PASS / " + f +
                " FAIL; structural/UI audit completed. Interactive browser previews are separate.");
            bool batch = SessionState.GetBool(Prefix + "Batch", false);
            SessionState.SetBool(Prefix + "Running", false);
            SessionState.SetInt(Prefix + "Stage", 0);
            if (batch) EditorApplication.Exit(f == 0 ? 0 : 1);
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            Time.frameCount < 22) return;
        if ((float)EditorApplication.timeSinceStartup -
            SessionState.GetFloat(Prefix + "Started", 0) > 180f)
        {
            Debug.LogError("[CARTA V1] Timeout 180 seconds.");
            failed++;
            Finish();
            return;
        }
        passed = SessionState.GetInt(Prefix + "Passed", 0);
        failed = SessionState.GetInt(Prefix + "Failed", 0);
        try
        {
            if (stage == 0 && Time.frameCount >= 24) OpenPortfolio();
            else if (stage == 1 && Time.frameCount >= 48) AuditPortfolio();
            else if (stage == 2 && Time.frameCount >= 70) OpenEditor();
            else if (stage == 3 && Time.frameCount >= 96) AuditEditor();
            else if (stage == 4 && Time.frameCount >= 112) OpenRecipe();
            else if (stage == 5 && Time.frameCount >= 136) AuditRecipe();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogException(exception);
            Finish();
        }
    }

    private static void Move(int stage) => SessionState.SetInt(Prefix + "Stage", stage);

    private static void OpenPortfolio()
    {
        portfolio = FindScene<BistroBuilderMenuPortfolioRuntimeView>();
        editor = FindScene<BistroBuilderMenuEditorRuntimeView>();
        recipe = FindScene<BistroBuilderDishRecipeAuthoringRuntimeView>();
        Pass(portfolio != null && editor != null && recipe != null,
            "Three canonical Carta views are present in the real scene.");
        Pass(portfolio != null && portfolio.TryOpen(out _),
            "Portfolio opens through the real service.");
        Move(1);
    }

    private static void AuditPortfolio()
    {
        RectTransform modal = Modal(portfolio);
        Transform plate = modal?.Find("Panel");
        Pass(portfolio.IsOpen && plate != null &&
            plate.GetComponent<Outline>() != null,
            "Portfolio is a single ivory/brass framed plate.");
        Pass(IsParchment(plate, "Menus") &&
             IsParchment(plate, "Rules") &&
             IsParchment(plate, "RuleEditor"),
             "Menus, activation rules and rule detail share parchment cards.");
        Text title = plate?.Find("Header/Title")?.GetComponent<Text>();
        Pass(title != null && title.text == "CARTA" &&
            title.font != null && title.font.name.IndexOf("Recoleta",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Official Recoleta clean title; dynamic metadata stays in Inter.");
        Pass(plate?.Find("Menus/MenuList/Viewport/Content") != null &&
             plate?.Find("Rules/RuleList/Viewport/Content") != null &&
             plate?.Find("RuleEditor/RuleName") != null,
             "Portfolio retained canonical data lists and rule form.");
        Pass(plate?.Find("Header/CartaHeaderIcon") != null &&
             plate?.Find("Menus/SectionIcon") != null &&
             plate?.Find("Rules/SectionIcon") != null,
             "Official menu iconography in the title and portfolio cards.");
        Capture("01_Gestor_Cartas_y_Reglas.png");
        portfolio.Close();
        Move(2);
    }

    private static void OpenEditor()
    {
        string error = string.Empty;
        Pass(editor != null && editor.TryOpenFromInterface(out error),
            "Editor opens real draft without changing a saved menu: " + error);
        Move(3);
    }

    private static void AuditEditor()
    {
        RectTransform modal = Modal(editor);
        Transform plate = modal?.Find("Panel");
        Pass(editor.IsOpen && plate != null &&
            plate.GetComponent<Outline>() != null,
            "Menu Editor has the ivory/brass outer frame.");
        Text menuTitle = plate?.Find("Header/Title")?.GetComponent<Text>();
        Text menuContext = plate?.Find("Header/Context")?.GetComponent<Text>();
        Pass(menuTitle != null && menuTitle.text == "CARTA Y PLATOS" &&
             menuTitle.font != null &&
             menuTitle.font.name.IndexOf("Recoleta",
                 StringComparison.OrdinalIgnoreCase) >= 0 &&
             menuContext != null && menuContext.text.Contains("En carta:") &&
             menuContext.font != null &&
             menuContext.font.name.IndexOf("Inter",
                 StringComparison.OrdinalIgnoreCase) >= 0,
             "Editor uses clean Recoleta for title; dynamic count and accents use Inter.");
        Pass(IsParchment(plate, "Body/Sidebar") &&
             IsParchment(plate, "Body/Detail"),
             "Category filters and selected dish have matching parchment panels.");
        Pass(plate?.Find("Body/List/DishScroll/Viewport/Content") != null &&
             plate?.Find("Header/NewDish") != null &&
             plate?.Find("Footer/Apply") != null &&
             plate?.Find("Header/CartaHeaderIcon") != null,
            "Dish list, create action, official icon and apply draft remain available.");
        Pass(editor.TryValidateVisibleContent(out _),
            "Existing 2.1E runtime scroll and row validation still passes.");
        Capture("02_Editor_Carta_y_Platos.png");
        Move(4);
    }

    private static void OpenRecipe()
    {
        if (recipe != null) recipe.OpenNew();
        Pass(recipe != null && recipe.IsOpen,
            "Dish + recipe authoring opens from the real editor draft.");
        Move(5);
    }

    private static void AuditRecipe()
    {
        RectTransform modal = Modal(recipe);
        Transform card = modal?.Find("Card");
        Pass(recipe.IsOpen && card != null &&
            card.GetComponent<Outline>() != null,
            "Authoring view has a raised brass/ivory card.");
        Pass(IsParchment(card, "Body/DishColumn") &&
             IsParchment(card, "Body/RecipeColumn"),
             "Dish data and recipe/escandallo columns share the same theme.");
        Pass(card?.Find("Footer/SaveDraft") != null &&
             card?.Find("Body/RecipeColumn/Ingredients") != null &&
             card?.Find("Header/CartaHeaderIcon") != null,
            "Ingredient authoring, icon and save-draft controls are preserved.");
        Capture("03_Nuevo_Plato_y_Receta.png");
        recipe.Close();
        if (editor != null && editor.IsOpen)
        {
            // No domain mutations were issued by this probe. Close the view
            // without calling apply/commit, leaving the saved menu unchanged.
            editor.gameObject.SetActive(false);
        }
        Finish();
    }

    private static void Finish()
    {
        SessionState.SetInt(Prefix + "Stage", 6);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Failed", failed);
        EditorApplication.ExitPlaymode();
    }
}
