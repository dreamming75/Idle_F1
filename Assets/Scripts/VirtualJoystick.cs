using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleF1.Combat
{
    /// <summary>
    /// Simple on-screen joystick for mobile/touch. Emits a normalized direction.
    /// </summary>
    [DisallowMultipleComponent]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField]
        private RectTransform background;

        [SerializeField]
        private RectTransform handle;

        [SerializeField]
        [Tooltip("Pixels from center to edge that represent full deflection.")]
        private float radius = 90f;

        [SerializeField]
        [Tooltip("Ignore tiny movements to prevent drift.")]
        private float deadZone = 0.05f;

        public Vector2 Direction { get; private set; }
        public bool HasInput { get; private set; }

        private Vector2 center;

        private void Start()
        {
            if (background == null)
            {
                background = transform as RectTransform;
            }

            if (background != null)
            {
                center = background.sizeDelta * 0.5f;
                if (radius <= 0f)
                {
                    radius = Mathf.Min(background.sizeDelta.x, background.sizeDelta.y) * 0.5f;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
            HasInput = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint);

            Vector2 delta = localPoint - center;
            Vector2 clamped = Vector2.ClampMagnitude(delta, radius);
            Vector2 rawDir = clamped / radius;

            float magnitude = rawDir.magnitude;
            Direction = magnitude < deadZone ? Vector2.zero : rawDir.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);

            if (handle != null)
            {
                handle.anchoredPosition = clamped;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            HasInput = false;
            Direction = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}

