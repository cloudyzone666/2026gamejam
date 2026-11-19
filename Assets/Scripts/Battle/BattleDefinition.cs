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
                    ally.initialAttack < 0 || ally.standbyBonusDamage < 0 ||
                    !IsValidMultiplier(ally.attackMultiplier) ||
                    !IsValidMultiplier(ally.healthMultiplier) ||
                    !IsValidMultiplier(ally.armorMultiplier) ||
                    Mathf.FloorToInt(ally.baseHealth * ally.healthMultiplier) < 1)
                {
                    error = $"我方第 {i + 1} 名角色的姓名、基础值或倍率无效。";
                    return false;
                }
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyDefinition enemy = enemies[i];
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.name) ||
                    enemy.maxHealth < 1 || enemy.maxArmor < 0 ||
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

            error = string.Empty;
            return true;
        }

        private static bool IsValidMultiplier(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class AllyDefinition
    {
        public string name = "无名剑客";
        [Min(1)] public int baseHealth = 40;
        [Min(0)] public int baseArmor = 6;
        [Min(1)] public int attackRange = 2;
        [Min(0)] public int initialAttack = 10;
        [Min(0)] public int standbyBonusDamage = 6;
        [Min(0.01f)] public float attackMultiplier = 1f;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0.01f)] public float armorMultiplier = 1f;

        [Tooltip("仅保留装备格数据，不参与首版战斗。")]
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
        [Min(1)] public int maxHealth = 30;
        [Min(0)] public int maxArmor = 2;
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
