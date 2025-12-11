using UnityEngine;
using UnityEngine.UI;

namespace IdleF1.Combat
{
    /// <summary>
    /// 전투 영역과 UI 영역을 분리하여 관리하는 매니저
    /// </summary>
    public class BattleAreaManager : MonoBehaviour
    {
        [Header("전투 영역 설정")]
        [SerializeField]
        private RectTransform battleAreaRect;
        
        [SerializeField]
        private Camera battleCamera;
        
        [SerializeField]
        private Canvas battleCanvas;
        
        [Header("UI 영역 설정")]
        [SerializeField]
        private Canvas uiCanvas;
        
        [Header("레이어 설정")]
        [SerializeField]
        private int battleLayer = 0;
        
        [SerializeField]
        private int uiLayer = 5; // UI 레이어
        
        [Header("Viewport 분할 (선택사항)")]
        [SerializeField]
        private bool useViewportSplit = false;
        
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("전투 영역이 차지할 화면 비율 (0.7 = 70%)")]
        private float battleAreaRatio = 0.7f;
        
        private void Awake()
        {
            InitializeBattleArea();
            InitializeUIArea();
        }
        
        private void Start()
        {
            // Start에서도 한 번 더 확인 (다른 스크립트가 Canvas를 변경했을 수 있음)
            if (battleCanvas != null && battleCamera != null)
            {
                if (battleCanvas.renderMode != RenderMode.ScreenSpaceCamera || battleCanvas.worldCamera != battleCamera)
                {
                    battleCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    battleCanvas.worldCamera = battleCamera;
                }
            }
        }
        
        private void InitializeBattleArea()
        {
            // 전투 영역 Canvas 설정
            if (battleCanvas != null)
            {
                // Canvas 활성화 확인
                if (!battleCanvas.gameObject.activeInHierarchy)
                {
                    battleCanvas.gameObject.SetActive(true);
                }
                
                // 카메라가 없으면 경고
                if (battleCamera == null)
                {
                    Debug.LogWarning("BattleAreaManager: Battle Camera가 설정되지 않았습니다!");
                    return;
                }
                
                // 카메라 활성화 확인
                if (!battleCamera.gameObject.activeInHierarchy)
                {
                    battleCamera.gameObject.SetActive(true);
                }
                
                // Canvas 설정
                battleCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                battleCanvas.worldCamera = battleCamera;
                battleCanvas.planeDistance = Mathf.Clamp(battleCanvas.planeDistance, battleCamera.nearClipPlane + 0.1f, battleCamera.farClipPlane - 0.1f);
                battleCanvas.sortingOrder = 0; // UI보다 낮은 순서
                
                // Canvas RectTransform 설정 (크기가 0이면 전체 화면으로 설정)
                RectTransform canvasRect = battleCanvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    // Scale이 0이면 1로 설정
                    if (canvasRect.localScale == Vector3.zero)
                    {
                        canvasRect.localScale = Vector3.one;
                    }
                    
                    // Anchor를 Stretch-Stretch로 설정하여 전체 화면 차지
                    canvasRect.anchorMin = Vector2.zero;
                    canvasRect.anchorMax = Vector2.one;
                    canvasRect.anchoredPosition = Vector2.zero;
                    canvasRect.sizeDelta = Vector2.zero;
                    canvasRect.pivot = new Vector2(0.5f, 0.5f);
                }
                
                // 전투 영역의 모든 자식 오브젝트를 전투 레이어로 설정
                SetLayerRecursive(battleCanvas.transform, battleLayer);
            }
            else
            {
                Debug.LogWarning("BattleAreaManager: Battle Canvas가 설정되지 않았습니다!");
            }
            
            // 전투 카메라 설정
            if (battleCamera != null && useViewportSplit)
            {
                Rect viewportRect = battleCamera.rect;
                viewportRect.width = battleAreaRatio;
                viewportRect.x = 0f;
                battleCamera.rect = viewportRect;
            }
            else if (battleCamera != null)
            {
                // Viewport Split이 비활성화되어 있으면 전체 화면으로 설정
                battleCamera.rect = new Rect(0, 0, 1, 1);
            }
        }
        
        private void InitializeUIArea()
        {
            // UI 영역 Canvas 설정
            if (uiCanvas != null)
            {
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 100; // 전투 영역보다 높은 순서
                
                // UI 영역의 모든 자식 오브젝트를 UI 레이어로 설정
                SetLayerRecursive(uiCanvas.transform, uiLayer);
            }
        }
        
        private void SetLayerRecursive(Transform parent, int layer)
        {
            parent.gameObject.layer = layer;
            foreach (Transform child in parent)
            {
                SetLayerRecursive(child, layer);
            }
        }
        
        /// <summary>
        /// 전투 영역의 크기와 위치를 설정
        /// </summary>
        public void SetBattleAreaBounds(Rect bounds)
        {
            if (battleAreaRect != null)
            {
                battleAreaRect.anchoredPosition = bounds.position;
                battleAreaRect.sizeDelta = bounds.size;
            }
        }
        
        /// <summary>
        /// Viewport 분할 모드 토글
        /// </summary>
        public void SetViewportSplit(bool enabled, float ratio = 0.7f)
        {
            useViewportSplit = enabled;
            battleAreaRatio = Mathf.Clamp01(ratio);
            
            if (battleCamera != null && useViewportSplit)
            {
                Rect viewportRect = battleCamera.rect;
                viewportRect.width = battleAreaRatio;
                viewportRect.x = 0f;
                battleCamera.rect = viewportRect;
            }
            else if (battleCamera != null)
            {
                battleCamera.rect = new Rect(0, 0, 1, 1);
            }
        }
        
        /// <summary>
        /// 전투 영역에 오브젝트 추가
        /// </summary>
        public void AddToBattleArea(Transform obj)
        {
            if (battleAreaRect != null)
            {
                obj.SetParent(battleAreaRect, false);
                SetLayerRecursive(obj, battleLayer);
            }
        }
        
        /// <summary>
        /// UI 영역에 오브젝트 추가
        /// </summary>
        public void AddToUIArea(Transform obj)
        {
            if (uiCanvas != null)
            {
                obj.SetParent(uiCanvas.transform, false);
                SetLayerRecursive(obj, uiLayer);
            }
        }
        
        /// <summary>
        /// 카메라의 시야 범위를 RectTransform 좌표계로 반환
        /// </summary>
        public Rect GetCameraBoundsInCanvasSpace()
        {
            if (battleCamera == null || battleCanvas == null)
            {
                return new Rect(0, 0, 0, 0);
            }
            
            RectTransform canvasRect = battleCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return new Rect(0, 0, 0, 0);
            }
            
            // 카메라의 시야 범위 계산 (World 단위)
            float cameraHeight, cameraWidth;
            
            if (battleCamera.orthographic)
            {
                cameraHeight = battleCamera.orthographicSize * 2f;
                cameraWidth = cameraHeight * battleCamera.aspect;
            }
            else
            {
                // Perspective 카메라의 경우
                float distance = battleCanvas.planeDistance;
                cameraHeight = 2f * distance * Mathf.Tan(battleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cameraWidth = cameraHeight * battleCamera.aspect;
            }
            
            // Canvas의 스케일 팩터를 사용하여 World 단위를 Canvas 픽셀 단위로 변환
            float scaleFactor = battleCanvas.scaleFactor;
            
            // Canvas의 실제 크기 가져오기
            Vector2 canvasSize = canvasRect.sizeDelta;
            if (canvasSize.x == 0 || canvasSize.y == 0)
            {
                // Canvas가 Stretch 모드인 경우
                CanvasScaler scaler = battleCanvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    canvasSize = scaler.referenceResolution;
                }
                else
                {
                    canvasSize = new Vector2(Screen.width / scaleFactor, Screen.height / scaleFactor);
                }
            }
            
            // 카메라 범위를 Canvas 픽셀 단위로 변환
            // World 단위를 Canvas 픽셀 단위로 변환하는 비율 계산
            float worldToCanvasRatio = canvasSize.y / cameraHeight;
            float canvasWidth = cameraWidth * worldToCanvasRatio;
            float canvasHeight = canvasSize.y; // 카메라 높이 = Canvas 높이
            
            // Canvas의 중심을 기준으로 범위 계산
            Rect bounds = new Rect(
                -canvasWidth * 0.5f,
                -canvasHeight * 0.5f,
                canvasWidth,
                canvasHeight
            );
            
            return bounds;
        }
        
        /// <summary>
        /// World 좌표계에서 카메라의 시야 범위 반환
        /// </summary>
        public Rect GetCameraBoundsInWorldSpace()
        {
            if (battleCamera == null)
            {
                return new Rect(0, 0, 0, 0);
            }
            
            float cameraHeight, cameraWidth;
            
            if (battleCamera.orthographic)
            {
                cameraHeight = battleCamera.orthographicSize * 2f;
                cameraWidth = cameraHeight * battleCamera.aspect;
            }
            else
            {
                float distance = battleCamera.transform.position.z;
                cameraHeight = 2f * distance * Mathf.Tan(battleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cameraWidth = cameraHeight * battleCamera.aspect;
            }
            
            Vector3 cameraPos = battleCamera.transform.position;
            Rect bounds = new Rect(
                cameraPos.x - cameraWidth * 0.5f,
                cameraPos.y - cameraHeight * 0.5f,
                cameraWidth,
                cameraHeight
            );
            
            return bounds;
        }
    }
}

