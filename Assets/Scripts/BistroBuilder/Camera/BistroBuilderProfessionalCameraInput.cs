using UnityEngine;
using UnityEngine.EventSystems;

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

            frame.PointerOverUi = IsPointerOverUi();
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

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
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
