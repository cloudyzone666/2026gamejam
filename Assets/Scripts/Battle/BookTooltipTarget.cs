using UnityEngine;
using UnityEngine.EventSystems;

namespace CardCards.Battle
{
    /// <summary>Enemy cards retain their action button while exposing nonblocking hover details.</summary>
    public sealed class BookTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private BattleInfoPanelView info;
        private BattleUnitState unit;
        private BattleActionKind? action;

        public void Bind(BattleInfoPanelView panel, BattleUnitState target)
        {
            info = panel;
            unit = target;
            action = null;
        }

        public void BindAction(BattleInfoPanelView panel, BattleActionKind kind)
        {
            info = panel;
            unit = null;
            action = kind;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (info == null) return;
            if (action.HasValue) info.PreviewAction(action.Value, this, eventData.position);
            else if (unit != null && unit.IsEnemy) info.PreviewEnemy(unit, this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (info != null) info.HidePreview(this);
        }

        private void OnDisable() { if (info != null) info.HidePreview(this); }
    }
}
