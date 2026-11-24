using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Click previews; dragging uses a non-interactive ghost and an atomic controller transfer.</summary>
    public sealed class EquipmentItemView : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private InventoryGridView source;
        private EquipmentInstance item;
        private Image background;
        private Text label;
        private Outline outline;
        private RectTransform ghost;
        private CanvasGroup sourceGroup;
        private bool previewRotated;
        private bool dragging;
        private float suppressClickUntil;
        private Vector2 pointerPosition;
        private Camera pointerCamera;
        private InventoryGridView hoveredGrid;
        private int hoveredX;
        private int hoveredY;
        private bool validPlacement;
        private string placementError;
        private Vector2 grabOffset;
        private int grabbedCellX;
        private int grabbedCellY;

        public EquipmentInstance Item => item;
        public InventoryGridView Source => source;
        public bool IsDragging => dragging;

        public void Initialize(InventoryGridView grid, EquipmentInstance equipment, Image image)
        {
            source = grid;
            item = equipment;
            background = image;
            label = BattleUiFactory.Text("Label", transform, BattleView.GetInterfaceFont(), 14);
            label.color = new Color(0.23f, 0.16f, 0.12f, 1f);
            BattleUiFactory.Stretch(label.rectTransform, 4f);
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.46f, 0.22f, 0.12f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = false;
            sourceGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Refresh(int x, int y)
        {
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(x * source.CellStep.x, -y * source.CellStep.y);
            rect.sizeDelta = source.PlacementSize(item, item.Rotated);
            background.color = PaperColor(item.Definition.kind);
            label.text = item.Definition.displayName + (item.Rotated && !source.IsEquipmentSlot ? " ↻" : "");
            outline.enabled = ReferenceEquals(source.View.SelectedEquipment, this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragging || Time.unscaledTime < suppressClickUntil) return;
            source.View.SelectEquipment(this, false);
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Rotate();
                source.View.InfoPanel.InspectItem(item);
            }
            else if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            {
                string error;
                BattleUnitState recipient = source.View.InfoPanel.InspectedAlly;
                if (!source.View.Controller.TryAutoMoveEquipment(item, source.Grid, recipient, out error))
                    source.View.ShowHint(error);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                source.View.InfoPanel.InspectItem(item);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!dragging && source != null && source.View != null)
                source.View.InfoPanel.PreviewItem(item, this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (source != null && source.View != null && source.View.InfoPanel != null) source.View.InfoPanel.HidePreview(this);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = true; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || source == null || item == null
                || !source.View.Controller.CanModifyEquipment || !source.View.BeginEquipmentDrag(this)) return;
            dragging = true;
            previewRotated = item.Rotated;
            Vector2 pressLocal;
            RectTransform ownRect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ownRect, eventData.pressPosition,
                eventData.pressEventCamera, out pressLocal);
            grabOffset = new Vector2(pressLocal.x - ownRect.rect.xMin, ownRect.rect.yMax - pressLocal.y);
            grabbedCellX = source.IsEquipmentSlot ? 0 : Mathf.Clamp(Mathf.FloorToInt(grabOffset.x / source.CellStep.x), 0, item.Width - 1);
            grabbedCellY = source.IsEquipmentSlot ? 0 : Mathf.Clamp(Mathf.FloorToInt(grabOffset.y / source.CellStep.y), 0, item.Height - 1);
            sourceGroup.alpha = 0.35f;
            Image ghostImage = BattleUiFactory.Image("EquipmentDragGhost", source.View.DragLayer, PaperColor(item.Definition.kind));
            ghost = ghostImage.rectTransform;
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0f, 1f);
            CanvasGroup group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.85f;
            Text ghostLabel = BattleUiFactory.Text("Label", ghost, BattleView.GetInterfaceFont(), 13);
            ghostLabel.color = new Color(0.23f, 0.16f, 0.12f, 1f);
            ghostLabel.text = item.Definition.displayName;
            BattleUiFactory.Stretch(ghostLabel.rectTransform, 2f);
            UpdateGhostSize();
            source.View.ShowHint("拖动到绿框落位；红框不可放；右键 / R 转向。");
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            pointerPosition = eventData.position;
            pointerCamera = eventData.pressEventCamera;
            UpdateDragPreview();
        }

        private void UpdateDragPreview()
        {
            if (!dragging || ghost == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(source.View.DragLayer,
                pointerPosition, pointerCamera, out local)) ghost.localPosition = local + new Vector2(-grabOffset.x, grabOffset.y);
            InventoryGridView.ClearPreviews(source.View);
            hoveredGrid = InventoryGridView.FindGrid(source.View, pointerPosition, pointerCamera, out hoveredX, out hoveredY);
            validPlacement = false;
            placementError = "未放入目标网格，物品保留原位。";
            if (hoveredGrid != null)
            {
                hoveredX = hoveredGrid.IsEquipmentSlot ? 0 : hoveredX - grabbedCellX;
                hoveredY = hoveredGrid.IsEquipmentSlot ? 0 : hoveredY - grabbedCellY;
                validPlacement = source.View.Controller.CanModifyEquipment
                    && hoveredGrid.Grid.CanPlace(item, hoveredX, hoveredY, previewRotated, out placementError);
                if (hoveredGrid.Grid.Owner != null && !hoveredGrid.Grid.Owner.IsAlive)
                {
                    validPlacement = false;
                    placementError = "倒下的角色不能换装。";
                }
                hoveredGrid.ShowPreview(item, hoveredX, hoveredY, previewRotated, validPlacement);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            pointerPosition = eventData.position;
            UpdateDragPreview();
            InventoryGridView destination = hoveredGrid;
            int x = hoveredX, y = hoveredY;
            bool rotated = previewRotated;
            bool valid = validPlacement;
            string error = placementError;
            InventoryGridState originalGrid = source.Grid;
            EquipmentInstance movingItem = item;
            BattleView view = source.View;
            CancelDrag();
            suppressClickUntil = Time.unscaledTime + 0.15f;
            if (destination == null || !valid) { view.ShowHint(error); return; }
            if (!view.Controller.TryMoveEquipment(movingItem, originalGrid, destination.Grid, x, y, rotated, out error))
                view.ShowHint(error);
        }

        public void CancelDrag()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            if (sourceGroup != null) sourceGroup.alpha = 1f;
            if (source != null && source.View != null)
            {
                InventoryGridView.ClearPreviews(source.View);
                source.View.EndEquipmentDrag(this);
            }
            dragging = false;
            hoveredGrid = null;
        }

        public void Rotate()
        {
            if (source == null || item == null) return;
            if (!source.View.Controller.CanModifyEquipment)
            {
                source.View.ShowHint("当前只能查看；战前整备或进入整顿后可以换装。");
                return;
            }
            if (dragging)
            {
                if (!item.Definition.canRotate) { source.View.ShowHint("该物品不可转向。"); return; }
                previewRotated = !previewRotated;
                UpdateGhostSize();
                UpdateDragPreview();
            }
            else
            {
                string error;
                if (!source.View.Controller.TryRotateEquipment(item, source.Grid, out error)) source.View.ShowHint(error);
            }
        }

        private void UpdateGhostSize()
        {
            int width = previewRotated ? item.Definition.height : item.Definition.width;
            int height = previewRotated ? item.Definition.width : item.Definition.height;
            if (ghost != null)
            {
                // An equipped item occupies one slot, but returns to its actual footprint in the bag.
                InventoryGridView bag = source.View.BackpackGrid;
                ghost.sizeDelta = bag != null ? bag.ItemSize(width, height) : source.ItemSize(width, height);
                grabbedCellX = Mathf.Clamp(grabbedCellX, 0, width - 1);
                grabbedCellY = Mathf.Clamp(grabbedCellY, 0, height - 1);
                grabOffset.x = Mathf.Clamp(grabOffset.x, 0f, ghost.sizeDelta.x - 1f);
                grabOffset.y = Mathf.Clamp(grabOffset.y, 0f, ghost.sizeDelta.y - 1f);
            }
        }

        private void Update()
        {
            if (source == null || item == null) return;
            if (outline != null) outline.enabled = ReferenceEquals(source.View.SelectedEquipment, this);
            bool rotateMouse = dragging && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            if (rotateMouse) Rotate();
        }

        private static Color PaperColor(EquipmentSlotKind kind)
        {
            switch (kind)
            {
                case EquipmentSlotKind.Handheld: return new Color(0.69f, 0.76f, 0.69f, 0.96f);
                case EquipmentSlotKind.Worn: return new Color(0.74f, 0.68f, 0.53f, 0.96f);
                default: return new Color(0.82f, 0.68f, 0.42f, 0.96f);
            }
        }

        private void OnDisable() { if (source != null && source.View != null && source.View.InfoPanel != null) source.View.InfoPanel.HidePreview(this); CancelDrag(); }
        private void OnDestroy() { CancelDrag(); }
    }
}
