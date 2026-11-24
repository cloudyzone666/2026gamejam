using UnityEngine;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Small uGUI helpers shared by the inventory views. No battle rules live here.</summary>
    internal static class BattleUiFactory
    {
        public static readonly Color Gold = new Color(0.89f, 0.72f, 0.36f, 1f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text Text(string name, Transform parent, Font font, int size)
        {
            RectTransform rect = Rect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.91f, 0.91f, 0.88f, 1f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static Color EquipmentColor(EquipmentSlotKind kind)
        {
            switch (kind)
            {
                case EquipmentSlotKind.Handheld: return new Color(0.19f, 0.38f, 0.57f, 1f);
                case EquipmentSlotKind.Worn: return new Color(0.20f, 0.42f, 0.33f, 1f);
                default: return new Color(0.55f, 0.43f, 0.20f, 1f);
            }
        }
    }
}
