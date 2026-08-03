#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369BInstaller
    {
        public const string ViewSettingsAssetPath =
            "Assets/BistroBuilder/Settings/Camera/BistroBuilderCameraViewSettings.asset";

        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Install or Repair 369B Preset Camera Views", false, 36910)]
        public static void InstallOrRepair()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - VISTAS PREDEFINIDAS DE CÁMARA 369B");

            try
            {
                EnsureAssetFolder("Assets/BistroBuilder");
                EnsureAssetFolder("Assets/BistroBuilder/Settings");
                EnsureAssetFolder("Assets/BistroBuilder/Settings/Camera");
                report.Pass("Las carpetas canónicas de configuración existen.");

                BistroBuilderCameraViewSettings viewSettings =
                    AssetDatabase.LoadAssetAtPath<BistroBuilderCameraViewSettings>(
                        ViewSettingsAssetPath);
                if (viewSettings == null)
                {
                    viewSettings = ScriptableObject.CreateInstance<BistroBuilderCameraViewSettings>();
                    viewSettings.ApplyCanonicalProfile();
                    AssetDatabase.CreateAsset(viewSettings, ViewSettingsAssetPath);
                    report.Pass("Se ha creado el perfil canónico de vistas 369B.");
                }
                else if (viewSettings.ProfileVersion <
                         BistroBuilderCameraViewSettings.CurrentProfileVersion)
                {
                    Undo.RecordObject(viewSettings, "Upgrade Bistro Builder 369B camera views");
                    viewSettings.ApplyCanonicalProfile();
                    EditorUtility.SetDirty(viewSettings);
                    report.Pass("El perfil de vistas se ha actualizado a 369B.");
                }
                else
                {
                    report.Pass("El perfil canónico de vistas 369B ya existía y se conserva.");
                }

                string viewReason;
                if (viewSettings.IsConfigurationValid(out viewReason))
                {
                    report.Pass("General, Isométrica, Cenital y Cercana están definidas correctamente.");
                }
                else
                {
                    report.Fail("El perfil de vistas es inválido: " + viewReason);
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid() || !activeScene.isLoaded)
                {
                    report.Fail("No existe una escena activa cargada.");
                    Finish(report, null);
                    return;
                }
                report.Pass("La escena activa es válida: " + activeScene.name + ".");

                BistroBuilderProfessionalCameraController controller =
                    FindSingleInScene<BistroBuilderProfessionalCameraController>(activeScene);
                if (controller == null)
                {
                    report.Fail("No existe el controlador profesional 369A. Instala y valida 369A antes de 369B.");
                    Finish(report, null);
                    return;
                }

                int controllerRuntimeRevision =
                    BistroBuilderProfessionalCameraController.RuntimeRevision;
                if (controllerRuntimeRevision >= 13)
                {
                    report.Pass("El runtime de cámara admite pitch extendido temporal y detección de navegación manual.");
                }
                else
                {
                    report.Fail("El runtime 369A no contiene la compatibilidad requerida por 369B.");
                }

                if (controller.ControlledCamera == null || controller.Settings == null ||
                    controller.NavigationBounds == null || !controller.NavigationBounds.IsValid)
                {
                    report.Fail("El controlador 369A no tiene cámara, configuración o límites válidos.");
                    Finish(report, controller);
                    return;
                }
                report.Pass("369B reutiliza la cámara, amortiguación y huella navegable validadas en 369A.");

                BistroBuilderCameraViewService service =
                    controller.GetComponent<BistroBuilderCameraViewService>();
                if (service == null)
                {
                    service = Undo.AddComponent<BistroBuilderCameraViewService>(
                        controller.gameObject);
                    report.Pass("Se ha añadido el servicio runtime de vistas 369B.");
                }
                else
                {
                    report.Pass("El servicio runtime de vistas 369B ya existía y no se duplica.");
                }

                SerializedObject serviceObject = new SerializedObject(service);
                serviceObject.FindProperty("controller").objectReferenceValue = controller;
                serviceObject.FindProperty("viewSettings").objectReferenceValue = viewSettings;
                serviceObject.FindProperty("navigationBounds").objectReferenceValue =
                    controller.NavigationBounds;
                serviceObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(service);
                report.Pass("El servicio referencia el controlador, el perfil y los límites canónicos.");

                int functionalDiagnosticRevision =
                    BistroBuilderCamera369BFunctionalTestWindow.DiagnosticRevision;
                if (functionalDiagnosticRevision >= 2)
                {
                    report.Pass("La previsualización manual refresca referencias al entrar en Play Mode y queda disponible después del diagnóstico.");
                }
                else
                {
                    report.Fail("La ventana funcional 369B no contiene el hotfix de controles manuales 369B2.");
                }

                int viewServiceRuntimeRevision =
                    BistroBuilderCameraViewService.RuntimeRevision;
                if (viewServiceRuntimeRevision >= 1)
                {
                    report.Pass("El servicio calcula vistas dinámicas sin guardar transforms absolutos de escena.");
                    report.Pass("La vista General encuadra el local completo conservando la orientación actual.");
                    report.Pass("La vista Isométrica usa orientación canónica y encuadre adaptativo.");
                    report.Pass("La vista Cenital usa pitch extendido temporal sin alterar los límites manuales de 369A.");
                    report.Pass("La vista Cercana conserva el foco actual y aproxima la inspección.");
                    report.Pass("Cualquier entrada manual abandona el preset y devuelve el control a 369A.");
                    report.Pass("La vista libre anterior puede restaurarse después de usar un preset.");
                }
                else
                {
                    report.Fail("El servicio runtime 369B no tiene una revisión reconocida.");
                }

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(activeScene);
                report.Pass("La escena queda marcada para guardar la instalación 369B.");

                Selection.activeGameObject = controller.gameObject;
                Finish(report, service);
            }
            catch (Exception exception)
            {
                report.Fail("Excepción durante la instalación: " + exception.Message);
                Debug.LogException(exception);
                Finish(report, null);
            }
        }

        internal static T FindSingleInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            T found = null;
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] components = roots[rootIndex].GetComponentsInChildren<T>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    if (found != null && found != components[componentIndex])
                    {
                        return null;
                    }
                    found = components[componentIndex];
                }
            }

            return found;
        }

        internal static int CountInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                count += roots[rootIndex].GetComponentsInChildren<T>(true).Length;
            }
            return count;
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
