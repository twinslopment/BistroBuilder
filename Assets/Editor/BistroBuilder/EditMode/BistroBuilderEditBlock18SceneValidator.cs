using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderEditBlock18SceneValidator
{
    private const string ReportName = "EditBlock18SceneValidationReport.txt";

    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static int LastPending { get; private set; }
    public static bool IsProductionReady => LastFailed == 0 && LastPending == 0;
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/18 Modo Edicion/Validate Scene Integration")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            Debug.Log(LastReport);
            EditorApplication.Exit(LastFailed == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use ValidateScene(scene) for an already running Play Mode test.");
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(BistroBuilderEditBlock18Installer.ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(BistroBuilderEditBlock18Installer.ScenePath, OpenSceneMode.Additive);
        try
        {
            ValidateScene(scene);
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    // Scene-scoped validation can be reused by the installer and by real Play Mode tests.
    // Pending production tariffs are reported separately from broken scene wiring.
    public static bool ValidateScene(Scene scene) => ValidateScene(scene, BistroBuilderEditBlock18Installer.ScenePath);

    public static bool ValidateScene(Scene scene, string expectedScenePath)
    {
        LastPassed = 0;
        LastFailed = 0;
        LastPending = 0;
        var report = new StringBuilder();
        report.AppendLine("BB EDIT MODE BLOCK 18 - SCENE INTEGRATION");
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Mode: " + (Application.isPlaying ? "Play Mode" : "Editor"));
        if (scene.isDirty && !Application.isPlaying)
            report.AppendLine("Validation uses the current in-memory scene, including unsaved changes.");
        Check(scene.IsValid() && scene.isLoaded &&
            scene.path == expectedScenePath,
            "Escena canónica cargada", report);

        var spatial = RequireUnique<BistroBuilderSpatialInteractionService>(scene, report);
        var navigation = RequireUnique<BistroBuilderNavigationService>(scene, report);
        var operational = RequireUnique<BistroBuilderOperationalSpatialCoordinator>(scene, report);
        var finance = RequireUnique<BistroBuilderFinanceService>(scene, report);
        var discretionary = RequireUnique<BistroBuilderDiscretionaryFinanceService>(scene, report);
        var gameState = RequireUnique<BistroBuilderGeneralGameStateService>(scene, report);
        var clock = RequireUnique<GameClock>(scene, report);
        var save = RequireUnique<BistroBuilderSaveGameService>(scene, report);
        var editMode = RequireUnique<RestaurantEditModeService>(scene, report);
        var availability = RequireUnique<RestaurantServiceEditModeAvailabilityRule>(scene, report);
        var document = RequireUnique<BistroBuilderEditDocumentRuntimeService>(scene, report);
        var materializer = RequireUnique<BistroBuilderArchitectureRuntimeMaterializer>(scene, report);
        var projection = RequireUnique<BistroBuilderEditDocumentMaterializationBridge>(scene, report);
        var coordinator = RequireUnique<BistroBuilderEditRuntimeCoordinator>(scene, report);
        var facade = RequireUnique<BistroBuilderEditPlayerFacade>(scene, report);
        var saveProvider = RequireUnique<BistroBuilderEditDocumentSaveSectionProvider>(scene, report);
        var gateway = RequireUnique<BistroBuilderEditFinanceGateway>(scene, report);
        var binder = RequireUnique<BistroBuilderEditFinanceGatewayBinder>(scene, report);
        var navValidator = RequireUnique<BistroBuilderEditNavigationValidationProvider>(scene, report);
        var editSaveGuard = RequireUnique<BistroBuilderEditSessionSaveGuard>(scene, report);

        CheckReference(projection, "runtimeService", document, report);
        CheckReference(projection, "materializer", materializer, report);
        CheckReference(projection, "spatialService", spatial, report);
        CheckReference(projection, "operationalSpatialCoordinator", operational, report);
        CheckReference(projection, "navigationService", navigation, report);
        CheckBool(projection, "rebuildOnEnable", true, report);
        CheckReference(coordinator, "documentService", document, report);
        CheckReference(coordinator, "editModeService", editMode, report);
        CheckReference(coordinator, "availabilityRuleSource", availability, report);
        CheckReference(facade, "coordinator", coordinator, report);
        CheckReference(saveProvider, "runtimeService", document, report);
        CheckReference(saveProvider, "materializer", materializer, report);
        CheckReference(gateway, "financeService", finance, report);
        CheckReference(gateway, "discretionaryFinanceService", discretionary, report);
        CheckReference(gateway, "generalGameStateService", gameState, report);
        CheckReference(gateway, "gameClock", clock, report);
        CheckReference(binder, "gateway", gateway, report);
        CheckReference(binder, "editCoordinator", coordinator, report);
        CheckReference(navValidator, "editDocumentService", document, report);
        CheckReference(editSaveGuard, "coordinator", coordinator, report);
        CheckBool(materializer, "createNavigationFootprints", true, report);
        CheckBool(materializer, "createSpatialSubjects", true, report);
        CheckBool(materializer, "createMeshColliders", true, report);

        var generatedRoot = ReadReference(materializer, "generatedRoot") as Transform;
        Check(generatedRoot != null && materializer != null &&
            generatedRoot.gameObject.scene == scene && generatedRoot != materializer.transform &&
            generatedRoot.IsChildOf(materializer.transform),
            "Materializer usa una raíz propia de geometría generada en la escena", report);
        var resourceContract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(
            BistroBuilderEditBlock18Installer.WallContractPath);
        Check(BistroBuilderEditBlock18Installer.IsWallContractValid(resourceContract, out string contractError),
            "Spatial Contract de arquitectura válido" + DescribeError(contractError), report);
        var boundContract = ReadReference(materializer, "wallSpatialContract") as BistroBuilderSpatialContractDefinition;
        Check(BistroBuilderEditBlock18Installer.IsWallContractValid(boundContract, out string boundError),
            "Materializer enlaza un Spatial Contract de paredes válido" + DescribeError(boundError), report);
        CheckMatchingFloat(materializer, "passableOpeningBottomTolerance", navValidator,
            "floorOpeningTolerance", report);
        CheckMatchingFloat(materializer, "passableOpeningMinimumHeight", navValidator,
            "minimumPassageHeight", report);
        Check(ReadFloat(navValidator, "minimumPassageWidth") > 0f,
            "Navigation conserva una anchura mínima de paso positiva", report);

        Check(save != null && saveProvider != null && saveProvider.gameObject == save.gameObject,
            "Save provider instalado en el host que descubre SaveGame", report);
        if (save != null)
        {
            // In Play Mode assert the real registration established at startup; do not repair it.
            if (!Application.isPlaying) save.RefreshExtensions();
            Check(save.HasProvider(BistroBuilderEditDocumentSaveSectionProvider.StableSectionId),
                "SaveGame registra restaurant.edit_architecture", report);
            Check(save.ValidateConfiguration(out string saveError),
                "Configuración real de SaveGame válida" + DescribeError(saveError), report);
            Check(save.TryGetSerializer(BistroBuilderJsonSaveSerializer.StableSerializerId, out _),
                "Serializador JSON de arquitectura registrado", report);
        }
        Check(saveProvider != null && saveProvider.SectionVersion == 1 &&
            saveProvider.StateType == typeof(BistroBuilderEditDocument),
            "Contrato persistente de arquitectura conserva identidad y versión", report);
        if (saveProvider != null && document != null)
            Check(saveProvider.ValidateState(document.GetCommittedSnapshot(), out string documentError),
                "Documento comprometido válido para Save/Load" + DescribeError(documentError), report);

        if (Application.isPlaying)
        {
            Check(binder != null && binder.IsBound,
                "Finance enlazado al coordinador durante Play Mode", report);
            Check(navValidator != null && navValidator.IsRegistered &&
                document != null && document.ValidationProviderCount > 0,
                "Navigation registrado en la autoridad documental durante Play Mode", report);
        }
        if (gateway != null)
        {
            if (gateway.ValidateTariffAuthority(out string tariffError))
                Check(true, "Tarifas de reforma aprobadas por la autoridad de Finanzas", report);
            else
            {
                LastPending++;
                report.AppendLine("PENDING - Producción / Finanzas: " + tariffError);
            }
        }

        report.AppendLine("Resultado estructural: " + LastPassed + " OK / " + LastFailed + " fallos.");
        report.AppendLine("Pendientes de producción: " + LastPending + ".");
        report.AppendLine("La validación estructural no sustituye la Queen Test de escena/Play Mode.");
        LastReport = report.ToString();
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), LastReport);
        return LastFailed == 0;
    }

    private static T RequireUnique<T>(Scene scene, StringBuilder report) where T : MonoBehaviour
    {
        var matches = BistroBuilderEditBlock18Installer.FindInScene<T>(scene);
        Check(matches.Count == 1, typeof(T).Name + " único en la escena (" + matches.Count + ")", report);
        if (matches.Count != 1) return null;
        T component = matches[0];
        Check(component.enabled && component.gameObject.activeInHierarchy,
            typeof(T).Name + " habilitado y activo", report);
        return component;
    }

    private static void CheckReference(UnityEngine.Object owner, string field,
        UnityEngine.Object expected, StringBuilder report)
    {
        Check(owner != null && expected != null && ReadReference(owner, field) == expected,
            (owner != null ? owner.GetType().Name : "Componente ausente") + "." + field + " enlazado", report);
    }

    private static UnityEngine.Object ReadReference(UnityEngine.Object owner, string field)
    {
        if (owner == null) return null;
        var state = new SerializedObject(owner);
        var property = state.FindProperty(field);
        return property != null ? property.objectReferenceValue : null;
    }

    private static void CheckBool(UnityEngine.Object owner, string field, bool expected, StringBuilder report)
    {
        var state = owner != null ? new SerializedObject(owner) : null;
        var property = state != null ? state.FindProperty(field) : null;
        Check(property != null && property.boolValue == expected,
            (owner != null ? owner.GetType().Name : "Componente ausente") + "." + field + " activo", report);
    }

    private static float ReadFloat(UnityEngine.Object owner, string field)
    {
        if (owner == null) return float.NaN;
        var property = new SerializedObject(owner).FindProperty(field);
        return property != null ? property.floatValue : float.NaN;
    }

    private static void CheckMatchingFloat(UnityEngine.Object left, string leftField,
        UnityEngine.Object right, string rightField, StringBuilder report)
    {
        float leftValue = ReadFloat(left, leftField);
        float rightValue = ReadFloat(right, rightField);
        Check(!float.IsNaN(leftValue) && !float.IsInfinity(leftValue) && leftValue >= 0f &&
            Mathf.Approximately(leftValue, rightValue),
            "Materializer/Navigation coherentes: " + leftField + " / " + rightField, report);
    }

    private static string DescribeError(string error)
    {
        return string.IsNullOrWhiteSpace(error) ? string.Empty : " (" + error + ")";
    }

    private static void Check(bool condition, string label, StringBuilder report)
    {
        if (condition) LastPassed++;
        else LastFailed++;
        report.AppendLine((condition ? "OK - " : "FAIL - ") + label);
    }
}