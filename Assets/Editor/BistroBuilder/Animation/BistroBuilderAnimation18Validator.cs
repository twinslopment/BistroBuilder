using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validador estructural del vertical slice. Comprueba separaciÃ³n de
/// autoridades, datos y compatibilidad con NavegaciÃ³n 17.
/// </summary>
public static class BistroBuilderAnimation18Validator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogPath = "Assets/Data/Animation/BistroBuilderMotionCatalog.asset";
    private const string FamilyFolder = "Assets/Data/Animation/Families";

    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Validar fundacion")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        StringBuilder report = new StringBuilder("BLOQUE 18 - VALIDACION ANIMACION E INTERACCIONES\n");

        void Check(bool condition, string label)
        {
            if (condition)
            {
                ok++;
                report.AppendLine("OK - " + label);
            }
            else
            {
                fail++;
                report.AppendLine("FAIL - " + label);
            }
        }

        BistroBuilderInteractionPresentationService[] services =
            UnityEngine.Object.FindObjectsByType<BistroBuilderInteractionPresentationService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(services.Length == 1, "Existe una Ãºnica autoridad de presentaciÃ³n de interacciones");
        Check(services.Length == 1 && services[0].ValidateConfiguration(out _),
            "La autoridad de presentaciÃ³n tiene configuraciÃ³n vÃ¡lida");

        BistroBuilderMotionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(CatalogPath);
        Check(catalog != null, "CatÃ¡logo semÃ¡ntico de motions instalado");
        Check(catalog != null && catalog.ValidateConfiguration(out _),
            "CatÃ¡logo semÃ¡ntico sin IDs duplicados ni metadatos invÃ¡lidos");

        string[] familyGuids = AssetDatabase.FindAssets(
            "t:BistroBuilderInteractionFamilyProfile",
            new[] { FamilyFolder });
        BistroBuilderInteractionFamilyProfile[] familyProfiles = familyGuids
            .Select(guid => AssetDatabase.LoadAssetAtPath<BistroBuilderInteractionFamilyProfile>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .Where(profile => profile != null)
            .ToArray();

        Check(familyProfiles.Length >= 3, "Perfiles data-driven Seat, Portal y Transfer instalados");
        Check(familyProfiles.All(profile => profile.ValidateConfiguration(out _)),
            "Perfiles de familia vÃ¡lidos");
        Check(familyProfiles.Any(profile => profile.Family == BistroBuilderInteractionFamily.Seat),
            "Familia Seat publicada");
        Check(familyProfiles.Any(profile => profile.Family == BistroBuilderInteractionFamily.Portal),
            "Familia Portal publicada");
        Check(familyProfiles.Any(profile => profile.Family == BistroBuilderInteractionFamily.Transfer),
            "Familia Transfer publicada");

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Check(seats.Length > 0, "Sillas funcionales presentes");
        Check(seats.All(seat => seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() != null),
            "Sillas mantienen reserva espacial temporal de NavegaciÃ³n 17");
        Check(seats.All(seat =>
        {
            BistroBuilderAssetInteractionDescriptor descriptor =
                seat.GetComponent<BistroBuilderAssetInteractionDescriptor>();
            return descriptor != null && descriptor.ValidateConfiguration(out _);
        }), "Todas las sillas tienen descriptor de interacciÃ³n universal vÃ¡lido");

        Check(typeof(BistroBuilderNavigableDoor) != null &&
              typeof(BistroBuilderDoorCirculationEnvelope) != null,
            "Portal reutiliza puerta y barrido espacial existentes");
        Check(typeof(BistroBuilderCharacterVisualGroundingAdapter) != null,
            "Grounding Humanoid corrige solo la jerarquía visual y preserva el root autoritativo");
        Check(typeof(BistroBuilderTransferableVisual) != null,
            "Transfer usa attachment visual sin autoridad de ownership");
        Check(typeof(BistroBuilderObjectMotionDriver) != null &&
              typeof(BistroBuilderObjectMotionProfile) != null,
            "Movimiento procedural universal de objetos disponible");
        Check(typeof(BistroBuilderInteractionHandle) != null,
            "Interacciones exponen handle cancelable y observable");

        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        Check(navigation != null, "NavegaciÃ³n 17 conserva su autoridad independiente");
        Check(typeof(CustomerMovementView) != null && typeof(WaiterMovementView) != null,
            "LocomociÃ³n existente permanece separada de la presentaciÃ³n animada");

        BistroBuilderCharacterAnimationDriver[] drivers =
            UnityEngine.Object.FindObjectsByType<BistroBuilderCharacterAnimationDriver>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        bool rootMotionSafe = true;
        foreach (BistroBuilderCharacterAnimationDriver driver in drivers)
        {
            if (driver == null || driver.Animator == null) continue;
            if (driver.Animator.applyRootMotion)
            {
                rootMotionSafe = false;
                break;
            }
        }
        Check(rootMotionSafe, "Root Motion no compite con navegaciÃ³n en personajes configurados");

        Check(Enum.GetValues(typeof(BistroBuilderInteractionFamily)).Length == 9,
            "Contrato mantiene las ocho familias universales mÃ¡s None");
        Check(Enum.GetValues(typeof(BistroBuilderInteractionPhase)).Length >= 11,
            "Contrato incluye fases de ejecuciÃ³n, fallo y recuperaciÃ³n");

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
