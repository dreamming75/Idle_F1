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
        
        [Header("카메라 범위 제한")]
        [SerializeField]
        private bool constrainToCamera = true;
        
        [SerializeField]
        private BattleAreaManager battleAreaManager;

        private RectTransform cachedRect;

        private void Awake()
        {
            cachedRect = rectTransformOverride != null
                ? rectTransformOverride
                : transform as RectTransform;
            
            // BattleAreaManager를 자동으로 찾기
            if (battleAreaManager == null && constrainToCamera)
            {
                battleAreaManager = FindFirstObjectByType<BattleAreaManager>();
            }
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

            if (cachedRect != null)
            {
                // RectTransform을 사용하는 경우: X, Y 축으로 이동
                Vector2 newPos = cachedRect.anchoredPosition + input * delta;

                // 부모 RectTransform 내부로 제한
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
                
                // 카메라 범위로 제한
                if (constrainToCamera && battleAreaManager != null)
                {
                    Rect cameraBounds = battleAreaManager.GetCameraBoundsInCanvasSpace();
                    if (cameraBounds.width > 0 && cameraBounds.height > 0)
                    {
                        Vector2 size = cachedRect.rect.size;
                        Vector2 pivot = cachedRect.pivot;
                        Vector2 min = cameraBounds.min + Vector2.Scale(size, pivot);
                        Vector2 max = cameraBounds.max - Vector2.Scale(size, Vector2.one - pivot);
                        newPos = new Vector2(
                            Mathf.Clamp(newPos.x, min.x, max.x),
                            Mathf.Clamp(newPos.y, min.y, max.y)
                        );
                    }
                }

                cachedRect.anchoredPosition = newPos;
            }
            else
            {
                // 일반 Transform을 사용하는 경우: X, Y 축으로 이동 (Z는 깊이)
                // UI Canvas에서 보통 X, Y를 사용하므로 Y축을 위아래로 사용
                Vector3 movement = new Vector3(input.x, input.y, 0f) * delta;
                Vector3 newPos = transform.position + movement;
                
                // 카메라 범위로 제한
                if (constrainToCamera && battleAreaManager != null)
                {
                    Rect cameraBounds = battleAreaManager.GetCameraBoundsInWorldSpace();
                    if (cameraBounds.width > 0 && cameraBounds.height > 0)
                    {
                        // 플레이어의 크기 고려 (Collider나 Renderer가 있으면 그 크기 사용)
                        float playerSize = 0.5f; // 기본값, 필요시 조정
                        Collider col = GetComponent<Collider>();
                        if (col != null)
                        {
                            playerSize = Mathf.Max(col.bounds.size.x, col.bounds.size.y) * 0.5f;
                        }
                        
                        newPos.x = Mathf.Clamp(newPos.x, cameraBounds.xMin + playerSize, cameraBounds.xMax - playerSize);
                        newPos.y = Mathf.Clamp(newPos.y, cameraBounds.yMin + playerSize, cameraBounds.yMax - playerSize);
                    }
                }
                
                transform.position = newPos;
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

