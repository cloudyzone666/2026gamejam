using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>The book's left page always shows an ally; inspection floats above the book.</summary>
    public sealed class BattleInfoPanelView : MonoBehaviour
    {
        [SerializeField] private Text unitNameText;
        [SerializeField] private Text healthValueText;
        [SerializeField] private Text attackValueText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image attackFill;
        [SerializeField] private InventoryGridView handheldGrid;
        [SerializeField] private InventoryGridView wornGrid;
        [SerializeField] private InventoryGridView accessoryGrid;
        [SerializeField] private BookTooltipView tooltip;

        private BattleView view;
        private BattleUnitState inspectedAlly;
        private BattleUnitState inspectedEnemy;
        private EquipmentInstance inspectedItem;
        private BattleActionKind? inspectedAction;

        public BattleUnitState InspectedAlly => inspectedAlly;
        public BookTooltipView Tooltip => tooltip;
        public bool HasItemTooltip => inspectedItem != null && tooltip != null && tooltip.IsVisible;
        public bool HasEnemyTooltip => inspectedEnemy != null && tooltip != null && tooltip.IsVisible;

        public void Initialize(BattleView battleView)
        {
            view = battleView;
            handheldGrid.Initialize(view);
            wornGrid.Initialize(view);
            accessoryGrid.Initialize(view);
            if (tooltip != null) tooltip.Initialize();
        }

        public void Refresh()
        {
            if (view == null || view.Controller == null) return;
            if (!Contains(view.Controller.Allies, inspectedAlly) || !inspectedAlly.IsAlive)
            {
                inspectedAlly = null;
                foreach (BattleUnitState ally in view.Controller.Allies)
                    if (ally.IsAlive) { inspectedAlly = ally; break; }
            }
            if (inspectedItem != null && !ItemExists(inspectedItem)) CloseTooltip();
            if (inspectedEnemy != null && !Contains(view.Controller.Enemies, inspectedEnemy)) CloseTooltip();
            unitNameText.text = inspectedAlly == null ? "请选择角色" : inspectedAlly.Name;
            healthValueText.text = inspectedAlly == null ? "—" : $"{inspectedAlly.CurrentHealth}/{inspectedAlly.MaxHealth}";
            attackValueText.text = inspectedAlly == null ? "—" : inspectedAlly.Attack.ToString();
            healthFill.fillAmount = inspectedAlly == null || inspectedAlly.MaxHealth == 0 ? 0f
                : (float)inspectedAlly.CurrentHealth / inspectedAlly.MaxHealth;
            // Attack has no maximum: the painted strip is decorative, with its actual value above it.
            attackFill.fillAmount = inspectedAlly == null ? 0f : 1f;
            BindEquipmentGrid(handheldGrid, EquipmentSlotKind.Handheld);
            BindEquipmentGrid(wornGrid, EquipmentSlotKind.Worn);
            BindEquipmentGrid(accessoryGrid, EquipmentSlotKind.Accessory);
            if (tooltip != null && tooltip.IsVisible)
            {
                if (inspectedItem != null) tooltip.RefreshContent(inspectedItem.Definition.displayName, DescribeItem(inspectedItem));
                else if (inspectedEnemy != null) tooltip.RefreshContent(inspectedEnemy.Name, DescribeEnemy(inspectedEnemy));
                else if (inspectedAction.HasValue) tooltip.RefreshContent(ActionName(inspectedAction.Value), DescribeAction(inspectedAction.Value));
            }
        }

        private void BindEquipmentGrid(InventoryGridView gridView, EquipmentSlotKind kind)
        {
            InventoryGridState grid = null;
            if (inspectedAlly != null)
                foreach (InventoryGridState candidate in inspectedAlly.EquipmentGrids)
                    if (candidate.AllowedKind == kind) { grid = candidate; break; }
            gridView.Bind(grid);
            gridView.Refresh();
        }

        public void InspectUnit(BattleUnitState unit)
        {
            if (unit == null) return;
            if (unit.IsEnemy) ShowEnemy(unit, this, PointerPosition(), true);
            else
            {
                inspectedAlly = unit;
                CloseTooltip();
                Refresh();
            }
        }

        public void InspectItem(EquipmentInstance item) { ShowItem(item, this, PointerPosition(), true); }
        public void PreviewItem(EquipmentInstance item, Object source, Vector2 position) { ShowItem(item, source, position, false); }
        public void PreviewEnemy(BattleUnitState unit, Object source, Vector2 position) { ShowEnemy(unit, source, position, false); }
        public void HidePreview(Object source) { if (tooltip != null) tooltip.HidePreview(source); }
        public void ShowCharacterPage() { CloseTooltip(); Refresh(); }

        public void CloseTooltip()
        {
            inspectedItem = null;
            inspectedEnemy = null;
            inspectedAction = null;
            if (tooltip != null) tooltip.Close();
        }

        private void ShowItem(EquipmentInstance item, Object source, Vector2 position, bool pinned)
        {
            if (item == null || view == null || view.IsDraggingEquipment || tooltip == null) return;
            if (!pinned && tooltip.IsPinned) return;
            inspectedItem = item;
            inspectedEnemy = null;
            inspectedAction = null;
            tooltip.Show(item.Definition.displayName, DescribeItem(item), source, position, pinned);
        }

        private void ShowEnemy(BattleUnitState unit, Object source, Vector2 position, bool pinned)
        {
            if (unit == null || view == null || view.IsDraggingEquipment || tooltip == null) return;
            if (!pinned && tooltip.IsPinned) return;
            inspectedEnemy = unit;
            inspectedItem = null;
            inspectedAction = null;
            tooltip.Show(unit.Name, DescribeEnemy(unit), source, position, pinned);
        }

        public void PreviewAction(BattleActionKind action, Object source, Vector2 position)
        {
            if (view == null || view.Controller == null || view.IsDraggingEquipment || tooltip == null || tooltip.IsPinned) return;
            inspectedAction = action;
            inspectedEnemy = null;
            inspectedItem = null;
            tooltip.Show(ActionName(action), DescribeAction(action), source, position, false);
        }

        private string DescribeAction(BattleActionKind action)
        {
            BattleController controller = view.Controller;
            if (action == BattleActionKind.Reorganize && controller.Phase == BattlePhase.Reorganizing)
                return "退出整顿 · 0 AP\n退出后才能继续行动；行动点耗尽时交给敌方。";
            int cost = action == BattleActionKind.Standby ? 0 : action == BattleActionKind.Flip ? 2 : 1;
            var text = new StringBuilder($"消耗 {cost} AP\n");
            string reason;
            text.AppendLine(controller.CanBeginAction(action, out reason) ? "当前可以执行" : reason);
            if (action == BattleActionKind.Reorganize)
                text.Append("拖动我方角色换位、整理装备；完成后主动退出，退出免费。");
            else
            {
                BattleUnitState actor = controller.SelectedAlly ?? inspectedAlly;
                if (actor == null) return text.ToString();
                text.AppendLine($"{actor.Name} · {actor.CurrentForm.name} · 射程 {actor.AttackRange}");
                foreach (BattleEffectStep step in BattleActionResolver.BuildPlan(actor, action).Steps)
                {
                    string verb = step.Kind == EffectKind.Damage ? "伤害" : step.Kind == EffectKind.Heal ? "治疗" : "护盾";
                    text.AppendLine($"{step.Source}：{TargetName(step.Target)} {verb} {step.Value}");
                }
            }
            return text.ToString().TrimEnd();
        }

        private static string ActionName(BattleActionKind action)
        {
            return action == BattleActionKind.Standby ? "静置" : action == BattleActionKind.Rotate ? "旋转"
                : action == BattleActionKind.Flip ? "翻面" : "调整 / 退出整顿";
        }

        private string DescribeEnemy(BattleUnitState enemy)
        {
            string distance = inspectedAlly == null ? "" : $"\n距 {inspectedAlly.Name}：{view.Controller.GetDistance(inspectedAlly, enemy)}";
            return $"生命 {enemy.CurrentHealth}/{enemy.MaxHealth}    护盾 {enemy.CurrentArmor}/{enemy.MaxArmor}\n"
                + $"位次 {view.Controller.GetLivingOrder(enemy)}    射程 {enemy.AttackRange}\n"
                + $"下一步：攻击 {enemy.NextIntentDamage}" + distance;
        }

        private static string DescribeItem(EquipmentInstance instance)
        {
            EquipmentDefinition item = instance.Definition;
            var text = new StringBuilder();
            text.AppendLine($"{KindName(item.kind)} · 背包占地 {instance.Width}×{instance.Height}");
            BattleUnitState owner = instance.Grid == null ? null : instance.Grid.Owner;
            text.AppendLine(owner == null ? "背包中，未生效" : $"{owner.Name} · {(owner.IsRotated ? "下端" : "上端")}生效");
            AppendEnd(text, "上端", item.top);
            AppendEnd(text, "下端", item.bottom);
            if (!string.IsNullOrEmpty(item.description)) text.AppendLine(item.description);
            text.Append("双击穿脱 · 拖动换装");
            if (item.canRotate) text.Append(" · 右键 / R 转向");
            return text.ToString();
        }

        private static void AppendEnd(StringBuilder text, string label, EquipmentEndDefinition end)
        {
            text.AppendLine($"{label}：攻击 {Signed(end.attack)} / 生命 {Signed(end.health)} / 护盾 {Signed(end.armor)}");
            text.AppendLine($"静置：{DescribeEffects(end.standby)}");
            text.AppendLine($"旋转：{DescribeEffects(end.rotation)}");
        }

        private static string DescribeEffects(EffectDefinition[] effects)
        {
            if (effects == null || effects.Length == 0) return "无";
            var text = new StringBuilder();
            foreach (EffectDefinition effect in effects)
            {
                if (effect == null) continue;
                if (text.Length > 0) text.Append("；");
                string verb = effect.kind == EffectKind.Damage ? "伤" : effect.kind == EffectKind.Heal ? "疗" : "盾";
                string formula = Mathf.Approximately(effect.scaling, 0f) ? Mathf.Max(0, effect.amount).ToString()
                    : Mathf.Approximately(effect.scaling, 1f) ? "攻击" : $"{effect.scaling:0.##}×攻击";
                if (!Mathf.Approximately(effect.scaling, 0f) && effect.amount != 0) formula += Signed(effect.amount);
                text.Append($"{TargetName(effect.target)}{verb} {formula}");
            }
            return text.Length > 0 ? text.ToString() : "无";
        }

        private static string TargetName(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.Self: return "自身";
                case EffectTarget.AllySingle: return "我方单体";
                case EffectTarget.AllyAll: return "我方全体";
                case EffectTarget.EnemyAll: return "敌方全体";
                default: return "敌方单体";
            }
        }

        private static string Signed(int value) { return value >= 0 ? "+" + value : value.ToString(); }
        private static string KindName(EquipmentSlotKind kind)
        {
            return kind == EquipmentSlotKind.Handheld ? "手持" : kind == EquipmentSlotKind.Worn ? "身着" : "佩戴";
        }

        private static Vector2 PointerPosition()
        {
            return Mouse.current == null ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : Mouse.current.position.ReadValue();
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<BattleUnitState> units, BattleUnitState unit)
        {
            if (unit == null) return false;
            foreach (BattleUnitState candidate in units) if (ReferenceEquals(candidate, unit)) return true;
            return false;
        }

        private bool ItemExists(EquipmentInstance item)
        {
            if (view.Controller.Backpack != null && view.Controller.Backpack.Find(item) != null) return true;
            foreach (BattleUnitState ally in view.Controller.Allies)
                foreach (InventoryGridState grid in ally.EquipmentGrids)
                    if (grid.Find(item) != null) return true;
            return false;
        }
    }
}
