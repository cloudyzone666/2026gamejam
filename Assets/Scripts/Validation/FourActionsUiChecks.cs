#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CardCards.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;

namespace CardCards.Validation
{
    /// <summary>Opt-in pointer and keyboard checks through the real Input System UI module.</summary>
    public static class FourActionsUiChecks
    {
        private sealed class PlacementSnapshot
        {
            public EquipmentInstance Item;
            public InventoryGridState Grid;
            public int X;
            public int Y;
            public bool Rotated;
        }

        public static IEnumerator Run(BattleController controller, BattleView view, List<string> checks,
            List<string> inputDiagnostics = null)
        {
            Require(controller != null && view != null, "UI checks require the battle controller and view.");
            Require(controller.Phase == BattlePhase.Preparation && controller.Allies.Count == 4,
                "UI checks require the initial four-person preparation.");
            var module = EventSystem.current == null ? null : EventSystem.current.currentInputModule as InputSystemUIInputModule;
            Require(module != null && module.actionsAsset != null, "UI checks require the real InputSystemUIInputModule.");
            var originalOrder = new List<BattleUnitState>(controller.Allies);
            var originalPlacements = SnapshotPlacements(controller);
            BattleUnitState actor = originalOrder[0];
            EquipmentInstance sword = FindNamed(actor.EquipmentGrids[0], "精铁剑");
            EquipmentInstance dagger = FindNamed(controller.Backpack, "短匕");
            EquipmentInstance warMark = FindNamed(controller.Backpack, "战纹");
            PlacementSnapshot daggerOrigin = FindSnapshot(originalPlacements, dagger);
            PlacementSnapshot markOrigin = FindSnapshot(originalPlacements, warMark);
            InputSettings previousSettings = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(previousSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputActionAsset actions = module.actionsAsset;
            ReadOnlyArray<InputDevice>? previousDevices = actions.devices;
            Mouse previousMouse = Mouse.current;
            Keyboard previousKeyboard = Keyboard.current;
            Mouse mouse = null;
            Keyboard keyboard = null;
            var suspendedDevices = new List<InputDevice>();
            try
            {
                InputSystem.settings = settings;
                // UI actions.devices does not isolate direct Mouse.current/Keyboard.current polling.
                // Suspend competing devices only in this opt-in test, restoring their enabled state below.
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (!(device is Mouse) && !(device is Keyboard) || !device.enabled) continue;
                    suspendedDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
                mouse = InputSystem.AddDevice<Mouse>("FourActionsUiMouse");
                keyboard = InputSystem.AddDevice<Keyboard>("FourActionsUiKeyboard");
                actions.devices = new InputDevice[] { mouse, keyboard };
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Canvas.ForceUpdateCanvases();
                TracePointer(view, mouse, Vector2.zero, "isolated virtual mouse/keyboard; suspended=" + suspendedDevices.Count,
                    inputDiagnostics);

                BattleUnitState originalInspection = view.InfoPanel.InspectedAlly;
                Vector2 emptyPoint = new Vector2(15f, Screen.height - 15f);
                var rotateButton = ReadField<UnityEngine.UI.Button>(view, "rotateButton");
                Require(!controller.CanBeginAction(BattleActionKind.Rotate, out string disabledReason) && !rotateButton.IsInteractable(),
                    "Preparation must keep rotation disabled.");
                yield return Move(mouse, Center((RectTransform)rotateButton.transform));
                Require(view.InfoPanel.Tooltip.IsVisible && !view.InfoPanel.Tooltip.IsPinned &&
                    ReadField<UnityEngine.UI.Text>(view.InfoPanel.Tooltip, "bodyText").text.Contains(disabledReason),
                    "Hovering a disabled painted action must explain why the action is unavailable.");
                yield return Move(mouse, emptyPoint);
                Require(!view.InfoPanel.Tooltip.IsVisible, "Action hover details did not close on pointer exit.");
                checks.Add("Disabled painted action controls expose the controller's current unavailability reason on real pointer hover");
                yield return Move(mouse, CardCenter(view, controller.Enemies[0]));
                Require(view.InfoPanel.HasEnemyTooltip && !view.InfoPanel.Tooltip.IsPinned &&
                    ReferenceEquals(view.InfoPanel.InspectedAlly, originalInspection),
                    "Enemy hover must show temporary details without replacing the book's selected ally.");
                foreach (UnityEngine.UI.Graphic graphic in view.InfoPanel.Tooltip.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    Require(!graphic.raycastTarget, "Tooltip graphics must not intercept battle targets or equipment drops.");
                CanvasGroup tooltipGroup = ReadField<RectTransform>(view.InfoPanel.Tooltip, "panel").GetComponent<CanvasGroup>();
                Require(tooltipGroup != null && !tooltipGroup.blocksRaycasts && !tooltipGroup.interactable,
                    "Tooltip must be nonblocking for the real EventSystem input path.");
                yield return Move(mouse, emptyPoint);
                Require(!view.InfoPanel.Tooltip.IsVisible, "Hover details did not disappear after the pointer left the enemy.");
                yield return Click(mouse, CardCenter(view, controller.Enemies[0]), MouseButton.Left);
                Require(view.InfoPanel.HasEnemyTooltip && view.InfoPanel.Tooltip.IsPinned &&
                    ReferenceEquals(view.InfoPanel.InspectedAlly, originalInspection),
                    "Enemy click must pin its tooltip and preserve ally equipment inspection.");
                yield return Move(mouse, emptyPoint);
                Require(view.InfoPanel.Tooltip.IsPinned, "Pinned details vanished when the pointer moved away.");
                yield return PressKey(keyboard, Key.Escape);
                Require(!view.InfoPanel.Tooltip.IsVisible, "Escape must close pinned details.");
                yield return Click(mouse, CardCenter(view, controller.Enemies[0]), MouseButton.Left);
                Require(view.InfoPanel.Tooltip.IsPinned && ReferenceEquals(Mouse.current, mouse),
                    "Outside-click verification requires pinned details and its isolated virtual Mouse.current.");
                RectTransform tooltipPanel = ReadField<RectTransform>(view.InfoPanel.Tooltip, "panel");
                Require(!RectTransformUtility.RectangleContainsScreenPoint(tooltipPanel, emptyPoint, null),
                    "Outside-click test point unexpectedly lies inside the tooltip.");
                yield return Click(mouse, emptyPoint, MouseButton.Left,
                    step => TracePointer(view, mouse, emptyPoint, "outside-click " + step, inputDiagnostics));
                string outsideTrace = TracePointer(view, mouse, emptyPoint, "outside-click assertion", inputDiagnostics);
                Require(!view.InfoPanel.Tooltip.IsVisible, "Clicking outside pinned details must close them. " + outsideTrace);
                checks.Add("Real enemy hover previews and pointer exit hides; click pins, outside click/Escape closes, the book retains its ally, and tooltip graphics never block UI input");

                // Keep the model's identity order even though the ally row is visually reversed.
                Vector2 first = CardCenter(view, originalOrder[0]);
                Vector2 second = CardCenter(view, originalOrder[1]);
                yield return Drag(mouse, CardCenter(view, originalOrder[3]), (first + second) * .5f);
                Require(ReferenceEquals(controller.Allies[0], originalOrder[0])
                    && ReferenceEquals(controller.Allies[1], originalOrder[3])
                    && ReferenceEquals(controller.Allies[2], originalOrder[1])
                    && ReferenceEquals(controller.Allies[3], originalOrder[2]),
                    "Pointer card drag must produce [A,D,B,C] and preserve the other relative order.");
                Require(controller.TryMoveAlly(originalOrder[3], 3, out string error), "Card drag restore failed: " + error);
                checks.Add("Real pointer drag inserts D before B as [A,D,B,C], with stable unit identities");
                yield return null;
                Canvas.ForceUpdateCanvases();

                EquipmentItemView markView = FindItem(view, warMark);
                yield return Click(mouse, Center((RectTransform)markView.transform), MouseButton.Right);
                Require(warMark.Rotated && warMark.Width == 1 && warMark.Height == 2,
                    "Right-click did not turn the 2x1 war mark to 1x2.");
                Require(view.InfoPanel.HasItemTooltip,
                    "Right-click war mark inspection did not open a floating item tooltip.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Require(!warMark.Rotated && warMark.Width == 2 && warMark.Height == 1,
                    "R with item details open did not restore the war mark to 2x1.");
                Require(ReferenceEquals(warMark.Grid, markOrigin.Grid) && warMark.X == markOrigin.X && warMark.Y == markOrigin.Y,
                    "Right-click / R unexpectedly moved the war mark anchor.");
                checks.Add("Real right-click and R turn a selected backpack item while its floating details remain available");

                controller.SelectAlly(actor);
                view.InfoPanel.InspectUnit(actor);
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return DoubleClick(mouse, Center((RectTransform)FindItem(view, sword).transform));
                Require(ReferenceEquals(sword.Grid, controller.Backpack) && actor.EquipmentGrids[0].Placements.Count == 0,
                    "Real double-click on the equipped sword did not unequip it to the shared backpack.");
                yield return new WaitForSecondsRealtime(.35f);
                yield return DoubleClick(mouse, Center((RectTransform)FindItem(view, dagger).transform));
                Require(ReferenceEquals(dagger.Grid, actor.EquipmentGrids[0]),
                    "Real double-click on the backpack dagger did not equip it to inspected actor A.");
                Require(controller.TryMoveEquipment(dagger, dagger.Grid, daggerOrigin.Grid,
                    daggerOrigin.X, daggerOrigin.Y, daggerOrigin.Rotated, out error), "Dagger cleanup failed: " + error);
                Require(controller.TryAutoMoveEquipment(sword, sword.Grid, actor, out error), "Sword re-equip cleanup failed: " + error);
                Require(ReferenceEquals(sword.Grid, actor.EquipmentGrids[0]), "Sword cleanup restored the wrong equipment area.");
                checks.Add("Real double-click unequips an equipped sword and equips a backpack dagger to the inspected character");

                view.InfoPanel.InspectUnit(actor);
                yield return null;
                Canvas.ForceUpdateCanvases();
                InventoryGridState originalGrid = warMark.Grid;
                int originalX = warMark.X, originalY = warMark.Y;
                bool originalRotation = warMark.Rotated;
                Vector2 outside = RectTransformUtility.WorldToScreenPoint(null,
                    view.DragLayer.TransformPoint(new Vector3(0f, view.DragLayer.rect.yMax - 20f, 0f)));
                view.InfoPanel.InspectItem(warMark);
                yield return Drag(mouse, Center((RectTransform)FindItem(view, warMark).transform), outside, view.InfoPanel.Tooltip);
                Require(ReferenceEquals(warMark.Grid, originalGrid) && warMark.X == originalX
                    && warMark.Y == originalY && warMark.Rotated == originalRotation,
                    "Dropping outside all grids changed or lost the original war mark instance.");
                var wrongGrid = ReadField<InventoryGridView>(view.InfoPanel, "wornGrid");
                yield return Drag(mouse, Center((RectTransform)FindItem(view, warMark).transform), FirstCellCenter(wrongGrid));
                Require(ReferenceEquals(warMark.Grid, originalGrid) && warMark.X == originalX
                    && warMark.Y == originalY && warMark.Rotated == originalRotation,
                    "Dropping an accessory in the worn area changed its original placement.");
                checks.Add("Starting a real equipment drag closes details; drops outside grids or on the wrong slot preserve the same instance and placement");

                yield return new WaitForSecondsRealtime(.35f);
                yield return Click(mouse, Center((RectTransform)FindItem(view, warMark).transform), MouseButton.Left);
                yield return new WaitForSecondsRealtime(.35f);
                Require(view.InfoPanel.HasItemTooltip && view.InfoPanel.Tooltip.IsPinned,
                    "Single pointer click did not pin floating item details after its double-click window.");
                Require(ReferenceEquals(warMark.Grid, originalGrid) && warMark.X == originalX
                    && warMark.Y == originalY && warMark.Rotated == originalRotation && !view.IsDraggingEquipment,
                    "Click-only inspection started a drag or changed the war mark placement.");
                yield return Move(mouse, emptyPoint);
                Require(view.InfoPanel.Tooltip.IsPinned, "Item click details must remain pinned after pointer exit.");
                yield return PressKey(keyboard, Key.Escape);
                Require(!view.InfoPanel.Tooltip.IsVisible, "Escape did not close the pinned item details.");
                checks.Add("Real single-click pins item details without moving equipment or starting a drag; pointer exit preserves them and Escape closes them");
            }
            finally
            {
                try
                {
                    foreach (EquipmentItemView itemView in view.GetComponentsInChildren<EquipmentItemView>(true)) itemView.CancelDrag();
                    RestorePlacements(controller, originalPlacements);
                    for (int index = 0; index < originalOrder.Count; index++)
                        controller.TryMoveAlly(originalOrder[index], index, out _);
                    view.SelectEquipment(null, false);
                    view.InfoPanel.InspectUnit(actor);
                    view.Refresh();
                }
                finally
                {
                    actions.devices = previousDevices;
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                    if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    foreach (InputDevice device in suspendedDevices)
                        if (device.added) InputSystem.EnableDevice(device);
                    if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                    if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                    InputSystem.settings = previousSettings;
                    UnityEngine.Object.Destroy(settings);
                }
            }
        }

        private static IEnumerator Click(Mouse mouse, Vector2 point, MouseButton button, Action<string> trace = null)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            trace?.Invoke("move first frame");
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(button));
            yield return null;
            trace?.Invoke("press first frame");
            yield return null;
            trace?.Invoke("press second frame");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            trace?.Invoke("release first frame");
            yield return null;
        }

        private static string TracePointer(BattleView view, Mouse injected, Vector2 requested, string step,
            List<string> diagnostics)
        {
            Mouse current = Mouse.current;
            Keyboard currentKeyboard = Keyboard.current;
            RectTransform panel = ReadField<RectTransform>(view.InfoPanel.Tooltip, "panel");
            Vector2 observed = current == null ? Vector2.zero : current.position.ReadValue();
            string message = "BookUI " + step + ": frame=" + Time.frameCount
                + ", current=" + (current == null ? "none" : current.name + "#" + current.deviceId)
                + ", injected=" + (injected == null ? "none" : injected.name + "#" + injected.deviceId)
                + ", same=" + ReferenceEquals(current, injected) + ", currentPos=" + observed
                + ", injectedPos=" + (injected == null ? Vector2.zero : injected.position.ReadValue())
                + ", requested=" + requested
                + ", currentPressed=" + (current != null && current.leftButton.wasPressedThisFrame)
                + ", injectedPressed=" + (injected != null && injected.leftButton.wasPressedThisFrame)
                + ", currentDown=" + (current != null && current.leftButton.isPressed)
                + ", containsCurrent=" + RectTransformUtility.RectangleContainsScreenPoint(panel, observed, null)
                + ", containsRequested=" + RectTransformUtility.RectangleContainsScreenPoint(panel, requested, null)
                + ", visible=" + view.InfoPanel.Tooltip.IsVisible + ", pinned=" + view.InfoPanel.Tooltip.IsPinned
                + ", keyboard=" + (currentKeyboard == null ? "none" : currentKeyboard.name + "#" + currentKeyboard.deviceId);
            diagnostics?.Add(message);
            Debug.Log(message);
            return message;
        }

        private static IEnumerator Move(Mouse mouse, Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }

        private static IEnumerator PressKey(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static IEnumerator DoubleClick(Mouse mouse, Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            // One frame per press/release keeps both complete clicks inside the UI module's click interval.
            for (int count = 0; count < 2; count++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
            }
            yield return null;
        }

        private static IEnumerator Drag(Mouse mouse, Vector2 start, Vector2 end, BookTooltipView closingTooltip = null)
        {
            Canvas.ForceUpdateCanvases();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            for (int step = 1; step <= 6; step++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(start, end, step / 6f) }.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                if (closingTooltip != null && step == 3)
                    Require(!closingTooltip.IsVisible, "Equipment drag must close floating details before the drop.");
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
            yield return null;
            yield return null;
        }

        private static List<PlacementSnapshot> SnapshotPlacements(BattleController controller)
        {
            var result = new List<PlacementSnapshot>();
            AddSnapshots(controller.Backpack, result);
            foreach (BattleUnitState ally in controller.Allies)
                foreach (InventoryGridState grid in ally.EquipmentGrids) AddSnapshots(grid, result);
            return result;
        }

        private static void AddSnapshots(InventoryGridState grid, List<PlacementSnapshot> snapshots)
        {
            foreach (InventoryPlacement placement in grid.Placements)
                snapshots.Add(new PlacementSnapshot { Item = placement.Item, Grid = grid, X = placement.X, Y = placement.Y, Rotated = placement.Item.Rotated });
        }

        private static void RestorePlacements(BattleController controller, List<PlacementSnapshot> snapshots)
        {
            // Items touched here have no recovery/stat effects. Multiple passes tolerate dependent free spaces.
            for (int pass = 0; pass < snapshots.Count; pass++)
            {
                bool complete = true;
                foreach (PlacementSnapshot snapshot in snapshots)
                {
                    if (ReferenceEquals(snapshot.Item.Grid, snapshot.Grid) && snapshot.Item.X == snapshot.X
                        && snapshot.Item.Y == snapshot.Y && snapshot.Item.Rotated == snapshot.Rotated) continue;
                    complete = false;
                    controller.TryMoveEquipment(snapshot.Item, snapshot.Item.Grid, snapshot.Grid,
                        snapshot.X, snapshot.Y, snapshot.Rotated, out _);
                }
                if (complete) return;
            }
            foreach (PlacementSnapshot snapshot in snapshots)
                if (!ReferenceEquals(snapshot.Item.Grid, snapshot.Grid) || snapshot.Item.X != snapshot.X
                    || snapshot.Item.Y != snapshot.Y || snapshot.Item.Rotated != snapshot.Rotated)
                    Debug.LogWarning("UI verification could not restore " + snapshot.Item.Definition.displayName + " to its original grid.");
        }

        private static PlacementSnapshot FindSnapshot(List<PlacementSnapshot> snapshots, EquipmentInstance item)
        {
            foreach (PlacementSnapshot snapshot in snapshots) if (ReferenceEquals(snapshot.Item, item)) return snapshot;
            throw new InvalidOperationException("UI check snapshot missing: " + item.Definition.displayName);
        }

        private static EquipmentInstance FindNamed(InventoryGridState grid, string name)
        {
            foreach (InventoryPlacement placement in grid.Placements)
                if (placement.Item.Definition.displayName == name) return placement.Item;
            throw new InvalidOperationException("UI checks need the initial item: " + name);
        }

        private static EquipmentItemView FindItem(BattleView view, EquipmentInstance item)
        {
            foreach (EquipmentItemView candidate in view.GetComponentsInChildren<EquipmentItemView>())
                if (ReferenceEquals(candidate.Item, item)) return candidate;
            throw new InvalidOperationException("No active item view for " + item.Definition.displayName);
        }

        private static Vector2 CardCenter(BattleView view, BattleUnitState unit)
        {
            foreach (CharacterCardView candidate in view.GetComponentsInChildren<CharacterCardView>())
                if (ReferenceEquals(candidate.Unit, unit)) return Center((RectTransform)candidate.transform);
            throw new InvalidOperationException("No active card view for " + unit.Name);
        }

        private static Vector2 FirstCellCenter(InventoryGridView grid)
        {
            RectTransform rect = (RectTransform)grid.transform;
            return RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector3(rect.rect.xMin + 20f, rect.rect.yMax - 20f, 0f)));
        }

        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static T ReadField<T>(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, "UI check binding missing: " + field);
            return (T)info.GetValue(owner);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
