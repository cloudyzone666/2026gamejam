using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCards.Battle
{
    // One battle's mutable values. This class never changes the ScriptableObject.
    public sealed class BattleUnitState
    {
        private readonly int[] intentDamage;
        private int intentIndex;

        public string Name { get; }
        public bool IsEnemy { get; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; }
        public int CurrentArmor { get; private set; }
        public int MaxArmor { get; }
        public int AttackRange { get; }
        public int StandbyDamage { get; }
        public bool HasUsedStandby { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public int NextIntentDamage => IsEnemy ? intentDamage[intentIndex] : 0;
        public IReadOnlyList<EquipmentSlotDefinition> EquipmentSlots { get; }
        public IReadOnlyList<BuffDefinition> Buffs { get; }

        public BattleUnitState(AllyDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Name = source.name;
            IsEnemy = false;
            MaxHealth = Mathf.FloorToInt(source.baseHealth * source.healthMultiplier);
            MaxArmor = Mathf.FloorToInt(source.baseArmor * source.armorMultiplier);
            CurrentHealth = MaxHealth;
            CurrentArmor = MaxArmor;
            AttackRange = source.attackRange;
            StandbyDamage = Mathf.FloorToInt(source.initialAttack * source.attackMultiplier) + source.standbyBonusDamage;
            intentDamage = Array.Empty<int>();
            EquipmentSlots = CopyEquipmentSlots(source.equipmentSlots);
            Buffs = CopyBuffs(source.buffs);
        }

        public BattleUnitState(EnemyDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Name = source.name;
            IsEnemy = true;
            MaxHealth = source.maxHealth;
            MaxArmor = source.maxArmor;
            CurrentHealth = MaxHealth;
            CurrentArmor = MaxArmor;
            intentDamage = (int[])source.intentDamage.Clone();
            EquipmentSlots = Array.Empty<EquipmentSlotDefinition>();
            Buffs = Array.Empty<BuffDefinition>();
        }

        internal void ApplyDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return;
            int absorbed = Mathf.Min(CurrentArmor, damage);
            CurrentArmor -= absorbed;
            CurrentHealth = Mathf.Max(0, CurrentHealth - (damage - absorbed));
        }

        internal void UseStandby() => HasUsedStandby = true;
        internal void ResetStandby() => HasUsedStandby = false;

        internal void AdvanceIntent()
        {
            if (IsEnemy) intentIndex = (intentIndex + 1) % intentDamage.Length;
        }

        private static EquipmentSlotDefinition[] CopyEquipmentSlots(EquipmentSlotDefinition[] source)
        {
            if (source == null) return Array.Empty<EquipmentSlotDefinition>();
            var result = new EquipmentSlotDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                EquipmentSlotDefinition slot = source[i];
                if (slot == null) continue;
                result[i] = new EquipmentSlotDefinition
                {
                    kind = slot.kind, width = slot.width, height = slot.height, count = slot.count
                };
            }
            return result;
        }

        private static BuffDefinition[] CopyBuffs(BuffDefinition[] source)
        {
            if (source == null) return Array.Empty<BuffDefinition>();
            var result = new BuffDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                BuffDefinition buff = source[i];
                if (buff == null) continue;
                result[i] = new BuffDefinition
                {
                    id = buff.id, stat = buff.stat, baseValueDelta = buff.baseValueDelta,
                    multiplierDelta = buff.multiplierDelta, additionalValue = buff.additionalValue,
                    independentMultiplier = buff.independentMultiplier
                };
            }
            return result;
        }
    }
}
