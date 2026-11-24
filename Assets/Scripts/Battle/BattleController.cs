using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardCards.Battle
{
    public enum BattlePhase { PlayerTurn, SelectingTarget, Resolving, EnemyTurn, Victory, Defeat, Preparation, Reorganizing }

    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] private BattleDefinition definition;
        [SerializeField] private BattleView view;
        [SerializeField, Min(0f)] private float enemyActionDelay = 0.5f;

        private readonly List<BattleUnitState> allies = new List<BattleUnitState>();
        private readonly List<BattleUnitState> enemies = new List<BattleUnitState>();
        private readonly List<string> recentLogs = new List<string>();
        private readonly List<BattleUnitState> chosenTargets = new List<BattleUnitState>();
        private Coroutine enemyTurnRoutine;
        private int enemyCursor;
        private bool battleInitialized;
        private BattleActionKind pendingAction;
        private BattleActionPlan pendingPlan;

        public BattlePhase Phase { get; private set; } = BattlePhase.Resolving;
        public int TurnNumber { get; private set; }
        public int ActionPoints { get; private set; }
        // List order is the formation. Dead units remain identifiable and do not occupy a living rank.
        public IReadOnlyList<BattleUnitState> Allies => allies;
        public IReadOnlyList<BattleUnitState> Enemies => enemies;
        public BattleUnitState SelectedAlly { get; private set; }
        public BattleUnitState ActiveEnemy { get; private set; }
        public string LastMessage { get; private set; } = string.Empty;
        public InventoryGridState Backpack { get; private set; }
        public bool CanModifyEquipment => isActiveAndEnabled && battleInitialized &&
            (Phase == BattlePhase.Preparation || Phase == BattlePhase.Reorganizing);
        public IReadOnlyList<string> RecentLogs => recentLogs;
        public EffectTarget PendingTargetRule => pendingPlan != null && chosenTargets.Count < pendingPlan.TargetRules.Count
            ? pendingPlan.TargetRules[chosenTargets.Count] : EffectTarget.EnemySingle;

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
            if (!isActiveAndEnabled || !battleInitialized || ally == null || !allies.Contains(ally) || !ally.IsAlive ||
                (Phase != BattlePhase.PlayerTurn && Phase != BattlePhase.Preparation && Phase != BattlePhase.Reorganizing)) return;
            SelectedAlly = ally;
            LastMessage = ally.HasUsedStandby ? $"{ally.Name} 本回合已收手，仍可查看信息。" : $"已选择 {ally.Name}。";
            RefreshView();
        }

        public void BeginStandby()
        {
            BeginAction(BattleActionKind.Standby);
        }

        public bool CanBeginAction(BattleActionKind kind, out string reason)
        {
            if (!CanAct(BattlePhase.PlayerTurn)) return Refuse("当前阶段不能执行行动。", out reason);
            if (!Enum.IsDefined(typeof(BattleActionKind), kind)) return Refuse("未知行动。", out reason);
            if (ActionPoints < ActionCost(kind)) return Refuse("行动点不足。", out reason);
            if (kind == BattleActionKind.Reorganize)
            {
                reason = string.Empty;
                return true;
            }
            if (SelectedAlly == null || !allies.Contains(SelectedAlly) || !SelectedAlly.IsAlive)
                return Refuse("请先选择存活的我方角色。", out reason);
            if (SelectedAlly.HasUsedStandby) return Refuse("该角色静置后本回合已收手。", out reason);
            if ((kind == BattleActionKind.Standby || kind == BattleActionKind.Rotate) && SelectedAlly.IsBack)
                return Refuse("背面不能静置或旋转，只能翻回正面。", out reason);
            if (kind == BattleActionKind.Standby && SelectedAlly.HasChangedFormThisTurn)
                return Refuse("本回合旋转或翻面后不能静置。", out reason);
            return HasLegalPlanTargets(BattleActionResolver.BuildPlan(SelectedAlly, kind), out reason);
        }

        public bool BeginAction(BattleActionKind kind)
        {
            if (!CanBeginAction(kind, out string reason))
            {
                LastMessage = reason;
                RefreshView();
                return false;
            }
            if (kind == BattleActionKind.Reorganize)
            {
                ActionPoints--;
                Phase = BattlePhase.Reorganizing;
                Log("进入整顿：可调整站位和装备，完成后主动退出。");
                RefreshView();
                return true;
            }
            pendingAction = kind;
            pendingPlan = BattleActionResolver.BuildPlan(SelectedAlly, kind);
            chosenTargets.Clear();
            if (pendingPlan.TargetRules.Count == 0)
            {
                CommitAction();
            }
            else
            {
                Phase = BattlePhase.SelectingTarget;
                SetTargetPrompt();
                RefreshView();
            }
            return true;
        }

        public bool CanTarget(BattleUnitState target)
        {
            if (!CanAct(BattlePhase.SelectingTarget) || pendingPlan == null || SelectedAlly == null ||
                !SelectedAlly.IsAlive || target == null || !target.IsAlive ||
                chosenTargets.Count >= pendingPlan.TargetRules.Count) return false;
            return IsValidSingleTarget(pendingPlan, chosenTargets.Count, target);
        }

        public void SelectTarget(BattleUnitState target)
        {
            if (!CanTarget(target)) return;
            chosenTargets.Add(target);
            if (chosenTargets.Count == pendingPlan.TargetRules.Count) CommitAction();
            else
            {
                SetTargetPrompt();
                RefreshView();
            }
        }

        public void CancelSelection()
        {
            if (!CanAct(BattlePhase.SelectingTarget) &&
                !(CanAct(BattlePhase.PlayerTurn) && SelectedAlly != null)) return;
            SelectedAlly = null;
            ClearPendingAction();
            Phase = BattlePhase.PlayerTurn;
            LastMessage = "已取消选择，没有消耗行动点或行动次数。";
            RefreshView();
        }

        public void StartBattle()
        {
            if (!CanAct(BattlePhase.Preparation)) return;
            ActionPoints = 4;
            TurnNumber = 1;
            foreach (BattleUnitState ally in allies) ally.ResetTurn();
            Phase = BattlePhase.PlayerTurn;
            Log("战斗开始：我方回合，行动点 4 / 4。");
            RefreshView();
        }

        public void EndPlayerTurn()
        {
            if (!CanAct(BattlePhase.PlayerTurn)) return;
            SelectedAlly = null;
            ClearPendingAction();
            ActiveEnemy = null;
            enemyCursor = 0;
            ActionPoints = 0;
            Phase = BattlePhase.EnemyTurn;
            Log("敌方回合。");
            RefreshView();
            enemyTurnRoutine = StartCoroutine(RunEnemyTurn());
        }

        public void ExitReorganization()
        {
            if (!CanAct(BattlePhase.Reorganizing)) return;
            Phase = BattlePhase.PlayerTurn;
            Log("已退出整顿。");
            if (ActionPoints == 0) EndPlayerTurn();
            else RefreshView();
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
            if (enemyTurnRoutine != null)
            {
                StopCoroutine(enemyTurnRoutine);
                enemyTurnRoutine = null;
            }
            battleInitialized = false;
            ClearPendingAction();
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
            recentLogs.Clear();
            try
            {
                foreach (AllyDefinition ally in definition.allies) allies.Add(new BattleUnitState(ally));
                foreach (EnemyDefinition enemy in definition.enemies) enemies.Add(new BattleUnitState(enemy));
                Backpack = new InventoryGridState(5, 3, "全队背包");
                if (definition.backpackEquipment != null)
                    foreach (EquipmentDefinition equipment in definition.backpackEquipment)
                        AddBackpackEquipment(equipment);
            }
            catch (ArgumentException exception)
            {
                LastMessage = $"战斗配置错误：{exception.Message}";
                Debug.LogError(LastMessage, this);
                enabled = false;
                RefreshView();
                return;
            }
            TurnNumber = 1;
            ActionPoints = 0;
            SelectedAlly = null;
            ActiveEnemy = null;
            enemyCursor = 0;
            battleInitialized = true;
            Phase = BattlePhase.Preparation;
            Log("战前整备：可调整站位和装备，准备好后开始战斗。");
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
                bool reached = CanReach(enemy, target);
                if (reached) target.ApplyDamage(damage);
                enemy.AdvanceIntent();
                Log(reached ? $"{enemy.Name} 对前排 {target.Name} 造成 {damage} 点伤害。" +
                    (!target.IsAlive ? " 目标已倒下。" : string.Empty) : $"{enemy.Name} 射程内没有目标，跳过行动。");
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
                ally.ResetTurn();
            ActionPoints = 4;
            Phase = BattlePhase.PlayerTurn;
            Log("我方新回合：行动点回满 4，行动标记已清空。");
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
                Log("胜利！敌人已全部倒下。");
            }
            else if (FirstAlive(allies) == null)
            {
                Phase = BattlePhase.Defeat;
                Log("失败：我方角色已全部倒下。");
            }
            else return false;

            SelectedAlly = null;
            ActiveEnemy = null;
            ClearPendingAction();
            return true;
        }

        public int GetLivingOrder(BattleUnitState unit)
        {
            if (unit == null || !unit.IsAlive) return 0;
            List<BattleUnitState> team = unit.IsEnemy ? enemies : allies;
            int order = 0;
            foreach (BattleUnitState candidate in team)
            {
                if (!candidate.IsAlive) continue;
                order++;
                if (ReferenceEquals(candidate, unit)) return order;
            }
            return 0;
        }

        public int GetDistance(BattleUnitState first, BattleUnitState second)
        {
            if (first == null || second == null || first.IsEnemy == second.IsEnemy) return 0;
            int firstOrder = GetLivingOrder(first);
            int secondOrder = GetLivingOrder(second);
            return firstOrder > 0 && secondOrder > 0 ? firstOrder + secondOrder - 1 : 0;
        }

        public bool CanReach(BattleUnitState attacker, BattleUnitState target)
        {
            int distance = GetDistance(attacker, target);
            return distance > 0 && distance <= attacker.AttackRange;
        }

        public bool TryMoveAlly(BattleUnitState unit, int livingIndex, out string error)
        {
            if (!CanModifyEquipment) return OperationFailed("仅整备或整顿时可以调整站位。", out error);
            if (unit == null || !unit.IsAlive || !allies.Contains(unit))
                return OperationFailed("只能移动当前队伍的存活角色。", out error);
            var living = new List<BattleUnitState>();
            foreach (BattleUnitState ally in allies)
                if (ally.IsAlive) living.Add(ally);
            if (livingIndex < 0 || livingIndex >= living.Count)
                return OperationFailed("目标位次不存在。", out error);
            living.Remove(unit);
            living.Insert(livingIndex, unit);
            int cursor = 0;
            for (int index = 0; index < allies.Count; index++)
                if (allies[index].IsAlive) allies[index] = living[cursor++];
            error = string.Empty;
            Log($"{unit.Name} 已移动至 {livingIndex + 1} 号位。");
            RefreshView();
            return true;
        }

        public bool TryMoveEquipment(EquipmentInstance item, InventoryGridState source,
            InventoryGridState destination, int x, int y, bool rotated, out string error)
        {
            if (!AuthorizeEquipmentGrid(source, out error) || !AuthorizeEquipmentGrid(destination, out error))
                return OperationFailed(error, out error);
            if (!InventoryGridState.TryTransfer(source, destination, item, x, y, rotated, out error))
                return OperationFailed(error, out error);
            source.Owner?.RecalculateStats();
            if (!ReferenceEquals(source.Owner, destination.Owner)) destination.Owner?.RecalculateStats();
            Log($"{item.Definition.displayName} 已移至{destination.Name}。");
            RefreshView();
            return true;
        }

        public bool TryRotateEquipment(EquipmentInstance item, InventoryGridState grid, out string error)
        {
            if (!AuthorizeEquipmentGrid(grid, out error)) return OperationFailed(error, out error);
            InventoryPlacement placement = grid.Find(item);
            if (placement == null) return OperationFailed("来源网格中没有这件装备。", out error);
            return TryMoveEquipment(item, grid, grid, placement.X, placement.Y, !item.Rotated, out error);
        }

        public bool TryAutoMoveEquipment(EquipmentInstance item, InventoryGridState source,
            BattleUnitState recipient, out string error)
        {
            if (!AuthorizeEquipmentGrid(source, out error)) return OperationFailed(error, out error);
            if (item == null || source.Find(item) == null)
                return OperationFailed("来源网格中没有这件装备。", out error);
            if (ReferenceEquals(source, Backpack))
            {
                if (recipient == null || !recipient.IsAlive || !allies.Contains(recipient))
                    return OperationFailed("请先选择接收装备的存活我方角色。", out error);
                foreach (InventoryGridState destination in recipient.EquipmentGrids)
                {
                    if (destination.AllowedKind != item.Definition.kind) continue;
                    if (TryFindDestination(item, destination, out int x, out int y, out bool rotated))
                        return TryMoveEquipment(item, source, destination, x, y, rotated, out error);
                }
                return OperationFailed("对应装备槽没有空位，请先卸下当前装备。", out error);
            }
            else if (TryFindDestination(item, Backpack, out int x, out int y, out bool rotated))
            {
                return TryMoveEquipment(item, source, Backpack, x, y, rotated, out error);
            }
            return OperationFailed("目标区域没有足够的连续空格。", out error);
        }

        private bool AuthorizeEquipmentGrid(InventoryGridState grid, out string reason)
        {
            if (!CanModifyEquipment) return Refuse("当前只能查看装备，请先进入整顿。", out reason);
            if (grid == null) return Refuse("网格不存在。", out reason);
            if (ReferenceEquals(grid, Backpack))
            {
                reason = string.Empty;
                return true;
            }
            BattleUnitState owner = grid.Owner;
            if (owner == null || !owner.IsAlive || !allies.Contains(owner))
                return Refuse("不能操作非本局或已倒下角色的装备。", out reason);
            foreach (InventoryGridState equipmentGrid in owner.EquipmentGrids)
                if (ReferenceEquals(equipmentGrid, grid))
                {
                    reason = string.Empty;
                    return true;
                }
            return Refuse("网格不属于当前战斗。", out reason);
        }

        private static bool TryFindDestination(EquipmentInstance item, InventoryGridState destination,
            out int x, out int y, out bool rotated)
        {
            rotated = item.Rotated;
            if (destination.TryFindSpace(item, rotated, out x, out y)) return true;
            if (item.Definition.canRotate)
            {
                rotated = !rotated;
                if (destination.TryFindSpace(item, rotated, out x, out y)) return true;
            }
            return false;
        }

        private bool HasLegalPlanTargets(BattleActionPlan plan, out string reason)
        {
            for (int index = 0; index < plan.TargetRules.Count; index++)
            {
                List<BattleUnitState> team = plan.TargetRules[index] == EffectTarget.EnemySingle ? enemies : allies;
                bool found = false;
                foreach (BattleUnitState candidate in team)
                    if (IsValidSingleTarget(plan, index, candidate)) { found = true; break; }
                if (!found) return Refuse("当前射程内没有合法目标。", out reason);
            }
            foreach (BattleEffectStep step in plan.Steps)
            {
                if (step.Target != EffectTarget.EnemyAll || step.Kind != EffectKind.Damage) continue;
                bool found = false;
                foreach (BattleUnitState enemy in enemies)
                    if (CanReach(SelectedAlly, enemy)) { found = true; break; }
                if (!found) return Refuse("当前射程内没有合法目标。", out reason);
            }
            reason = string.Empty;
            return true;
        }

        private bool IsValidSingleTarget(BattleActionPlan plan, int targetIndex, BattleUnitState target)
        {
            if (target == null || !target.IsAlive || SelectedAlly == null) return false;
            EffectTarget rule = plan.TargetRules[targetIndex];
            bool enemyTarget = rule == EffectTarget.EnemySingle;
            if (enemyTarget ? !enemies.Contains(target) : !allies.Contains(target)) return false;
            foreach (BattleEffectStep step in plan.Steps)
                if (step.TargetIndex == targetIndex && step.Kind == EffectKind.Damage && enemyTarget &&
                    !CanReach(SelectedAlly, target)) return false;
            return true;
        }

        private void CommitAction()
        {
            BattleUnitState actor = SelectedAlly;
            if (actor == null || !actor.IsAlive || pendingPlan == null ||
                chosenTargets.Count != pendingPlan.TargetRules.Count) return;
            // Rebuild the numeric snapshot at the commit point. Selecting targets has no side effects.
            BattleActionPlan plan = BattleActionResolver.BuildPlan(actor, pendingAction);
            if (plan.TargetRules.Count != chosenTargets.Count) { CancelSelection(); return; }
            for (int index = 0; index < chosenTargets.Count; index++)
                if (!IsValidSingleTarget(plan, index, chosenTargets[index]))
                {
                    CancelSelection();
                    return;
                }
            BattleActionKind action = pendingAction;
            var targets = new List<BattleUnitState>(chosenTargets);
            // Lock before changing any number: another click cannot commit this action twice.
            Phase = BattlePhase.Resolving;
            ClearPendingAction();
            ActionPoints -= ActionCost(action);
            Log($"{actor.Name} {ActionName(action)}。");
            foreach (BattleEffectStep step in plan.Steps)
            {
                if (!actor.IsAlive) break;
                foreach (BattleUnitState target in ResolveTargets(actor, step, targets))
                {
                    if (!target.IsAlive) continue;
                    // A previous effect may have removed a front unit. Always query current compact ranks.
                    if (step.Kind == EffectKind.Damage && target.IsEnemy != actor.IsEnemy && !CanReach(actor, target))
                        continue;
                    BattleActionResolver.Apply(step, target);
                    Log($"{step.Source} → {target.Name}：{EffectName(step.Kind)} {step.Value}" +
                        (!target.IsAlive ? "，目标已倒下。" : "。"));
                }
            }
            switch (action)
            {
                case BattleActionKind.Standby: actor.UseStandby(); break;
                case BattleActionKind.Rotate: actor.Rotate(); break;
                case BattleActionKind.Flip: actor.Flip(); break;
            }
            if (TryFinishBattle()) { RefreshView(); return; }
            Phase = BattlePhase.PlayerTurn;
            if (!actor.IsAlive) SelectedAlly = null;
            if (ActionPoints == 0) EndPlayerTurn();
            else RefreshView();
        }

        private IEnumerable<BattleUnitState> ResolveTargets(BattleUnitState actor, BattleEffectStep step,
            List<BattleUnitState> targets)
        {
            switch (step.Target)
            {
                case EffectTarget.Self:
                    yield return actor;
                    break;
                case EffectTarget.AllySingle:
                case EffectTarget.EnemySingle:
                    yield return targets[step.TargetIndex];
                    break;
                case EffectTarget.AllyAll:
                    foreach (BattleUnitState ally in allies) yield return ally;
                    break;
                case EffectTarget.EnemyAll:
                    foreach (BattleUnitState enemy in enemies) yield return enemy;
                    break;
            }
        }

        private void AddBackpackEquipment(EquipmentDefinition equipment)
        {
            if (equipment == null) throw new ArgumentException("初始背包包含空装备。");
            var item = new EquipmentInstance(equipment);
            if (TryFindDestination(item, Backpack, out int x, out int y, out bool rotated) &&
                Backpack.TryAdd(item, x, y, rotated, out _)) return;
            throw new ArgumentException($"初始背包无法放入“{equipment.displayName}”。");
        }

        private void SetTargetPrompt()
        {
            string team = PendingTargetRule == EffectTarget.AllySingle ? "我方" : "敌方";
            LastMessage = $"为 {SelectedAlly.Name} 选择{team}目标（{chosenTargets.Count + 1}/{pendingPlan.TargetRules.Count}）；可取消。";
        }

        private void ClearPendingAction()
        {
            pendingPlan = null;
            chosenTargets.Clear();
        }

        private void Log(string message)
        {
            LastMessage = message;
            recentLogs.Add(message);
            if (recentLogs.Count > 6) recentLogs.RemoveAt(0);
        }

        private bool OperationFailed(string message, out string error)
        {
            error = message;
            LastMessage = message;
            RefreshView();
            return false;
        }

        private static bool Refuse(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static int ActionCost(BattleActionKind kind)
        {
            return kind == BattleActionKind.Flip ? 2 : kind == BattleActionKind.Standby ? 0 : 1;
        }

        private static string ActionName(BattleActionKind kind)
        {
            return kind == BattleActionKind.Standby ? "静置" : kind == BattleActionKind.Rotate ? "旋转" : "翻面";
        }

        private static string EffectName(EffectKind kind)
        {
            return kind == EffectKind.Damage ? "伤害" : kind == EffectKind.Heal ? "回复生命" : "增加护甲";
        }

        private bool CanAct(BattlePhase requiredPhase)
        {
            return isActiveAndEnabled && battleInitialized && Phase == requiredPhase;
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
