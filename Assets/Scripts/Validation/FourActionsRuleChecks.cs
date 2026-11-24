#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CardCards.Battle;
using UnityEngine;

namespace CardCards.Validation
{
    // Real runtime rule checks. Fixtures exist only in memory and never author assets.
    public static class FourActionsRuleChecks
    {
        public static IEnumerator Run(BattleController controller, List<string> checks)
        {
            Require(controller != null && checks != null, "Rule verification requires a controller and report.");
            BattleDefinition original = ReadField<BattleDefinition>(controller, "definition");
            string originalJson = JsonUtility.ToJson(original);
            float originalDelay = ReadField<float>(controller, "enemyActionDelay");
            var temporary = new List<UnityEngine.Object>();
            SetField(controller, "enemyActionDelay", 0f);
            try
            {
                BattleDefinition formation = MakeBattle(temporary, 4, 4);
                for (int i = 0; i < 4; i++) formation.allies[i].attackRange = i + 2;
                Load(controller, formation);
                Require(controller.Phase == BattlePhase.Preparation && controller.CanModifyEquipment &&
                    controller.Allies.Count == 4 && controller.Enemies.Count == 4, "4v4 preparation failed.");
                for (int i = 0; i < 4; i++)
                    Require(controller.GetLivingOrder(controller.Allies[i]) == i + 1 &&
                        controller.GetLivingOrder(controller.Enemies[i]) == i + 1, "Living order must start at one.");
                Require(controller.GetDistance(controller.Allies[3], controller.Enemies[3]) == 7,
                    "Fourth-to-fourth distance must be seven.");
                Require(controller.CanReach(controller.Allies[0], controller.Enemies[1]) &&
                    !controller.CanReach(controller.Allies[0], controller.Enemies[2]) &&
                    controller.CanReach(controller.Allies[3], controller.Enemies[1]) &&
                    !controller.CanReach(controller.Allies[3], controller.Enemies[2]), "Per-character ranges were not enforced.");
                controller.StartBattle();
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4 &&
                    !controller.CanModifyEquipment, "Battle must start with four shared action points.");
                BattleUnitState actor = controller.Allies[0];
                BattleUnitState target = controller.Enemies[0];
                int originalTargetHealth = target.CurrentHealth;
                controller.SelectAlly(actor);
                Require(controller.BeginAction(BattleActionKind.Rotate) && controller.Phase == BattlePhase.SelectingTarget,
                    "Rotation did not request a target.");
                Require(controller.ActionPoints == 4 && !actor.IsRotated && !actor.HasChangedFormThisTurn,
                    "Selecting an action must not spend resources or change form.");
                Require(!controller.CanTarget(controller.Enemies[2]), "Out-of-range enemy is targetable.");
                controller.SelectTarget(controller.Enemies[2]);
                Require(controller.Phase == BattlePhase.SelectingTarget && controller.ActionPoints == 4 &&
                    target.CurrentHealth == originalTargetHealth, "An out-of-range click committed an action.");
                controller.CancelSelection();
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4 &&
                    !actor.IsRotated && !actor.HasChangedFormThisTurn && !actor.HasUsedStandby &&
                    target.CurrentHealth == originalTargetHealth, "Cancel changed the pending action's state.");
                checks.Add("4v4 preparation, one-based positions, distance 1-7, fixed character ranges, AP4, and cancel without mutation");

                Submit(controller, actor, BattleActionKind.Rotate, target);
                Require(actor.IsRotated && actor.HasChangedFormThisTurn && controller.ActionPoints == 3 &&
                    target.CurrentHealth == originalTargetHealth - 7, "First rotation did not use the lower profession.");
                controller.SelectTarget(target);
                Require(target.CurrentHealth == originalTargetHealth - 7 && controller.ActionPoints == 3,
                    "Repeated target submission committed the action twice.");
                controller.SelectAlly(actor);
                Require(!controller.CanBeginAction(BattleActionKind.Standby, out _), "Rotation must forbid standby this turn.");
                Submit(controller, actor, BattleActionKind.Rotate, target);
                Require(!actor.IsRotated && controller.ActionPoints == 2 &&
                    target.CurrentHealth == originalTargetHealth - 17, "Repeated rotation must alternate professions.");
                Submit(controller, actor, BattleActionKind.Flip, target);
                Require(actor.IsBack && controller.ActionPoints == 0, "Flip must cost two action points.");
                yield return WaitForEnemy(controller);
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.TurnNumber == 2 && controller.ActionPoints == 4 &&
                    !actor.HasChangedFormThisTurn, "AP depletion must advance the turn and reset action marks.");
                controller.SelectAlly(actor);
                Require(!controller.CanBeginAction(BattleActionKind.Standby, out _) &&
                    !controller.CanBeginAction(BattleActionKind.Rotate, out _), "Back form must reject standby and rotation.");
                int beforeReturn = target.CurrentHealth;
                Submit(controller, actor, BattleActionKind.Flip, target);
                Require(!actor.IsBack && !actor.IsRotated && controller.ActionPoints == 2 &&
                    target.CurrentHealth == beforeReturn - 14, "Returning from back must trigger both front professions.");
                checks.Add("Alternating repeated rotations, one commit per action, flip AP2, automatic enemy turn at AP0, back restrictions, and two-profession return");

                BattleDefinition locking = MakeBattle(temporary, 2, 2);
                Load(controller, locking);
                controller.StartBattle();
                actor = controller.Allies[0];
                Submit(controller, actor, BattleActionKind.Standby, controller.Enemies[0]);
                controller.SelectAlly(actor);
                Require(actor.HasUsedStandby && controller.ActionPoints == 4 &&
                    !controller.CanBeginAction(BattleActionKind.Standby, out _) &&
                    !controller.CanBeginAction(BattleActionKind.Rotate, out _) &&
                    !controller.CanBeginAction(BattleActionKind.Flip, out _), "Standby must end that character's skills for this turn.");
                Require(controller.BeginAction(BattleActionKind.Reorganize) && controller.ActionPoints == 3 &&
                    controller.Phase == BattlePhase.Reorganizing, "A resting character must not block team reorganization.");
                Require(controller.TryMoveAlly(actor, 1, out _) && controller.GetLivingOrder(actor) == 2,
                    "A resting character must remain movable during reorganization.");
                controller.ExitReorganization();
                controller.EndPlayerTurn();
                yield return WaitForEnemy(controller);
                controller.SelectAlly(actor);
                Require(!actor.HasUsedStandby && controller.CanBeginAction(BattleActionKind.Standby, out _) &&
                    controller.ActionPoints == 4, "A new turn must clear standby lock and refill AP.");
                checks.Add("Free standby locks that character's skills, team reorganization still works, and new turns reset locks");

                BattleDefinition forms = MakeBattle(temporary, 1, 2);
                Load(controller, forms);
                controller.StartBattle();
                actor = controller.Allies[0];
                target = controller.Enemies[0];
                Submit(controller, actor, BattleActionKind.Rotate, target);
                Require(actor.IsRotated && actor.MaxHealth == 150 && actor.CurrentHealth == 100 &&
                    actor.MaxArmor == 30 && actor.CurrentArmor == 20, "Increasing form maxima must not restore health or armor.");
                Submit(controller, actor, BattleActionKind.Flip, target);
                Require(actor.IsBack && actor.IsRotated && actor.MaxHealth == 50 && actor.CurrentHealth == 50 &&
                    actor.MaxArmor == 10 && actor.CurrentArmor == 10, "Lower form maxima must clamp current values.");
                controller.SelectAlly(actor);
                Require(!controller.CanBeginAction(BattleActionKind.Flip, out _) && controller.ActionPoints == 1,
                    "Insufficient AP must reject flip.");
                controller.EndPlayerTurn();
                yield return WaitForEnemy(controller);
                Submit(controller, actor, BattleActionKind.Flip, target);
                Require(!actor.IsBack && actor.IsRotated && actor.MaxHealth == 150 && actor.CurrentHealth == 50 &&
                    actor.MaxArmor == 30 && actor.CurrentArmor == 10, "Flip return must preserve orientation and depleted current values.");
                checks.Add("Form health/armor clamps, no refill when maxima grow, insufficient-AP rejection, and rotated orientation restored from back");

                EquipmentDefinition snapshotSword = MakeEquipment(temporary, "Snapshot sword", EquipmentSlotKind.Handheld, 2, 1);
                snapshotSword.top.attack = 2;
                snapshotSword.top.standby = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, 3, 0.5f) };
                snapshotSword.bottom.attack = 1;
                snapshotSword.bottom.rotation = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, 2, 0.5f) };
                BattleDefinition snapshots = MakeBattle(temporary, 1, 2);
                snapshots.allies[0].frontTop.initialAttack = 8;
                snapshots.allies[0].frontTop.standby = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, -2) };
                snapshots.allies[0].frontBottom.attackMultiplier = 0.9f;
                snapshots.allies[0].startingEquipment = new[] { snapshotSword };
                Load(controller, snapshots);
                controller.StartBattle();
                actor = controller.Allies[0];
                target = controller.Enemies[0];
                Require(actor.Attack == 10 && actor.StandbyDamage == 16, "Equipped opening attack/standby must be 10/16.");
                beforeReturn = target.CurrentHealth;
                Submit(controller, actor, BattleActionKind.Rotate, target);
                Require(target.CurrentHealth == beforeReturn - 12 && actor.Attack == 4 && actor.IsRotated,
                    "Rotation snapshot must use pre-turn equipment stats with lower profession/equipment triggers (12 damage, then attack4).");
                Submit(controller, actor, BattleActionKind.Rotate, target);
                Require(target.CurrentHealth == beforeReturn - 21 && actor.Attack == 10 && !actor.IsRotated,
                    "Second rotation must use the old lower equipment stats (9 damage, then attack10).");
                checks.Add("Equipment/profession snapshot ordering, 16 standby preview, rotation damage12/9, and resulting attack4/10");

                BattleDefinition multiTarget = MakeBattle(temporary, 2, 2);
                multiTarget.allies[0].frontTop.standby = new[]
                {
                    Effect(EffectKind.Damage, EffectTarget.EnemySingle, 0),
                    Effect(EffectKind.Heal, EffectTarget.AllySingle, 5, 0f),
                    Effect(EffectKind.GainArmor, EffectTarget.AllyAll, 3, 0f)
                };
                Load(controller, multiTarget);
                controller.StartBattle();
                actor = controller.Allies[0];
                actor.ApplyDamage(25); // Fixture precondition: armor0 and health95.
                target = controller.Enemies[0];
                beforeReturn = target.CurrentHealth;
                controller.SelectAlly(actor);
                Require(controller.BeginAction(BattleActionKind.Standby), "Mixed-target standby did not start.");
                controller.SelectTarget(target);
                Require(controller.Phase == BattlePhase.SelectingTarget && target.CurrentHealth == beforeReturn &&
                    !actor.HasUsedStandby && actor.CurrentHealth == 95, "A partial mixed-target selection must not commit effects.");
                controller.CancelSelection();
                Require(controller.Phase == BattlePhase.PlayerTurn && target.CurrentHealth == beforeReturn &&
                    !actor.HasUsedStandby && actor.CurrentHealth == 95 && actor.CurrentArmor == 0,
                    "Cancel after one target must roll back the entire uncommitted action.");
                controller.SelectAlly(actor);
                Require(controller.BeginAction(BattleActionKind.Standby), "Mixed-target retry did not start.");
                controller.SelectTarget(target);
                controller.SelectTarget(actor);
                Require(controller.Phase == BattlePhase.PlayerTurn && target.CurrentHealth == beforeReturn - 10 &&
                    actor.CurrentHealth == 100 && actor.CurrentArmor == 3 && actor.HasUsedStandby && controller.ActionPoints == 4,
                    "Damage, ally heal, and whole-team armor must resolve only after all targets are selected.");
                checks.Add("Mixed enemy/ally target selection is atomic; damage, heal, armor, and cancellation before final target");

                yield return CheckAutomaticTargetRules(controller, temporary, checks);

                BattleDefinition noOverflow = MakeBattle(temporary, 4, 2);
                noOverflow.enemies[0].maxHealth = 5;
                noOverflow.enemies[1].maxHealth = 100;
                noOverflow.allies[0].frontTop.standby = new[]
                {
                    Effect(EffectKind.Damage, EffectTarget.EnemySingle, 0),
                    Effect(EffectKind.Damage, EffectTarget.EnemySingle, 0)
                };
                Load(controller, noOverflow);
                controller.StartBattle();
                target = controller.Enemies[0];
                Submit(controller, controller.Allies[0], BattleActionKind.Standby, target);
                Require(!target.IsAlive && controller.GetLivingOrder(target) == 0 &&
                    controller.Enemies[1].CurrentHealth == 100 && controller.GetLivingOrder(controller.Enemies[1]) == 1 &&
                    controller.GetDistance(controller.Allies[3], controller.Enemies[1]) == 4,
                    "Dead-target later hits must stop without overflow, and living enemies must compact immediately.");
                checks.Add("Multi-hit single-target death skips later hits without spillover; death compacts positions and distances");

                BattleDefinition movement = MakeBattle(temporary, 4, 1);
                Load(controller, movement);
                BattleUnitState first = controller.Allies[0];
                BattleUnitState second = controller.Allies[1];
                BattleUnitState third = controller.Allies[2];
                BattleUnitState fourth = controller.Allies[3];
                Require(controller.TryMoveAlly(fourth, 1, out _) && controller.GetLivingOrder(first) == 1 &&
                    controller.GetLivingOrder(fourth) == 2 && controller.GetLivingOrder(second) == 3 &&
                    controller.GetLivingOrder(third) == 4, "Moving a card must insert it while preserving other cards' order.");
                controller.StartBattle();
                Require(!controller.TryMoveAlly(first, 3, out _), "Ordinary combat must reject formation changes.");
                controller.SelectAlly(first);
                Require(controller.BeginAction(BattleActionKind.Reorganize) && controller.ActionPoints == 3,
                    "Reorganization must cost one AP at entry.");
                Require(controller.TryMoveAlly(first, 3, out _) && controller.ActionPoints == 3,
                    "Moving inside reorganization must not charge a second time.");
                Require(!controller.BeginAction(BattleActionKind.Rotate) && controller.Phase == BattlePhase.Reorganizing,
                    "Reorganization must block skill actions until explicit exit.");
                controller.ExitReorganization();
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 3,
                    "Exiting reorganization must be free.");
                checks.Add("Insertion-based movement, stable relative order, phase authorization, one-AP reorganization entry, and free exit");

                yield return CheckMovementAfterDeath(controller, temporary, checks);

                BattleDefinition single = MakeBattle(temporary, 1, 1);
                EquipmentDefinition singleGear = MakeEquipment(temporary, "Single survivor gear", EquipmentSlotKind.Accessory, 1, 1);
                single.backpackEquipment = new[] { singleGear };
                Load(controller, single);
                controller.StartBattle();
                actor = controller.Allies[0];
                target = controller.Enemies[0];
                for (int i = 0; i < 3; i++) Submit(controller, actor, BattleActionKind.Rotate, target);
                controller.SelectAlly(actor);
                Require(controller.BeginAction(BattleActionKind.Reorganize) && controller.ActionPoints == 0 &&
                    controller.Phase == BattlePhase.Reorganizing && controller.CanModifyEquipment,
                    "One survivor must be able to spend the final AP on equipment reorganization.");
                EquipmentInstance singleItem = controller.Backpack.Placements[0].Item;
                Require(controller.TryAutoMoveEquipment(singleItem, controller.Backpack, actor, out _) &&
                    singleItem.Grid.Owner == actor, "A single survivor must be able to equip in reorganization.");
                controller.EndPlayerTurn();
                yield return null;
                yield return null;
                Require(controller.Phase == BattlePhase.Reorganizing && controller.TurnNumber == 1,
                    "AP0 reorganization must wait for explicit exit before enemies act.");
                controller.ExitReorganization();
                yield return WaitForEnemy(controller);
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.TurnNumber == 2 && controller.ActionPoints == 4,
                    "Exiting AP0 reorganization must advance through enemies to a new turn.");
                checks.Add("Single-survivor equipment reorganization, AP0 waits for explicit exit, then enemy turn and AP refill");

                yield return CheckEquipmentTransactions(controller, temporary, checks);

                BattleDefinition enemyRange = MakeBattle(temporary, 2, 2);
                enemyRange.enemies[0].attackRange = 1;
                enemyRange.enemies[0].intentDamage = new[] { 3, 5 };
                enemyRange.enemies[1].attackRange = 1;
                enemyRange.enemies[1].intentDamage = new[] { 1000, 1000 };
                Load(controller, enemyRange);
                controller.StartBattle();
                controller.EndPlayerTurn();
                yield return WaitForEnemy(controller);
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.Allies[0].CurrentHealth == 100 &&
                    controller.Allies[0].CurrentArmor == 17 && controller.Allies[1].CurrentArmor == 20,
                    "An enemy beyond its attack range must not hit; an in-range enemy must hit the front.");
                checks.Add("Enemy attack range is enforced; unreachable enemies skip without damaging allies");

                yield return CheckEarlyTurnEnd(controller, temporary, checks);
                yield return CheckEnemyTurnDisableResume(controller, temporary, checks);

                BattleDefinition defeat = MakeBattle(temporary, 2, 2);
                for (int i = 0; i < 2; i++)
                {
                    defeat.allies[i].baseHealth = 4;
                    defeat.allies[i].baseArmor = 0;
                    defeat.enemies[i].intentDamage = new[] { 10 };
                }
                Load(controller, defeat);
                controller.StartBattle();
                controller.EndPlayerTurn();
                yield return WaitForEnemy(controller);
                Require(controller.Phase == BattlePhase.Defeat && !controller.Allies[0].IsAlive &&
                    !controller.Allies[1].IsAlive && controller.TurnNumber == 1,
                    "Enemies must retarget after each front death and stop at defeat without starting another turn.");
                Require(!controller.CanModifyEquipment && !controller.BeginAction(BattleActionKind.Rotate),
                    "Defeat must lock gameplay and equipment changes.");
                controller.RestartBattle();
                Require(controller.Phase == BattlePhase.Preparation && controller.Allies[0].CurrentHealth == 4 &&
                    controller.Allies[1].CurrentHealth == 4, "Defeat restart must restore the fixture and return to preparation.");

                BattleDefinition victory = MakeBattle(temporary, 1, 1);
                victory.enemies[0].maxHealth = 1;
                Load(controller, victory);
                controller.StartBattle();
                Submit(controller, controller.Allies[0], BattleActionKind.Standby, controller.Enemies[0]);
                Require(controller.Phase == BattlePhase.Victory && !controller.CanModifyEquipment,
                    "The final enemy death must immediately lock the victory state.");
                controller.EndPlayerTurn();
                Require(controller.Phase == BattlePhase.Victory, "Victory accepted an end-turn input.");
                controller.RestartBattle();
                Require(controller.Phase == BattlePhase.Preparation && controller.Enemies[0].CurrentHealth == 1 &&
                    !controller.Allies[0].HasUsedStandby, "Victory restart must reset forms, action locks, and enemies.");
                checks.Add("Per-hit front retargeting, immediate defeat/victory, terminal locks, and both restart paths return to preparation");
            }
            finally
            {
                SetField(controller, "enemyActionDelay", originalDelay);
                SetField(controller, "definition", original);
                Initialize(controller);
                foreach (UnityEngine.Object resource in temporary) UnityEngine.Object.Destroy(resource);
                Require(JsonUtility.ToJson(original) == originalJson, "Rule checks changed the authored battle definition.");
            }
            checks.Add("Original authored definition restored in finally; JSON unchanged and temporary fixtures destroyed");
        }

        private static IEnumerator CheckAutomaticTargetRules(BattleController controller,
            List<UnityEngine.Object> temporary, List<string> checks)
        {
            BattleDefinition self = MakeBattle(temporary, 2, 1);
            self.allies[0].frontTop.standby = new[]
            {
                Effect(EffectKind.Heal, EffectTarget.Self, 3, 0f),
                Effect(EffectKind.GainArmor, EffectTarget.Self, 4, 0f)
            };
            Load(controller, self);
            controller.StartBattle();
            BattleUnitState actor = controller.Allies[0];
            BattleUnitState companion = controller.Allies[1];
            actor.ApplyDamage(25); // Armor0, health95.
            companion.ApplyDamage(35); // Armor0, health85.
            controller.SelectAlly(actor);
            Require(controller.BeginAction(BattleActionKind.Standby) && controller.Phase == BattlePhase.PlayerTurn &&
                actor.CurrentHealth == 98 && actor.CurrentArmor == 4 && actor.HasUsedStandby &&
                companion.CurrentHealth == 85 && companion.CurrentArmor == 0 && controller.ActionPoints == 4,
                "Self effects must resolve without target selection and must not affect another ally.");
            checks.Add("Self target: automatic heal/armor affects only the actor, commits standby once, and spends no AP");

            BattleDefinition allAllies = MakeBattle(temporary, 3, 1);
            allAllies.allies[0].frontTop.standby = new[]
            {
                Effect(EffectKind.Heal, EffectTarget.AllyAll, 8, 0f),
                Effect(EffectKind.GainArmor, EffectTarget.AllyAll, 3, 0f)
            };
            Load(controller, allAllies);
            controller.StartBattle();
            actor = controller.Allies[0];
            companion = controller.Allies[1];
            BattleUnitState dead = controller.Allies[2];
            actor.ApplyDamage(27); // Armor0, health93.
            companion.ApplyDamage(40); // Armor0, health80.
            dead.ApplyDamage(1000);
            controller.SelectAlly(actor);
            Require(controller.BeginAction(BattleActionKind.Standby) && controller.Phase == BattlePhase.PlayerTurn &&
                actor.CurrentHealth == 100 && actor.CurrentArmor == 3 && companion.CurrentHealth == 88 &&
                companion.CurrentArmor == 3 && dead.CurrentHealth == 0 && dead.CurrentArmor == 0 && !dead.IsAlive &&
                controller.Enemies[0].CurrentHealth == 1000 && controller.ActionPoints == 4,
                "AllyAll must include every living ally, cap healing at max health, and skip dead allies and enemies.");
            checks.Add("AllyAll target: automatic heal/armor reaches every living teammate, clamps health, and never revives or affects enemies");

            BattleDefinition allEnemies = MakeBattle(temporary, 2, 4);
            allEnemies.allies[0].attackRange = 2;
            allEnemies.allies[0].frontTop.standby = new[]
            {
                Effect(EffectKind.Damage, EffectTarget.EnemyAll, 0)
            };
            allEnemies.enemies[0].maxArmor = 2;
            allEnemies.enemies[1].maxArmor = 4;
            Load(controller, allEnemies);
            controller.StartBattle();
            actor = controller.Allies[0];
            controller.SelectAlly(actor);
            Require(controller.BeginAction(BattleActionKind.Standby) && controller.Phase == BattlePhase.PlayerTurn &&
                controller.Enemies[0].CurrentHealth == 992 && controller.Enemies[0].CurrentArmor == 0 &&
                controller.Enemies[1].CurrentHealth == 994 && controller.Enemies[1].CurrentArmor == 0 &&
                controller.Enemies[2].CurrentHealth == 1000 && controller.Enemies[3].CurrentHealth == 1000 &&
                controller.Allies[0].CurrentHealth == 100 && controller.Allies[1].CurrentHealth == 100 &&
                controller.ActionPoints == 4 && actor.HasUsedStandby,
                "EnemyAll must automatically hit each reachable enemy once, apply armor individually, and exclude allies/out-of-range enemies.");

            BattleDefinition unreachable = MakeBattle(temporary, 4, 2);
            unreachable.allies[3].attackRange = 1;
            unreachable.allies[3].frontTop.standby = allEnemies.allies[0].frontTop.standby;
            Load(controller, unreachable);
            controller.StartBattle();
            actor = controller.Allies[3];
            controller.SelectAlly(actor);
            Require(!controller.CanBeginAction(BattleActionKind.Standby, out _) &&
                !controller.BeginAction(BattleActionKind.Standby) && controller.Phase == BattlePhase.PlayerTurn &&
                !actor.HasUsedStandby && controller.ActionPoints == 4 &&
                controller.Enemies[0].CurrentHealth == 1000 && controller.Enemies[1].CurrentHealth == 1000,
                "An EnemyAll action with no reachable targets must be rejected without spending AP or standby eligibility.");
            checks.Add("EnemyAll target: automatic multi-enemy damage respects individual armor and range; no reachable target rejects without mutation");
            yield break;
        }

        private static IEnumerator CheckMovementAfterDeath(BattleController controller,
            List<UnityEngine.Object> temporary, List<string> checks)
        {
            BattleDefinition definition = MakeBattle(temporary, 4, 2);
            definition.allies[3].attackRange = 2;
            Load(controller, definition);
            BattleUnitState dead = controller.Allies[0];
            BattleUnitState second = controller.Allies[1];
            BattleUnitState third = controller.Allies[2];
            BattleUnitState mover = controller.Allies[3];
            BattleUnitState enemy = controller.Enemies[0];
            dead.ApplyDamage(1000);
            Require(controller.GetLivingOrder(dead) == 0 && controller.GetLivingOrder(second) == 1 &&
                controller.GetLivingOrder(third) == 2 && controller.GetLivingOrder(mover) == 3 &&
                controller.GetDistance(mover, enemy) == 3 && !controller.CanReach(mover, enemy),
                "A dead ally must release its rank immediately; range must use compact living positions.");
            Require(controller.TryMoveAlly(mover, 0, out _) && controller.GetLivingOrder(mover) == 1 &&
                controller.GetLivingOrder(second) == 2 && controller.GetLivingOrder(third) == 3 &&
                controller.GetLivingOrder(dead) == 0 && controller.GetDistance(mover, enemy) == 1 &&
                controller.CanReach(mover, enemy), "Preparation reorder after death did not update rank and reach.");
            controller.StartBattle();
            controller.SelectAlly(mover);
            Require(controller.CanBeginAction(BattleActionKind.Rotate, out _) &&
                controller.BeginAction(BattleActionKind.Reorganize) &&
                controller.TryMoveAlly(mover, 2, out _) && controller.ActionPoints == 3 &&
                controller.GetLivingOrder(second) == 1 && controller.GetLivingOrder(third) == 2 &&
                controller.GetLivingOrder(mover) == 3 && controller.GetLivingOrder(dead) == 0 &&
                !controller.CanReach(mover, enemy),
                "Reorganization after death must preserve survivors' order and update range without charging per move.");
            Require(!controller.TryMoveAlly(mover, 3, out _) && !controller.TryMoveAlly(dead, 0, out _) &&
                controller.GetLivingOrder(second) == 1 && controller.GetLivingOrder(third) == 2 &&
                controller.GetLivingOrder(mover) == 3 && controller.ActionPoints == 3,
                "Dead-card or out-of-range-index moves must leave the compact formation and AP unchanged.");
            controller.ExitReorganization();
            controller.SelectAlly(mover);
            Require(!controller.CanBeginAction(BattleActionKind.Rotate, out _) && controller.ActionPoints == 3 &&
                controller.Enemies[0].CurrentHealth == 1000 && controller.Enemies[1].CurrentHealth == 1000,
                "After a reorder moves the actor out of reach, damage actions must stop being available immediately.");
            checks.Add("Post-death reorder: dead ranks disappear, survivors preserve order, preparation/reorganization update range immediately, and invalid moves are atomic");
            yield break;
        }

        private static IEnumerator CheckEarlyTurnEnd(BattleController controller,
            List<UnityEngine.Object> temporary, List<string> checks)
        {
            BattleDefinition definition = MakeBattle(temporary, 2, 2);
            Load(controller, definition);
            controller.StartBattle();
            BattleUnitState actor = controller.Allies[0];
            Submit(controller, actor, BattleActionKind.Rotate, controller.Enemies[0]);
            Require(controller.ActionPoints == 3 && actor.HasChangedFormThisTurn && controller.TurnNumber == 1,
                "Early-turn-end fixture must leave three unused action points.");
            controller.EndPlayerTurn();
            Require(controller.Phase == BattlePhase.EnemyTurn && controller.ActionPoints == 0 && controller.TurnNumber == 1,
                "Ending a turn early must immediately discard its unused action points.");
            controller.EndPlayerTurn();
            yield return WaitForEnemy(controller);
            Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4 && controller.TurnNumber == 2 &&
                !actor.HasChangedFormThisTurn && !actor.HasUsedStandby,
                "The next turn must refill to four rather than carry over unused points or duplicate the enemy turn.");
            Submit(controller, actor, BattleActionKind.Rotate, controller.Enemies[0]);
            Require(controller.ActionPoints == 3 && controller.TurnNumber == 2,
                "A new-turn action must spend from the refilled four-point pool.");
            checks.Add("Early end turn: unused AP is discarded immediately, duplicate end is ignored, and next turn refills to four without carryover");
        }

        private static IEnumerator CheckEnemyTurnDisableResume(BattleController controller,
            List<UnityEngine.Object> temporary, List<string> checks)
        {
            BattleDefinition definition = MakeBattle(temporary, 2, 3);
            definition.enemies[0].intentDamage = new[] { 3, 30 };
            definition.enemies[1].intentDamage = new[] { 5, 50 };
            definition.enemies[2].intentDamage = new[] { 7, 70 };
            Load(controller, definition);
            float delay = ReadField<float>(controller, "enemyActionDelay");
            try
            {
                // A real delay exposes both an uncommitted attack and a committed cursor checkpoint.
                SetField(controller, "enemyActionDelay", 0.15f);
                controller.StartBattle();
                controller.EndPlayerTurn();
                controller.enabled = false;
                yield return null;
                yield return null;
                Require(controller.Phase == BattlePhase.EnemyTurn && controller.ActiveEnemy == null &&
                    ReadField<int>(controller, "enemyCursor") == 0 && controller.Allies[0].CurrentArmor == 20 &&
                    controller.Enemies[0].NextIntentDamage == 3,
                    "Disabling before the first commit must pause the enemy turn without advancing damage or intent.");
                controller.enabled = true;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (ReadField<int>(controller, "enemyCursor") < 1)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Resumed enemy turn did not commit its first attack.");
                    yield return null;
                }
                Require(ReadField<int>(controller, "enemyCursor") == 1 && controller.Allies[0].CurrentArmor == 17 &&
                    controller.Enemies[0].NextIntentDamage == 30 && controller.Enemies[1].NextIntentDamage == 5,
                    "Enemy pause checkpoint must follow exactly one committed attack.");
                controller.enabled = false;
                controller.EndPlayerTurn();
                Require(!controller.BeginAction(BattleActionKind.Rotate), "Disabled controller accepted a player action.");
                yield return null;
                yield return null;
                Require(controller.Phase == BattlePhase.EnemyTurn && controller.ActiveEnemy == null &&
                    ReadField<int>(controller, "enemyCursor") == 1 && controller.Allies[0].CurrentArmor == 17 &&
                    controller.Enemies[0].NextIntentDamage == 30 && controller.Enemies[1].NextIntentDamage == 5,
                    "Disabling after a committed hit must freeze damage, cursor, and enemy intents.");
                controller.enabled = true;
                yield return WaitForEnemy(controller);
                Require(controller.Phase == BattlePhase.PlayerTurn && controller.TurnNumber == 2 && controller.ActionPoints == 4 &&
                    controller.Allies[0].CurrentHealth == 100 && controller.Allies[0].CurrentArmor == 5 &&
                    controller.Allies[1].CurrentHealth == 100 && controller.Allies[1].CurrentArmor == 20 &&
                    controller.Enemies[0].NextIntentDamage == 30 && controller.Enemies[1].NextIntentDamage == 50 &&
                    controller.Enemies[2].NextIntentDamage == 70,
                    "Re-enabling must resume each remaining enemy once, without repeating the committed hit or advancing two turns.");
                checks.Add("Enemy-turn lifecycle: disable before/after commit freezes state; re-enable resumes the pending cursor with exactly one hit/intent advance per enemy");
            }
            finally
            {
                SetField(controller, "enemyActionDelay", delay);
                controller.enabled = true;
            }
        }

        private static IEnumerator CheckEquipmentTransactions(BattleController controller,
            List<UnityEngine.Object> temporary, List<string> checks)
        {
            EquipmentDefinition sword = MakeEquipment(temporary, "Stat sword", EquipmentSlotKind.Handheld, 2, 1);
            sword.top.attack = 2;
            sword.top.health = 20;
            sword.top.armor = 10;
            EquipmentDefinition spare = MakeEquipment(temporary, "Spare sword", EquipmentSlotKind.Handheld, 2, 1);
            BattleDefinition definition = MakeBattle(temporary, 2, 1);
            definition.allies[0].startingEquipment = new[] { sword };
            definition.backpackEquipment = new[] { spare };
            Load(controller, definition);
            BattleUnitState actor = controller.Allies[0];
            BattleUnitState recipient = controller.Allies[1];
            InventoryGridState hand = actor.EquipmentGrids[0];
            InventoryGridState worn = actor.EquipmentGrids[1];
            InventoryGridState bag = controller.Backpack;
            EquipmentInstance item = hand.Placements[0].Item;
            EquipmentInstance spareItem = bag.Placements[0].Item;
            string id = item.Id;
            Require(bag.Width == 5 && bag.Height == 3, "The shared backpack must be five columns by three rows.");
            foreach (InventoryGridState slot in actor.EquipmentGrids)
                Require(slot.IsEquipmentSlot && slot.Width == 1 && slot.Height == 1 && slot.Placements.Count <= 1,
                    "Every character equipment area must be a single typed slot.");
            Require(actor.MaxHealth == 120 && actor.CurrentHealth == 120 && actor.MaxArmor == 30 && actor.CurrentArmor == 30,
                "Starting equipment must be installed before current health/armor are filled.");
            Require(!controller.TryMoveEquipment(item, hand, worn, 0, 0, false, out _) &&
                !controller.TryMoveEquipment(item, hand, bag, 4, 0, false, out _) &&
                !controller.TryMoveEquipment(item, hand, bag, 0, 0, false, out _) &&
                !controller.TryMoveEquipment(spareItem, bag, hand, 0, 0, false, out _) &&
                !controller.TryAutoMoveEquipment(spareItem, bag, actor, out _),
                "Type, bounds, collision, or occupied single-slot placement was accepted.");
            Require(item.Grid == hand && item.X == 0 && item.Y == 0 && !item.Rotated && hand.Placements.Count == 1 &&
                bag.Placements.Count == 1 && spareItem.Grid == bag && actor.CurrentHealth == 120,
                "A rejected transaction changed, replaced, or lost equipment.");
            Require(!bag.TryAdd(item, 2, 0, false, out _), "The same item was added to two grids.");
            var foreign = new InventoryGridState(5, 3, "foreign");
            Require(!controller.TryMoveEquipment(item, hand, foreign, 0, 0, false, out _), "Controller accepted a foreign grid.");
            Require(bag.TryFindSpace(item, false, out int x, out int y) && x == 2 && y == 0,
                "First-fit must find the first empty footprint in row-major order.");
            Require(controller.TryMoveEquipment(item, hand, bag, x, y, false, out _) && item.Id == id &&
                actor.MaxHealth == 100 && actor.CurrentHealth == 100 && actor.MaxArmor == 20 && actor.CurrentArmor == 20,
                "Unequipping must preserve item identity and clamp current health/armor.");
            Require(controller.TryRotateEquipment(item, bag, out _) && item.Rotated && item.Width == 1 && item.Height == 2,
                "Backpack rotation must change footprint only after successful commit.");
            Require(controller.TryAutoMoveEquipment(item, bag, actor, out _) && item.Rotated && item.Grid == hand &&
                item.Width == 1 && item.Height == 2 && hand.Placements.Count == 1 &&
                actor.MaxHealth == 120 && actor.CurrentHealth == 100 && actor.MaxArmor == 30 && actor.CurrentArmor == 20,
                "A 2x1 backpack item must fit one equipment slot without losing its rotation or refilling stats.");
            Require(controller.TryMoveEquipment(item, hand, recipient.EquipmentGrids[0], 0, 0, true, out _) &&
                actor.MaxHealth == 100 && recipient.MaxHealth == 120 && recipient.CurrentHealth == 100 && item.Id == id,
                "Cross-character moves must recalculate both owners without duplicating or refilling the item.");
            Require(controller.TryAutoMoveEquipment(item, recipient.EquipmentGrids[0], recipient, out _) && item.Grid == bag &&
                item.Rotated && item.Width == 1 && item.Height == 2 && item.Id == id,
                "Unequipping must restore the real backpack footprint and preserve identity and orientation.");
            controller.StartBattle();
            Require(!controller.TryAutoMoveEquipment(item, bag, actor, out _) && item.Grid == bag,
                "Ordinary combat must reject equipment edits.");
            controller.SelectAlly(actor);
            Require(controller.BeginAction(BattleActionKind.Reorganize) &&
                controller.TryAutoMoveEquipment(item, bag, actor, out _), "Reorganization must authorize equipment edits.");
            controller.ExitReorganization();
            checks.Add("5x3 backpack and three single equipment slots: occupied/type/bounds/collision rejection is atomic; 2x1 items preserve identity and rotation through equip/unequip, with stat clamps and no refill");

            EquipmentDefinition filler = MakeEquipment(temporary, "Backpack filler", EquipmentSlotKind.Accessory, 1, 1);
            BattleDefinition fullBag = MakeBattle(temporary, 1, 1);
            fullBag.allies[0].startingEquipment = new[] { sword };
            fullBag.backpackEquipment = new EquipmentDefinition[15];
            for (int index = 0; index < fullBag.backpackEquipment.Length; index++) fullBag.backpackEquipment[index] = filler;
            Load(controller, fullBag);
            hand = controller.Allies[0].EquipmentGrids[0];
            item = hand.Placements[0].Item;
            bag = controller.Backpack;
            Require(bag.Placements.Count == 15 && !controller.TryAutoMoveEquipment(item, hand, controller.Allies[0], out _) &&
                item.Grid == hand && hand.Placements.Count == 1 && bag.Placements.Count == 15,
                "A full backpack must reject unequip without detaching or losing the equipped item.");
            filler.canRotate = false;
            EquipmentInstance fixedItem = bag.Placements[0].Item;
            Require(!controller.TryRotateEquipment(fixedItem, bag, out _) && !fixedItem.Rotated && fixedItem.Grid == bag,
                "A nonrotatable item must reject rotation without changing its placement.");
            checks.Add("Full backpack refuses unequip without losing items; nonrotatable equipment retains its original placement");

            BattleDefinition deadOwner = MakeBattle(temporary, 2, 1);
            deadOwner.allies[0].startingEquipment = new[] { sword };
            deadOwner.enemies[0].intentDamage = new[] { 1000 };
            Load(controller, deadOwner);
            actor = controller.Allies[0];
            recipient = controller.Allies[1];
            hand = actor.EquipmentGrids[0];
            item = hand.Placements[0].Item;
            controller.StartBattle();
            controller.EndPlayerTurn();
            yield return WaitForEnemy(controller);
            Require(!actor.IsAlive && recipient.IsAlive && controller.GetLivingOrder(recipient) == 1,
                "A dead front ally must compact the surviving ally immediately.");
            controller.SelectAlly(recipient);
            Require(controller.BeginAction(BattleActionKind.Reorganize), "A surviving ally must still enter reorganization.");
            Require(!controller.TryAutoMoveEquipment(item, hand, recipient, out _) && item.Grid == hand,
                "Equipment owned by a dead character must reject transfer without losing the item.");
            controller.ExitReorganization();
            checks.Add("Death compacts allies, one survivor can reorganize, and dead-owner equipment remains protected");
        }

        private static BattleDefinition MakeBattle(List<UnityEngine.Object> temporary, int allyCount, int enemyCount)
        {
            BattleDefinition result = ScriptableObject.CreateInstance<BattleDefinition>();
            result.name = "Runtime rule fixture";
            temporary.Add(result);
            result.allies = new AllyDefinition[allyCount];
            result.enemies = new EnemyDefinition[enemyCount];
            for (int i = 0; i < allyCount; i++)
            {
                result.allies[i] = new AllyDefinition
                {
                    name = "Rule ally " + (i + 1), baseHealth = 100, baseArmor = 20, attackRange = 7,
                    frontTop = Form("Top", 10, 1f, 1f, 1f, 0, 0, 0),
                    frontBottom = Form("Bottom", 4, 1f, 1.5f, 1.5f, 0, 3, 0),
                    back = Form("Back", 6, 1f, 0.5f, 0.5f, 0, 0, 1),
                    startingEquipment = Array.Empty<EquipmentDefinition>()
                };
                result.allies[i].back.standby = Array.Empty<EffectDefinition>();
                result.allies[i].back.rotation = Array.Empty<EffectDefinition>();
            }
            for (int i = 0; i < enemyCount; i++)
                result.enemies[i] = new EnemyDefinition
                {
                    name = "Rule enemy " + (i + 1), maxHealth = 1000, maxArmor = 0,
                    attackRange = 4, intentDamage = new[] { 0 }
                };
            result.backpackEquipment = Array.Empty<EquipmentDefinition>();
            return result;
        }

        private static FormDefinition Form(string name, int attack, float attackMultiplier,
            float healthMultiplier, float armorMultiplier, int standby, int rotation, int flip)
        {
            return new FormDefinition
            {
                name = name, initialAttack = attack, attackMultiplier = attackMultiplier,
                healthMultiplier = healthMultiplier, armorMultiplier = armorMultiplier,
                standby = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, standby) },
                rotation = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, rotation) },
                flip = new[] { Effect(EffectKind.Damage, EffectTarget.EnemySingle, flip) }
            };
        }

        private static EquipmentDefinition MakeEquipment(List<UnityEngine.Object> temporary,
            string name, EquipmentSlotKind kind, int width, int height)
        {
            EquipmentDefinition result = ScriptableObject.CreateInstance<EquipmentDefinition>();
            result.displayName = name;
            result.kind = kind;
            result.width = width;
            result.height = height;
            result.canRotate = true;
            temporary.Add(result);
            return result;
        }

        private static EffectDefinition Effect(EffectKind kind, EffectTarget target, int amount, float scaling = 1f)
        {
            return new EffectDefinition { kind = kind, target = target, amount = amount, scaling = scaling };
        }

        private static void Submit(BattleController controller, BattleUnitState actor,
            BattleActionKind kind, BattleUnitState target)
        {
            controller.SelectAlly(actor);
            Require(controller.BeginAction(kind), "Action rejected: " + kind + ". " + controller.LastMessage);
            if (controller.Phase == BattlePhase.SelectingTarget)
            {
                Require(controller.CanTarget(target), "Helper target rejected: " + kind + ". " + controller.LastMessage);
                controller.SelectTarget(target);
            }
            Require(controller.Phase != BattlePhase.SelectingTarget, "Single-target helper left an unresolved target group.");
        }

        private static IEnumerator WaitForEnemy(BattleController controller)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (controller.Phase == BattlePhase.EnemyTurn || controller.Phase == BattlePhase.Resolving)
            {
                Require(Time.realtimeSinceStartup < deadline, "Enemy turn did not finish within ten seconds.");
                yield return null;
            }
        }

        private static void Load(BattleController controller, BattleDefinition definition)
        {
            SetField(controller, "definition", definition);
            Initialize(controller);
            Require(controller.enabled && controller.Phase == BattlePhase.Preparation,
                "Runtime fixture initialization failed: " + controller.LastMessage);
        }

        private static void Initialize(BattleController controller)
        {
            MethodInfo method = typeof(BattleController).GetMethod("InitializeBattle", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "InitializeBattle verification hook is missing.");
            method.Invoke(controller, null);
        }

        private static T ReadField<T>(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, "Missing verification field: " + field);
            return (T)info.GetValue(owner);
        }

        private static void SetField(object owner, string field, object value)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, "Missing verification field: " + field);
            info.SetValue(owner, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
