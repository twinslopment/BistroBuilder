using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Instalador transaccional e idempotente del Bloque 11.</summary>
public static class BistroBuilderAdvancedOrders11Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string UiRootName = "BistroBuilderAdvancedOrdersUI";

    [MenuItem("Tools/Bistro Builder/Orders/11 - Instalar + validar", false, 11000)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report)) Debug.LogError(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Comandas 11", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        if (!BistroBuilderSceneLockGuard.TryEnsureWritable(ScenePath, 5, 150, out string lockError))
            throw new InvalidOperationException("BB Scene Lock Guard bloqueó 11: " + lockError);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 11.";
            return false;
        }
        string absoluteScene = Path.GetFullPath(ScenePath);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        try
        {
            GameObject host = FindUniqueNamed(scene, "GameSystems");
            if (host == null) throw new InvalidOperationException("No existe un GameSystems canónico único.");

            var canonical = RequireUnique<BistroBuilderCanonicalOrderService>(scene);
            var orderSystem = RequireUnique<OrderSystem>(scene);
            var inventory = RequireUnique<BistroBuilderOrderInventoryLifecycleService>(scene);
            var menu = RequireUnique<BistroBuilderRestaurantMenuService>(scene);
            var catalog = RequireUnique<BistroBuilderDishCatalogService>(scene);
            var advanced = EnsureUniqueOnHost<BistroBuilderAdvancedOrderService>(scene, host);
            var facade = EnsureUniqueOnHost<BistroBuilderAdvancedOrderPlayerFacade>(scene, host);

            Assign(advanced, "canonicalOrderService", canonical);
            Assign(advanced, "orderSystem", orderSystem);
            Assign(advanced, "inventoryLifecycle", inventory);
            Assign(facade, "advancedOrderService", advanced);
            Assign(facade, "canonicalOrderService", canonical);
            Assign(facade, "menuService", menu);
            Assign(facade, "dishCatalogService", catalog);
            if (!advanced.ValidateConfiguration(out string advancedError))
                throw new InvalidOperationException(advancedError);
            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);

            BuildUi(scene, facade);
            EditorUtility.SetDirty(advanced);
            EditorUtility.SetDirty(facade);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación del Bloque 11.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var validation = BistroBuilderAdvancedOrders11Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedOrders11SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("Bloque 11 no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");
            report = "Bloque 11 instalado correctamente.\n" + validation.BuildReport() +
                "\nAutotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 11 falló y fue restaurada. " + exception.Message;
            return false;
        }
    }

    private static void BuildUi(Scene scene, BistroBuilderAdvancedOrderPlayerFacade facade)
    {
        GameObject previous = FindDirectRoot(scene, UiRootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        GameObject ui = NewUi(UiRootName, null);
        SceneManager.MoveGameObjectToScene(ui, scene);
        Stretch(ui.GetComponent<RectTransform>());
        Canvas canvas = Add<Canvas>(ui); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 48;
        CanvasScaler scaler = Add<CanvasScaler>(ui); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
        Add<GraphicRaycaster>(ui);
        BistroBuilderAdvancedOrderPlayerScreen screen = Add<BistroBuilderAdvancedOrderPlayerScreen>(ui);

        GameObject root = NewUi("AdvancedOrdersModal", ui.transform); Stretch(root.GetComponent<RectTransform>());
        Add<Image>(root).color = new Color(0.025f, 0.03f, 0.028f, 0.995f);
        CanvasGroup group = Add<CanvasGroup>(root);
        CreateText(root.transform, "Title", "REVISIÓN DE COMANDAS", 31f, .03f,.92f,.40f,.98f, FontStyles.Bold);
        TMP_Text summary = CreateText(root.transform, "Summary", "", 16f, .42f,.93f,.82f,.98f);
        Button close = CreateButton(root.transform,"Close","Cerrar",.86f,.925f,.975f,.98f,Danger);

        GameObject listPanel = CreatePanel(root.transform,"Lines",.03f,.18f,.49f,.89f,Panel);
        TMP_Text list = CreateText(listPanel.transform,"List","",16f,.035f,.13f,.965f,.965f);
        list.alignment = TextAlignmentOptions.TopLeft; list.textWrappingMode = TextWrappingModes.Normal;
        Button prev = CreateButton(listPanel.transform,"Prev","◀ Línea",.035f,.025f,.47f,.105f,Inset);
        Button next = CreateButton(listPanel.transform,"Next","Línea ▶",.53f,.025f,.965f,.105f,Inset);

        GameObject detailPanel = CreatePanel(root.transform,"Detail",.51f,.18f,.975f,.89f,Panel);
        TMP_Text detail = CreateText(detailPanel.transform,"DetailText","",17f,.045f,.57f,.955f,.95f);
        detail.textWrappingMode = TextWrappingModes.Normal; detail.alignment = TextAlignmentOptions.TopLeft;
        TMP_Text replacement = CreateText(detailPanel.transform,"Replacement","",15f,.045f,.48f,.955f,.56f,FontStyles.Bold);
        replacement.textWrappingMode = TextWrappingModes.Normal;
        Button prevDish = CreateButton(detailPanel.transform,"PrevDish","◀ Plato",.045f,.415f,.47f,.475f,Inset);
        Button nextDish = CreateButton(detailPanel.transform,"NextDish","Plato ▶",.53f,.415f,.955f,.475f,Inset);
        Button correct = CreateButton(detailPanel.transform,"Correct","Corregir",.045f,.32f,.31f,.39f,Accent);
        Button repeat = CreateButton(detailPanel.transform,"Repeat","Repetir",.365f,.32f,.635f,.39f,Accent);
        Button cancel = CreateButton(detailPanel.transform,"Cancel","Cancelar línea",.69f,.32f,.955f,.39f,Danger);
        Button incident = CreateButton(detailPanel.transform,"Incident","Incidencia",.045f,.225f,.31f,.295f,Warn);
        Button replace = CreateButton(detailPanel.transform,"Replace","Reponer",.365f,.225f,.635f,.295f,Accent);
        Button courtesy = CreateButton(detailPanel.transform,"Courtesy","Cortesía",.69f,.225f,.955f,.295f,Accent);
        Button returnButton = CreateButton(detailPanel.transform,"Return","Devolver / retirar",.365f,.13f,.635f,.20f,Danger);
        TMP_Text feedback = CreateText(detailPanel.transform,"Feedback","",14f,.045f,.025f,.955f,.115f);
        feedback.textWrappingMode = TextWrappingModes.Normal;

        Assign(screen,"facade",facade); Assign(screen,"panelRoot",root); Assign(screen,"canvasGroup",group);
        Assign(screen,"closeButton",close); Assign(screen,"previousLineButton",prev); Assign(screen,"nextLineButton",next);
        Assign(screen,"previousReplacementButton",prevDish); Assign(screen,"nextReplacementButton",nextDish);
        Assign(screen,"correctButton",correct); Assign(screen,"repeatButton",repeat); Assign(screen,"cancelButton",cancel);
        Assign(screen,"incidentButton",incident); Assign(screen,"replaceButton",replace); Assign(screen,"courtesyButton",courtesy);
        Assign(screen,"returnButton",returnButton); Assign(screen,"summaryText",summary); Assign(screen,"listText",list);
        Assign(screen,"detailText",detail); Assign(screen,"replacementText",replacement); Assign(screen,"feedbackText",feedback);

        Button launcher = CreateButton(ui.transform,"OpenAdvancedOrdersButton","Comandas",.64f,.925f,.755f,.975f,Accent);
        UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
        launcher.transform.SetAsFirstSibling(); root.SetActive(false);
        if (!screen.ValidateConfiguration(out string screenError))
            throw new InvalidOperationException(screenError);
        EditorUtility.SetDirty(screen); EditorUtility.SetDirty(ui);
    }

    private static readonly Color Panel = new Color(.06f,.07f,.065f,.99f);
    private static readonly Color Inset = new Color(.09f,.105f,.095f,1f);
    private static readonly Color Accent = new Color(.28f,.40f,.27f,1f);
    private static readonly Color Danger = new Color(.30f,.15f,.14f,1f);
    private static readonly Color Warn = new Color(.42f,.31f,.13f,1f);

    private static GameObject CreatePanel(Transform parent,string name,float a,float b,float c,float d,Color color)
    { GameObject go=NewUi(name,parent); Anchor(go.GetComponent<RectTransform>(),a,b,c,d); Add<Image>(go).color=color; return go; }
    private static Button CreateButton(Transform parent,string name,string label,float a,float b,float c,float d,Color color)
    { GameObject go=NewUi(name,parent); Anchor(go.GetComponent<RectTransform>(),a,b,c,d); Image image=Add<Image>(go); image.color=color; Button button=Add<Button>(go); button.targetGraphic=image; TMP_Text text=CreateText(go.transform,"Label",label,15f,.02f,.04f,.98f,.96f,FontStyles.Bold); text.alignment=TextAlignmentOptions.Center; return button; }
    private static TMP_Text CreateText(Transform parent,string name,string value,float size,float a,float b,float c,float d,FontStyles style=FontStyles.Normal)
    { GameObject go=NewUi(name,parent); Anchor(go.GetComponent<RectTransform>(),a,b,c,d); TextMeshProUGUI text=Add<TextMeshProUGUI>(go); text.text=value; text.fontSize=size; text.fontStyle=style; text.color=new Color(.92f,.93f,.90f,1f); text.alignment=TextAlignmentOptions.MidlineLeft; text.raycastTarget=false; text.textWrappingMode=TextWrappingModes.NoWrap; text.overflowMode=TextOverflowModes.Ellipsis; return text; }
    private static GameObject NewUi(string name,Transform parent) { GameObject go=new GameObject(name,typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Crear "+name); if(parent!=null)go.transform.SetParent(parent,false); return go; }
    private static void Stretch(RectTransform rect)=>Anchor(rect,0,0,1,1);
    private static void Anchor(RectTransform rect,float a,float b,float c,float d){rect.anchorMin=new Vector2(a,b);rect.anchorMax=new Vector2(c,d);rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}
    private static T Add<T>(GameObject owner) where T:Component { T current=owner.GetComponent<T>(); return current!=null?current:Undo.AddComponent<T>(owner); }
    private static T EnsureUniqueOnHost<T>(Scene scene,GameObject host) where T:Component { T[] values=FindScene<T>(scene); if(values.Length>1)throw new InvalidOperationException("Duplicado: "+typeof(T).Name); T value=values.Length==1?values[0]:Undo.AddComponent<T>(host); if(value.gameObject!=host)throw new InvalidOperationException(typeof(T).Name+" debe vivir en GameSystems."); return value; }
    private static T RequireUnique<T>(Scene scene) where T:Component { T[] values=FindScene<T>(scene); if(values.Length!=1)throw new InvalidOperationException("Se esperaba 1 "+typeof(T).Name+"; hay "+values.Length+"."); return values[0]; }
    private static T[] FindScene<T>(Scene scene) where T:Component { var list=new List<T>(); foreach(GameObject root in scene.GetRootGameObjects()){T[] found=root.GetComponentsInChildren<T>(true); for(int i=0;i<found.Length;i++)if(found[i]!=null)list.Add(found[i]);} return list.ToArray(); }
    private static GameObject FindUniqueNamed(Scene scene,string name){GameObject found=null;int count=0;foreach(GameObject root in scene.GetRootGameObjects())foreach(Transform tr in root.GetComponentsInChildren<Transform>(true))if(tr!=null&&tr.name==name){found=tr.gameObject;count++;}return count==1?found:null;}
    private static GameObject FindDirectRoot(Scene scene,string name){foreach(GameObject root in scene.GetRootGameObjects())if(root!=null&&root.name==name)return root;return null;}
    private static void Assign(UnityEngine.Object owner,string fieldName,UnityEngine.Object value){var field=owner.GetType().GetField(fieldName,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic); if(field==null)throw new MissingFieldException(owner.GetType().Name,fieldName); field.SetValue(owner,value); EditorUtility.SetDirty(owner);}
    private static bool TrySaveSceneWithRetry(Scene scene){for(int i=0;i<4;i++){if(EditorSceneManager.SaveScene(scene))return true;Thread.Sleep(150);}return false;}
}
