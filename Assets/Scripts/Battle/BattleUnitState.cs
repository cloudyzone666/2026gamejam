using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCards.Battle
{
    // One battle's mutable values. This class never changes the ScriptableObject.
    public sealed class BattleUnitState
    {
        private readonly int[] intentDamage;
        private readonly int baseHealth;
        private readonly int baseArmor;
        private readonly InventoryGridState[] equipmentGrids;
        private int intentIndex;

        public string Name { get; }
        public Sprite Portrait { get; }
        public bool IsEnemy { get; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int CurrentArmor { get; private set; }
        public int MaxArmor { get; private set; }
        public int AttackRange { get; }
        public int Attack { get; private set; }
        public int StandbyDamage => IsEnemy ? 0 : GetStandbyDamage();
        public bool HasUsedStandby { get; private set; }
        public bool HasChangedFormThisTurn { get; private set; }
        public bool IsBack { get; private set; }
        public bool IsRotated { get; private set; }
        public FormDefinition FrontTop { get; }
        public FormDefinition FrontBottom { get; }
        public FormDefinition BackForm { get; }
        public FormDefinition CurrentForm => IsEnemy ? null : IsBack ? BackForm : IsRotated ? FrontBottom : FrontTop;
        public bool IsAlive => CurrentHealth > 0;
        public int NextIntentDamage => IsEnemy ? intentDamage[intentIndex] : 0;
        public IReadOnlyList<EquipmentSlotDefinition> EquipmentSlots { get; }
        public IReadOnlyList<BuffDefinition> Buffs { get; }
        public IReadOnlyList<InventoryGridState> EquipmentGrids => equipmentGrids;

        public BattleUnitState(AllyDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Name = source.name;
            Portrait = source.portrait;
            IsEnemy = false;
            baseHealth = source.baseHealth;
            baseArmor = source.baseArmor;
            FrontTop = CopyForm(source.frontTop);
            FrontBottom = CopyForm(source.frontBottom);
            BackForm = CopyForm(source.back);
            AttackRange = source.attackRange;
            intentDamage = Array.Empty<int>();
            EquipmentSlots = CopyEquipmentSlots(source.equipmentSlots);
            Buffs = CopyBuffs(source.buffs);
            equipmentGrids = new[]
            {
                new InventoryGridState(1, 1, "手持", EquipmentSlotKind.Handheld, this),
                new InventoryGridState(1, 1, "身着", EquipmentSlotKind.Worn, this),
                new InventoryGridState(1, 1, "佩戴", EquipmentSlotKind.Accessory, this)
            };
            if (source.startingEquipment != null)
                foreach (EquipmentDefinition equipment in source.startingEquipment)
                    AddStartingEquipment(equipment);
            RecalculateStats();
            // Equipment changes the opening maxima. Fill only after every item is installed.
            CurrentHealth = MaxHealth;
            CurrentArmor = MaxArmor;
        }

        public BattleUnitState(EnemyDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Name = source.name;
            Portrait = source.portrait;
            IsEnemy = true;
            MaxHealth = source.maxHealth;
            MaxArmor = source.maxArmor;
            CurrentHealth = MaxHealth;
            CurrentArmor = MaxArmor;
            intentDamage = (int[])source.intentDamage.Clone();
            AttackRange = source.attackRange;
            Attack = NextIntentDamage;
            equipmentGrids = Array.Empty<InventoryGridState>();
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
        internal void ResetStandby() => ResetTurn();

        internal void ResetTurn()
        {
            HasUsedStandby = false;
            HasChangedFormThisTurn = false;
        }

        internal void Rotate()
        {
            IsRotated = !IsRotated;
            HasChangedFormThisTurn = true;
            RecalculateStats();
        }

        internal void Flip()
        {
            IsBack = !IsBack;
            HasChangedFormThisTurn = true;
            RecalculateStats();
        }

        internal void Heal(int amount)
        {
            if (IsAlive && amount > 0) CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        internal void GainArmor(int amount)
        {
            if (IsAlive && amount > 0) CurrentArmor = Mathf.Min(MaxArmor, CurrentArmor + amount);
        }

        public EquipmentEndDefinition GetEquipmentEnd(EquipmentInstance item)
        {
            if (item == null) return null;
            return IsRotated ? item.Definition.bottom : item.Definition.top;
        }

        public void RecalculateStats()
        {
            if (IsEnemy) return;
            int healthBonus = 0;
            int armorBonus = 0;
            foreach (EquipmentInstance item in GetEquippedItems())
            {
                EquipmentEndDefinition end = GetEquipmentEnd(item);
                if (end == null) continue;
                healthBonus += end.health;
                armorBonus += end.armor;
            }
            Attack = GetAttack(CurrentForm);
            MaxHealth = Mathf.Max(1, Mathf.FloorToInt((baseHealth + healthBonus) * CurrentForm.healthMultiplier));
            MaxArmor = Mathf.Max(0, Mathf.FloorToInt((baseArmor + armorBonus) * CurrentForm.armorMultiplier));
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            CurrentArmor = Mathf.Min(CurrentArmor, MaxArmor);
        }

        internal int GetAttack(FormDefinition form)
        {
            int bonus = 0;
            foreach (EquipmentInstance item in GetEquippedItems())
            {
                EquipmentEndDefinition end = GetEquipmentEnd(item);
                if (end != null) bonus += end.attack;
            }
            return Mathf.Max(1, Mathf.FloorToInt((form.initialAttack + bonus) * form.attackMultiplier));
        }

        internal IEnumerable<EquipmentInstance> GetEquippedItems()
        {
            foreach (InventoryGridState grid in equipmentGrids)
                foreach (InventoryPlacement placement in grid.Placements)
                    yield return placement.Item;
        }

        internal void AdvanceIntent()
        {
            if (!IsEnemy) return;
            intentIndex = (intentIndex + 1) % intentDamage.Length;
            Attack = NextIntentDamage;
        }

        private void AddStartingEquipment(EquipmentDefinition equipment)
        {
            if (equipment == null) throw new ArgumentException($"{Name} 的初始装备包含空引用。");
            var item = new EquipmentInstance(equipment);
            foreach (InventoryGridState grid in equipmentGrids)
            {
                if (grid.AllowedKind != equipment.kind) continue;
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    bool rotated = orientation == 1;
                    if (rotated && !equipment.canRotate) break;
                    if (grid.TryFindSpace(item, rotated, out int x, out int y) &&
                        grid.TryAdd(item, x, y, rotated, out _)) return;
                }
            }
            throw new ArgumentException($"{Name} 的初始装备“{equipment.displayName}”无法放入对应装备区。");
        }

        private int GetStandbyDamage()
        {
            if (IsBack) return 0;
            int total = SumDamage(CurrentForm.standby, Attack);
            foreach (EquipmentInstance item in GetEquippedItems())
            {
                EquipmentEndDefinition end = GetEquipmentEnd(item);
                if (end != null) total += SumDamage(end.standby, Attack);
            }
            return total;
        }

        private static int SumDamage(EffectDefinition[] effects, int attack)
        {
            int result = 0;
            if (effects != null)
                foreach (EffectDefinition effect in effects)
                    if (effect != null && effect.kind == EffectKind.Damage)
                        result += Mathf.Max(0, Mathf.FloorToInt(attack * effect.scaling) + effect.amount);
            return result;
        }

        private static FormDefinition CopyForm(FormDefinition source)
        {
            if (source == null) throw new ArgumentException("角色的三种形态必须全部配置。");
            return new FormDefinition
            {
                name = source.name, initialAttack = source.initialAttack,
                attackMultiplier = source.attackMultiplier, healthMultiplier = source.healthMultiplier,
                armorMultiplier = source.armorMultiplier, standby = CopyEffects(source.standby),
                rotation = CopyEffects(source.rotation), flip = CopyEffects(source.flip)
            };
        }

        private static EffectDefinition[] CopyEffects(EffectDefinition[] source)
        {
            if (source == null) return Array.Empty<EffectDefinition>();
            var result = new EffectDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                EffectDefinition effect = source[i];
                result[i] = new EffectDefinition
                {
                    kind = effect.kind, target = effect.target, amount = effect.amount, scaling = effect.scaling
                };
            }
            return result;
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
