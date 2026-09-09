using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderBBSISPhase1Validator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 1/Validar")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BBSIS FASE 1 - VALIDACION\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderSpatialInteractionService[] services =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSpatialInteractionService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        BistroBuilderSpatialInteractionService service = services.Length == 1 ? services[0] : null;
        Check(services.Length == 1, "Existe una única autoridad semántica BBSIS");
        Check(service != null && service.ValidateConfiguration(out _),
            "Configuración base BBSIS válida");

        Check(service != null && service.FamilyCatalog != null &&
              service.FamilyCatalog.ValidateCatalog(out _),
            "Catálogo data-driven de familias/traits instalado");
        Check(service != null && service.FamilyCatalog.ContainsFamily("seating.chair") &&
              service.FamilyCatalog.ContainsFamily("architecture.door") &&
              service.FamilyCatalog.ContainsFamily("work.kitchen") &&
              service.FamilyCatalog.ContainsFamily("logistics.cart"),
            "Familias espaciales base disponibles sin hardcodes por asset");
        Check(service != null && service.GetComponent<Rigidbody>() == null,
            "BBSIS no introduce Rigidbody indiscriminado");

        BistroBuilderNavigationService[] navigation =
            UnityEngine.Object.FindObjectsByType<BistroBuilderNavigationService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(navigation.Length == 1, "Bloque 17 conserva una única autoridad de rutas");
        FieldInfo spatialField = typeof(BistroBuilderNavigationService).GetField(
            "spatialService", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(spatialField != null,
            "Bloque 17 consume BBSIS sin absorber su semántica espacial");
        Check(typeof(BistroBuilderDynamicCirculationEnvelope).GetProperty("HasSpatialLease") != null,
            "Obstáculos dinámicos pueden delegar su reserva temporal en BBSIS");
        Check(BistroBuilderSpatialRuntimeSnapshot.CurrentSchemaId == "spatial.runtime" &&
              BistroBuilderSpatialRuntimeSnapshot.CurrentSchemaVersion == 1,
            "Contrato de reconstrucción Save/Load BBSIS versionado");

        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSpatialSubject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        var subjectIds = new HashSet<string>(StringComparer.Ordinal);
        bool subjectsValid = true;
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null || !subject.ValidateSubject(out _) ||
                !subjectIds.Add(subject.SubjectId))
            {
                subjectsValid = false;
                break;
            }
        }

        Check(subjectsValid,
            "Spatial Subjects existentes tienen IDs estables y contratos/proxies válidos");
        Check(typeof(BistroBuilderAdaptiveSpatialProxy).GetMethod("BuildWorldVolumes") != null &&
              typeof(BistroBuilderAdaptiveSpatialProxy).GetMethod("ContainsPoint") != null,
            "Adaptive Spatial Proxy expone geometría funcional desacoplada del render");
        Check(Enum.GetValues(typeof(BistroBuilderAdaptiveSpatialProxyMode)).Length == 4,
            "Adaptive Spatial Proxy soporta Simple/Compound/Layered/Articulated");
        Check(typeof(BistroBuilderSpatialInteractionService).GetMethod("TryAcquireLease") != null &&
              typeof(BistroBuilderSpatialInteractionService).GetMethod("TryBeginEpisode") != null,
            "Claims, Spatial Leases y Spatial Episodes están disponibles");
        Check(typeof(BistroBuilderSpatialInteractionService).GetMethod(
                  "ResetTransientRuntimeStateAfterLoad") != null,
            "Load puede descartar Claims/Leases/Episodes transitorios y reconstruir BBSIS");

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
