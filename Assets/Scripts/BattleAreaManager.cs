using UnityEngine;

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
        
        private void InitializeBattleArea()
        {
            // 전투 영역 Canvas 설정
            if (battleCanvas != null)
            {
                battleCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                battleCanvas.worldCamera = battleCamera;
                battleCanvas.sortingOrder = 0; // UI보다 낮은 순서
                
                // 전투 영역의 모든 자식 오브젝트를 전투 레이어로 설정
                SetLayerRecursive(battleCanvas.transform, battleLayer);
            }
            
            // 전투 카메라 설정
            if (battleCamera != null && useViewportSplit)
            {
                Rect viewportRect = battleCamera.rect;
                viewportRect.width = battleAreaRatio;
                viewportRect.x = 0f;
                battleCamera.rect = viewportRect;
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
    }
}

