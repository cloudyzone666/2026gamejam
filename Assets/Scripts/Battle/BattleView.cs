using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Scene UI wiring and presentation. Rules and inventory transactions remain in the controller.</summary>
    public sealed class BattleView : MonoBehaviour
    {
        [Header("Character cards")]
        [SerializeField] private CharacterCardView characterCardPrefab;
        [SerializeField] private RectTransform allyContainer;
        [SerializeField] private RectTransform enemyContainer;
        [Header("Battle information")]
        [SerializeField] private Text turnText;
        [SerializeField] private Text instructionText;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text actionPointText;
        [SerializeField] private Text logText;
        [Header("Actions")]
        [SerializeField] private Button standbyButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private Button flipButton;
        [SerializeField] private Button adjustButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button endTurnButton;
        [Header("Information and inventory")]
        [SerializeField] private BattleInfoPanelView infoPanel;
        [SerializeField] private InventoryGridView backpackGrid;
        [SerializeField] private RectTransform dragLayer;
        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private Button restartButton;

        private static Font sharedFont;
        private readonly Dictionary<BattleUnitState, CharacterCardView> allyCards = new Dictionary<BattleUnitState, CharacterCardView>();
        private readonly Dictionary<BattleUnitState, CharacterCardView> enemyCards = new Dictionary<BattleUnitState, CharacterCardView>();
        private BattleController controller;
        private bool buttonsBound;
        private bool configured;
        private EquipmentItemView activeEquipmentDrag;

        public BattleController Controller => controller;
        public RectTransform DragLayer => dragLayer;
        public BattleInfoPanelView InfoPanel => infoPanel;
        public InventoryGridView BackpackGrid => backpackGrid;
        public EquipmentItemView SelectedEquipment { get; private set; }
        public bool IsDraggingEquipment => activeEquipmentDrag != null;
        public bool CanReorderCards => controller != null && (controller.Phase == BattlePhase.Preparation || controller.Phase == BattlePhase.Reorganizing);

        public void Initialize(BattleController battleController)
        {
            controller = battleController;
            configured = HasRequiredReferences();
            if (!configured)
            {
                Debug.LogError("BattleView 的 Inspector 引用未绑定完整，请检查卡面、四个行动、信息栏、背包与拖拽层。", this);
                return;
            }
            Font font = GetInterfaceFont();
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = font;
            if (EventSystem.current != null) EventSystem.current.pixelDragThreshold = 6;
            infoPanel.Initialize(this);
            backpackGrid.Initialize(this);
            BindActionTooltip(standbyButton, BattleActionKind.Standby);
            BindActionTooltip(rotateButton, BattleActionKind.Rotate);
            BindActionTooltip(flipButton, BattleActionKind.Flip);
            BindActionTooltip(adjustButton, BattleActionKind.Reorganize);
            if (!buttonsBound)
            {
                standbyButton.onClick.AddListener(HandleStandby);
                rotateButton.onClick.AddListener(HandleRotate);
                flipButton.onClick.AddListener(HandleFlip);
                adjustButton.onClick.AddListener(HandleAdjust);
                cancelButton.onClick.AddListener(HandleCancel);
                endTurnButton.onClick.AddListener(HandleEndTurn);
                restartButton.onClick.AddListener(HandleRestart);
                buttonsBound = true;
            }
            Refresh();
        }

        public void Refresh()
        {
            if (!configured || controller == null) return;
            BattlePhase phase = controller.Phase;
            bool finished = phase == BattlePhase.Victory || phase == BattlePhase.Defeat;
            turnText.text = phase == BattlePhase.Preparation ? "战前整备" : $"第 {controller.TurnNumber} 回合 · {GetPhaseName(phase)}";
            actionPointText.text = phase == BattlePhase.Preparation ? "整备中" : $"行动点 {controller.ActionPoints} / 4";
            instructionText.text = controller.LastMessage;
            if (selectionText != null) selectionText.text = GetSelectionText();
            string reason;
            standbyButton.interactable = controller.CanBeginAction(BattleActionKind.Standby, out reason);
            rotateButton.interactable = controller.CanBeginAction(BattleActionKind.Rotate, out reason);
            flipButton.interactable = controller.CanBeginAction(BattleActionKind.Flip, out reason);
            adjustButton.interactable = phase == BattlePhase.Reorganizing || controller.CanBeginAction(BattleActionKind.Reorganize, out reason);
            SetButtonText(adjustButton, phase == BattlePhase.Reorganizing ? "退出\n0" : "调整\n1");
            cancelButton.interactable = phase == BattlePhase.SelectingTarget || (phase == BattlePhase.PlayerTurn && controller.SelectedAlly != null);
            endTurnButton.interactable = phase == BattlePhase.Preparation || phase == BattlePhase.PlayerTurn;
            SetButtonText(endTurnButton, phase == BattlePhase.Preparation ? "开始战斗" : "结束回合");
            resultPanel.SetActive(finished);
            restartButton.interactable = finished;
            if (finished) resultText.text = phase == BattlePhase.Victory ? "战斗胜利\n敌方已全部倒下" : "战斗失败\n我方已全部倒下";
            RefreshCards(controller.Allies, allyCards, allyContainer, false);
            RefreshCards(controller.Enemies, enemyCards, enemyContainer, true);
            backpackGrid.Bind(controller.Backpack);
            backpackGrid.Refresh();
            infoPanel.Refresh();
            if (!controller.CanModifyEquipment && activeEquipmentDrag != null) activeEquipmentDrag.CancelDrag();
        }

        private void RefreshCards(IReadOnlyList<BattleUnitState> units,
            Dictionary<BattleUnitState, CharacterCardView> cards, RectTransform container, bool isEnemy)
        {
            // Identity, rather than living-index, owns each view. Reordering never rebinds a dragged card.
            var obsolete = new List<BattleUnitState>();
            foreach (var pair in cards)
                if (!Contains(units, pair.Key)) obsolete.Add(pair.Key);
            foreach (BattleUnitState oldUnit in obsolete)
            {
                cards[oldUnit].ClearBinding();
                cards[oldUnit].gameObject.SetActive(false);
                Destroy(cards[oldUnit].gameObject);
                cards.Remove(oldUnit);
            }
            int count = 0;
            foreach (BattleUnitState unit in units) if (unit.IsAlive) count++;
            float width = count >= 4 ? 134f : count == 3 ? 177f : 190f;
            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.spacing = count >= 4 ? 20f : count == 3 ? 32f : 56f;
            foreach (BattleUnitState unit in units)
            {
                CharacterCardView card;
                if (!cards.TryGetValue(unit, out card))
                {
                    card = Instantiate(characterCardPrefab, container, false);
                    card.Initialize(this);
                    card.Bind(unit, isEnemy ? (UnityEngine.Events.UnityAction<BattleUnitState>)HandleEnemyClicked : HandleAllyClicked, GetInterfaceFont());
                    if (isEnemy)
                    {
                        BookTooltipTarget tooltipTarget = card.GetComponent<BookTooltipTarget>();
                        if (tooltipTarget == null) tooltipTarget = card.gameObject.AddComponent<BookTooltipTarget>();
                        tooltipTarget.Bind(infoPanel, unit);
                    }
                    cards.Add(unit, card);
                }
                card.gameObject.SetActive(unit.IsAlive);
                if (!unit.IsAlive) continue;
                int order = controller.GetLivingOrder(unit);
                card.transform.SetSiblingIndex(order - 1);
                ((RectTransform)card.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                int distance = isEnemy && controller.SelectedAlly != null ? controller.GetDistance(controller.SelectedAlly, unit) : 0;
                bool target = controller.Phase == BattlePhase.SelectingTarget && controller.CanTarget(unit);
                card.Refresh(ReferenceEquals(unit, controller.SelectedAlly), target, true,
                    ReferenceEquals(unit, controller.ActiveEnemy), order, distance);
            }
        }

        private static bool Contains(IReadOnlyList<BattleUnitState> units, BattleUnitState target)
        {
            for (int i = 0; i < units.Count; i++) if (ReferenceEquals(units[i], target)) return true;
            return false;
        }

        public void CommitCardReorder(BattleUnitState unit, Vector2 screenPoint)
        {
            if (!CanReorderCards || !RectTransformUtility.RectangleContainsScreenPoint(allyContainer, screenPoint, null))
            {
                ShowHint("请在我方队列内松手；原位次已保留。");
                return;
            }
            float firstX = 0f, lastX = 0f;
            bool found = false;
            foreach (BattleUnitState ally in controller.Allies)
            {
                if (!ally.IsAlive) continue;
                float x = RectTransformUtility.WorldToScreenPoint(null, allyCards[ally].transform.position).x;
                if (!found) { firstX = x; found = true; }
                lastX = x;
            }
            bool increasing = lastX >= firstX;
            int insertion = 0;
            foreach (BattleUnitState ally in controller.Allies)
            {
                if (!ally.IsAlive || ReferenceEquals(ally, unit)) continue;
                float x = RectTransformUtility.WorldToScreenPoint(null, allyCards[ally].transform.position).x;
                if (increasing ? screenPoint.x > x : screenPoint.x < x) insertion++;
            }
            string error;
            if (!controller.TryMoveAlly(unit, insertion, out error)) ShowHint(error);
        }

        public void SelectEquipment(EquipmentItemView itemView, bool inspect = true)
        {
            SelectedEquipment = itemView;
            if (inspect && itemView != null) infoPanel.InspectItem(itemView.Item);
        }

        private void Update()
        {
            if (controller == null || infoPanel == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                infoPanel.CloseTooltip();
                if (activeEquipmentDrag != null) activeEquipmentDrag.CancelDrag();
                if (controller.Phase == BattlePhase.SelectingTarget) controller.CancelSelection();
            }
            if (SelectedEquipment != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SelectedEquipment.Rotate();
        }

        public bool BeginEquipmentDrag(EquipmentItemView itemView)
        {
            if (activeEquipmentDrag != null || !controller.CanModifyEquipment) return false;
            activeEquipmentDrag = itemView;
            SelectedEquipment = itemView;
            infoPanel.ShowCharacterPage();
            return true;
        }

        public void EndEquipmentDrag(EquipmentItemView itemView)
        {
            if (ReferenceEquals(activeEquipmentDrag, itemView)) activeEquipmentDrag = null;
        }

        public void ShowHint(string message)
        {
            if (instructionText != null && !string.IsNullOrEmpty(message)) instructionText.text = message;
        }

        private string GetSelectionText()
        {
            BattleUnitState selected = controller.SelectedAlly;
            if (controller.Phase == BattlePhase.SelectingTarget)
                return controller.PendingTargetRule == EffectTarget.AllySingle ? "选择我方目标 · 取消不耗行动点" : "选择射程内敌人 · 点我方卡取消";
            if (controller.Phase == BattlePhase.Reorganizing) return "整顿中：拖动角色换位 / 拖动装备换装；完成后主动退出";
            if (controller.Phase == BattlePhase.Preparation) return "可自由换位与换装；准备好后点击“开始战斗”";
            if (controller.Phase == BattlePhase.EnemyTurn || controller.Phase == BattlePhase.Resolving)
                return "行动结算中，暂不能操作；仍可点击卡面查看信息。";
            if (selected == null) return "先选角色，再选行动与目标；灰色行动表示当前不可用。";
            var unavailable = new System.Text.StringBuilder();
            var reasons = new Dictionary<string, string>();
            AddActionReason(reasons, BattleActionKind.Standby, "静置");
            AddActionReason(reasons, BattleActionKind.Rotate, "旋转");
            AddActionReason(reasons, BattleActionKind.Flip, "翻面");
            AddActionReason(reasons, BattleActionKind.Reorganize, "调整");
            foreach (var pair in reasons)
            {
                if (unavailable.Length > 0) unavailable.Append("  |  ");
                unavailable.Append(pair.Value + "：" + pair.Key);
            }
            return unavailable.Length > 0 ? unavailable.ToString() : $"已选 {selected.Name} · {selected.CurrentForm.name} · 攻击 {selected.Attack} · 射程 {selected.AttackRange}";
        }

        private void AddActionReason(Dictionary<string, string> reasons, BattleActionKind action, string label)
        {
            string reason;
            if (controller.CanBeginAction(action, out reason)) return;
            reason = reason.TrimEnd('。');
            string existing;
            reasons[reason] = reasons.TryGetValue(reason, out existing) ? existing + "/" + label : label;
        }

        private static string GetPhaseName(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.PlayerTurn: return "我方回合";
                case BattlePhase.SelectingTarget: return "选择目标";
                case BattlePhase.Resolving: return "结算中";
                case BattlePhase.EnemyTurn: return "敌方回合";
                case BattlePhase.Reorganizing: return "整顿中";
                case BattlePhase.Victory: return "胜利";
                case BattlePhase.Defeat: return "失败";
                default: return "战前整备";
            }
        }

        public static Font GetInterfaceFont()
        {
            if (sharedFont != null) return sharedFont;
            foreach (string fontName in Font.GetOSInstalledFontNames())
                if (fontName == "Microsoft YaHei" || fontName == "微软雅黑")
                {
                    sharedFont = Font.CreateDynamicFontFromOSFont(fontName, 18);
                    break;
                }
            if (sharedFont == null) sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return sharedFont;
        }

        private bool HasRequiredReferences()
        {
            return controller != null && characterCardPrefab != null && allyContainer != null && enemyContainer != null
                && turnText != null && instructionText != null && actionPointText != null
                && standbyButton != null && rotateButton != null && flipButton != null && adjustButton != null
                && cancelButton != null && endTurnButton != null && resultPanel != null && resultText != null && restartButton != null
                && infoPanel != null && backpackGrid != null && dragLayer != null;
        }

        private static void SetButtonText(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        private void BindActionTooltip(Button button, BattleActionKind action)
        {
            BookTooltipTarget target = button.GetComponent<BookTooltipTarget>();
            if (target == null) target = button.gameObject.AddComponent<BookTooltipTarget>();
            target.BindAction(infoPanel, action);
        }

        private void HandleStandby() { controller.BeginAction(BattleActionKind.Standby); }
        private void HandleRotate() { controller.BeginAction(BattleActionKind.Rotate); }
        private void HandleFlip() { controller.BeginAction(BattleActionKind.Flip); }
        private void HandleAdjust()
        {
            if (controller.Phase == BattlePhase.Reorganizing) controller.ExitReorganization();
            else controller.BeginAction(BattleActionKind.Reorganize);
        }
        private void HandleCancel() { controller.CancelSelection(); }
        private void HandleEndTurn()
        {
            if (controller.Phase == BattlePhase.Preparation) controller.StartBattle();
            else controller.EndPlayerTurn();
        }
        private void HandleRestart()
        {
            if (activeEquipmentDrag != null) activeEquipmentDrag.CancelDrag();
            SelectedEquipment = null;
            controller.RestartBattle();
        }
        private void HandleAllyClicked(BattleUnitState unit)
        {
            if (controller.Phase == BattlePhase.SelectingTarget)
            {
                if (controller.PendingTargetRule == EffectTarget.AllySingle && controller.CanTarget(unit)) controller.SelectTarget(unit);
                else controller.CancelSelection();
            }
            else controller.SelectAlly(unit);
            infoPanel.InspectUnit(unit);
        }
        private void HandleEnemyClicked(BattleUnitState unit)
        {
            infoPanel.InspectUnit(unit);
            if (controller.Phase == BattlePhase.SelectingTarget) controller.SelectTarget(unit);
        }

        private void OnDestroy()
        {
            if (!buttonsBound) return;
            standbyButton.onClick.RemoveListener(HandleStandby);
            rotateButton.onClick.RemoveListener(HandleRotate);
            flipButton.onClick.RemoveListener(HandleFlip);
            adjustButton.onClick.RemoveListener(HandleAdjust);
            cancelButton.onClick.RemoveListener(HandleCancel);
            endTurnButton.onClick.RemoveListener(HandleEndTurn);
            restartButton.onClick.RemoveListener(HandleRestart);
        }
    }
}
