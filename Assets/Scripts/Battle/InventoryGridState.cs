using System;
using System.Collections.Generic;

namespace CardCards.Battle
{
    public sealed class InventoryPlacement
    {
        public EquipmentInstance Item { get; }
        public int X { get; }
        public int Y { get; }

        internal InventoryPlacement(EquipmentInstance item, int x, int y)
        {
            Item = item;
            X = x;
            Y = y;
        }
    }

    // This model checks spatial rules only. The controller authorizes battle phases
    // and recalculates affected characters after a successful transaction.
    public sealed class InventoryGridState
    {
        private readonly List<InventoryPlacement> placements = new List<InventoryPlacement>();

        public int Width { get; }
        public int Height { get; }
        public string Name { get; }
        public EquipmentSlotKind? AllowedKind { get; }
        public BattleUnitState Owner { get; }
        public bool IsEquipmentSlot => Owner != null;
        public IReadOnlyList<InventoryPlacement> Placements { get; }

        public InventoryGridState(int width, int height, string name,
            EquipmentSlotKind? allowedKind = null, BattleUnitState owner = null)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            if (allowedKind.HasValue && !Enum.IsDefined(typeof(EquipmentSlotKind), allowedKind.Value))
                throw new ArgumentOutOfRangeException(nameof(allowedKind));
            Width = width;
            Height = height;
            Name = name ?? string.Empty;
            AllowedKind = allowedKind;
            Owner = owner;
            Placements = placements.AsReadOnly();
        }

        public bool CanPlace(EquipmentInstance item, int x, int y, bool rotated, out string error)
        {
            if (item == null || item.Definition == null)
            {
                error = "装备不存在。";
                return false;
            }
            EquipmentDefinition definition = item.Definition;
            if (AllowedKind.HasValue && AllowedKind.Value != definition.kind)
            {
                error = $"{definition.displayName} 不能放进{Name}，装备部位不匹配。";
                return false;
            }
            if (rotated && !definition.canRotate)
            {
                error = $"{definition.displayName} 不可转向。";
                return false;
            }

            if (definition.width < 1 || definition.height < 1)
            {
                error = $"{definition.displayName} 的背包尺寸无效。";
                return false;
            }
            // A character's equipment area is one slot. The authored footprint is
            // used only in the backpack; its rotation is preserved when equipped.
            int itemWidth = IsEquipmentSlot ? 1 : rotated ? definition.height : definition.width;
            int itemHeight = IsEquipmentSlot ? 1 : rotated ? definition.width : definition.height;
            // Subtraction avoids integer overflow for invalid authored sizes/positions.
            if (itemWidth < 1 || itemHeight < 1 || x < 0 || y < 0 || x >= Width || y >= Height ||
                itemWidth > Width - x || itemHeight > Height - y)
            {
                error = $"{definition.displayName} 超出{Name}的边界。";
                return false;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                InventoryPlacement occupied = placements[i];
                if (ReferenceEquals(occupied.Item, item)) continue;
                if (IsEquipmentSlot)
                {
                    error = $"{Name}只能穿戴一件装备，请先卸下当前装备。";
                    return false;
                }
                if (x < occupied.X + occupied.Item.Width && x + itemWidth > occupied.X &&
                    y < occupied.Y + occupied.Item.Height && y + itemHeight > occupied.Y)
                {
                    error = $"{Name}的目标位置已有装备。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryAdd(EquipmentInstance item, int x, int y, bool rotated, out string error)
        {
            if (item != null && item.Grid != null)
            {
                error = "装备已放入网格，请使用移动操作。";
                return false;
            }
            if (!CanPlace(item, x, y, rotated, out error)) return false;
            var placement = new InventoryPlacement(item, x, y);
            placements.Add(placement);
            item.CommitPlacement(this, x, y, rotated);
            return true;
        }

        public bool TryFindSpace(EquipmentInstance item, bool rotated, out int x, out int y)
        {
            for (int row = 0; row < Height; row++)
            {
                for (int column = 0; column < Width; column++)
                {
                    if (!CanPlace(item, column, row, rotated, out _)) continue;
                    x = column;
                    y = row;
                    return true;
                }
            }
            x = -1;
            y = -1;
            return false;
        }

        public InventoryPlacement Find(EquipmentInstance item)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                if (ReferenceEquals(placements[i].Item, item)) return placements[i];
            }
            return null;
        }

        public static bool TryTransfer(InventoryGridState source, InventoryGridState destination,
            EquipmentInstance item, int x, int y, bool rotated, out string error)
        {
            if (source == null || destination == null || item == null)
            {
                error = "装备或来源、目标网格不存在。";
                return false;
            }
            InventoryPlacement original = source.Find(item);
            if (original == null || !ReferenceEquals(item.Grid, source))
            {
                error = "来源网格中没有这件装备。";
                return false;
            }
            if (!ReferenceEquals(source, destination) && destination.Find(item) != null)
            {
                error = "目标网格已包含这件装备。";
                return false;
            }
            if (!destination.CanPlace(item, x, y, rotated, out error)) return false;

            var replacement = new InventoryPlacement(item, x, y);
            if (ReferenceEquals(source, destination))
            {
                source.placements[source.placements.IndexOf(original)] = replacement;
            }
            else
            {
                // Add before remove: a failed allocation cannot detach the source item.
                destination.placements.Add(replacement);
                source.placements.Remove(original);
            }
            item.CommitPlacement(destination, x, y, rotated);
            error = string.Empty;
            return true;
        }
    }
}
