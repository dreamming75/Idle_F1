using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UITouch : MonoBehaviour
{
    public RectTransform prefab;
    public Canvas canvas;
    public int maxCount = 5;

    private Queue<RectTransform> spawned = new Queue<RectTransform>();

    void Update()
    {
        // 1. 마우스/터치 입력 체크
        if (Input.GetMouseButtonDown(0))
        {
            // 2. 항상 프리팹 생성 (버튼 위든 어디든 무조건)
            Vector2 localPoint;
            
            // Canvas의 RenderMode에 따라 카메라 처리
            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                cam = canvas.worldCamera;
            }
            // Overlay 모드일 때는 cam이 null이어야 함
            
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                cam,
                out localPoint
            );

            // Overlay 모드일 경우 직접 계산 (RectTransformUtility가 제대로 작동하지 않을 수 있음)
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransform canvasRect = canvas.transform as RectTransform;
                Vector2 screenPoint = Input.mousePosition;
                
                // CanvasScaler가 있으면 스케일 고려
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                float scaleFactor = 1f;
                
                if (scaler != null)
                {
                    // CanvasScaler의 Match 설정에 따라 스케일 계산
                    float matchWidthOrHeight = scaler.matchWidthOrHeight;
                    Vector2 referenceResolution = scaler.referenceResolution;
                    
                    float scaleX = Screen.width / referenceResolution.x;
                    float scaleY = Screen.height / referenceResolution.y;
                    scaleFactor = Mathf.Lerp(scaleX, scaleY, matchWidthOrHeight);
                }
                else
                {
                    // CanvasScaler가 없으면 Canvas의 스케일 팩터 사용
                    scaleFactor = canvas.scaleFactor;
                }
                
                // 화면 좌표를 Canvas 로컬 좌표로 변환
                localPoint = new Vector2(
                    (screenPoint.x - Screen.width * 0.5f) / scaleFactor,
                    (screenPoint.y - Screen.height * 0.5f) / scaleFactor
                );
            }
            // 좌표 변환이 실패한 경우 (다른 모드)
            else if (!success)
            {
                // 기본값 사용 (화면 중앙)
                localPoint = Vector2.zero;
                Debug.LogWarning("UITouch: 좌표 변환 실패. Canvas RenderMode를 확인하세요.");
            }

            var obj = Instantiate(prefab, canvas.transform);
            obj.anchoredPosition = localPoint;
            obj.SetAsLastSibling(); // 최상단

            // 3. 반드시 raycastTarget 꺼주기 (프리팹 하위 전체!)
            foreach (var img in obj.GetComponentsInChildren<Graphic>())
                img.raycastTarget = false;

            spawned.Enqueue(obj);
            if (spawned.Count > maxCount)
            {
                var oldObj = spawned.Dequeue();
                if (oldObj != null)
                    Destroy(oldObj.gameObject);
            }
        }
    }
}
