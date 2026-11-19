using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>A reusable view for one ally or enemy. It never changes battle state.</summary>
    public sealed class CharacterCardView : MonoBehaviour
    {
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image borderImage;
        [SerializeField] private Button cardButton;
        [SerializeField] private Text nameText;
        [SerializeField] private Text orderText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text armorText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text intentText;
        [SerializeField] private Text distanceText;
        [SerializeField] private Image healthFill;

        private BattleUnitState unit;
        private UnityAction<BattleUnitState> onClicked;

        public BattleUnitState Unit => unit;

        public void Bind(BattleUnitState battleUnit, UnityAction<BattleUnitState> clickCallback, Font font)
        {
            ClearBinding();
            unit = battleUnit;
            onClicked = clickCallback;
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }

            cardButton.onClick.AddListener(HandleClicked);
        }

        public void Refresh(bool isSelected, bool isTarget, bool canClick,
            bool isActiveEnemy, int order, int distance)
        {
            if (unit == null)
            {
                return;
            }

            cardButton.interactable = canClick && unit.IsAlive;
            cardBackground.color = unit.IsEnemy
                ? new Color(0.39f, 0.17f, 0.22f, 1f)
                : new Color(0.13f, 0.28f, 0.43f, 1f);
            borderImage.enabled = isSelected || isTarget || isActiveEnemy;
            borderImage.color = isActiveEnemy
                ? new Color(1f, 0.40f, 0.32f, 1f)
                : isSelected ? new Color(0.42f, 0.90f, 1f, 1f) : new Color(1f, 0.80f, 0.25f, 1f);

            nameText.text = unit.Name;
            orderText.text = order == 1 ? "前排 1" : $"位置 {order}";
            healthText.text = $"生命 {unit.CurrentHealth} / {unit.MaxHealth}";
            armorText.text = $"护甲 {unit.CurrentArmor} / {unit.MaxArmor}";
            healthFill.fillAmount = unit.MaxHealth > 0
                ? Mathf.Clamp01((float)unit.CurrentHealth / unit.MaxHealth) : 0f;
            healthFill.color = unit.IsEnemy
                ? new Color(0.93f, 0.34f, 0.37f, 1f) : new Color(0.29f, 0.83f, 0.68f, 1f);

            intentText.gameObject.SetActive(unit.IsEnemy);
            if (unit.IsEnemy)
            {
                intentText.text = $"下次意图：攻击 {unit.NextIntentDamage}";
                statusText.text = isActiveEnemy ? "正在行动" : isTarget ? "点击选择目标" : "敌方角色";
                distanceText.text = distance > 0 ? $"距所选角色 {distance}（仅显示）" : "选择我方后显示距离";
            }
            else
            {
                statusText.text = unit.HasUsedStandby ? "本回合已静置" : $"可静置 · 伤害 {unit.StandbyDamage}";
                distanceText.text = $"攻击距离 {unit.AttackRange}";
            }
        }

        public void ClearBinding()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleClicked);
                cardButton.interactable = false;
            }

            unit = null;
            onClicked = null;
        }

        private void HandleClicked()
        {
            if (unit != null && unit.IsAlive && cardButton.interactable)
            {
                onClicked?.Invoke(unit);
            }
        }

        private void OnDestroy()
        {
            ClearBinding();
        }
    }
}
