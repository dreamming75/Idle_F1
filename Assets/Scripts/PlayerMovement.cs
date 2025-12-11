using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IdleF1.Combat
{
    /// <summary>
    /// 플레이어 캐릭터를 이동시키는 핵심 컴포넌트입니다.
    /// 조이스틱/키보드를 통한 수동 이동과 화면 탭을 통한 자동 이동을 모두 지원합니다.
    /// Tap 이동 시 최대 거리 제한 및 UI Raycast Target을 통한 이동 허용/차단 필터링 기능을 포함합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour
    {
        // 탭 이동 방식 정의
        public enum TapMovementMode
        {
            SpeedBased,   // 일정한 속도로 이동 (커브 미적용)
            DurationBased // 지정된 시간 동안 이동 (커브 적용됨)
        }

        #region Inspector Settings

        [Header("Manual Movement Settings (Joystick/Keys)")]
        [Tooltip("조이스틱이나 키보드로 이동할 때의 속도입니다.")]
        [SerializeField]
        private float manualMoveSpeed = 250f;

        [Header("Tap To Move General Settings")]
        [Tooltip("화면 탭으로 이동하는 기능을 활성화합니다.")]
        [SerializeField]
        private bool enableTapToMove = true;
        
        [Tooltip("탭 이동 방식을 선택합니다.\n- SpeedBased: 일정한 속도\n- DurationBased: 애니메이션 커브 기반")]
        [SerializeField] 
        private TapMovementMode tapMoveMode = TapMovementMode.DurationBased;

        [Header("Tap Move - Speed Based")]
        [SerializeField] 
        private float tapMoveSpeedConstant = 500f;
        
        [Header("Tap Move - Duration Based & Curve")]
        [SerializeField] 
        private float tapMoveStepDuration = 0.5f;

        [SerializeField]
        private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Tap Move - Segmentation (Long Distance)")]
        [SerializeField]
        private float maxStepDistance = 100f;
        [SerializeField]
        private float stepDelay = 0.5f;

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
        
        // ★ 추가된 기능 1: Tap Max Distance Limit
        [Header("Tap Max Distance Limit")]
        [Tooltip("탭 이동 시 한 번의 탭으로 이동할 수 있는 최대 거리를 제한합니다.")]
        [SerializeField]
        private bool enableTapDistanceLimit = false; 

        [Tooltip("탭 이동 시 허용되는 최대 거리입니다. 이 값을 초과하면 클램프됩니다.")]
        [SerializeField]
        private float maxTapDistance = 100f; 

        // ★ 추가된 기능 2: 터치를 허용할 Image 목록
        [Header("Tap Input Filtering")]
        [Tooltip("이 리스트에 포함된 Image 위를 터치하면 이동합니다. 리스트에 없는 Raycast Target UI를 터치하면 이동이 차단됩니다.")]
        [SerializeField]
        private List<Image> tapAllowedImages = new List<Image>();

        #endregion

        #region Internal State Variables

        private RectTransform cachedRect;
        private Canvas parentCanvas;
        private Camera mainCamera;

        private bool isMovingToTarget = false;

        private Vector2 intermediateTargetAnchored;
        private Vector3 intermediateTargetWorld;
        private Vector2 finalDestinationAnchored;
        private Vector3 finalDestinationWorld;

        private bool isWaitingBetweenSteps = false;
        private float waitStartTime;

        private Vector2 stepStartAnchoredPosition;
        private Vector3 stepStartWorldPosition;
        private float stepMovementStartTime;
        
        // UI Raycasting 관련 변수
        private PointerEventData pointerEventData;
        private List<RaycastResult> raycastResults;

        #endregion

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

#if UNITY_2023_1_OR_NEWER
            if (battleAreaManager == null && constrainToCamera)
                battleAreaManager = FindFirstObjectByType<BattleAreaManager>();
#else
            if (battleAreaManager == null && constrainToCamera)
                battleAreaManager = FindObjectOfType<BattleAreaManager>();
#endif
            
            // Raycasting 초기화: EventSystem.current가 null일 수 있지만, 
            // RaycastAll 전에 다시 체크하므로 일단 초기화합니다.
            pointerEventData = new PointerEventData(EventSystem.current);
            raycastResults = new List<RaycastResult>();
        }

        private void Update()
        {
            // 1. 수동 입력 우선
            Vector2 manualInput = ReadMoveInput();
            if (manualInput.sqrMagnitude > 0.0001f)
            {
                StopTapMovement();
                if (manualInput.sqrMagnitude > 1f)
                    manualInput.Normalize();

                MoveContinuous(manualInput);
                return;
            }

            // 2. 탭 입력 시 필터링 검증
            if (enableTapToMove && TryGetTapPosition(out Vector2 screenPos))
            {
                if (IsTapAllowed(screenPos)) // UI 필터링
                {
                    StartTapMovement(screenPos);
                }
            }

            // 3. 탭 이동 처리
            if (isMovingToTarget)
            {
                ProcessTapMovement();
            }
        }

        private void StopTapMovement()
        {
            isMovingToTarget = false;
            isWaitingBetweenSteps = false;
        }

        #region Tap Input & Validation

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
        /// 터치 위치가 이동을 허용하는 영역인지 판단합니다. (UI 필터링)
        /// </summary>
        private bool IsTapAllowed(Vector2 screenPos)
        {
            // EventSystem이 없으면 UI Raycast 불가
            if (EventSystem.current == null) return true; 

            pointerEventData.position = screenPos;
            raycastResults.Clear();
            
            // 1. UI Raycast 수행
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            // 2. UI를 전혀 누르지 않았을 경우 (월드/빈 공간 터치)
            if (raycastResults.Count == 0)
            {
                return true; 
            }

            // 3. UI를 눌렀을 경우
            foreach (var result in raycastResults)
            {
                Image hitImage = result.gameObject.GetComponent<Image>();
                
                // Raycast에 걸린 오브젝트가 Image 컴포넌트가 없는 경우 (예: Text, Mask) 다음 결과로 넘어감
                if (hitImage == null) continue;

                // 4. 눌린 Image가 허용된 리스트에 있는지 확인
                if (tapAllowedImages.Contains(hitImage))
                {
                    // 허용된 Image를 눌렀으므로 이동 허용
                    return true;
                }
                else if (hitImage.raycastTarget)
                {
                    // 허용되지 않은 Image(예: 버튼, 인벤토리)를 눌렀고, 
                    // 해당 Image가 RaycastTarget이 켜져있다면 (즉, 이 입력이 소모되어야 함)
                    // 이동을 차단합니다.
                    return false; 
                }
            }
            
            // 모든 Raycast 결과를 검사했는데도 허용된 이미지를 찾지 못했거나, 
            // Raycast Target이 꺼진 UI만 눌린 경우 (이동 허용의 안전장치)
            return true;
        }

        private void StartTapMovement(Vector2 screenPos)
        {
            StopTapMovement();

            Vector2 newDestinationAnchored;
            Vector3 newDestinationWorld;

            if (cachedRect != null)
            {
                if (parentCanvas == null || cachedRect.parent == null) return;

                Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
                RectTransform parentRect = cachedRect.parent as RectTransform;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 localPoint))
                {
                    // 1. 제약 조건 (경계) 적용
                    newDestinationAnchored = ApplyRectConstraints(localPoint);
                    
                    // 2. 최대 거리 제한 로직 적용
                    if (enableTapDistanceLimit)
                    {
                        Vector2 currentPos = cachedRect.anchoredPosition;
                        float distance = Vector2.Distance(currentPos, newDestinationAnchored);

                        if (distance > maxTapDistance)
                        {
                            Vector2 direction = (newDestinationAnchored - currentPos).normalized;
                            newDestinationAnchored = currentPos + direction * maxTapDistance;
                        }
                    }
                    
                    finalDestinationAnchored = newDestinationAnchored;
                    CalculateNextIntermediateTargetAnchored();
                    isMovingToTarget = true;
                    ResetStepMovementParameters();
                }
            }
            else // World Space Movement
            {
                if (mainCamera == null) return;

                float dist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, dist));

                // 1. 제약 조건 (경계) 적용
                newDestinationWorld = ApplyWorldConstraints(worldPoint);
                
                // 2. 최대 거리 제한 로직 적용
                if (enableTapDistanceLimit)
                {
                    Vector3 currentPos = transform.position;
                    float distance = Vector3.Distance(currentPos, newDestinationWorld);

                    if (distance > maxTapDistance)
                    {
                        Vector3 direction = (newDestinationWorld - currentPos).normalized;
                        newDestinationWorld = currentPos + direction * maxTapDistance;
                    }
                }
                
                finalDestinationWorld = newDestinationWorld;
                CalculateNextIntermediateTargetWorld();
                isMovingToTarget = true;
                ResetStepMovementParameters();
            }
        }

        private void ResetStepMovementParameters()
        {
            stepMovementStartTime = Time.time;
            if (cachedRect != null)
                stepStartAnchoredPosition = cachedRect.anchoredPosition;
            else
                stepStartWorldPosition = transform.position;
        }

        private void CalculateNextIntermediateTargetAnchored()
        {
            Vector2 currentPos = cachedRect.anchoredPosition;
            float distanceToFinal = Vector2.Distance(currentPos, finalDestinationAnchored);

            if (distanceToFinal <= maxStepDistance)
            {
                intermediateTargetAnchored = finalDestinationAnchored;
            }
            else
            {
                Vector2 dir = (finalDestinationAnchored - currentPos).normalized;
                intermediateTargetAnchored = currentPos + dir * maxStepDistance;
            }
        }

        private void CalculateNextIntermediateTargetWorld()
        {
            Vector3 currentPos = transform.position;
            float distanceToFinal = Vector3.Distance(currentPos, finalDestinationWorld);

            if (distanceToFinal <= maxStepDistance)
            {
                intermediateTargetWorld = finalDestinationWorld;
            }
            else
            {
                Vector3 dir = (finalDestinationWorld - currentPos).normalized;
                intermediateTargetWorld = currentPos + dir * maxStepDistance;
            }
        }

        #endregion

        #region Tap Movement Processing

        private void ProcessTapMovement()
        {
            if (isWaitingBetweenSteps)
            {
                if (Time.time - waitStartTime >= stepDelay)
                {
                    isWaitingBetweenSteps = false;
                    ResetStepMovementParameters();
                }
                else return;
            }

            if (cachedRect != null)
                MoveRectToTarget();
            else
                MoveTransformToTarget();
        }

        private void MoveRectToTarget()
        {
            Vector2 currentPos = cachedRect.anchoredPosition;
            Vector2 newPos;

            if (tapMoveMode == TapMovementMode.SpeedBased)
            {
                float step = tapMoveSpeedConstant * Time.deltaTime;
                newPos = Vector2.MoveTowards(currentPos, intermediateTargetAnchored, step);
            }
            else
            {
                float t = Mathf.Clamp01((Time.time - stepMovementStartTime) / tapMoveStepDuration);
                float curvedT = movementCurve.Evaluate(t);
                newPos = Vector2.Lerp(stepStartAnchoredPosition, intermediateTargetAnchored, curvedT);
            }

            UpdateFacingDirection(newPos.x - currentPos.x);
            cachedRect.anchoredPosition = newPos;

            if (Vector2.Distance(newPos, intermediateTargetAnchored) < 0.1f)
            {
                cachedRect.anchoredPosition = intermediateTargetAnchored;

                if (Vector2.Distance(intermediateTargetAnchored, finalDestinationAnchored) < 0.1f)
                {
                    isMovingToTarget = false;
                }
                else
                {
                    isWaitingBetweenSteps = true;
                    waitStartTime = Time.time;
                    CalculateNextIntermediateTargetAnchored();
                }
            }
        }

        private void MoveTransformToTarget()
        {
            Vector3 currentPos = transform.position;
            Vector3 newPos;

            if (tapMoveMode == TapMovementMode.SpeedBased)
            {
                newPos = Vector3.MoveTowards(currentPos, intermediateTargetWorld, tapMoveSpeedConstant * Time.deltaTime);
            }
            else
            {
                float t = Mathf.Clamp01((Time.time - stepMovementStartTime) / tapMoveStepDuration);
                float curvedT = movementCurve.Evaluate(t);
                newPos = Vector3.Lerp(stepStartWorldPosition, intermediateTargetWorld, curvedT);
            }

            UpdateFacingDirection(newPos.x - currentPos.x);
            transform.position = newPos;

            if (Vector3.Distance(newPos, intermediateTargetWorld) < 0.1f)
            {
                transform.position = intermediateTargetWorld;

                if (Vector3.Distance(intermediateTargetWorld, finalDestinationWorld) < 0.1f)
                {
                    isMovingToTarget = false;
                }
                else
                {
                    isWaitingBetweenSteps = true;
                    waitStartTime = Time.time;
                    CalculateNextIntermediateTargetWorld();
                }
            }
        }

        #endregion

        #region Manual Movement & Helpers

        private Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            if (joystick != null)
            {
                input = joystick.Direction;
                if (joystick.HasInput || input.sqrMagnitude > 0.0001f)
                    return input;
            }

#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
                input = gamepad.leftStick.ReadValue();

            if (input.sqrMagnitude < 0.0001f)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
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
            float delta = manualMoveSpeed * Time.deltaTime;

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

                targetPos = new Vector2(
                    Mathf.Clamp(targetPos.x, min.x, max.x),
                    Mathf.Clamp(targetPos.y, min.y, max.y)
                );
            }
            
            if (constrainToCamera && battleAreaManager != null)
            {
                Rect bounds = battleAreaManager.GetCameraBoundsInCanvasSpace();
                if (bounds.width > 0 && bounds.height > 0)
                {
                    Vector2 size = cachedRect.rect.size;
                    Vector2 pivot = cachedRect.pivot;
                    Vector2 min = bounds.min + Vector2.Scale(size, pivot);
                    Vector2 max = bounds.max - Vector2.Scale(size, Vector2.one - pivot);

                    targetPos = new Vector2(
                        Mathf.Clamp(targetPos.x, min.x, max.x),
                        Mathf.Clamp(targetPos.y, min.y, max.y)
                    );
                }
            }
            return targetPos;
        }

        private Vector3 ApplyWorldConstraints(Vector3 targetPos)
        {
            if (constrainToCamera && battleAreaManager != null)
            {
                Rect bounds = battleAreaManager.GetCameraBoundsInWorldSpace();
                if (bounds.width > 0 && bounds.height > 0)
                {
                    float playerSize = 0.5f;
                    Collider col = GetComponent<Collider>();

                    if (col != null)
                        playerSize = Mathf.Max(col.bounds.size.x, col.bounds.size.y) * 0.5f;

                    targetPos.x = Mathf.Clamp(targetPos.x, bounds.xMin + playerSize, bounds.xMax - playerSize);
                    targetPos.y = Mathf.Clamp(targetPos.y, bounds.yMin + playerSize, bounds.yMax - playerSize);
                }
            }
            return targetPos;
        }

        private void UpdateFacingDirection(float horizontalMovement)
        {
            if (Mathf.Abs(horizontalMovement) > 0.0001f)
            {
                Vector3 faceDir = horizontalMovement > 0 ? Vector3.right : Vector3.left;
                transform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
            }
        }

        #endregion
    }
}