using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.RenderStreaming
{
    public interface IInput
    {
        Mouse Mouse { get; }
        Keyboard Keyboard { get; }
        Touchscreen Touchscreen { get; }
        Gamepad Gamepad { get; }
    }

    public class DefaultInput : IInput
    {
        public Mouse Mouse { get; }
        public Keyboard Keyboard { get; }
        public Touchscreen Touchscreen { get; }
        public Gamepad Gamepad { get; }

        public DefaultInput()
        {
            Mouse = Mouse.current != null ? Mouse.current : InputSystem.AddDevice<Mouse>();
            Keyboard = Keyboard.current != null ? Keyboard.current : InputSystem.AddDevice<Keyboard>();
            Touchscreen = Touchscreen.current != null ? Touchscreen.current : InputSystem.AddDevice<Touchscreen>();
            Gamepad = Gamepad.current != null ? Gamepad.current : InputSystem.AddDevice<Gamepad>();
        }

        public void MakeCurrent()
        {
            Mouse.MakeCurrent();
            Keyboard.MakeCurrent();
            Touchscreen.MakeCurrent();
            Gamepad.MakeCurrent();
        }
    }

    public class SimpleCameraController : MonoBehaviour
    {
        class CameraState
        {
            public float yaw;
            public float pitch;
            public float roll;
            public float x;
            public float y;
            public float z;

            public void SetFromTransform(Transform t)
            {
                pitch = t.eulerAngles.x;
                yaw = t.eulerAngles.y;
                roll = t.eulerAngles.z;
                x = t.position.x;
                y = t.position.y;
                z = t.position.z;
            }

            public void Translate(Vector3 translation)
            {
                Vector3 rotatedTranslation = Quaternion.Euler(pitch, yaw, roll) * translation;

                x += rotatedTranslation.x;
                y += rotatedTranslation.y;
                z += rotatedTranslation.z;
            }

            public void LerpTowards(CameraState target, float positionLerpPct, float rotationLerpPct)
            {
                yaw = Mathf.Lerp(yaw, target.yaw, rotationLerpPct);
                pitch = Mathf.Lerp(pitch, target.pitch, rotationLerpPct);
                roll = Mathf.Lerp(roll, target.roll, rotationLerpPct);

                x = Mathf.Lerp(x, target.x, positionLerpPct);
                y = Mathf.Lerp(y, target.y, positionLerpPct);
                z = Mathf.Lerp(z, target.z, positionLerpPct);
            }

            public void UpdateTransform(Transform t)
            {
                t.eulerAngles = new Vector3(pitch, yaw, roll);
                t.position = new Vector3(x, y, z);
            }
        }

        CameraState m_TargetCameraState = new CameraState();
        CameraState m_InterpolatingCameraState = new CameraState();
        CameraState m_InitialCameraState = new CameraState();

        [Header("Movement Settings")]
        [Tooltip("Movement Sensitivity Factor."), Range(0.001f, 1f)]
        [SerializeField] float       m_movementSensitivityFactor = 0.1f;

        [Tooltip("Exponential boost factor on translation, controllable by mouse wheel.")]
        public float boost = 3.5f;

        [Tooltip("Time it takes to interpolate camera position 99% of the way to the target."), Range(0.001f, 1f)]
        public float positionLerpTime = 0.2f;


        [Header("Rotation Settings")]
        [Tooltip("X = Change in mouse position.\nY = Multiplicative factor for camera rotation.")]
        public AnimationCurve mouseSensitivityCurve = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 5f), new Keyframe(1f, 2.5f, 0f, 0f));

        [Tooltip("Time it takes to interpolate camera rotation 99% of the way to the target."), Range(0.001f, 1f)]
        public float rotationLerpTime = 0.01f;

        [Tooltip("Whether or not to invert our Y axis for mouse input to rotation.")]
        public bool invertY = false;

        private Gamepad m_gamepad;
        private Keyboard m_keyboard;
        private Mouse m_mouse;
        private Touchscreen m_screen;

        public void SetInput(IInput input)
        {
            m_mouse = input.Mouse;
            m_keyboard = input.Keyboard;
            m_screen = input.Touchscreen;
            m_gamepad = input.Gamepad;
        }

        void Start()
        {
            m_InitialCameraState.SetFromTransform(transform);

            RenderStreaming.Instance?.AddController(this);
        }

        void OnDestroy()
        {
            RenderStreaming.Instance?.RemoveController(this);
        }

        void OnEnable()
        {
            m_TargetCameraState.SetFromTransform(transform);
            m_InterpolatingCameraState.SetFromTransform(transform);
        }

//---------------------------------------------------------------------------------------------------------------------
        Vector3 GetTranslationFromInput(Vector2 input) {
            if (!invertY) {
                input.y *= -1;
            }

            Vector3 dir = Vector3.right * input.x * m_movementSensitivityFactor;
            dir += Vector3.back * input.y * m_movementSensitivityFactor;

            return dir;
        }

//---------------------------------------------------------------------------------------------------------------------

        void UpdateTargetCameraStateFromInput(Vector2 input)
        {
            if (!invertY) {
                input.y *= -1;
            }
            float mouseSensitivityFactor = mouseSensitivityCurve.Evaluate(input.magnitude);

            m_TargetCameraState.yaw += input.x * mouseSensitivityFactor;
            m_TargetCameraState.pitch += input.y * mouseSensitivityFactor;
        }

//---------------------------------------------------------------------------------------------------------------------

        Vector3 GetInputTranslationDirection()
        {
            Vector3 direction = new Vector3();
            if (m_keyboard.wKey.isPressed)
            {
                direction += Vector3.forward;
            }
            if (m_keyboard.sKey.isPressed)
            {
                direction += Vector3.back;
            }
            if (m_keyboard.aKey.isPressed)
            {
                direction += Vector3.left;
            }
            if (m_keyboard.dKey.isPressed)
            {
                direction += Vector3.right;
            }
            if (m_keyboard.qKey.isPressed)
            {
                direction += Vector3.down;
            }
            if (m_keyboard.eKey.isPressed)
            {
                direction += Vector3.up;
            }

            // gamepad right stick control
            if (m_gamepad.rightStick != null)
            {
                var axis = m_gamepad.rightStick.ReadValue();
                direction += new Vector3(axis.x, 0, axis.y);
            }

            //Translation
            if (m_screen.touches.Count == 2)
            {
                var activeTouches = m_screen.touches.ToArray();
                direction = GetTranslationFromInput((activeTouches[0].delta.ReadValue() + activeTouches[1].delta.ReadValue()) / 2f);
            }
            else if (IsMouseDragged(m_mouse,true))
            {
                direction = GetTranslationFromInput(m_mouse.delta.ReadValue());
            }
            return direction;
        }

        void FixedUpdate()
        {
            if (m_keyboard.uKey.isPressed)
            {
                ResetCamera();
                return;
            }

            // Rotation 
            if (IsMouseDragged(m_mouse,false))
            {
                UpdateTargetCameraStateFromInput(m_mouse.delta.ReadValue());
            }
            else if (m_screen.touches.Count == 1)
            {
                var activeTouches = m_screen.touches.ToArray();
                UpdateTargetCameraStateFromInput(activeTouches[0].delta.ReadValue());
            }
            
            // Rotation from joystick
            if(m_gamepad.leftStick != null)
                UpdateTargetCameraStateFromInput(m_gamepad.leftStick.ReadValue());
            // Translation
            var translation = GetInputTranslationDirection() * Time.deltaTime;

            // Speed up movement when shift key held
            if (m_keyboard.leftShiftKey.isPressed)
            {
                translation *= 10.0f;
            }

            translation *= Mathf.Pow(2.0f, boost);

            m_TargetCameraState.Translate(translation);

            // Framerate-independent interpolation
            // Calculate the lerp amount, such that we get 99% of the way to our target in the specified time
            var positionLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / positionLerpTime) * Time.deltaTime);
            var rotationLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / rotationLerpTime) * Time.deltaTime);
            m_InterpolatingCameraState.LerpTowards(m_TargetCameraState, positionLerpPct, rotationLerpPct);

            m_InterpolatingCameraState.UpdateTransform(transform);
        }

        void ResetCamera()
        {
            m_InitialCameraState.UpdateTransform(transform);
            m_TargetCameraState.SetFromTransform(transform);
            m_InterpolatingCameraState.SetFromTransform(transform);

        }

//---------------------------------------------------------------------------------------------------------------------
        static bool IsMouseDragged(Mouse m, bool useLeftButton) {
            if (null == m)
                return false;

            if (Screen.safeArea.Contains(m.position.ReadValue())) {
                //check left/right click
                if ((useLeftButton && m.leftButton.isPressed) || (!useLeftButton && m.rightButton.isPressed)) {               
                    return true;
                }
            }

            return false;
        }

    }


}
