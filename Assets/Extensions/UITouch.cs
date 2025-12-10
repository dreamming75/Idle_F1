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
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out localPoint
            );

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
