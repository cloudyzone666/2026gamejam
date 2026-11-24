using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Owns screen order and stable slots. Floating and perspective are confined to each card's presentation.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CardFormationLayout : MonoBehaviour
    {
        private sealed class Motion
        {
            public CardPresentation presentation;
            public float floating;
            public float hover;
            public bool initialized;
        }

        private readonly List<CharacterCardView> cards = new List<CharacterCardView>();
        private readonly Dictionary<CharacterCardView, Motion> motions = new Dictionary<CharacterCardView, Motion>();
        private readonly List<CharacterCardView> obsolete = new List<CharacterCardView>();
        [SerializeField] private CardFormationSettings settings;
        [SerializeField] private bool isEnemy;
        private RectTransform row;
        private RectTransform canvasRect;
        private readonly List<RaycastResult> hoverHits = new List<RaycastResult>();
        private PointerEventData hoverPointer;
        private EventSystem hoverEventSystem;

        public CardFormationSettings Settings => settings;
        public bool IsEnemy => isEnemy;
        public CharacterCardView HoveredCard { get; private set; }
        public CharacterCardView PinnedCard { get; private set; }
        public IReadOnlyList<CharacterCardView> Cards => cards;

        public void Configure(CardFormationSettings settings, bool isEnemy)
        {
            this.settings = settings;
            this.isEnemy = isEnemy;
            row = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            HorizontalLayoutGroup automaticLayout = GetComponent<HorizontalLayoutGroup>();
            if (automaticLayout != null) automaticLayout.enabled = false;
            row.localScale = Vector3.one;
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(.5f, .5f);
            ApplyLayout();
        }

        /// <param name="cardsInBattleOrder">Living cards in battle order: front to back.</param>
        public void SetCards(IReadOnlyList<CharacterCardView> cardsInBattleOrder)
        {
            cards.Clear();
            for (int i = 0; i < cardsInBattleOrder.Count; i++)
            {
                int index = IsEnemy ? i : cardsInBattleOrder.Count - 1 - i;
                CharacterCardView card = cardsInBattleOrder[index];
                if (card == null || !card.gameObject.activeSelf || (card.Unit != null && !card.Unit.IsAlive)) continue;
                card.SetFormation(this);
                cards.Add(card);
                if (!motions.ContainsKey(card))
                {
                    CardPresentation presentation = card.GetComponent<CardPresentation>();
                    if (presentation == null) presentation = card.gameObject.AddComponent<CardPresentation>();
                    motions.Add(card, new Motion { presentation = presentation });
                }
            }
            obsolete.Clear();
            foreach (var pair in motions) if (!cards.Contains(pair.Key)) obsolete.Add(pair.Key);
            foreach (CharacterCardView card in obsolete) motions.Remove(card);
            if (!cards.Contains(HoveredCard)) HoveredCard = null;
            if (!cards.Contains(PinnedCard)) PinnedCard = null;
            ApplyLayout();
        }

        public void SetHovered(CharacterCardView card, bool hovered)
        {
            if (hovered && cards.Contains(card))
            {
                if (HoveredCard != card && RetainsCurrentHover()) return;
                if (HoveredCard != card)
                {
                    if (HoveredCard != null) motions[HoveredCard].presentation.SetPointerState(false, HoveredCard.IsDragging);
                    HoveredCard = card;
                    motions[card].presentation.SetPointerState(true, card.IsDragging);
                }
            }
            else if (!hovered && HoveredCard == card && !RetainsCurrentHover())
            {
                if (motions.TryGetValue(card, out Motion motion)) motion.presentation.SetPointerState(false, card.IsDragging);
                HoveredCard = null;
            }
            ApplyStacking();
        }

        private bool RetainsCurrentHover()
        {
            return HoveredCard != null && HoveredCard.gameObject.activeInHierarchy && !HoveredCard.IsDragging
                && Mouse.current != null && Mouse.current.enabled
                && motions.TryGetValue(HoveredCard, out Motion motion)
                && motion.presentation.RetainsHover(Mouse.current.position.ReadValue());
        }

        private void UpdatePointerHover()
        {
            if (Mouse.current == null || !Mouse.current.enabled || EventSystem.current == null) return;
            if (RetainsCurrentHover()) return;
            if (HoveredCard != null)
            {
                if (motions.TryGetValue(HoveredCard, out Motion previous)) previous.presentation.SetPointerState(false, HoveredCard.IsDragging);
                HoveredCard = null;
            }
            if (hoverPointer == null || hoverEventSystem != EventSystem.current)
            {
                hoverEventSystem = EventSystem.current;
                hoverPointer = new PointerEventData(hoverEventSystem);
            }
            hoverPointer.position = Mouse.current.position.ReadValue();
            hoverHits.Clear();
            hoverEventSystem.RaycastAll(hoverPointer, hoverHits);
            if (hoverHits.Count == 0) return;
            CharacterCardView next = hoverHits[0].gameObject.GetComponentInParent<CharacterCardView>();
            if (next != null && cards.Contains(next) && !next.IsDragging) SetHovered(next, true);
        }

        public void Pin(CharacterCardView card)
        {
            if (cards.Contains(card))
            {
                PinnedCard = card;
                // An explicit click on an exposed neighbour ends hover hysteresis immediately.
                if (HoveredCard != null && HoveredCard != card && Mouse.current != null
                    && motions.TryGetValue(HoveredCard, out Motion oldMotion)
                    && !oldMotion.presentation.ContainsScreenPoint(Mouse.current.position.ReadValue()))
                {
                    oldMotion.presentation.SetPointerState(false, HoveredCard.IsDragging);
                    HoveredCard = card;
                    motions[card].presentation.SetPointerState(true, card.IsDragging);
                }
            }
            ApplyStacking();
        }

        public void ClearPin()
        {
            PinnedCard = null;
            ApplyStacking();
        }

        public Vector2 GetCardScreenPosition(CharacterCardView card)
        {
            int index = cards.IndexOf(card);
            if (index < 0 || Settings == null) return Vector2.zero;
            CardFormationSettings.SideSettings side = IsEnemy ? Settings.enemy : Settings.ally;
            float step = Mathf.Clamp(side.cardSize.x, 80f, 260f) * (1f - Mathf.Clamp(side.overlap, 0f, .8f));
            Vector3 slot = new Vector3((index - (cards.Count - 1) * .5f) * step, 0f, 0f);
            return RectTransformUtility.WorldToScreenPoint(null, transform.TransformPoint(slot));
        }

        private void LateUpdate() { UpdatePointerHover(); ApplyLayout(); }

        private void ApplyLayout()
        {
            if (Settings == null || row == null) return;
            CardFormationSettings.SideSettings side = IsEnemy ? Settings.enemy : Settings.ally;
            if (side == null) return;
            row.anchoredPosition = new Vector2(side.position.x, -side.position.y);
            float width = Mathf.Clamp(side.cardSize.x, 80f, 260f);
            float step = width * (1f - Mathf.Clamp(side.overlap, 0f, .8f));
            // Include the full face and readable exterior labels in the reorder drop area.
            row.sizeDelta = new Vector2(Mathf.Max(150f, step * Mathf.Max(0, cards.Count - 1) + width * Mathf.Max(side.innerScale, side.outerScale) + 64f),
                274f * Mathf.Max(side.innerScale, side.outerScale) + 80f);
            float blend = side.transitionDuration <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f / side.transitionDuration);
            Vector3 perspectiveWorld = canvasRect != null
                ? canvasRect.TransformPoint(new Vector3(canvasRect.rect.xMin + Settings.perspectiveCenter.x,
                    canvasRect.rect.yMax - Settings.perspectiveCenter.y, 0f))
                : row.TransformPoint(new Vector3(Settings.perspectiveCenter.x - side.position.x, side.position.y - Settings.perspectiveCenter.y, 0f));

            for (int i = 0; i < cards.Count; i++)
            {
                CharacterCardView card = cards[i];
                if (card == null) continue;
                Motion motion = motions[card];
                RectTransform rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                if (!Mathf.Approximately(rect.rect.width, width))
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    card.RefreshPresentationWidth();
                }
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 274f);
                Vector2 targetPosition = new Vector2((i - (cards.Count - 1) * .5f) * step, 0f);
                float orderFraction = cards.Count > 1 ? (float)i / (cards.Count - 1) : .5f;
                float outward = IsEnemy ? orderFraction : 1f - orderFraction;
                float scale = Mathf.Lerp(side.innerScale, side.outerScale, Mathf.Pow(outward, side.scaleExponent));
                rect.anchoredPosition = motion.initialized ? Vector2.Lerp(rect.anchoredPosition, targetPosition, blend) : targetPosition;
                rect.localScale = Vector3.one * (motion.initialized ? Mathf.Lerp(rect.localScale.x, scale, blend) : scale);
                bool hovered = card == HoveredCard;
                float targetFloat = 0f;
                if (side.floatEnabled && !card.IsDragging)
                {
                    // Freezing the current offset prevents an entering edge moving away from the pointer.
                    // The editor's pause switch also holds the visible pose for reliable alignment.
                    targetFloat = Settings.pauseFloat || hovered ? motion.floating
                        : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(.2f, side.floatPeriod)
                            + i * side.floatPhase) * side.floatAmplitude;
                }
                motion.floating = Mathf.Lerp(motion.floating, targetFloat, blend);
                motion.hover = Mathf.Lerp(motion.hover, hovered && !card.IsDragging ? 1f : 0f, blend);
                float yaw = IsEnemy ? -side.yaw : side.yaw;
                Vector3 centerLocal = rect.InverseTransformPoint(perspectiveWorld);
                motion.presentation.Configure(yaw, Settings.perspectiveDistance, new Vector2(centerLocal.x, centerLocal.y),
                    motion.floating, side.hoverLift * motion.hover, Mathf.Lerp(1f, side.hoverScale, motion.hover));
                motion.initialized = true;
            }
            ApplyStacking();
        }

        private void ApplyStacking()
        {
            // Bottom to top: even display numbers, odd display numbers, pinned, hovered.
            int sibling = 0;
            for (int parity = 1; parity >= 0; parity--)
                for (int i = parity; i < cards.Count; i += 2)
                {
                    CharacterCardView card = cards[i];
                    if (card == null || card == PinnedCard || card == HoveredCard) continue;
                    card.transform.SetSiblingIndex(sibling++);
                }
            if (PinnedCard != null && PinnedCard != HoveredCard) PinnedCard.transform.SetSiblingIndex(sibling++);
            if (HoveredCard != null) HoveredCard.transform.SetSiblingIndex(sibling);
        }
    }
}
