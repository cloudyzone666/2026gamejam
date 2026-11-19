using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Displays battle state and forwards button clicks to the controller.</summary>
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

        [Header("Actions")]
        [SerializeField] private Button standbyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button endTurnButton;

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private Button restartButton;

        private static Font sharedFont;
        private readonly List<CharacterCardView> allyCards = new List<CharacterCardView>();
        private readonly List<CharacterCardView> enemyCards = new List<CharacterCardView>();
        private BattleController controller;
        private bool buttonsBound;
        private bool configured;

        public void Initialize(BattleController battleController)
        {
            controller = battleController;
            configured = HasRequiredReferences();
            if (!configured)
            {
                Debug.LogError("BattleView 的 Inspector 引用未绑定完整，请检查角色、文本、按钮和结果面板。", this);
                return;
            }

            Font font = GetInterfaceFont();
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }

            if (!buttonsBound)
            {
                standbyButton.onClick.AddListener(HandleStandby);
                cancelButton.onClick.AddListener(HandleCancel);
                endTurnButton.onClick.AddListener(HandleEndTurn);
                restartButton.onClick.AddListener(HandleRestart);
                buttonsBound = true;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (!configured || controller == null)
            {
                return;
            }

            BattlePhase phase = controller.Phase;
            bool playerTurn = phase == BattlePhase.PlayerTurn;
            bool selectingTarget = phase == BattlePhase.SelectingTarget;
            bool finished = phase == BattlePhase.Victory || phase == BattlePhase.Defeat;
            BattleUnitState selectedAlly = controller.SelectedAlly;

            turnText.text = $"第 {controller.TurnNumber} 回合 · {GetPhaseName(phase)}";
            instructionText.text = controller.LastMessage;
            selectionText.text = GetSelectionText(phase, selectedAlly);

            standbyButton.interactable = playerTurn && selectedAlly != null
                && selectedAlly.IsAlive && !selectedAlly.HasUsedStandby;
            cancelButton.interactable = selectingTarget || (playerTurn && selectedAlly != null);
            endTurnButton.interactable = playerTurn;
            resultPanel.SetActive(finished);
            restartButton.interactable = finished;
            if (finished)
            {
                resultText.text = phase == BattlePhase.Victory
                    ? "战斗胜利\n敌方已全部倒下"
                    : "战斗失败\n我方已全部倒下";
            }

            RefreshCards(controller.Allies, allyCards, allyContainer, false);
            RefreshCards(controller.Enemies, enemyCards, enemyContainer, true);
        }

        private void RefreshCards(IReadOnlyList<BattleUnitState> units,
            List<CharacterCardView> cards, RectTransform container, bool isEnemy)
        {
            int livingCount = 0;
            int selectedOrder = GetLivingOrder(controller.Allies, controller.SelectedAlly);
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnitState unit = units[index];
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (livingCount == cards.Count)
                {
                    cards.Add(Instantiate(characterCardPrefab, container, false));
                }

                CharacterCardView card = cards[livingCount];
                if (!ReferenceEquals(card.Unit, unit))
                {
                    if (isEnemy)
                    {
                        card.Bind(unit, HandleEnemyClicked, GetInterfaceFont());
                    }
                    else
                    {
                        card.Bind(unit, HandleAllyClicked, GetInterfaceFont());
                    }
                }

                card.gameObject.SetActive(true);
                card.transform.SetSiblingIndex(livingCount);
                int order = livingCount + 1;
                int distance = isEnemy && selectedOrder > 0 ? selectedOrder + order - 1 : 0;
                bool canClick = isEnemy
                    ? controller.Phase == BattlePhase.SelectingTarget
                    : controller.Phase == BattlePhase.PlayerTurn && !unit.HasUsedStandby;
                card.Refresh(ReferenceEquals(unit, controller.SelectedAlly),
                    isEnemy && controller.Phase == BattlePhase.SelectingTarget,
                    canClick, ReferenceEquals(unit, controller.ActiveEnemy), order, distance);
                livingCount++;
            }

            // Hidden slots are rebound on restart; their old units cannot receive clicks.
            for (int index = livingCount; index < cards.Count; index++)
            {
                cards[index].ClearBinding();
                cards[index].gameObject.SetActive(false);
            }
        }

        private static int GetLivingOrder(IReadOnlyList<BattleUnitState> units, BattleUnitState target)
        {
            if (target == null)
            {
                return 0;
            }

            int order = 0;
            for (int index = 0; index < units.Count; index++)
            {
                if (!units[index].IsAlive)
                {
                    continue;
                }

                order++;
                if (ReferenceEquals(units[index], target))
                {
                    return order;
                }
            }

            return 0;
        }

        private static string GetPhaseName(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.PlayerTurn: return "我方回合";
                case BattlePhase.SelectingTarget: return "选择敌方目标";
                case BattlePhase.Resolving: return "结算中";
                case BattlePhase.EnemyTurn: return "敌方回合";
                case BattlePhase.Victory: return "胜利";
                case BattlePhase.Defeat: return "失败";
                default: return string.Empty;
            }
        }

        private static string GetSelectionText(BattlePhase phase, BattleUnitState selectedAlly)
        {
            if (phase == BattlePhase.SelectingTarget && selectedAlly != null)
            {
                return $"{selectedAlly.Name}：请选择敌人 · 静置伤害 {selectedAlly.StandbyDamage} · 可取消";
            }

            if (phase == BattlePhase.PlayerTurn)
            {
                return selectedAlly == null
                    ? "选择蓝色角色 → 静置 → 选择敌人；攻击后请主动结束回合"
                    : $"已选 {selectedAlly.Name} · 攻击距离 {selectedAlly.AttackRange} · 静置伤害 {selectedAlly.StandbyDamage}";
            }

            if (phase == BattlePhase.EnemyTurn)
            {
                return "敌人按位置顺序攻击我方最前方的存活角色";
            }

            return phase == BattlePhase.Resolving ? "正在结算行动" : "点击“重新开始”恢复初始战斗";
        }

        private static Font GetInterfaceFont()
        {
            if (sharedFont != null)
            {
                return sharedFont;
            }

            string[] installedFonts = Font.GetOSInstalledFontNames();
            foreach (string fontName in installedFonts)
            {
                if (fontName == "Microsoft YaHei" || fontName == "微软雅黑")
                {
                    sharedFont = Font.CreateDynamicFontFromOSFont(fontName, 18);
                    break;
                }
            }

            if (sharedFont == null)
            {
                sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return sharedFont;
        }

        private bool HasRequiredReferences()
        {
            return controller != null && characterCardPrefab != null && allyContainer != null
                && enemyContainer != null && turnText != null && instructionText != null
                && selectionText != null && standbyButton != null && cancelButton != null
                && endTurnButton != null && resultPanel != null && resultText != null
                && restartButton != null;
        }

        private void HandleStandby() { controller.BeginStandby(); }
        private void HandleCancel() { controller.CancelSelection(); }
        private void HandleEndTurn() { controller.EndPlayerTurn(); }
        private void HandleRestart() { controller.RestartBattle(); }
        private void HandleAllyClicked(BattleUnitState unit) { controller.SelectAlly(unit); }
        private void HandleEnemyClicked(BattleUnitState unit) { controller.SelectTarget(unit); }

        private void OnDestroy()
        {
            if (!buttonsBound)
            {
                return;
            }

            if (standbyButton != null) standbyButton.onClick.RemoveListener(HandleStandby);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
            if (endTurnButton != null) endTurnButton.onClick.RemoveListener(HandleEndTurn);
            if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestart);
        }
    }
}
