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

        [SerializeField]
        [Tooltip("How far (in pixels) the handle must be dragged from center before visuals appear.")]
        private float showDistance = 8f;

        public Vector2 Direction { get; private set; }
        public bool HasInput { get; private set; }

        private Vector2 center;
        private Graphic backgroundGraphic;
        private Graphic handleGraphic;
        private bool isVisible;

        private void Start()
        {
            if (background == null)
            {
                background = transform as RectTransform;
            }

            backgroundGraphic = background != null ? background.GetComponent<Graphic>() : null;
            handleGraphic = handle != null ? handle.GetComponent<Graphic>() : null;

            if (background != null)
            {
                center = background.rect.center;
                if (radius <= 0f)
                {
                    radius = Mathf.Min(background.sizeDelta.x, background.sizeDelta.y) * 0.5f;
                }
                showDistance = Mathf.Clamp(showDistance, 0f, radius);
            }

            SetVisible(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (background != null && background.parent is RectTransform parent)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var parentLocal))
                {
                    background.anchoredPosition = parentLocal;
                }

                center = background.rect.center;
                if (handle != null)
                {
                    handle.anchoredPosition = Vector2.zero;
                }
            }

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

            if (!isVisible && clamped.magnitude >= showDistance)
            {
                SetVisible(true);
            }

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

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (isVisible == visible) return;
            isVisible = visible;

            if (backgroundGraphic != null)
            {
                Color c = backgroundGraphic.color;
                c.a = visible ? 1f : 0f;
                backgroundGraphic.color = c;
                backgroundGraphic.raycastTarget = true; // keep capturing touches
            }

            if (handleGraphic != null)
            {
                Color c = handleGraphic.color;
                c.a = visible ? 1f : 0f;
                handleGraphic.color = c;
            }
        }
    }
}

