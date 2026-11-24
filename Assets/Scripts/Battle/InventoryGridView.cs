using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Draws one contiguous grid. This view never changes occupied cells itself.</summary>
    public sealed class InventoryGridView : MonoBehaviour
    {
        public const float CellSize = 40f;
        public const float Gap = 5f;
        [SerializeField] private Vector2 cellSize = new Vector2(CellSize, CellSize);
        [SerializeField] private Vector2 cellGap = new Vector2(Gap, Gap);
        [SerializeField] private Color cellColor = Color.clear;
        [SerializeField] private Color lineColor = Color.clear;
        private static readonly List<InventoryGridView> registered = new List<InventoryGridView>();
        private readonly Dictionary<EquipmentInstance, EquipmentItemView> items = new Dictionary<EquipmentInstance, EquipmentItemView>();
        private readonly List<Image> cells = new List<Image>();
        private BattleView view;
        private InventoryGridState grid;
        private Image placementPreview;
        private int drawnWidth = -1;
        private int drawnHeight = -1;

        public InventoryGridState Grid => grid;
        public BattleView View => view;
        public RectTransform Rect => (RectTransform)transform;
        public Vector2 CellStep => cellSize + cellGap;
        public bool IsEquipmentSlot => grid != null && grid.Owner != null;

        public void Initialize(BattleView battleView) { view = battleView; }

        public void Bind(InventoryGridState state)
        {
            if (ReferenceEquals(grid, state)) return;
            foreach (EquipmentItemView item in items.Values)
            {
                item.CancelDrag();
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            items.Clear();
            grid = state;
            drawnWidth = -1;
            drawnHeight = -1;
            HidePreview();
        }

        public void Refresh()
        {
            if (view == null) return;
            int width = grid == null ? 0 : grid.Width;
            int height = grid == null ? 0 : grid.Height;
            if (drawnWidth != width || drawnHeight != height) RebuildCells(width, height);
            if (grid == null) return;
            var removed = new List<EquipmentInstance>();
            foreach (var pair in items)
                if (grid.Find(pair.Key) == null) removed.Add(pair.Key);
            foreach (EquipmentInstance item in removed)
            {
                EquipmentItemView oldView = items[item];
                // A transfer calls Refresh synchronously from the controller after drag release.
                oldView.CancelDrag();
                oldView.gameObject.SetActive(false);
                Destroy(oldView.gameObject);
                items.Remove(item);
            }
            foreach (InventoryPlacement placement in grid.Placements)
            {
                EquipmentItemView itemView;
                if (!items.TryGetValue(placement.Item, out itemView))
                {
                    RectTransform rect = BattleUiFactory.Rect(placement.Item.Definition.displayName, transform);
                    Image image = rect.gameObject.AddComponent<Image>();
                    image.raycastTarget = true;
                    itemView = rect.gameObject.AddComponent<EquipmentItemView>();
                    itemView.Initialize(this, placement.Item, image);
                    items.Add(placement.Item, itemView);
                }
                itemView.Refresh(placement.X, placement.Y);
            }
            if (placementPreview != null) placementPreview.transform.SetAsLastSibling();
        }

        private void RebuildCells(int width, int height)
        {
            foreach (Image cell in cells) if (cell != null) Destroy(cell.gameObject);
            cells.Clear();
            drawnWidth = width;
            drawnHeight = height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Image cell = BattleUiFactory.Image($"Cell_{x}_{y}", transform, cellColor);
                    cell.rectTransform.sizeDelta = cellSize;
                    cell.rectTransform.anchoredPosition = new Vector2(x * CellStep.x, -y * CellStep.y);
                    if (lineColor.a > 0f)
                    {
                        const float thickness = 1.25f;
                        AddCellEdge(cell.transform, "Top", Vector2.zero, new Vector2(cellSize.x, thickness));
                        AddCellEdge(cell.transform, "Bottom", new Vector2(0f, -cellSize.y + thickness), new Vector2(cellSize.x, thickness));
                        AddCellEdge(cell.transform, "Left", Vector2.zero, new Vector2(thickness, cellSize.y));
                        AddCellEdge(cell.transform, "Right", new Vector2(cellSize.x - thickness, 0f), new Vector2(thickness, cellSize.y));
                    }
                    cell.transform.SetAsFirstSibling();
                    cells.Add(cell);
                }
        }

        private void AddCellEdge(Transform parent, string name, Vector2 position, Vector2 size)
        {
            // Outline duplicates the entire quad and fills transparent cells. Thin images keep the paper visible.
            Image edge = BattleUiFactory.Image(name, parent, lineColor);
            edge.rectTransform.anchoredPosition = position;
            edge.rectTransform.sizeDelta = size;
        }

        public Vector2 ItemSize(int width, int height)
        {
            return new Vector2(width * cellSize.x + (width - 1) * cellGap.x,
                height * cellSize.y + (height - 1) * cellGap.y);
        }

        public Vector2 PlacementSize(EquipmentInstance item, bool rotated)
        {
            if (IsEquipmentSlot) return cellSize;
            return ItemSize(rotated ? item.Definition.height : item.Definition.width,
                rotated ? item.Definition.width : item.Definition.height);
        }

        public bool TryGetCell(Vector2 screenPoint, Camera camera, out int x, out int y)
        {
            x = y = -1;
            if (grid == null || !gameObject.activeInHierarchy) return false;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPoint, camera, out local)) return false;
            float left = local.x - Rect.rect.xMin;
            float top = Rect.rect.yMax - local.y;
            x = Mathf.FloorToInt(left / CellStep.x);
            y = Mathf.FloorToInt(top / CellStep.y);
            return left >= 0f && top >= 0f && left <= Rect.rect.width && top <= Rect.rect.height
                && x >= 0 && y >= 0 && x < grid.Width && y < grid.Height;
        }

        public void ShowPreview(EquipmentInstance item, int x, int y, bool rotated, bool valid)
        {
            if (placementPreview == null)
                placementPreview = BattleUiFactory.Image("PlacementPreview", transform, Color.white);
            placementPreview.gameObject.SetActive(true);
            placementPreview.rectTransform.anchoredPosition = new Vector2(x * CellStep.x, -y * CellStep.y);
            placementPreview.rectTransform.sizeDelta = PlacementSize(item, rotated);
            placementPreview.color = valid ? new Color(0.20f, 0.80f, 0.43f, 0.48f) : new Color(0.92f, 0.23f, 0.25f, 0.48f);
            placementPreview.transform.SetAsLastSibling();
        }

        public void HidePreview()
        {
            if (placementPreview != null) placementPreview.gameObject.SetActive(false);
        }

        public static InventoryGridView FindGrid(BattleView battleView, Vector2 screenPoint, Camera camera, out int x, out int y)
        {
            x = y = -1;
            foreach (InventoryGridView candidate in registered)
                if (candidate != null && ReferenceEquals(candidate.view, battleView) && candidate.TryGetCell(screenPoint, camera, out x, out y))
                    return candidate;
            return null;
        }

        public static void ClearPreviews(BattleView battleView)
        {
            foreach (InventoryGridView candidate in registered)
                if (candidate != null && ReferenceEquals(candidate.view, battleView)) candidate.HidePreview();
        }

        private void OnEnable() { if (!registered.Contains(this)) registered.Add(this); }
        private void OnDisable() { registered.Remove(this); HidePreview(); }
        private void OnDestroy() { registered.Remove(this); }
    }
}
