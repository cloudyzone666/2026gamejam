using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardCards.Battle
{
    public enum BattlePhase { PlayerTurn, SelectingTarget, Resolving, EnemyTurn, Victory, Defeat }

    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] private BattleDefinition definition;
        [SerializeField] private BattleView view;
        [SerializeField, Min(0f)] private float enemyActionDelay = 0.5f;

        private readonly List<BattleUnitState> allies = new List<BattleUnitState>();
        private readonly List<BattleUnitState> enemies = new List<BattleUnitState>();
        private Coroutine enemyTurnRoutine;
        private int enemyCursor;
        private bool battleInitialized;

        public BattlePhase Phase { get; private set; } = BattlePhase.Resolving;
        public int TurnNumber { get; private set; }
        // Keep original order and dead units; the view filters IsAlive and renumbers survivors.
        public IReadOnlyList<BattleUnitState> Allies => allies;
        public IReadOnlyList<BattleUnitState> Enemies => enemies;
        public BattleUnitState SelectedAlly { get; private set; }
        public BattleUnitState ActiveEnemy { get; private set; }
        public string LastMessage { get; private set; } = string.Empty;

        private void Start()
        {
            if (view == null)
            {
                Debug.LogError("BattleController 缺少 BattleView 场景引用。", this);
                enabled = false;
                return;
            }
            view.Initialize(this);
            InitializeBattle();
        }

        private void OnEnable()
        {
            if (!battleInitialized) return;
            RefreshView();
            if (Phase == BattlePhase.EnemyTurn && enemyTurnRoutine == null)
                enemyTurnRoutine = StartCoroutine(RunEnemyTurn());
        }

        private void OnDisable()
        {
            if (enemyTurnRoutine != null)
            {
                StopCoroutine(enemyTurnRoutine);
                enemyTurnRoutine = null;
            }
            // Keep the phase and the next uncommitted enemy index. Re-enabling resumes this turn.
            ActiveEnemy = null;
        }

        public void SelectAlly(BattleUnitState ally)
        {
            if (!CanAct(BattlePhase.PlayerTurn) || !CanUseStandby(ally)) return;
            SelectedAlly = ally;
            LastMessage = $"已选择 {ally.Name}，点击“静置”再选择敌人。";
            RefreshView();
        }

        public void BeginStandby()
        {
            if (!CanAct(BattlePhase.PlayerTurn) || !CanUseStandby(SelectedAlly)) return;
            Phase = BattlePhase.SelectingTarget;
            LastMessage = $"为 {SelectedAlly.Name} 选择敌人：造成 {SelectedAlly.StandbyDamage} 点伤害。";
            RefreshView();
        }

        public void SelectTarget(BattleUnitState target)
        {
            if (!CanAct(BattlePhase.SelectingTarget) || !CanUseStandby(SelectedAlly) ||
                target == null || !target.IsAlive || !enemies.Contains(target)) return;

            BattleUnitState attacker = SelectedAlly;
            // Commit the phase before touching numbers: another click cannot submit this action twice.
            Phase = BattlePhase.Resolving;
            RefreshView();
            attacker.UseStandby();
            target.ApplyDamage(attacker.StandbyDamage);
            SelectedAlly = null;
            LastMessage = $"{attacker.Name} 静置，对 {target.Name} 造成 {attacker.StandbyDamage} 点伤害。";
            if (!target.IsAlive) LastMessage += " 目标已倒下。";
            if (!TryFinishBattle()) Phase = BattlePhase.PlayerTurn;
            RefreshView();
        }

        public void CancelSelection()
        {
            if (!CanAct(BattlePhase.SelectingTarget) &&
                !(CanAct(BattlePhase.PlayerTurn) && SelectedAlly != null)) return;
            SelectedAlly = null;
            Phase = BattlePhase.PlayerTurn;
            LastMessage = "已取消选择，没有消耗静置次数。";
            RefreshView();
        }

        public void EndPlayerTurn()
        {
            if (!CanAct(BattlePhase.PlayerTurn)) return;
            SelectedAlly = null;
            ActiveEnemy = null;
            enemyCursor = 0;
            Phase = BattlePhase.EnemyTurn;
            LastMessage = "敌方回合。";
            RefreshView();
            enemyTurnRoutine = StartCoroutine(RunEnemyTurn());
        }

        public void RestartBattle()
        {
            if (!isActiveAndEnabled || !battleInitialized ||
                (Phase != BattlePhase.Victory && Phase != BattlePhase.Defeat)) return;
            if (enemyTurnRoutine != null)
            {
                StopCoroutine(enemyTurnRoutine);
                enemyTurnRoutine = null;
            }
            InitializeBattle();
        }

        private void InitializeBattle()
        {
            string error = "BattleDefinition 引用为空。";
            if (definition == null || !definition.TryValidate(out error))
            {
                LastMessage = $"战斗配置错误：{error}";
                Debug.LogError(LastMessage, this);
                enabled = false;
                RefreshView();
                return;
            }

            allies.Clear();
            enemies.Clear();
            foreach (AllyDefinition ally in definition.allies) allies.Add(new BattleUnitState(ally));
            foreach (EnemyDefinition enemy in definition.enemies) enemies.Add(new BattleUnitState(enemy));
            TurnNumber = 1;
            SelectedAlly = null;
            ActiveEnemy = null;
            enemyCursor = 0;
            battleInitialized = true;
            Phase = BattlePhase.PlayerTurn;
            LastMessage = "选择我方角色，使用静置；准备好后主动结束回合。";
            RefreshView();
        }

        private IEnumerator RunEnemyTurn()
        {
            // Always yield once so StartCoroutine has assigned its handle before completion is possible.
            yield return null;
            while (Phase == BattlePhase.EnemyTurn && enemyCursor < enemies.Count)
            {
                BattleUnitState enemy = enemies[enemyCursor];
                if (!enemy.IsAlive)
                {
                    enemyCursor++;
                    continue;
                }

                ActiveEnemy = enemy;
                LastMessage = $"{enemy.Name} 准备攻击 {enemy.NextIntentDamage}。";
                RefreshView();
                yield return WaitForEnemyAction();

                BattleUnitState target = FirstAlive(allies);
                if (target == null)
                {
                    TryFinishBattle();
                    RefreshView();
                    enemyTurnRoutine = null;
                    yield break;
                }

                int damage = enemy.NextIntentDamage;
                // Advance the cursor at the commit point so disabling cannot repeat a resolved hit.
                enemyCursor++;
                target.ApplyDamage(damage);
                enemy.AdvanceIntent();
                LastMessage = $"{enemy.Name} 对前排 {target.Name} 造成 {damage} 点伤害。";
                if (!target.IsAlive) LastMessage += " 目标已倒下。";
                bool finished = TryFinishBattle();
                RefreshView();
                if (finished)
                {
                    enemyTurnRoutine = null;
                    yield break;
                }
                yield return WaitForEnemyAction();
            }

            ActiveEnemy = null;
            SelectedAlly = null;
            TurnNumber++;
            foreach (BattleUnitState ally in allies)
                if (ally.IsAlive) ally.ResetStandby();
            Phase = BattlePhase.PlayerTurn;
            LastMessage = "我方新回合：存活角色可以再次静置。";
            enemyTurnRoutine = null;
            RefreshView();
        }

        private IEnumerator WaitForEnemyAction()
        {
            if (enemyActionDelay > 0f) yield return new WaitForSecondsRealtime(enemyActionDelay);
            else yield return null;
        }

        private bool TryFinishBattle()
        {
            if (FirstAlive(enemies) == null)
            {
                Phase = BattlePhase.Victory;
                LastMessage = "胜利！敌人已全部倒下。";
            }
            else if (FirstAlive(allies) == null)
            {
                Phase = BattlePhase.Defeat;
                LastMessage = "失败：我方角色已全部倒下。";
            }
            else return false;

            SelectedAlly = null;
            ActiveEnemy = null;
            return true;
        }

        private bool CanAct(BattlePhase requiredPhase)
        {
            return isActiveAndEnabled && battleInitialized && Phase == requiredPhase;
        }

        private bool CanUseStandby(BattleUnitState ally)
        {
            return ally != null && allies.Contains(ally) && ally.IsAlive && !ally.HasUsedStandby;
        }

        private static BattleUnitState FirstAlive(List<BattleUnitState> units)
        {
            foreach (BattleUnitState unit in units)
                if (unit.IsAlive) return unit;
            return null;
        }

        private void RefreshView()
        {
            if (view != null) view.Refresh();
        }
    }
}
