using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>One stable view per unit. Dragging commits a position change only on release.</summary>
    public sealed class CharacterCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image borderImage;
        [SerializeField] private Button cardButton;
        [SerializeField] private Text nameText;
        [SerializeField] private Text mirroredNameText;
        [SerializeField] private Text orderText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text armorText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text intentText;
        [SerializeField] private Text distanceText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image armorTrack;
        [SerializeField] private Image armorFill;
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Text topProfessionText;
        [SerializeField] private Text bottomProfessionText;

        private BattleUnitState unit;
        private UnityAction<BattleUnitState> onClicked;
        private BattleView view;
        private RectTransform dragGhost;
        private CanvasGroup sourceGroup;
        private Coroutine rotationRoutine;
        private Coroutine hitRoutine;
        private Text damageNumber;
        private Color baseColor;
        private int lastHealth;
        private int lastArmor;
        private int lastMaxHealth;
        private int lastMaxArmor;
        private bool lastBack;
        private bool lastPoseRotated;
        private bool valuesInitialized;
        private bool poseInitialized;
        private bool lastRotated;
        private float suppressClickUntil;

        public BattleUnitState Unit => unit;
        public bool IsDragging => dragGhost != null;

        public void Initialize(BattleView battleView) { view = battleView; }

        public void Bind(BattleUnitState battleUnit, UnityAction<BattleUnitState> clickCallback, Font font)
        {
            ClearBinding();
            unit = battleUnit;
            onClicked = clickCallback;
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = font;
            cardButton.onClick.AddListener(HandleClicked);
        }

        public void Refresh(bool isSelected, bool isTarget, bool canClick,
            bool isActiveEnemy, int order, int distance)
        {
            if (unit == null) return;
            cardButton.interactable = canClick && unit.IsAlive;
            baseColor = unit.IsEnemy
                ? new Color(0.30f, 0.15f, 0.20f, 1f) : unit.IsBack ? new Color(0.28f, 0.18f, 0.37f, 1f) : new Color(0.13f, 0.23f, 0.34f, 1f);
            if (hitRoutine == null) cardBackground.color = baseColor;
            UpdateDamageFeedback();
            UpdateVisualWidth();
            UpdatePortrait();
            borderImage.enabled = isSelected || isTarget || isActiveEnemy;
            borderImage.color = isActiveEnemy ? new Color(1f, 0.40f, 0.32f, 1f) : BattleUiFactory.Gold;
            nameText.text = unit.Name;
            if (mirroredNameText != null) mirroredNameText.text = unit.Name;
            SetActive(mirroredNameText, !unit.IsEnemy);
            orderText.text = order == 1 ? "前排 1" : $"位次 {order}";
            SetActive(healthText, unit.IsEnemy);
            SetActive(healthFill, unit.IsEnemy);
            UpdateShieldBar();
            if (visualRoot != null)
            {
                string[] decorations = { "PortraitFrame", "Blade", "Hilt" };
                foreach (string decorationName in decorations)
                {
                    Transform decoration = visualRoot.Find(decorationName);
                    if (decoration != null) decoration.gameObject.SetActive(!unit.IsEnemy && unit.Portrait == null);
                }
            }
            if (healthFill != null && healthFill.transform.parent != null)
            {
                Transform track = healthFill.transform.parent.Find("HealthTrack");
                if (track != null) track.gameObject.SetActive(unit.IsEnemy);
            }
            if (healthText != null) healthText.text = $"生命 {unit.CurrentHealth} / {unit.MaxHealth}";
            if (healthFill != null)
            {
                healthFill.fillAmount = unit.MaxHealth > 0 ? Mathf.Clamp01((float)unit.CurrentHealth / unit.MaxHealth) : 0f;
                healthFill.color = new Color(0.85f, 0.29f, 0.33f, 1f);
            }
            SetActive(intentText, unit.IsEnemy);
            SetActive(statusText, !unit.IsEnemy);
            if (unit.IsEnemy)
            {
                intentText.text = (isActiveEnemy ? "行动中 · " : "") + $"意图：攻击 {unit.NextIntentDamage}";
                distanceText.text = distance > 0 ? $"距离 {distance} · {(view.Controller.CanReach(view.Controller.SelectedAlly, unit) ? "射程内" : "超出射程")}" : "";
                SetActive(topProfessionText, false);
                SetActive(bottomProfessionText, false);
            }
            else
            {
                statusText.rectTransform.anchoredPosition = intentText.rectTransform.anchoredPosition;
                statusText.text = unit.HasUsedStandby ? "本回合收手" : unit.HasChangedFormThisTurn ? "本回合已变换" : "";
                distanceText.text = $"攻击距离 {unit.AttackRange}";
                SetActive(topProfessionText, true);
                SetActive(bottomProfessionText, true);
                if (topProfessionText != null) topProfessionText.text = unit.IsBack ? unit.BackForm.name : unit.FrontTop.name;
                if (bottomProfessionText != null) bottomProfessionText.text = unit.IsBack ? unit.BackForm.name : unit.FrontBottom.name;
                UpdatePose();
            }
        }

        private void UpdateShieldBar()
        {
            // ShieldBar is a sibling of visualRoot, so the card's 180-degree turn
            // never turns its frame, left-to-right fill, or readable value.
            SetActive(armorTrack, true);
            SetActive(armorFill, true);
            SetActive(armorText, true);
            if (armorText != null) armorText.text = $"护盾 {unit.CurrentArmor} / {unit.MaxArmor}";
            if (armorFill == null) return;
            armorFill.type = Image.Type.Filled;
            armorFill.fillMethod = Image.FillMethod.Horizontal;
            armorFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            armorFill.fillAmount = unit.MaxArmor > 0 ? Mathf.Clamp01((float)unit.CurrentArmor / unit.MaxArmor) : 0f;
            armorFill.color = new Color(0.30f, 0.64f, 0.91f, 1f);
            armorFill.raycastTarget = false;
        }

        private void UpdatePortrait()
        {
            if (portraitImage == null) return;
            bool hasPortrait = unit.Portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            portraitImage.sprite = unit.Portrait;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            portraitImage.color = unit.IsBack ? new Color(0.78f, 0.66f, 0.92f, 1f) : Color.white;
            RectTransform portraitRect = portraitImage.rectTransform;
            portraitRect.anchoredPosition = unit.IsEnemy ? new Vector2(0f, 15f) : Vector2.zero;
            portraitRect.sizeDelta = unit.IsEnemy ? new Vector2(124f, 112f) : new Vector2(124f, 204f);
            // The two labels on each end remain readable over the full character illustration.
            if (visualRoot != null)
            {
                Transform upperBacking = visualRoot.Find("UpperTextBacking");
                Transform lowerBacking = visualRoot.Find("LowerTextBacking");
                if (upperBacking != null) upperBacking.gameObject.SetActive(hasPortrait && !unit.IsEnemy);
                if (lowerBacking != null) lowerBacking.gameObject.SetActive(hasPortrait && !unit.IsEnemy);
            }
            if (!unit.IsEnemy) return;

            // Keep enemy statistics below the wide monster illustration.
            if (nameText != null) nameText.rectTransform.anchoredPosition = new Vector2(0f, hasPortrait ? 83f : 53f);
            if (healthText != null) healthText.rectTransform.anchoredPosition = new Vector2(0f, hasPortrait ? -57f : 16f);
            if (healthFill != null)
            {
                Vector2 barPosition = new Vector2(0f, hasPortrait ? -96f : -42f);
                healthFill.rectTransform.anchoredPosition = barPosition;
                Transform track = healthFill.transform.parent.Find("HealthTrack");
                if (track is RectTransform trackRect) trackRect.anchoredPosition = barPosition;
            }
        }

        private void UpdateVisualWidth()
        {
            if (visualRoot == null) return;
            float width = ((RectTransform)transform).rect.width;
            visualRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            cardBackground.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 2f);
            borderImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + 4f);
            Text[] cardTexts = { nameText, mirroredNameText, topProfessionText, bottomProfessionText, healthText, armorText };
            foreach (Text text in cardTexts)
                if (text != null) text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 8f);
            if (armorTrack != null)
            {
                RectTransform shieldBar = armorTrack.rectTransform.parent as RectTransform;
                if (shieldBar != null && shieldBar.parent == transform)
                    shieldBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 8f);
            }
        }

        private void UpdateDamageFeedback()
        {
            bool sameLimitsAndPose = valuesInitialized && lastMaxHealth == unit.MaxHealth && lastMaxArmor == unit.MaxArmor
                && lastBack == unit.IsBack && lastPoseRotated == unit.IsRotated;
            int loss = sameLimitsAndPose ? Mathf.Max(0, lastHealth - unit.CurrentHealth) + Mathf.Max(0, lastArmor - unit.CurrentArmor) : 0;
            lastHealth = unit.CurrentHealth;
            lastArmor = unit.CurrentArmor;
            lastMaxHealth = unit.MaxHealth;
            lastMaxArmor = unit.MaxArmor;
            lastBack = unit.IsBack;
            lastPoseRotated = unit.IsRotated;
            valuesInitialized = true;
            if (loss <= 0 || !isActiveAndEnabled) return;
            StopHitFeedback();
            hitRoutine = StartCoroutine(ShowHitFeedback(loss));
        }

        private IEnumerator ShowHitFeedback(int damage)
        {
            damageNumber = BattleUiFactory.Text("DamageNumber", transform, BattleView.GetInterfaceFont(), 25);
            RectTransform rect = damageNumber.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 40f);
            damageNumber.text = "-" + damage;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.unscaledDeltaTime;
                rect.anchoredPosition = new Vector2(0f, 28f + elapsed * 90f);
                damageNumber.color = new Color(1f, 0.34f, 0.32f, Mathf.Clamp01((0.45f - elapsed) / 0.18f));
                cardBackground.color = elapsed < 0.15f ? new Color(0.85f, 0.15f, 0.18f, 1f) : baseColor;
                yield return null;
            }
            StopHitFeedback();
        }

        private void StopHitFeedback()
        {
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = null;
            if (damageNumber != null) Destroy(damageNumber.gameObject);
            damageNumber = null;
            if (cardBackground != null) cardBackground.color = baseColor;
        }

        private void UpdatePose()
        {
            if (visualRoot == null) return;
            bool rotated = unit.IsRotated;
            float targetAngle = rotated ? 180f : 0f;
            if (!poseInitialized)
            {
                visualRoot.localEulerAngles = new Vector3(0f, 0f, targetAngle);
                poseInitialized = true;
            }
            else if (lastRotated != rotated && isActiveAndEnabled)
            {
                if (rotationRoutine != null) StopCoroutine(rotationRoutine);
                rotationRoutine = StartCoroutine(RotateVisual(targetAngle));
            }
            lastRotated = rotated;
        }

        private IEnumerator RotateVisual(float targetAngle)
        {
            float initial = visualRoot.localEulerAngles.z;
            float elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.22f));
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(initial, targetAngle, t));
                yield return null;
            }
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            rotationRoutine = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || view == null || unit == null || unit.IsEnemy
                || !view.CanReorderCards || !unit.IsAlive || view.IsDraggingEquipment) return;
            GameObject ghostObject = Instantiate(gameObject, view.DragLayer, false);
            CharacterCardView ghostView = ghostObject.GetComponent<CharacterCardView>();
            ghostView.ClearBinding();
            ghostView.enabled = false;
            dragGhost = (RectTransform)ghostObject.transform;
            dragGhost.sizeDelta = ((RectTransform)transform).rect.size;
            CanvasGroup group = ghostObject.GetComponent<CanvasGroup>();
            if (group == null) group = ghostObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.80f;
            if (sourceGroup == null) sourceGroup = gameObject.GetComponent<CanvasGroup>();
            if (sourceGroup == null) sourceGroup = gameObject.AddComponent<CanvasGroup>();
            sourceGroup.alpha = 0.4f;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(view.DragLayer,
                eventData.position, eventData.pressEventCamera, out local)) dragGhost.localPosition = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            BattleUnitState draggedUnit = unit;
            CancelDrag();
            suppressClickUntil = Time.unscaledTime + 0.15f;
            view.CommitCardReorder(draggedUnit, eventData.position);
        }

        private void CancelDrag()
        {
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            dragGhost = null;
            if (sourceGroup != null) sourceGroup.alpha = 1f;
        }

        public void ClearBinding()
        {
            CancelDrag();
            if (rotationRoutine != null) StopCoroutine(rotationRoutine);
            rotationRoutine = null;
            poseInitialized = false;
            valuesInitialized = false;
            StopHitFeedback();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleClicked);
                cardButton.interactable = false;
            }
            unit = null;
            onClicked = null;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        private void HandleClicked()
        {
            if (Time.unscaledTime < suppressClickUntil) return;
            if (unit != null && unit.IsAlive && cardButton.interactable) onClicked?.Invoke(unit);
        }

        private void OnDisable() { CancelDrag(); StopHitFeedback(); }
        private void OnDestroy() { ClearBinding(); }
    }
}
