using System;
using UnityEngine;

namespace CardCards.Battle
{
    [CreateAssetMenu(fileName = "BattleDefinition", menuName = "Card Cards/Battle Definition")]
    public sealed class BattleDefinition : ScriptableObject
    {
        public AllyDefinition[] allies =
        {
            new AllyDefinition(),
            new AllyDefinition { name = "无名剑客·影", baseHealth = 44, baseArmor = 4 }
        };
        public EnemyDefinition[] enemies = { new EnemyDefinition(), new EnemyDefinition() };
        public EquipmentDefinition[] backpackEquipment = Array.Empty<EquipmentDefinition>();

        // Fail before creating runtime state rather than silently repairing authored data.
        public bool TryValidate(out string error)
        {
            if (allies == null || allies.Length < 1 || allies.Length > 4 ||
                enemies == null || enemies.Length < 1 || enemies.Length > 4)
            {
                error = "双方编成必须各为 1–4 名角色。";
                return false;
            }

            for (int i = 0; i < allies.Length; i++)
            {
                AllyDefinition ally = allies[i];
                if (ally == null || string.IsNullOrWhiteSpace(ally.name) ||
                    ally.baseHealth < 1 || ally.baseArmor < 0 || ally.attackRange < 1 ||
                    !IsValidForm(ally.frontTop, ally.baseHealth) ||
                    !IsValidForm(ally.frontBottom, ally.baseHealth) ||
                    !IsValidForm(ally.back, ally.baseHealth))
                {
                    error = $"我方第 {i + 1} 名角色的姓名、基础值或倍率无效。";
                    return false;
                }
                if (!AreValidEquipment(ally.startingEquipment, out error)) return false;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyDefinition enemy = enemies[i];
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.name) ||
                    enemy.maxHealth < 1 || enemy.maxArmor < 0 || enemy.attackRange < 1 ||
                    enemy.intentDamage == null || enemy.intentDamage.Length == 0)
                {
                    error = $"敌方第 {i + 1} 名角色的基础值或意图序列无效。";
                    return false;
                }

                for (int j = 0; j < enemy.intentDamage.Length; j++)
                {
                    if (enemy.intentDamage[j] < 0)
                    {
                        error = $"敌方第 {i + 1} 名角色的意图伤害不能为负数。";
                        return false;
                    }
                }
            }

            return AreValidEquipment(backpackEquipment, out error);
        }

        private static bool IsValidMultiplier(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidForm(FormDefinition form, int health)
        {
            return form != null && !string.IsNullOrWhiteSpace(form.name) && form.initialAttack >= 0 &&
                IsValidMultiplier(form.attackMultiplier) && IsValidMultiplier(form.healthMultiplier) &&
                IsValidMultiplier(form.armorMultiplier) &&
                Mathf.FloorToInt(health * form.healthMultiplier) >= 1 &&
                AreValidEffects(form.standby) && AreValidEffects(form.rotation) && AreValidEffects(form.flip);
        }

        private static bool AreValidEffects(EffectDefinition[] effects)
        {
            if (effects == null) return true;
            foreach (EffectDefinition effect in effects)
                if (effect == null || !Enum.IsDefined(typeof(EffectKind), effect.kind) ||
                    !Enum.IsDefined(typeof(EffectTarget), effect.target) ||
                    float.IsNaN(effect.scaling) || float.IsInfinity(effect.scaling) || effect.scaling < 0f)
                    return false;
            return true;
        }

        private static bool AreValidEquipment(EquipmentDefinition[] equipment, out string error)
        {
            if (equipment != null)
                foreach (EquipmentDefinition item in equipment)
                {
                    if (item == null)
                    {
                        error = "初始装备中存在空引用。";
                        return false;
                    }
                    if (!item.TryValidate(out error)) return false;
                    if (!AreValidEffects(item.top.standby) || !AreValidEffects(item.top.rotation) ||
                        !AreValidEffects(item.bottom.standby) || !AreValidEffects(item.bottom.rotation))
                    {
                        error = $"{item.displayName} 的效果配置无效。";
                        return false;
                    }
                }
            error = string.Empty;
            return true;
        }
    }

    public enum BattleActionKind { Standby, Rotate, Flip, Reorganize }
    public enum EffectKind { Damage, Heal, GainArmor }
    public enum EffectTarget { Self, AllySingle, AllyAll, EnemySingle, EnemyAll }

    [Serializable]
    public sealed class EffectDefinition
    {
        public EffectKind kind = EffectKind.Damage;
        public EffectTarget target = EffectTarget.EnemySingle;
        public int amount;
        [Min(0f)] public float scaling = 1f;

        public static EffectDefinition Damage(int additionalValue)
        {
            return new EffectDefinition { amount = additionalValue };
        }
    }

    [Serializable]
    public sealed class FormDefinition
    {
        public string name = "剑士";
        [Min(0)] public int initialAttack = 8;
        [Min(0.01f)] public float attackMultiplier = 1f;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0.01f)] public float armorMultiplier = 1f;
        public EffectDefinition[] standby = { EffectDefinition.Damage(-2) };
        public EffectDefinition[] rotation = { EffectDefinition.Damage(0) };
        public EffectDefinition[] flip = { EffectDefinition.Damage(-3) };

        public static FormDefinition Guard()
        {
            return new FormDefinition
            {
                name = "守卫", initialAttack = 4, attackMultiplier = 0.9f,
                healthMultiplier = 1.2f, armorMultiplier = 1.5f,
                standby = new[] { EffectDefinition.Damage(1) },
                rotation = new[] { EffectDefinition.Damage(3) },
                flip = new[] { EffectDefinition.Damage(0) }
            };
        }

        public static FormDefinition Mirror()
        {
            return new FormDefinition
            {
                name = "镜影", initialAttack = 6, attackMultiplier = 1.1f,
                healthMultiplier = 0.9f, armorMultiplier = 1f,
                standby = Array.Empty<EffectDefinition>(), rotation = Array.Empty<EffectDefinition>(),
                flip = new[] { EffectDefinition.Damage(1) }
            };
        }
    }

    [Serializable]
    public sealed class AllyDefinition
    {
        public string name = "无名剑客";
        public Sprite portrait;
        [Min(1)] public int baseHealth = 40;
        [Min(0)] public int baseArmor = 6;
        [Min(1)] public int attackRange = 2;
        [Min(0)] public int initialAttack = 10;
        [Min(0)] public int standbyBonusDamage = 6;
        [Min(0.01f)] public float attackMultiplier = 1f;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0.01f)] public float armorMultiplier = 1f;

        public FormDefinition frontTop = new FormDefinition();
        public FormDefinition frontBottom = FormDefinition.Guard();
        public FormDefinition back = FormDefinition.Mirror();
        public EquipmentDefinition[] startingEquipment = Array.Empty<EquipmentDefinition>();

        [Tooltip("旧配置兼容字段；运行时使用手持、身着、佩戴三个单装备槽，每个部位只能穿戴一件。")]
        public EquipmentSlotDefinition[] equipmentSlots =
        {
            new EquipmentSlotDefinition { kind = EquipmentSlotKind.Handheld, width = 2, height = 1, count = 2 },
            new EquipmentSlotDefinition { kind = EquipmentSlotKind.Worn, width = 2, height = 1, count = 2 },
            new EquipmentSlotDefinition { kind = EquipmentSlotKind.Accessory, width = 2, height = 2, count = 4 }
        };

        [Tooltip("仅保留状态数据，不参与首版计算。")]
        public BuffDefinition[] buffs = Array.Empty<BuffDefinition>();
    }

    [Serializable]
    public sealed class EnemyDefinition
    {
        public string name = "腐化史莱姆";
        public Sprite portrait;
        [Min(1)] public int maxHealth = 30;
        [Min(0)] public int maxArmor = 2;
        [Min(1)] public int attackRange = 4;
        public int[] intentDamage = { 6, 6, 9 };
    }

    public enum EquipmentSlotKind { Handheld, Worn, Accessory }

    [Serializable]
    public sealed class EquipmentSlotDefinition
    {
        public EquipmentSlotKind kind;
        [Min(1)] public int width = 1;
        [Min(1)] public int height = 1;
        [Min(1)] public int count = 1;
    }

    public enum BuffStat { Attack, Health, Armor }

    [Serializable]
    public sealed class BuffDefinition
    {
        public string id;
        public BuffStat stat;
        public float baseValueDelta;
        public float multiplierDelta;
        public float additionalValue;
        public float independentMultiplier = 1f;
    }
}
