using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BistroBuilder.CameraSystem
{
    public struct BistroBuilderCameraInputFrame
    {
        public Vector2 Pan;
        public float Yaw;
        public float Elevation;
        public Vector2 PointerPosition;
        public Vector2 PointerDelta;
        public float RawScroll;
        public bool MiddlePressed;
        public bool MiddleHeld;
        public bool MiddleReleased;
        public bool RightPressed;
        public bool RightHeld;
        public bool RightReleased;
        public bool FastModifier;
        public bool PointerOverUi;
        public bool TextInputFocused;
        public bool ApplicationFocused;
        public bool PointerAvailable;
    }

    /// <summary>
    /// Adaptador de entrada sin dependencias obligatorias. Compila con Input System, Input Manager
    /// o ambos y expone un único fotograma neutral al controlador de cámara.
    /// </summary>
    public static class BistroBuilderProfessionalCameraInput
    {
        private static int cachedSelectedObjectId = int.MinValue;
        private static bool cachedSelectedObjectIsTextInput;
        private static Vector2 previousLegacyPointerPosition;
        private static bool previousLegacyPointerPositionValid;

        // El EventSystem puede usar InputSystemUIInputModule, StandaloneInputModule o cambiar
        // durante Play Mode. No dependemos únicamente de IsPointerOverGameObject(): hacemos
        // además un raycast UI explícito y reutilizamos sus buffers para no generar basura.
        private static EventSystem cachedRaycastEventSystem;
        private static PointerEventData cachedPointerEventData;
        private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(24);

        // Margen ortogonal usado al sondear una franja de borde ocupada por UI. De esta forma
        // el edge-pan no se activa mientras el jugador cruza hacia una barra o botón pegado
        // al borde, incluso si el píxel exacto bajo el cursor cae entre dos controles.
        private const float EdgeUiProbeOrthogonalOffset = 24.0f;
        private static readonly float[] edgeUiProbeDepthFactors =
            { 0.20f, 0.45f, 0.70f, 0.95f };
        private static readonly float[] edgeUiProbeOffsets =
            { 0.0f, -EdgeUiProbeOrthogonalOffset, EdgeUiProbeOrthogonalOffset };

        public static string ActiveBackend
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
                return "Input System + Input Manager";
#elif ENABLE_INPUT_SYSTEM
                return "Input System";
#else
                return "Input Manager";
#endif
            }
        }

        public static bool HasSupportedBackend
        {
            get { return true; }
        }

        public static BistroBuilderCameraInputFrame Read()
        {
            BistroBuilderCameraInputFrame frame = new BistroBuilderCameraInputFrame();
            frame.ApplicationFocused = Application.isFocused;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            // Solo son necesarios cuando existe una ruta de respaldo al Input Manager.
            // En proyectos configurados exclusivamente con Input System no se declaran,
            // evitando advertencias CS0219 sin desactivar diagnósticos del compilador.
            bool keyboardRead = false;
            bool pointerRead = false;
#endif

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                frame.Pan = ReadInputSystemPan(keyboard);
                frame.Yaw = ReadInputSystemYaw(keyboard);
                frame.Elevation = ReadInputSystemElevation(keyboard);
                frame.FastModifier = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
#if ENABLE_LEGACY_INPUT_MANAGER
                keyboardRead = true;
#endif
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                frame.PointerPosition = mouse.position.ReadValue();
                frame.PointerDelta = mouse.delta.ReadValue();
                frame.RawScroll = mouse.scroll.ReadValue().y;
                frame.MiddlePressed = mouse.middleButton.wasPressedThisFrame;
                frame.MiddleHeld = mouse.middleButton.isPressed;
                frame.MiddleReleased = mouse.middleButton.wasReleasedThisFrame;
                frame.RightPressed = mouse.rightButton.wasPressedThisFrame;
                frame.RightHeld = mouse.rightButton.isPressed;
                frame.RightReleased = mouse.rightButton.wasReleasedThisFrame;
                frame.PointerAvailable = true;
#if ENABLE_LEGACY_INPUT_MANAGER
                pointerRead = true;
#endif
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!keyboardRead)
            {
                frame.Pan = ReadLegacyPan();
                frame.Yaw = ReadLegacyYaw();
                frame.Elevation = ReadLegacyElevation();
                frame.FastModifier = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }

            if (!pointerRead)
            {
                frame.PointerPosition = Input.mousePosition;
                frame.PointerDelta = previousLegacyPointerPositionValid
                    ? frame.PointerPosition - previousLegacyPointerPosition
                    : Vector2.zero;
                previousLegacyPointerPosition = frame.PointerPosition;
                previousLegacyPointerPositionValid = true;
                frame.RawScroll = Input.mouseScrollDelta.y;
                frame.MiddlePressed = Input.GetMouseButtonDown(2);
                frame.MiddleHeld = Input.GetMouseButton(2);
                frame.MiddleReleased = Input.GetMouseButtonUp(2);
                frame.RightPressed = Input.GetMouseButtonDown(1);
                frame.RightHeld = Input.GetMouseButton(1);
                frame.RightReleased = Input.GetMouseButtonUp(1);
                frame.PointerAvailable = Input.mousePresent;
            }
#endif

            frame.PointerOverUi = IsPointerOverUi(frame.PointerPosition);
            frame.TextInputFocused = IsTextInputFocused();
            return frame;
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2 ReadInputSystemPan(Keyboard keyboard)
        {
            float horizontal = 0.0f;
            float vertical = 0.0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                horizontal -= 1.0f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                horizontal += 1.0f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                vertical -= 1.0f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                vertical += 1.0f;
            }

            Vector2 result = new Vector2(horizontal, vertical);
            return result.sqrMagnitude > 1.0f ? result.normalized : result;
        }

        private static float ReadInputSystemYaw(Keyboard keyboard)
        {
            float yaw = 0.0f;
            if (keyboard.qKey.isPressed)
            {
                yaw -= 1.0f;
            }

            if (keyboard.eKey.isPressed)
            {
                yaw += 1.0f;
            }

            return yaw;
        }

        private static float ReadInputSystemElevation(Keyboard keyboard)
        {
            float elevation = 0.0f;
            if (keyboard.rKey.isPressed)
            {
                elevation += 1.0f;
            }

            if (keyboard.fKey.isPressed)
            {
                elevation -= 1.0f;
            }

            return elevation;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        private static Vector2 ReadLegacyPan()
        {
            float horizontal = 0.0f;
            float vertical = 0.0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1.0f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1.0f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                vertical -= 1.0f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                vertical += 1.0f;
            }

            Vector2 result = new Vector2(horizontal, vertical);
            return result.sqrMagnitude > 1.0f ? result.normalized : result;
        }

        private static float ReadLegacyYaw()
        {
            float yaw = 0.0f;
            if (Input.GetKey(KeyCode.Q))
            {
                yaw -= 1.0f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                yaw += 1.0f;
            }

            return yaw;
        }

        private static float ReadLegacyElevation()
        {
            float elevation = 0.0f;
            if (Input.GetKey(KeyCode.R))
            {
                elevation += 1.0f;
            }

            if (Input.GetKey(KeyCode.F))
            {
                elevation -= 1.0f;
            }

            return elevation;
        }
#endif

        private static bool IsPointerOverUi(Vector2 pointerPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // Ruta rápida. Sigue siendo útil con StandaloneInputModule.
            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }

            // Ruta robusta para Input System y para configuraciones en las que la consulta
            // parameterless no identifica correctamente el puntero activo. Solo aceptamos
            // GraphicRaycaster para no confundir colliders del mundo con UI.
            return IsUiAtScreenPoint(pointerPosition);
        }

        /// <summary>
        /// Indica si la franja de borde hacia la que intenta desplazarse la cámara está
        /// ocupada por UI. Se usa únicamente para proteger el edge-pan; no bloquea el
        /// resto de navegación cuando el cursor está realmente sobre el mundo.
        /// </summary>
        public static bool IsUiProtectingEdge(
            Vector2 pointerPosition,
            Vector2 edgePanDirection,
            int screenWidth,
            int screenHeight,
            float probeDepthPixels)
        {
            if (EventSystem.current == null ||
                screenWidth <= 0 || screenHeight <= 0 ||
                edgePanDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float width = Mathf.Max(1.0f, screenWidth);
            float height = Mathf.Max(1.0f, screenHeight);
            float depth = Mathf.Clamp(probeDepthPixels, 8.0f, 120.0f);
            float x = Mathf.Clamp(pointerPosition.x, 0.0f, width - 1.0f);
            float y = Mathf.Clamp(pointerPosition.y, 0.0f, height - 1.0f);

            if (edgePanDirection.y > 0.0001f &&
                ProbeHorizontalEdge(x, height, width, depth, true))
            {
                return true;
            }

            if (edgePanDirection.y < -0.0001f &&
                ProbeHorizontalEdge(x, height, width, depth, false))
            {
                return true;
            }

            if (edgePanDirection.x < -0.0001f &&
                ProbeVerticalEdge(y, width, height, depth, false))
            {
                return true;
            }

            if (edgePanDirection.x > 0.0001f &&
                ProbeVerticalEdge(y, width, height, depth, true))
            {
                return true;
            }

            return false;
        }

        private static bool ProbeHorizontalEdge(
            float pointerX,
            float screenHeight,
            float screenWidth,
            float depth,
            bool top)
        {
            for (int depthIndex = 0; depthIndex < edgeUiProbeDepthFactors.Length; depthIndex++)
            {
                float inward = depth * edgeUiProbeDepthFactors[depthIndex];
                float y = top
                    ? Mathf.Clamp(screenHeight - 1.0f - inward, 0.0f, screenHeight - 1.0f)
                    : Mathf.Clamp(inward, 0.0f, screenHeight - 1.0f);

                for (int offsetIndex = 0; offsetIndex < edgeUiProbeOffsets.Length; offsetIndex++)
                {
                    float x = Mathf.Clamp(
                        pointerX + edgeUiProbeOffsets[offsetIndex],
                        0.0f,
                        screenWidth - 1.0f);
                    if (IsUiAtScreenPoint(new Vector2(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ProbeVerticalEdge(
            float pointerY,
            float screenWidth,
            float screenHeight,
            float depth,
            bool right)
        {
            for (int depthIndex = 0; depthIndex < edgeUiProbeDepthFactors.Length; depthIndex++)
            {
                float inward = depth * edgeUiProbeDepthFactors[depthIndex];
                float x = right
                    ? Mathf.Clamp(screenWidth - 1.0f - inward, 0.0f, screenWidth - 1.0f)
                    : Mathf.Clamp(inward, 0.0f, screenWidth - 1.0f);

                for (int offsetIndex = 0; offsetIndex < edgeUiProbeOffsets.Length; offsetIndex++)
                {
                    float y = Mathf.Clamp(
                        pointerY + edgeUiProbeOffsets[offsetIndex],
                        0.0f,
                        screenHeight - 1.0f);
                    if (IsUiAtScreenPoint(new Vector2(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsUiAtScreenPoint(Vector2 screenPoint)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (cachedRaycastEventSystem != eventSystem || cachedPointerEventData == null)
            {
                cachedRaycastEventSystem = eventSystem;
                cachedPointerEventData = new PointerEventData(eventSystem);
            }

            cachedPointerEventData.Reset();
            cachedPointerEventData.position = screenPoint;
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(cachedPointerEventData, uiRaycastResults);

            for (int index = 0; index < uiRaycastResults.Count; index++)
            {
                BaseRaycaster module = uiRaycastResults[index].module;
                if (module is GraphicRaycaster)
                {
                    uiRaycastResults.Clear();
                    return true;
                }
            }

            uiRaycastResults.Clear();
            return false;
        }

        private static bool IsTextInputFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            int selectedId = selected != null ? selected.GetInstanceID() : 0;

            if (selectedId == cachedSelectedObjectId)
            {
                return cachedSelectedObjectIsTextInput;
            }

            cachedSelectedObjectId = selectedId;
            cachedSelectedObjectIsTextInput = false;
            if (selected == null)
            {
                return false;
            }

            Component[] components = selected.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (typeName == "InputField" ||
                    typeName == "TMP_InputField" ||
                    typeName == "TextField")
                {
                    cachedSelectedObjectIsTextInput = true;
                    break;
                }
            }

            return cachedSelectedObjectIsTextInput;
        }
    }
}
