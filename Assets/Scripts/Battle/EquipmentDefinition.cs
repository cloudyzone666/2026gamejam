using System;
using UnityEngine;

namespace CardCards.Battle
{
    // Authored data is shared. Each equipped or stored copy has its own EquipmentInstance.
    [CreateAssetMenu(fileName = "Equipment", menuName = "Card Cards/Equipment Definition")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        public string displayName = "新装备";
        public EquipmentSlotKind kind;
        [Min(1)] public int width = 1;
        [Min(1)] public int height = 1;
        public bool canRotate = true;
        [TextArea] public string description;
        public EquipmentEndDefinition top = new EquipmentEndDefinition();
        public EquipmentEndDefinition bottom = new EquipmentEndDefinition();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "装备名称不能为空。";
                return false;
            }
            if (width < 1 || height < 1 || !Enum.IsDefined(typeof(EquipmentSlotKind), kind))
            {
                error = $"{displayName} 的尺寸或部位无效。";
                return false;
            }
            if (top == null || bottom == null)
            {
                error = $"{displayName} 必须配置上端与下端。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class EquipmentEndDefinition
    {
        public int attack;
        public int health;
        public int armor;
        public EffectDefinition[] standby = Array.Empty<EffectDefinition>();
        public EffectDefinition[] rotation = Array.Empty<EffectDefinition>();
    }

    // Grid rotation affects footprint only. BattleUnitState chooses the effective end
    // from the character's 180-degree orientation, independently of this rotation.
    public sealed class EquipmentInstance
    {
        public string Id { get; }
        public EquipmentDefinition Definition { get; }
        public bool Rotated { get; private set; }
        public int Width => Rotated ? Definition.height : Definition.width;
        public int Height => Rotated ? Definition.width : Definition.height;
        public InventoryGridState Grid { get; private set; }
        public int X { get; private set; } = -1;
        public int Y { get; private set; } = -1;

        public EquipmentInstance(EquipmentDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            Id = Guid.NewGuid().ToString("N");
        }

        internal void CommitPlacement(InventoryGridState grid, int x, int y, bool rotated)
        {
            Grid = grid;
            X = x;
            Y = y;
            Rotated = rotated;
        }
    }
}
