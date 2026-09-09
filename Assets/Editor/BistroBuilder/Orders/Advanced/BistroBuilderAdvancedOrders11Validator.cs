using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedOrders11ValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    public void Check(bool condition, string ok, string fail)
    {
        if (condition) { Passed++; lines.Add("[OK] " + ok); }
        else { Errors++; lines.Add("[ERROR] " + fail); }
    }
    public string BuildReport() => "=== BISTRO BUILDER — BLOQUE 11 / VALIDACIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedOrders11Validator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    [MenuItem("Tools/Bistro Builder/Orders/11 - Validar", false, 11001)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport()); else Debug.LogError(result.BuildReport());
    }
    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderAdvancedOrders11ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedOrders11ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.", "La escena principal no está activa/guardada.");
        GameObject host = FindUniqueNamed(scene, "GameSystems");
        result.Check(host != null, "Existe un único GameSystems canónico.", "GameSystems falta o está duplicado.");

        var advanced = FindScene<BistroBuilderAdvancedOrderService>(scene);
        var facade = FindScene<BistroBuilderAdvancedOrderPlayerFacade>(scene);
        var screen = FindScene<BistroBuilderAdvancedOrderPlayerScreen>(scene);
        result.Check(advanced.Length == 1 && host != null && advanced[0].gameObject == host,
            "AdvancedOrderService es único y vive en GameSystems.", "AdvancedOrderService falta, está duplicado o mal ubicado.");
        result.Check(facade.Length == 1 && host != null && facade[0].gameObject == host,
            "La fachada de jugador es única y no crea otra autoridad.", "La fachada 11 falta o está duplicada.");
        result.Check(screen.Length == 1,
            "Existe una única pantalla jugable de revisión de comandas.", "La pantalla jugable 11 falta o está duplicada.");
        if (advanced.Length == 1)
            result.Check(advanced[0].ValidateConfiguration(out _),
                "11 reutiliza OrderSystem, comanda canónica e inventario 368CD.", "El servicio 11 no valida sus autoridades.");
        if (facade.Length == 1)
            result.Check(facade[0].ValidateConfiguration(out _),
                "La proyección 11 valida carta, catálogo y mutaciones canónicas.", "La fachada 11 no valida.");
        if (screen.Length == 1)
            result.Check(screen[0].ValidateConfiguration(out _),
                "La UI expone revisión, corrección, repetición, incidencias, reposición y devolución.", "La UI 11 está incompleta.");

        var canonical = FindScene<BistroBuilderCanonicalOrderService>(scene);
        var inventory = FindScene<BistroBuilderOrderInventoryLifecycleService>(scene);
        var financeBridge = FindScene<BistroBuilderSalesRevenueBridge>(scene);
        result.Check(canonical.Length == 1, "Se conserva una sola autoridad de comandas canónicas.", "La autoridad canónica falta o está duplicada.");
        result.Check(inventory.Length == 1, "Se conserva una sola autoridad de inventario de comandas 368CD.", "El lifecycle 368CD falta o está duplicado.");
        result.Check(financeBridge.Length == 1,
            "Economía conserva un único SalesRevenueBridge y cobra el total canónico.", "SalesRevenueBridge falta o está duplicado.");

        Type lineType = typeof(BistroBuilderCanonicalOrderLine);
        string[] persistentFields = { "advancedOriginKind", "advancedBillingMode", "advancedSourceLineId", "advancedIncidentKind", "advancedChangeReason" };
        bool serialized = true;
        for (int i = 0; i < persistentFields.Length; i++)
        {
            FieldInfo field = lineType.GetField(persistentFields[i], BindingFlags.Instance | BindingFlags.NonPublic);
            serialized &= field != null && field.GetCustomAttribute<SerializeField>() != null;
        }
        result.Check(serialized,
            "Origen, billing, SourceLineId, incidencia y motivo viajan dentro de service.runtime.",
            "La metadata avanzada no está totalmente serializada.");

        result.Check(typeof(BistroBuilderAdvancedOrderService).GetMethod("TryCorrectLine") != null &&
                     typeof(BistroBuilderAdvancedOrderService).GetMethod("TryRepeatLine") != null &&
                     typeof(BistroBuilderAdvancedOrderService).GetMethod("TryCancelLine") != null &&
                     typeof(BistroBuilderAdvancedOrderService).GetMethod("TryReplaceAfterIncident") != null &&
                     typeof(BistroBuilderAdvancedOrderService).GetMethod("TryReturnWithoutReplacement") != null,
            "Las operaciones avanzadas son API explícita, no cambios ad-hoc de estado.",
            "Falta alguna operación avanzada requerida.");
        return result;
    }

    private static GameObject FindUniqueNamed(Scene scene,string name)
    { GameObject found=null; int count=0; foreach(GameObject root in scene.GetRootGameObjects()) foreach(Transform tr in root.GetComponentsInChildren<Transform>(true)) if(tr!=null&&tr.name==name){found=tr.gameObject;count++;} return count==1?found:null; }
    private static T[] FindScene<T>(Scene scene) where T:Component
    { var list=new List<T>(); if(!scene.IsValid()||!scene.isLoaded)return list.ToArray(); foreach(GameObject root in scene.GetRootGameObjects()){T[] values=root.GetComponentsInChildren<T>(true); for(int i=0;i<values.Length;i++)if(values[i]!=null)list.Add(values[i]);} return list.ToArray(); }
}
