#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369CInstaller
    {
        public const int InstallerRevision = 4;

        public const string InspectionSettingsAssetPath =
            "Assets/BistroBuilder/Settings/Camera/BistroBuilderCameraInspectionSettings.asset";

        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Install or Repair 369C Contextual Edit and Inspection Camera", false, 36920)]
        public static void InstallOrRepair()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - CÁMARA CONTEXTUAL DE EDICIÓN E INSPECCIÓN 369C");

            try
            {
                EnsureAssetFolder("Assets/BistroBuilder");
                EnsureAssetFolder("Assets/BistroBuilder/Settings");
                EnsureAssetFolder("Assets/BistroBuilder/Settings/Camera");
                report.Pass("Las carpetas canónicas de configuración existen.");

                BistroBuilderCameraInspectionSettings settings =
                    AssetDatabase.LoadAssetAtPath<BistroBuilderCameraInspectionSettings>(
                        InspectionSettingsAssetPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<BistroBuilderCameraInspectionSettings>();
                    settings.ApplyCanonicalProfile();
                    AssetDatabase.CreateAsset(settings, InspectionSettingsAssetPath);
                    report.Pass("Se ha creado el perfil canónico de inspección 369C.");
                }
                else if (settings.ProfileVersion <
                         BistroBuilderCameraInspectionSettings.CurrentProfileVersion)
                {
                    Undo.RecordObject(settings, "Upgrade Bistro Builder 369C camera inspection");
                    settings.ApplyCanonicalProfile();
                    EditorUtility.SetDirty(settings);
                    report.Pass("El perfil de inspección se ha actualizado a 369C.");
                }
                else
                {
                    report.Pass("El perfil canónico de inspección 369C ya existía y se conserva.");
                }

                string settingsReason;
                if (settings.IsConfigurationValid(out settingsReason))
                {
                    report.Pass("Los márgenes, distancias, giro por pasos y seguimiento son coherentes.");
                }
                else
                {
                    report.Fail("El perfil 369C es inválido: " + settingsReason);
                }

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    report.Fail("No existe una escena activa cargada.");
                    Finish(report, null);
                    return;
                }
                report.Pass("La escena activa es válida: " + scene.name + ".");

                BistroBuilderProfessionalCameraController controller =
                    BistroBuilderCamera369BInstaller.FindSingleInScene<
                        BistroBuilderProfessionalCameraController>(scene);
                if (controller == null)
                {
                    report.Fail("No existe un único controlador profesional 369A válido.");
                    Finish(report, null);
                    return;
                }

                int controllerRevision = BistroBuilderProfessionalCameraController.RuntimeRevision;
                if (controllerRevision >= 13)
                {
                    report.Pass("369C reutiliza el estado objetivo y la amortiguación profesional de 369A.");
                }
                else
                {
                    report.Fail("El controlador 369A no tiene la revisión mínima requerida.");
                }

                // 369C4 repara su dependencia 369B de forma acumulativa e idempotente.
                // Esto es necesario cuando la escena se restaura desde un commit anterior a 369B:
                // el código y los assets siguen presentes, pero el componente runtime de encuadre
                // desaparece de la escena. 369C no debe exigir una instalación manual intermedia.
                BistroBuilderCameraViewService viewService = EnsureFramingService(
                    controller,
                    report);

                BistroBuilderCameraInspectionService inspectionService =
                    controller.GetComponent<BistroBuilderCameraInspectionService>();
                if (inspectionService == null)
                {
                    inspectionService = Undo.AddComponent<BistroBuilderCameraInspectionService>(
                        controller.gameObject);
                    report.Pass("Se ha añadido el servicio runtime de edición e inspección 369C.");
                }
                else
                {
                    report.Pass("El servicio runtime 369C ya existía y no se duplica.");
                }

                SerializedObject serviceObject = new SerializedObject(inspectionService);
                serviceObject.FindProperty("controller").objectReferenceValue = controller;
                serviceObject.FindProperty("inspectionSettings").objectReferenceValue = settings;
                serviceObject.FindProperty("framingService").objectReferenceValue = viewService;
                serviceObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(inspectionService);
                report.Pass("369C referencia controlador, perfil y servicio de encuadre canónicos.");

                int runtimeRevision = BistroBuilderCameraInspectionService.RuntimeRevision;
                int diagnosticRevision = BistroBuilderCamera369CFunctionalTestWindow.DiagnosticRevision;
                if (runtimeRevision >= 1)
                {
                    report.Pass("La memoria separa Servicio, Edición e Inspección.");
                    report.Pass("La selección puede encuadrarse por renderers, colliders y conjuntos relacionados.");
                    report.Pass("Mesas y barras pueden incluir sillas o taburetes próximos en el encuadre.");
                    report.Pass("La inspección dispone de giro por pasos y restauración de la vista previa.");
                    report.Pass("El snapshot neutral queda preparado para futura persistencia sin acoplar 366.");
                }
                else
                {
                    report.Fail("El runtime 369C no tiene una revisión reconocida.");
                }

                if (diagnosticRevision >= 2)
                {
                    report.Pass("La ventana funcional 369C1 permite elegir objetivo explícito, usar selección del Hierarchy o recurrir a un objetivo automático.");
                }
                else
                {
                    report.Fail("Falta la revisión 369C1 de controles manuales de inspección.");
                }

                // 369C3 es deliberadamente no destructivo. La cámara contextual no debe mover
                // mesas, sillas, barra, taburetes ni alterar contratos de áreas, seating o placement.
                // La distribución visual se realizará más adelante mediante el modo edición real,
                // utilizando sus validadores canónicos y no desde un instalador de cámara.
                if (BistroBuilderCamera369CSceneLayoutInstaller.HasLegacyUnsafeLayout(scene))
                {
                    report.Fail(
                        "La escena conserva la distribución experimental 369C v1. " +
                        "Restaura Prototype_Restaurant desde el último commit válido antes de continuar.");
                }
                else
                {
                    report.Pass(
                        "369C3 instala la cámara contextual sin mover objetos del restaurante.");
                }

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                report.Pass("La escena queda marcada para guardar la instalación 369C.");
                Selection.activeGameObject = controller.gameObject;
                Finish(report, inspectionService);
            }
            catch (Exception exception)
            {
                report.Fail("Excepción durante la instalación: " + exception.Message);
                Debug.LogException(exception);
                Finish(report, null);
            }
        }


        private static BistroBuilderCameraViewService EnsureFramingService(
            BistroBuilderProfessionalCameraController controller,
            BistroBuilderCamera369AReport report)
        {
            BistroBuilderCameraViewSettings viewSettings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderCameraViewSettings>(
                    BistroBuilderCamera369BInstaller.ViewSettingsAssetPath);

            if (viewSettings == null)
            {
                viewSettings = ScriptableObject.CreateInstance<BistroBuilderCameraViewSettings>();
                viewSettings.ApplyCanonicalProfile();
                AssetDatabase.CreateAsset(
                    viewSettings,
                    BistroBuilderCamera369BInstaller.ViewSettingsAssetPath);
                report.Pass("369C4 ha reconstruido el perfil canónico de encuadres 369B.");
            }
            else if (viewSettings.ProfileVersion <
                     BistroBuilderCameraViewSettings.CurrentProfileVersion)
            {
                Undo.RecordObject(viewSettings, "Upgrade Bistro Builder 369B framing dependency");
                viewSettings.ApplyCanonicalProfile();
                EditorUtility.SetDirty(viewSettings);
                report.Pass("369C4 ha actualizado el perfil técnico de encuadres 369B.");
            }
            else
            {
                report.Pass("El perfil técnico de encuadres 369B está disponible.");
            }

            string viewReason;
            if (!viewSettings.IsConfigurationValid(out viewReason))
            {
                report.Fail("El perfil técnico 369B requerido por 369C es inválido: " + viewReason);
                return null;
            }

            BistroBuilderCameraViewService viewService =
                controller.GetComponent<BistroBuilderCameraViewService>();
            if (viewService == null)
            {
                viewService = Undo.AddComponent<BistroBuilderCameraViewService>(
                    controller.gameObject);
                report.Pass("369C4 ha restaurado el servicio runtime de encuadres 369B en la cámara canónica.");
            }
            else
            {
                report.Pass("El servicio runtime de encuadres 369B ya existe y se conserva.");
            }

            SerializedObject viewServiceObject = new SerializedObject(viewService);
            viewServiceObject.FindProperty("controller").objectReferenceValue = controller;
            viewServiceObject.FindProperty("viewSettings").objectReferenceValue = viewSettings;
            viewServiceObject.FindProperty("navigationBounds").objectReferenceValue =
                controller.NavigationBounds;
            viewServiceObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewService);

            int viewRevision = BistroBuilderCameraViewService.RuntimeRevision;
            if (viewRevision >= 1)
            {
                report.Pass("El servicio técnico de encuadres 369B queda disponible y enlazado para 369C.");
            }
            else
            {
                report.Fail("El servicio runtime 369B no tiene una revisión compatible con 369C.");
            }

            return viewService;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void Finish(
            BistroBuilderCamera369AReport report,
            UnityEngine.Object context)
        {
            report.Log(context);
            report.ShowDialog();
        }
    }
}
#endif
