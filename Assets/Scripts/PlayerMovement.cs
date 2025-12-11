using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IdleF1.Combat
{
    /// <summary>
    /// Moves the player character using a joystick / gamepad OR by tapping the screen smoothly.
    /// Works with the new Input System and falls back to legacy axes.
    /// Attach to the player object (RectTransform or Transform).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour
    {
        public enum TapMovementMode
        {
            SpeedBased,   // 일정한 속도로 이동
            DurationBased // 거리에 상관없이 지정된 시간 동안 이동
        }

        [Header("Manual Movement Settings (Joystick/Keys)")]
        [SerializeField]
        private float moveSpeed = 250f;

        [Header("Tap To Move Settings")]
        [SerializeField]
        private bool enableTapToMove = true; // 터치 이동 활성화 여부
        
        [SerializeField] 
        private TapMovementMode tapMoveMode = TapMovementMode.SpeedBased;

        [Tooltip("SpeedBased 모드일 때의 이동 속도")]
        [SerializeField] 
        private float tapMoveSpeed = 500f;
        
        [Tooltip("DurationBased 모드일 때 이동에 걸리는 시간 (초)")]
        [SerializeField] 
        private float tapMoveDuration = 0.5f;

        [Header("Constraints")]
        [SerializeField]
        private bool constrainWithinParent = true;

        [SerializeField]
        private RectTransform rectTransformOverride;

        [Header("Input References")]
        [SerializeField]
        private VirtualJoystick joystick;
        
        [Header("Camera Constraint")]
        [SerializeField]
        private bool constrainToCamera = true;
        
        [SerializeField]
        private BattleAreaManager battleAreaManager;

        private RectTransform cachedRect;
        private Canvas parentCanvas;
        private Camera mainCamera;

        // --- 터치 이동 관련 상태 변수 ---
        private bool isMovingToTarget = false;
        private Vector2 targetAnchoredPosition; // UI용 목표 지점
        private Vector3 targetWorldPosition;    // World용 목표 지점
        
        // Duration 모드용 변수
        private Vector2 startAnchoredPosition;
        private Vector3 startWorldPosition;
        private float movementStartTime;

        private void Awake()
        {
            cachedRect = rectTransformOverride != null
                ? rectTransformOverride
                : transform as RectTransform;

            if (cachedRect != null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
            
            mainCamera = Camera.main;

            if (battleAreaManager == null && constrainToCamera)
            {
                battleAreaManager = FindFirstObjectByType<BattleAreaManager>();
            }
        }

        private void Update()
        {
            // 1. 수동 입력(조이스틱/키보드) 확인
            // 수동 입력이 발생하면 진행 중인 터치 자동 이동을 취소하고 수동 조작을 우선합니다.
            Vector2 manualInput = ReadMoveInput();
            if (manualInput.sqrMagnitude > 0.0001f)
            {
                isMovingToTarget = false; // 터치 이동 취소
                if (manualInput.sqrMagnitude > 1f) manualInput.Normalize();
                MoveContinuous(manualInput);
                return; // 수동 입력 처리 후 이번 프레임 종료
            }

            // 2. 새로운 터치 입력 확인
            if (enableTapToMove && TryGetTapPosition(out Vector2 screenPos))
            {
                // 새로운 목표 지점으로 이동 시작
                StartTapMovement(screenPos);
            }

            // 3. 터치 목표 지점으로 이동 중이라면 계속 처리
            if (isMovingToTarget)
            {
                ProcessTapMovement();
            }
        }

        /// <summary>
        /// 화면 터치 입력을 감지하고 스크린 좌표를 반환합니다.
        /// </summary>
        private bool TryGetTapPosition(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// 터치한 스크린 좌표를 기반으로 목표 지점을 계산하고 이동 상태를 시작합니다.
        /// </summary>
        private void StartTapMovement(Vector2 screenPos)
        {
            isMovingToTarget = true;
            movementStartTime = Time.time;

            if (cachedRect != null)
            {
                // --- RectTransform (UI) 처리 ---
                if (parentCanvas == null || cachedRect.parent == null)
                {
                    isMovingToTarget = false;
                    return;
                }

                Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
                RectTransform parentRect = cachedRect.parent as RectTransform;
                
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 localPoint))
                {
                    startAnchoredPosition = cachedRect.anchoredPosition;
                    // 목표 지점에 미리 제약 조건을 적용합니다.
                    targetAnchoredPosition = ApplyRectConstraints(localPoint);
                }
                else
                {
                     isMovingToTarget = false;
                }
            }
            else
            {
                // --- Transform (World Space) 처리 ---
                if (mainCamera == null)
                {
                    isMovingToTarget = false;
                    return;
                }

                float distanceToCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceToCamera));
                
                startWorldPosition = transform.position;
                // 목표 지점에 미리 제약 조건을 적용합니다.
                targetWorldPosition = ApplyWorldConstraints(worldPoint);
            }
        }

        /// <summary>
        /// 매 프레임 목표 지점을 향해 이동시킵니다.
        /// </summary>
        private void ProcessTapMovement()
        {
            if (cachedRect != null)
            {
                MoveRectToTarget();
            }
            else
            {
                MoveTransformToTarget();
            }
        }

        private void MoveRectToTarget()
        {
            Vector2 currentPos = cachedRect.anchoredPosition;
            Vector2 newPos;

            if (tapMoveMode == TapMovementMode.SpeedBased)
            {
                float step = tapMoveSpeed * Time.deltaTime;
                newPos = Vector2.MoveTowards(currentPos, targetAnchoredPosition, step);
            }
            else // DurationBased
            {
                float timeSinceStart = Time.time - movementStartTime;
                float t = Mathf.Clamp01(timeSinceStart / tapMoveDuration);
                // 필요하다면 여기에 Easing 함수를 적용할 수 있습니다 (예: Mathf.SmoothStep(0, 1, t))
                newPos = Vector2.Lerp(startAnchoredPosition, targetAnchoredPosition, t);
            }

            UpdateFacingDirection(newPos.x - currentPos.x);
            cachedRect.anchoredPosition = newPos;

            // 도착 확인 (약간의 오차 허용)
            if (Vector2.Distance(newPos, targetAnchoredPosition) < 0.1f)
            {
                cachedRect.anchoredPosition = targetAnchoredPosition; // 최종 위치로 스냅
                isMovingToTarget = false;
            }
        }

        private void MoveTransformToTarget()
        {
            Vector3 currentPos = transform.position;
            Vector3 newPos;

            if (tapMoveMode == TapMovementMode.SpeedBased)
            {
                float step = tapMoveSpeed * Time.deltaTime;
                newPos = Vector3.MoveTowards(currentPos, targetWorldPosition, step);
            }
            else // DurationBased
            {
                float timeSinceStart = Time.time - movementStartTime;
                float t = Mathf.Clamp01(timeSinceStart / tapMoveDuration);
                newPos = Vector3.Lerp(startWorldPosition, targetWorldPosition, t);
            }
            
            UpdateFacingDirection(newPos.x - currentPos.x);
            transform.position = newPos;

             // 도착 확인
            if (Vector3.Distance(newPos, targetWorldPosition) < 0.1f)
            {
                transform.position = targetWorldPosition;
                isMovingToTarget = false;
            }
        }
        
        // --- 기존 수동 이동 및 헬퍼 함수들 ---

        private Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            if (joystick != null)
            {
                input = joystick.Direction;
                if (joystick.HasInput || input.sqrMagnitude > 0.0001f) return input;
            }

#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null) input = gamepad.leftStick.ReadValue();

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

        private void MoveContinuous(Vector2 input)
        {
            float delta = moveSpeed * Time.deltaTime;

            if (cachedRect != null)
            {
                Vector2 newPos = cachedRect.anchoredPosition + input * delta;
                newPos = ApplyRectConstraints(newPos);
                cachedRect.anchoredPosition = newPos;
            }
            else
            {
                Vector3 movement = new Vector3(input.x, input.y, 0f) * delta;
                Vector3 newPos = transform.position + movement;
                newPos = ApplyWorldConstraints(newPos);
                transform.position = newPos;
            }

            UpdateFacingDirection(input.x);
        }

        private Vector2 ApplyRectConstraints(Vector2 targetPos)
        {
            if (constrainWithinParent && cachedRect.parent is RectTransform parent)
            {
                Rect parentRect = parent.rect;
                Vector2 size = cachedRect.rect.size;
                Vector2 pivot = cachedRect.pivot;
                Vector2 min = parentRect.min + Vector2.Scale(size, pivot);
                Vector2 max = parentRect.max - Vector2.Scale(size, Vector2.one - pivot);
                targetPos = new Vector2(Mathf.Clamp(targetPos.x, min.x, max.x), Mathf.Clamp(targetPos.y, min.y, max.y));
            }
            
            if (constrainToCamera && battleAreaManager != null)
            {
                Rect cameraBounds = battleAreaManager.GetCameraBoundsInCanvasSpace();
                if (cameraBounds.width > 0 && cameraBounds.height > 0)
                {
                    Vector2 size = cachedRect.rect.size;
                    Vector2 pivot = cachedRect.pivot;
                    Vector2 min = cameraBounds.min + Vector2.Scale(size, pivot);
                    Vector2 max = cameraBounds.max - Vector2.Scale(size, Vector2.one - pivot);
                    targetPos = new Vector2(Mathf.Clamp(targetPos.x, min.x, max.x), Mathf.Clamp(targetPos.y, min.y, max.y));
                }
            }
            return targetPos;
        }

        private Vector3 ApplyWorldConstraints(Vector3 targetPos)
        {
            if (constrainToCamera && battleAreaManager != null)
            {
                Rect cameraBounds = battleAreaManager.GetCameraBoundsInWorldSpace();
                if (cameraBounds.width > 0 && cameraBounds.height > 0)
                {
                    float playerSize = 0.5f;
                    Collider col = GetComponent<Collider>();
                    if (col != null) playerSize = Mathf.Max(col.bounds.size.x, col.bounds.size.y) * 0.5f;
                    
                    targetPos.x = Mathf.Clamp(targetPos.x, cameraBounds.xMin + playerSize, cameraBounds.xMax - playerSize);
                    targetPos.y = Mathf.Clamp(targetPos.y, cameraBounds.yMin + playerSize, cameraBounds.yMax - playerSize);
                }
            }
            return targetPos;
        }

        private void UpdateFacingDirection(float horizontalMovement)
        {
            if (Mathf.Abs(horizontalMovement) > 0.0001f)
            {
                Vector3 faceDir = horizontalMovement > 0f ? Vector3.right : Vector3.left;
                transform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
            }
        }
    }
}