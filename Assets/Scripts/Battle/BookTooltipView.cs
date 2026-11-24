using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Nonblocking inspection. Hover is temporary; click pins until an outside click or Escape.</summary>
    public sealed class BookTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        private Object source;
        private bool pinned;
        private int openedFrame;
        private Vector2 lastPosition;

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;
        public bool IsPinned => IsVisible && pinned;
        public Object Source => source;
        public string Title => titleText == null ? "" : titleText.text;

        public void Initialize()
        {
            if (panel == null) return;
            foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Close();
        }

        public void Show(string title, string body, Object owner, Vector2 position, bool pin)
        {
            if (panel == null || titleText == null || bodyText == null) return;
            source = owner;
            pinned = pin;
            openedFrame = Time.frameCount;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            lastPosition = position;
            RefreshContent(title, body);
        }

        public void RefreshContent(string title, string body)
        {
            if (panel == null || titleText == null || bodyText == null) return;
            titleText.text = title;
            bodyText.text = body;
            float height = Mathf.Clamp(bodyText.preferredHeight + 56f, 120f, 430f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Position(lastPosition);
        }

        public void HidePreview(Object owner)
        {
            if (!pinned && ReferenceEquals(source, owner)) Close();
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            source = null;
            pinned = false;
        }

        private void Update()
        {
            if (!IsVisible) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            if (!pinned) { lastPosition = position; Position(position); }
            if (Time.frameCount > openedFrame && mouse.leftButton.wasPressedThisFrame
                && !RectTransformUtility.RectangleContainsScreenPoint(panel, position, null)) Close();
        }

        private void Position(Vector2 screenPosition)
        {
            RectTransform parent = panel.parent as RectTransform;
            if (parent == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, null, out local)) return;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);
            local += new Vector2(18f, -18f);
            Rect bounds = parent.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + 8f, bounds.xMax - panel.rect.width - 8f);
            local.y = Mathf.Clamp(local.y, bounds.yMin + panel.rect.height + 8f, bounds.yMax - 8f);
            panel.localPosition = new Vector3(local.x, local.y, 0f);
        }

        private void OnDisable() { Close(); }
    }
}
