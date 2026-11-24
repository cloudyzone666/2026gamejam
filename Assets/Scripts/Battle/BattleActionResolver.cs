using System.Collections.Generic;
using UnityEngine;

namespace CardCards.Battle
{
    // A plan contains value snapshots, not references to mutable configuration effects.
    internal sealed class BattleEffectStep
    {
        public EffectKind Kind;
        public EffectTarget Target;
        public int Value;
        public string Source;
        public int TargetIndex = -1;
    }

    internal sealed class BattleActionPlan
    {
        public readonly List<BattleEffectStep> Steps = new List<BattleEffectStep>();
        public readonly List<EffectTarget> TargetRules = new List<EffectTarget>();
    }

    internal static class BattleActionResolver
    {
        internal static BattleActionPlan BuildPlan(BattleUnitState actor, BattleActionKind action)
        {
            var plan = new BattleActionPlan();
            switch (action)
            {
                case BattleActionKind.Standby:
                    Add(plan, actor.CurrentForm.standby, actor.Attack, actor.CurrentForm.name);
                    AddEquipment(plan, actor, action, actor.Attack);
                    break;
                case BattleActionKind.Rotate:
                    FormDefinition lower = actor.IsRotated ? actor.FrontTop : actor.FrontBottom;
                    int lowerAttack = actor.GetAttack(lower);
                    Add(plan, lower.rotation, lowerAttack, lower.name);
                    AddEquipment(plan, actor, action, lowerAttack);
                    break;
                case BattleActionKind.Flip:
                    if (!actor.IsBack)
                    {
                        Add(plan, actor.BackForm.flip, actor.GetAttack(actor.BackForm), actor.BackForm.name);
                    }
                    else
                    {
                        FormDefinition upper = actor.IsRotated ? actor.FrontBottom : actor.FrontTop;
                        FormDefinition bottom = actor.IsRotated ? actor.FrontTop : actor.FrontBottom;
                        Add(plan, upper.flip, actor.GetAttack(upper), upper.name);
                        Add(plan, bottom.flip, actor.GetAttack(bottom), bottom.name);
                    }
                    break;
            }

            // Consecutive single-target effects share a target. Changing rule starts another segment.
            EffectTarget? previousRule = null;
            int targetIndex = -1;
            foreach (BattleEffectStep step in plan.Steps)
            {
                if (step.Target == EffectTarget.AllySingle || step.Target == EffectTarget.EnemySingle)
                {
                    if (previousRule != step.Target)
                    {
                        targetIndex = plan.TargetRules.Count;
                        plan.TargetRules.Add(step.Target);
                    }
                    step.TargetIndex = targetIndex;
                }
                previousRule = step.Target;
            }
            return plan;
        }

        internal static void Apply(BattleEffectStep effect, BattleUnitState target)
        {
            switch (effect.Kind)
            {
                case EffectKind.Damage: target.ApplyDamage(effect.Value); break;
                case EffectKind.Heal: target.Heal(effect.Value); break;
                case EffectKind.GainArmor: target.GainArmor(effect.Value); break;
            }
        }

        private static void AddEquipment(BattleActionPlan plan, BattleUnitState actor,
            BattleActionKind action, int attack)
        {
            foreach (EquipmentInstance item in actor.GetEquippedItems())
            {
                // Rotation triggers the end currently facing down, before the card changes orientation.
                EquipmentEndDefinition end = action == BattleActionKind.Rotate
                    ? actor.IsRotated ? item.Definition.top : item.Definition.bottom
                    : actor.GetEquipmentEnd(item);
                if (end != null)
                    Add(plan, action == BattleActionKind.Rotate ? end.rotation : end.standby,
                        attack, item.Definition.displayName);
            }
        }

        private static void Add(BattleActionPlan plan, EffectDefinition[] effects, int attack, string source)
        {
            if (effects == null) return;
            foreach (EffectDefinition effect in effects)
            {
                if (effect == null) continue;
                plan.Steps.Add(new BattleEffectStep
                {
                    Kind = effect.kind, Target = effect.target, Source = source,
                    Value = Mathf.Max(0, Mathf.FloorToInt(attack * effect.scaling) + effect.amount)
                });
            }
        }
    }
}
