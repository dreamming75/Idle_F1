using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IdleF1.Combat
{
    /// <summary>
    /// Moves the player character using a joystick / gamepad.
    /// Works with the new Input System and falls back to legacy axes.
    /// Attach to the player object (RectTransform or Transform).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private float moveSpeed = 250f;

        [SerializeField]
        private bool constrainWithinParent = true;

        [SerializeField]
        private RectTransform rectTransformOverride;

        [SerializeField]
        private VirtualJoystick joystick;

        private RectTransform cachedRect;

        private void Awake()
        {
            cachedRect = rectTransformOverride != null
                ? rectTransformOverride
                : transform as RectTransform;
        }

        private void Update()
        {
            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude < 0.0001f) return;

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Move(input);
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            if (joystick != null)
            {
                input = joystick.Direction;
                if (joystick.HasInput || input.sqrMagnitude > 0.0001f)
                {
                    return input;
                }
            }

#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                input = gamepad.leftStick.ReadValue();
            }

            if (input.sqrMagnitude < 0.0001f)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                }
            }
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
#endif

            return input;
        }

        private void Move(Vector2 input)
        {
            float delta = moveSpeed * Time.deltaTime;
            Vector3 planar = new Vector3(input.x, 0f, input.y);

            if (cachedRect != null)
            {
                Vector2 newPos = cachedRect.anchoredPosition + input * delta;

                if (constrainWithinParent && cachedRect.parent is RectTransform parent)
                {
                    Rect parentRect = parent.rect;
                    Vector2 size = cachedRect.rect.size;
                    Vector2 pivot = cachedRect.pivot;
                    Vector2 min = parentRect.min + Vector2.Scale(size, pivot);
                    Vector2 max = parentRect.max - Vector2.Scale(size, Vector2.one - pivot);
                    newPos = new Vector2(
                        Mathf.Clamp(newPos.x, min.x, max.x),
                        Mathf.Clamp(newPos.y, min.y, max.y)
                    );
                }

                cachedRect.anchoredPosition = newPos;
            }
            else
            {
                transform.position += planar * delta;
            }

            // Snap-facing: look only fully left or fully right based on horizontal input.
            if (Mathf.Abs(input.x) > 0.0001f)
            {
                Vector3 faceDir = input.x > 0f ? Vector3.right : Vector3.left;
                transform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
            }
        }
    }
}

