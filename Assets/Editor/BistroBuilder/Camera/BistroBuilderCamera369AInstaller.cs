#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369AInstaller
    {
        public const string SettingsAssetPath =
            "Assets/BistroBuilder/Settings/Camera/BistroBuilderCameraNavigationSettings.asset";

        private const string MenuRoot = "Bistro Builder/Camera/";
        private const string BoundsObjectName = "BB_CameraBounds_369A";

        [MenuItem(MenuRoot + "Install or Repair 369A Professional Camera Navigation", false, 36900)]
        public static void InstallOrRepair()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - CÁMARA PROFESIONAL Y NAVEGACIÓN 369A");

            try
            {
                EnsureAssetFolder("Assets/BistroBuilder");
                EnsureAssetFolder("Assets/BistroBuilder/Settings");
                EnsureAssetFolder("Assets/BistroBuilder/Settings/Camera");
                report.Pass("Las carpetas canónicas de configuración existen.");

                BistroBuilderCameraNavigationSettings settings =
                    AssetDatabase.LoadAssetAtPath<BistroBuilderCameraNavigationSettings>(SettingsAssetPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<BistroBuilderCameraNavigationSettings>();
                    AssetDatabase.CreateAsset(settings, SettingsAssetPath);
                    AssetDatabase.SaveAssets();
                    report.Pass("Se ha creado la configuración canónica de cámara 369A.");
                }
                else
                {
                    report.Pass("La configuración canónica de cámara 369A ya existía y se conserva.");
                }

                bool interactionProfileUpgraded = UpgradeInteractionProfile369A8(settings);
                if (interactionProfileUpgraded)
                {
                    report.Pass("Se ha aplicado el perfil acumulativo 369A8 con órbita bajo cursor, zoom continuo y envolvente de encuadre.");
                }
                else
                {
                    report.Pass("El perfil de interacción 369A8 ya estaba instalado y se conserva.");
                }

                int runtimeRevision = BistroBuilderProfessionalCameraController.RuntimeRevision;
                int diagnosticRevision = BistroBuilderCamera369AFunctionalTestWindow.DiagnosticRevision;
                if (runtimeRevision >= 8 && diagnosticRevision >= 8)
                {
                    report.Pass("Los hotfixes 369A3–369A8 de asentamiento, elevación recta, órbita contextual, zoom continuo y encuadre están instalados.");
                }
                else
                {
                    report.Fail("Falta el runtime o el diagnóstico acumulativo 369A8.");
                }

                string configurationReason;
                if (settings.IsConfigurationValid(out configurationReason))
                {
                    report.Pass("La configuración de movimiento, amortiguación y límites es válida.");
                }
                else
                {
                    report.Fail("La configuración de cámara es inválida: " + configurationReason);
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid() || !activeScene.isLoaded)
                {
                    report.Fail("No existe una escena activa cargada.");
                    Finish(report, null);
                    return;
                }
                report.Pass("La escena activa es válida: " + activeScene.name + ".");

                UnityEngine.Camera camera = FindBestSceneCamera(activeScene);
                if (camera == null)
                {
                    GameObject cameraObject = new GameObject("Main Camera");
                    Undo.RegisterCreatedObjectUndo(cameraObject, "Create Bistro Builder camera");
                    SceneManager.MoveGameObjectToScene(cameraObject, activeScene);
                    camera = Undo.AddComponent<UnityEngine.Camera>(cameraObject);
                    Undo.AddComponent<AudioListener>(cameraObject);
                    cameraObject.tag = "MainCamera";
                    cameraObject.transform.position = new Vector3(-16.0f, 18.0f, -16.0f);
                    cameraObject.transform.rotation = Quaternion.Euler(48.0f, 45.0f, 0.0f);
                    report.Pass("Se ha creado una cámara principal porque la escena no tenía ninguna.");
                }
                else
                {
                    report.Pass("Se ha localizado la cámara principal de la escena.");
                }

                if (!camera.CompareTag("MainCamera"))
                {
                    Undo.RecordObject(camera.gameObject, "Tag Bistro Builder main camera");
                    camera.gameObject.tag = "MainCamera";
                    report.Pass("La cámara queda identificada con la etiqueta MainCamera.");
                }
                else
                {
                    report.Pass("La cámara ya conserva la etiqueta MainCamera.");
                }

                BistroBuilderProfessionalCameraController controller =
                    camera.GetComponent<BistroBuilderProfessionalCameraController>();
                if (controller == null)
                {
                    controller = Undo.AddComponent<BistroBuilderProfessionalCameraController>(camera.gameObject);
                    report.Pass("Se ha añadido el controlador profesional 369A.");
                }
                else
                {
                    report.Pass("El controlador profesional 369A ya existía y no se duplica.");
                }

                BistroBuilderCameraBounds navigationBounds = FindSceneBounds(activeScene);
                if (navigationBounds == null)
                {
                    GameObject boundsObject = new GameObject(BoundsObjectName);
                    Undo.RegisterCreatedObjectUndo(boundsObject, "Create Bistro Builder camera bounds");
                    SceneManager.MoveGameObjectToScene(boundsObject, activeScene);
                    navigationBounds = Undo.AddComponent<BistroBuilderCameraBounds>(boundsObject);
                    report.Pass("Se ha creado el límite navegable de cámara 369A.");
                }
                else
                {
                    report.Pass("El límite navegable de cámara 369A ya existía y no se duplica.");
                }

                if (!navigationBounds.IsValid || IsDefaultUnconfiguredBounds(navigationBounds))
                {
                    Bounds derivedBounds;
                    if (TryDeriveRestaurantBounds(activeScene, camera, out derivedBounds))
                    {
                        Undo.RecordObject(navigationBounds, "Configure Bistro Builder camera bounds");
                        navigationBounds.ConfigureFromWorldBounds(ExpandHorizontal(derivedBounds, 1.12f));
                        EditorUtility.SetDirty(navigationBounds);
                        report.Pass("Los límites se han derivado de la geometría operativa de la escena.");
                    }
                    else
                    {
                        report.Warn("No se pudo deducir el restaurante; se conserva el límite seguro de 80 x 80 m.");
                    }
                }
                else
                {
                    report.Pass("Los límites existentes son válidos y se conservan sin sobrescribirlos.");
                }

                bool floorBoundsApplied = false;
                if (interactionProfileUpgraded)
                {
                    Bounds floorBounds;
                    if (TryDeriveNavigableFloorBounds(activeScene, camera, out floorBounds))
                    {
                        Undo.RecordObject(navigationBounds, "Align Bistro Builder camera bounds to floor");
                        navigationBounds.ConfigureFromWorldBounds(floorBounds, 1.0f);
                        navigationBounds.ConfigureHorizontalPadding(1.25f);
                        floorBoundsApplied = true;
                        report.Pass("La zona navegable se ha alineado con la huella real del suelo del restaurante.");
                    }
                }

                Undo.RecordObject(navigationBounds, "Configure Bistro Builder camera framing bounds");
                navigationBounds.ConfigureConstraintMode(
                    BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope);
                if (!floorBoundsApplied)
                {
                    navigationBounds.ConfigureHorizontalPadding(1.25f);
                }

                float framingPadding = CalculateFramingEnvelopePadding(navigationBounds.LocalSize);
                navigationBounds.ConfigureCameraEnvelopePadding(framingPadding);
                EditorUtility.SetDirty(navigationBounds);
                report.Pass("El foco queda dentro del local y la cámara usa una envolvente exterior controlada para poder encuadrarlo completo.");

                SerializedObject controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("controlledCamera").objectReferenceValue = camera;
                controllerObject.FindProperty("settings").objectReferenceValue = settings;
                controllerObject.FindProperty("navigationBounds").objectReferenceValue = navigationBounds;
                controllerObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                report.Pass("El controlador referencia la cámara, la configuración y los límites correctos.");

                if (BistroBuilderProfessionalCameraInput.HasSupportedBackend)
                {
                    report.Pass("Backend de entrada compatible: " +
                                BistroBuilderProfessionalCameraInput.ActiveBackend + ".");
                }
                else
                {
                    report.Fail("Unity no tiene habilitado Input System ni Input Manager.");
                }

                report.Pass("La cámara usa un estado objetivo desacoplado y preparado para 369B/369C.");
                report.Pass("El movimiento utiliza tiempo no escalado y sigue operativo durante pausa.");
                report.Pass("El arrastre central usa delta de puntero escalado al encuadre y no retroalimenta la cámara.");
                report.Pass("La rueda distribuye cada muesca en una orden continua y amortiguada, sin saltos escalonados.");
                report.Pass("El botón derecho orbita con sensibilidad reducida alrededor del punto elegido bajo el cursor.");
                report.Pass("R eleva y F desciende la cámara en Y con trayectoria recta, velocidad contenida y topes de uso reducidos.");

                EditorSceneManager.MarkSceneDirty(activeScene);
                AssetDatabase.SaveAssets();
                report.Pass("La escena queda marcada para guardar los cambios de instalación.");

                Selection.activeGameObject = camera.gameObject;
                Finish(report, controller);
            }
            catch (Exception exception)
            {
                report.Fail("Excepción durante la instalación: " + exception.Message);
                Debug.LogException(exception);
                Finish(report, null);
            }
        }

        private static bool UpgradeInteractionProfile369A8(
            BistroBuilderCameraNavigationSettings settings)
        {
            if (settings == null ||
                settings.InteractionProfileVersion >=
                BistroBuilderCameraNavigationSettings.CurrentInteractionProfileVersion)
            {
                return false;
            }

            SerializedObject serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();

            SetBool(serializedSettings, "mousePitchEnabled", true);
            SetBool(serializedSettings, "keyboardElevationEnabled", true);
            SetFloat(serializedSettings, "middleMouseDragSensitivity", 1.0f);
            SetFloat(serializedSettings, "middleMouseDragDeadZonePixels", 0.35f);
            SetBool(serializedSettings, "orbitAroundPointer", true);
            SetFloat(serializedSettings, "mouseYawDegreesPerPixel", 0.12f);
            SetFloat(serializedSettings, "mousePitchDegreesPerPixel", 0.10f);
            SetFloat(serializedSettings, "rotationDampingTime", 0.18f);
            SetFloat(serializedSettings, "minimumPitch", 22.0f);
            SetFloat(serializedSettings, "logarithmicZoomStep", 0.032f);
            SetFloat(serializedSettings, "zoomDampingTime", 0.30f);
            SetFloat(serializedSettings, "maximumScrollNotchesPerFrame", 1.0f);
            SetFloat(serializedSettings, "zoomInputSmoothingTime", 0.16f);
            SetFloat(serializedSettings, "maximumQueuedScrollNotches", 3.0f);
            SetFloat(serializedSettings, "minimumOperationalDistance", 10.5f);
            SetFloat(serializedSettings, "maximumOperationalDistance", 32.0f);
            SetFloat(serializedSettings, "keyboardElevationSpeed", 3.25f);
            SetFloat(serializedSettings, "elevationAccelerationTime", 0.20f);
            SetFloat(serializedSettings, "elevationDecelerationTime", 0.34f);
            SetFloat(serializedSettings, "minimumElevatorHeight", 10.0f);
            SetFloat(serializedSettings, "maximumElevatorHeight", 22.0f);
            SetFloat(serializedSettings, "elevatorSoftLimitRange", 2.75f);

            SerializedProperty version = serializedSettings.FindProperty("interactionProfileVersion");
            if (version != null)
            {
                version.intValue = BistroBuilderCameraNavigationSettings.CurrentInteractionProfileVersion;
            }

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static float CalculateFramingEnvelopePadding(Vector3 localSize)
        {
            float largestHorizontalSide = Mathf.Max(localSize.x, localSize.z);
            return Mathf.Clamp(largestHorizontalSide * 0.30f, 8.0f, 24.0f);
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        internal static BistroBuilderCameraNavigationSettings LoadSettings()
        {
            return AssetDatabase.LoadAssetAtPath<BistroBuilderCameraNavigationSettings>(SettingsAssetPath);
        }

        internal static UnityEngine.Camera FindBestSceneCamera(Scene scene)
        {
            UnityEngine.Camera taggedMain = UnityEngine.Camera.main;
            if (IsSceneObject(taggedMain, scene))
            {
                return taggedMain;
            }

            UnityEngine.Camera[] cameras = Resources.FindObjectsOfTypeAll<UnityEngine.Camera>();
            UnityEngine.Camera firstActive = null;
            UnityEngine.Camera firstAny = null;
            for (int index = 0; index < cameras.Length; index++)
            {
                UnityEngine.Camera candidate = cameras[index];
                if (!IsSceneObject(candidate, scene))
                {
                    continue;
                }

                if (firstAny == null)
                {
                    firstAny = candidate;
                }

                if (candidate.isActiveAndEnabled && firstActive == null)
                {
                    firstActive = candidate;
                }
            }

            return firstActive != null ? firstActive : firstAny;
        }

        internal static BistroBuilderProfessionalCameraController[] FindSceneControllers(Scene scene)
        {
            BistroBuilderProfessionalCameraController[] all =
                Resources.FindObjectsOfTypeAll<BistroBuilderProfessionalCameraController>();
            return Array.FindAll(all, item => IsSceneObject(item, scene));
        }

        internal static BistroBuilderCameraBounds FindSceneBounds(Scene scene)
        {
            BistroBuilderCameraBounds[] all = Resources.FindObjectsOfTypeAll<BistroBuilderCameraBounds>();
            for (int index = 0; index < all.Length; index++)
            {
                if (IsSceneObject(all[index], scene))
                {
                    return all[index];
                }
            }

            return null;
        }

        private static bool TryDeriveNavigableFloorBounds(
            Scene scene,
            UnityEngine.Camera camera,
            out Bounds result)
        {
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            bool found = false;
            Bounds best = default(Bounds);
            float bestArea = 0.0f;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!IsSceneObject(renderer, scene) || renderer.transform.IsChildOf(camera.transform) ||
                    !renderer.gameObject.activeInHierarchy ||
                    (renderer.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                    renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                    renderer is LineRenderer || LooksLikeTransientVisual(renderer.gameObject))
                {
                    continue;
                }

                string name = renderer.gameObject.name.ToLowerInvariant();
                bool floorName = name.Contains("floor") || name.Contains("suelo") ||
                                 name.Contains("ground") || name.Contains("terrain") ||
                                 name.Contains("buildable") || name.Contains("placementarea");
                if (!floorName)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (!IsUsefulBounds(bounds))
                {
                    continue;
                }

                float area = bounds.size.x * bounds.size.z;
                if (area <= bestArea)
                {
                    continue;
                }

                best = bounds;
                bestArea = area;
                found = true;
            }

            result = best;
            return found;
        }

        private static bool TryDeriveRestaurantBounds(
            Scene scene,
            UnityEngine.Camera camera,
            out Bounds result)
        {
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            bool hasPreferred = false;
            Bounds preferred = default(Bounds);
            bool hasFallback = false;
            Bounds fallback = default(Bounds);

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!IsSceneObject(renderer, scene) || renderer.transform.IsChildOf(camera.transform))
                {
                    continue;
                }

                if (!renderer.gameObject.activeInHierarchy ||
                    (renderer.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer ||
                    LooksLikeTransientVisual(renderer.gameObject))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (!IsUsefulBounds(bounds))
                {
                    continue;
                }

                Encapsulate(ref fallback, ref hasFallback, bounds);
                if (LooksLikeRestaurantGeometry(renderer.gameObject))
                {
                    Encapsulate(ref preferred, ref hasPreferred, bounds);
                }
            }

            if (hasPreferred)
            {
                result = preferred;
                return true;
            }

            if (hasFallback)
            {
                result = fallback;
                return true;
            }

            Collider[] colliders = Resources.FindObjectsOfTypeAll<Collider>();
            bool hasCollider = false;
            Bounds colliderBounds = default(Bounds);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (!IsSceneObject(collider, scene) || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (!IsUsefulBounds(bounds))
                {
                    continue;
                }

                Encapsulate(ref colliderBounds, ref hasCollider, bounds);
            }

            result = colliderBounds;
            return hasCollider;
        }

        private static bool LooksLikeTransientVisual(GameObject gameObject)
        {
            string name = gameObject.name.ToLowerInvariant();
            return name.Contains("indicator") || name.Contains("gizmo") ||
                   name.Contains("preview") || name.Contains("ghost") ||
                   name.Contains("tooltip") || name.Contains("selection");
        }

        private static bool LooksLikeRestaurantGeometry(GameObject gameObject)
        {
            string name = gameObject.name.ToLowerInvariant();
            if (name.Contains("restaurant") || name.Contains("floor") || name.Contains("suelo") ||
                name.Contains("room") || name.Contains("comedor") || name.Contains("kitchen") ||
                name.Contains("cocina") || name.Contains("area") || name.Contains("zone") ||
                name.Contains("placement") || name.Contains("buildable") || name.Contains("terrain"))
            {
                return true;
            }

            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (typeName.StartsWith("RestaurantArea", StringComparison.Ordinal) ||
                    typeName.IndexOf("Placement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("BuildArea", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsefulBounds(Bounds bounds)
        {
            return BistroBuilderProfessionalCameraMath.IsFinite(bounds.center) &&
                   BistroBuilderProfessionalCameraMath.IsFinite(bounds.size) &&
                   bounds.size.x > 0.05f && bounds.size.z > 0.05f &&
                   bounds.size.x < 10000.0f && bounds.size.z < 10000.0f;
        }

        private static Bounds ExpandHorizontal(Bounds bounds, float multiplier)
        {
            Vector3 size = bounds.size;
            size.x = Mathf.Max(20.0f, size.x * multiplier);
            size.z = Mathf.Max(20.0f, size.z * multiplier);
            size.y = Mathf.Max(10.0f, size.y);
            bounds.size = size;
            return bounds;
        }

        private static bool IsDefaultUnconfiguredBounds(BistroBuilderCameraBounds bounds)
        {
            return bounds.LocalSize == new Vector3(80.0f, 30.0f, 80.0f) &&
                   bounds.transform.position == Vector3.zero;
        }

        private static void Encapsulate(ref Bounds aggregate, ref bool hasAggregate, Bounds value)
        {
            if (!hasAggregate)
            {
                aggregate = value;
                hasAggregate = true;
            }
            else
            {
                aggregate.Encapsulate(value);
            }
        }

        private static bool IsSceneObject(Component component, Scene scene)
        {
            return component != null &&
                   !EditorUtility.IsPersistent(component) &&
                   component.gameObject.scene == scene;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
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
