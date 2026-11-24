#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CardCards.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CardCards.Validation
{
    // Opt-in verification only. Normal launches create no object and run no checks.
    public sealed class BattlePlayerSmoke : MonoBehaviour
    {
        private BattleVerificationReport report;
        private string reportPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--battle-smoke") < 0)
                return;
            // The automated player may start without OS focus; normal launches keep authored settings.
            Application.runInBackground = true;
            new GameObject("Development Battle Verification").AddComponent<BattlePlayerSmoke>();
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int pathIndex = Array.IndexOf(arguments, "--battle-smoke-report");
            reportPath = pathIndex >= 0 && pathIndex + 1 < arguments.Length
                ? Path.GetFullPath(arguments[pathIndex + 1])
                : Path.Combine(Application.dataPath, "..", "Logs", "BookUI-PlayerVerification.json");
            report = new BattleVerificationReport { source = "Windows Development Player" };
            Application.logMessageReceived += ReceiveLog;
            yield return null;
            var stack = new Stack<IEnumerator>();
            stack.Push(BattleVerificationSuite.Run(report, reportPath));
            while (stack.Count > 0)
            {
                object current = null;
                Exception failure = null;
                try
                {
                    IEnumerator iterator = stack.Peek();
                    if (!iterator.MoveNext()) stack.Pop();
                    else current = iterator.Current;
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    report.failure = failure.ToString();
                    break;
                }
                if (current is IEnumerator nested) stack.Push(nested);
                else if (stack.Count > 0) yield return current;
            }
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception exception) { report.runtimeErrors.Add("Verification cleanup: " + exception); }
            }
            report.success = string.IsNullOrEmpty(report.failure) && report.runtimeErrors.Count == 0;
            BattleVerificationSuite.WriteReport(report, reportPath);
            Application.logMessageReceived -= ReceiveLog;
            Application.Quit(report.success ? 0 : 1);
        }

        private void ReceiveLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.runtimeErrors.Add(message + "\n" + trace);
        }
    }

    [Serializable]
    public sealed class BattleVerificationReport
    {
        public string source;
        public bool success;
        public string failure;
        public string completedUtc;
        public string unityVersion;
        public string graphicsDevice;
        public int screenWidth;
        public int screenHeight;
        public string font;
        public string builtInFallbackFont;
        public List<string> checks = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<string> textSnapshot = new List<string>();
        public List<string> runtimeErrors = new List<string>();
        public List<string> inputDiagnostics = new List<string>();
    }

    /// <summary>Shared by the real Play Mode runner and the opt-in Windows player.</summary>
    public static class BattleVerificationSuite
    {
        private sealed class Context
        {
            public BattleController controller;
            public BattleView view;
            public BattleVerificationReport report;
            public string reportPath;
            public Button Button(string field) => ReadField<Button>(view, field);
        }

        public static IEnumerator Run(BattleVerificationReport report, string reportPath)
        {
            var suspendedDevices = new List<InputDevice>();
            Mouse previousMouse = Mouse.current;
            Keyboard previousKeyboard = Keyboard.current;
            try
            {
                // Keep native input from reopening hover details between the suite's virtual devices.
                // This state is confined to opt-in verification and restored even if a nested check fails.
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (!(device is Mouse) && !(device is Keyboard) || !device.enabled) continue;
                    suspendedDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
                report.inputDiagnostics.Add("BookUI suite suspended enabled native mouse/keyboard devices: " + suspendedDevices.Count);
                yield return RunIsolated(report, reportPath);
            }
            finally
            {
                foreach (InputDevice device in suspendedDevices)
                    if (device.added) InputSystem.EnableDevice(device);
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            }
        }

        private static IEnumerator RunIsolated(BattleVerificationReport report, string reportPath)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            BattleController controller;
            while ((controller = UnityEngine.Object.FindObjectOfType<BattleController>()) == null || controller.Backpack == null)
            {
                Require(Time.realtimeSinceStartup < deadline, "Battle scene initialization timed out.");
                yield return null;
            }
            var context = new Context { controller = controller, view = UnityEngine.Object.FindObjectOfType<BattleView>(), report = report, reportPath = reportPath };
            Require(context.view != null, "BattleView missing.");
            report.unityVersion = Application.unityVersion;
            report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            report.screenWidth = Screen.width; report.screenHeight = Screen.height;
            Require(EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule,
                "New Input System UI module missing.");
            Require(context.view.GetComponent<CanvasScaler>().referenceResolution == new Vector2(1280, 720), "Reference resolution changed.");
            if (!Application.isEditor) Require(Screen.width == 1280 && Screen.height == 720 && !Screen.fullScreen, "Player window is not 1280x720.");
            SetField(controller, "enemyActionDelay", .01f);
            yield return FourActionsRuleChecks.Run(controller, report.checks);
            Require(controller.Phase == BattlePhase.Preparation && controller.Allies.Count == 4 && controller.Enemies.Count == 4, "Default 4v4 preparation missing.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            VerifyFont(context); VerifyLayout(context, "allyContainer", 4); VerifyLayout(context, "enemyContainer", 4);
            Require(controller.Backpack.Width == 5 && controller.Backpack.Height == 3 && controller.Backpack.Placements.Count == 4, "Starting 5x3 backpack invalid.");
            foreach (BattleUnitState ally in controller.Allies)
            {
                Require(ally.EquipmentGrids.Count == 3 && ally.EquipmentGrids[0].Placements.Count == 1 && ally.EquipmentGrids[1].Placements.Count == 1, "Starting equipment missing.");
                foreach (InventoryGridState slot in ally.EquipmentGrids)
                    Require(slot.Width == 1 && slot.Height == 1 && slot.IsEquipmentSlot && slot.Placements.Count <= 1,
                        "Character equipment must use three single-item slots.");
            }
            VerifyBookBindings(context);
            VerifyShieldBars(context);
            yield return Capture(context, "BookUI-01-preparation");

            yield return FourActionsUiChecks.Run(controller, context.view, report.checks, report.inputDiagnostics);

            yield return PointerClick(context, CardButton(context, controller.Enemies[0]));
            Require(context.view.InfoPanel.HasEnemyTooltip && context.view.InfoPanel.Tooltip.IsPinned, "Enemy inspection tooltip did not pin.");
            yield return PointerClick(context, CardButton(context, controller.Allies[0]));
            bool inspectedMatches = ReferenceEquals(context.view.InfoPanel.InspectedAlly, controller.Allies[0]);
            Require(inspectedMatches && !context.view.InfoPanel.Tooltip.IsVisible,
                "Ally inspection did not update the book and close floating details: inspectedMatches=" + inspectedMatches
                + ", inspected=" + (context.view.InfoPanel.InspectedAlly == null ? "none" : context.view.InfoPanel.InspectedAlly.Name)
                + ", expected=" + controller.Allies[0].Name + ", tooltipVisible=" + context.view.InfoPanel.Tooltip.IsVisible
                + ", tooltipPinned=" + context.view.InfoPanel.Tooltip.IsPinned + ", tooltipTitle=" + context.view.InfoPanel.Tooltip.Title
                + ", currentMouse=" + (Mouse.current == null ? "none" : Mouse.current.name + "#" + Mouse.current.deviceId));
            Check(context, "Virtual mouse selects allies in the book and pins enemy details without replacing the equipment page");

            BattleUnitState actor = controller.Allies[0];
            EquipmentInstance sword = actor.EquipmentGrids[0].Placements[0].Item;
            Require(controller.TryAutoMoveEquipment(sword, actor.EquipmentGrids[0], actor, out string moveError), moveError);
            EquipmentInstance dagger = null;
            foreach (InventoryPlacement placement in controller.Backpack.Placements)
                if (placement.Item.Definition.displayName == "短匕") dagger = placement.Item;
            Require(dagger != null, "Dagger missing.");
            yield return null;
            EquipmentItemView daggerView = FindItem(context, dagger);
            var handGrid = ReadField<InventoryGridView>(context.view.InfoPanel, "handheldGrid");
            Vector2 start = Center((RectTransform)daggerView.transform);
            RectTransform handRect = (RectTransform)handGrid.transform;
            Vector2 destination = RectTransformUtility.WorldToScreenPoint(null,
                handRect.TransformPoint(new Vector3(handRect.rect.xMin + 20f, handRect.rect.yMax - 20f, 0f)));
            yield return PointerDrag(context, start, destination);
            Require(ReferenceEquals(dagger.Grid, actor.EquipmentGrids[0]), "Real UI drag did not equip dagger.");
            Check(context, "Virtual mouse drag equips backpack item into a real equipment grid without losing instances");
            context.view.InfoPanel.InspectItem(dagger);
            yield return Capture(context, "BookUI-02-item-tooltip");
            context.view.InfoPanel.InspectUnit(actor);
            yield return Capture(context, "BookUI-03-equipment");

            yield return PointerClick(context, context.Button("endTurnButton"));
            Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4 && !controller.CanModifyEquipment, "Start battle did not lock equipment.");
            yield return PointerClick(context, CardButton(context, actor));
            yield return PointerClick(context, context.Button("rotateButton"));
            Require(controller.Phase == BattlePhase.SelectingTarget, "Rotate did not request a target.");
            Require(!controller.CanTarget(controller.Enemies[3]), "Out-of-range target is legal.");
            yield return Capture(context, "BookUI-04-target-range");
            yield return PointerClick(context, context.Button("cancelButton"));
            Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4 && !actor.IsRotated,
                "Pointer cancellation changed AP or card orientation.");
            yield return PointerClick(context, CardButton(context, actor));
            Require(ReferenceEquals(controller.SelectedAlly, actor), "Pointer re-selection after cancellation did not restore the actor.");
            yield return PointerClick(context, context.Button("rotateButton"));
            yield return PointerClick(context, CardButton(context, controller.Enemies[0]));
            Require(actor.IsRotated && controller.ActionPoints == 3, "Rotate failed through pointer input.");
            yield return new WaitForSecondsRealtime(.3f);
            VerifyShieldBars(context);
            yield return PointerClick(context, CardButton(context, controller.Allies[2]));
            yield return PointerClick(context, context.Button("standbyButton"));
            yield return PointerClick(context, CardButton(context, FirstLegalTarget(controller)));
            Require(controller.Allies[2].HasUsedStandby && controller.ActionPoints == 3,
                "The painted standby control did not commit a zero-AP action through real pointer input.");
            yield return PointerClick(context, CardButton(context, controller.Allies[1]));
            yield return PointerClick(context, context.Button("flipButton"));
            yield return PointerClick(context, CardButton(context, FirstLegalTarget(controller)));
            Require(controller.Allies[1].IsBack && controller.ActionPoints == 1, "Flip did not reach mirror form.");
            yield return PointerClick(context, context.Button("adjustButton"));
            Require(controller.Phase == BattlePhase.Reorganizing && controller.ActionPoints == 0, "Final AP did not enter persistent reorganization.");
            VerifyShieldBars(context);
            yield return Capture(context, "BookUI-05-reorganization");
            yield return PointerClick(context, context.Button("adjustButton"));
            yield return WaitForEnemy(context);
            Require(controller.Phase == BattlePhase.PlayerTurn && controller.ActionPoints == 4, "Reorganization exit did not advance/reset the turn.");
            VerifyShieldBars(context);
            Check(context, "All four painted controls use real pointer input: standby/rotate/flip/adjust, cancel is atomic, target clicks pass tooltips, shields stay upright, and final-AP adjustment waits for explicit exit");

            int rounds = 0;
            while (controller.Phase != BattlePhase.Victory && controller.Phase != BattlePhase.Defeat && rounds++ < 30)
            {
                Require(controller.Phase == BattlePhase.PlayerTurn, "Unexpected win helper phase.");
                foreach (BattleUnitState ally in controller.Allies)
                {
                    if (!ally.IsAlive || controller.Phase != BattlePhase.PlayerTurn) continue;
                    controller.SelectAlly(ally);
                    if (ally.IsBack)
                    {
                        if (controller.BeginAction(BattleActionKind.Flip)) ChooseAllTargets(controller);
                    }
                    else if (controller.BeginAction(BattleActionKind.Standby)) ChooseAllTargets(controller);
                }
                if (controller.Phase == BattlePhase.PlayerTurn) controller.EndPlayerTurn();
                yield return WaitForEnemy(context);
            }
            Require(controller.Phase == BattlePhase.Victory, "Default 4v4 did not finish in victory.");
            yield return Capture(context, "BookUI-06-victory");
            yield return PointerClick(context, context.Button("restartButton"));
            Require(controller.Phase == BattlePhase.Preparation && controller.Backpack.Placements.Count == 4 && !controller.Allies[0].IsRotated, "Restart did not reset equipment/preparation.");
            controller.StartBattle();
            for (int round = 0; controller.Phase != BattlePhase.Defeat && round < 80; round++)
            {
                controller.EndPlayerTurn(); yield return WaitForEnemy(context);
            }
            Require(controller.Phase == BattlePhase.Defeat, "Default battle did not reach defeat.");
            yield return Capture(context, "BookUI-07-defeat");
            controller.RestartBattle();
            Require(controller.Phase == BattlePhase.Preparation && controller.Allies[0].CurrentHealth == controller.Allies[0].MaxHealth, "Defeat restart failed.");
            Check(context, "Full 4v4 victory, defeat and both restart paths restore initial state");
            RecordText(context);
        }

        private static void ChooseAllTargets(BattleController controller)
        {
            int guard = 0;
            while (controller.Phase == BattlePhase.SelectingTarget && guard++ < 20)
            {
                BattleUnitState target = null;
                foreach (BattleUnitState unit in controller.Enemies) if (controller.CanTarget(unit)) { target = unit; break; }
                if (target == null) foreach (BattleUnitState unit in controller.Allies) if (controller.CanTarget(unit)) { target = unit; break; }
                Require(target != null, "Action had no legal target."); controller.SelectTarget(target);
            }
        }

        private static BattleUnitState FirstLegalTarget(BattleController controller)
        {
            foreach (BattleUnitState unit in controller.Enemies) if (controller.CanTarget(unit)) return unit;
            foreach (BattleUnitState unit in controller.Allies) if (controller.CanTarget(unit)) return unit;
            throw new InvalidOperationException("The pointer action had no legal target.");
        }

        private static void VerifyBookBindings(Context context)
        {
            BattleInfoPanelView info = context.view.InfoPanel;
            Transform surface = context.view.transform.Find("BookSurface");
            Require(surface != null && surface.GetComponent<Image>() != null && surface.GetComponent<Image>().sprite != null,
                "The supplied book surface sprite is not connected to the Canvas.");
            foreach (string field in new[] { "unitNameText", "healthValueText", "attackValueText" })
                Require(ReadField<Text>(info, field) != null, "Book value binding missing: " + field);
            foreach (string field in new[] { "healthFill", "attackFill" })
                Require(ReadField<Image>(info, field) != null && ReadField<Image>(info, field).sprite != null,
                    "Painted book stat-bar binding missing: " + field);
            Require(info.Tooltip != null && ReadField<RectTransform>(info.Tooltip, "panel") != null,
                "The floating book tooltip is not wired.");
            foreach (string field in new[] { "handheldGrid", "wornGrid", "accessoryGrid" })
                Require(ReadField<InventoryGridView>(info, field) != null, "Single equipment-slot view missing: " + field);
            Check(context, "Supplied book surface, name/health/attack strips, three equipment slots and floating details have authored scene bindings");
        }

        private static void VerifyShieldBars(Context context)
        {
            foreach (CharacterCardView card in context.view.GetComponentsInChildren<CharacterCardView>())
            {
                if (card.Unit == null || !card.Unit.IsAlive) continue;
                Image track = ReadField<Image>(card, "armorTrack");
                Image fill = ReadField<Image>(card, "armorFill");
                RectTransform visual = ReadField<RectTransform>(card, "visualRoot");
                Require(track != null && fill != null && fill.gameObject.activeInHierarchy && !fill.transform.IsChildOf(visual),
                    "Every live ally/enemy needs a shield bar outside the rotating card art.");
                float expected = card.Unit.MaxArmor > 0 ? (float)card.Unit.CurrentArmor / card.Unit.MaxArmor : 0f;
                Require(Mathf.Abs(fill.fillAmount - expected) < .001f && fill.type == Image.Type.Filled &&
                    fill.fillMethod == Image.FillMethod.Horizontal && fill.fillOrigin == (int)Image.OriginHorizontal.Left,
                    "Shield fill must match the live shield amount and grow from the left.");
                Require(Quaternion.Angle(fill.transform.rotation, context.view.transform.rotation) < .1f &&
                    Quaternion.Angle(track.transform.rotation, context.view.transform.rotation) < .1f,
                    "Shield bars must remain upright when the character card rotates or flips.");
            }
        }

        private static EquipmentItemView FindItem(Context context, EquipmentInstance item)
        {
            foreach (EquipmentItemView view in context.view.GetComponentsInChildren<EquipmentItemView>())
            {
                // The model identity is stable even when moving between panels.
                foreach (FieldInfo field in typeof(EquipmentItemView).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(EquipmentInstance) && ReferenceEquals(field.GetValue(view), item)) return view;
            }
            throw new InvalidOperationException("No item view for " + item.Definition.displayName);
        }

        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static IEnumerator PointerDrag(Context context, Vector2 start, Vector2 end)
        {
            InputSettings previousSettings = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(previousSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
            InputActionAsset actions = module.actionsAsset;
            ReadOnlyArray<InputDevice>? devices = actions.devices;
            Mouse mouse = null;
            try
            {
                InputSystem.settings = settings; mouse = InputSystem.AddDevice<Mouse>("VerificationDragMouse");
                actions.devices = new[] { (InputDevice)mouse };
                InputSystem.QueueStateEvent(mouse, new MouseState { position = start }); yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left)); yield return null; yield return null;
                for (int step = 1; step <= 6; step++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(start, end, step / 6f) }.WithButton(MouseButton.Left));
                    yield return null; yield return null;
                }
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end }); yield return null; yield return null;
            }
            finally
            {
                actions.devices = devices;
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previousSettings; UnityEngine.Object.Destroy(settings);
            }
        }

        private static IEnumerator WaitForEnemy(Context context)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (context.controller.Phase == BattlePhase.EnemyTurn || context.controller.Phase == BattlePhase.Resolving)
            {
                Require(Time.realtimeSinceStartup < deadline, "Enemy turn timed out."); yield return null;
            }
        }

        private static Button CardButton(Context context, BattleUnitState unit)
        {
            foreach (CharacterCardView card in context.view.GetComponentsInChildren<CharacterCardView>())
                if (ReferenceEquals(card.Unit, unit)) return ReadField<Button>(card, "cardButton");
            throw new InvalidOperationException("No active card for " + unit.Name);
        }

        private static IEnumerator PointerClick(Context context, Button button)
        {
            Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(),
                "Pointer click target unavailable: target=" + (button == null ? "null" : button.name)
                + ", active=" + (button != null && button.gameObject.activeInHierarchy)
                + ", interactable=" + (button != null && button.IsInteractable())
                + ", phase=" + context.controller.Phase + ", AP=" + context.controller.ActionPoints
                + ", selected=" + (context.controller.SelectedAlly == null ? "none" : context.controller.SelectedAlly.Name)
                + ", message=" + context.controller.LastMessage);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            // Clone settings in memory; batch Editor has no focused Game View. Never save these settings.
            InputSettings previousSettings = InputSystem.settings;
            InputSettings verificationSettings = UnityEngine.Object.Instantiate(previousSettings);
            verificationSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            verificationSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            Mouse mouse = null;
            var module = EventSystem.current.currentInputModule as InputSystemUIInputModule;
            Require(module != null && module.actionsAsset != null, "Pointer verification requires a UI action asset.");
            InputActionAsset actions = module.actionsAsset;
            ReadOnlyArray<InputDevice>? previousDevices = actions.devices;
            try
            {
                InputSystem.settings = verificationSettings;
                mouse = InputSystem.AddDevice<Mouse>("VerificationMouse");
                // The real OS mouse can move during an automated run; restrict only these UI actions.
                actions.devices = new[] { (InputDevice)mouse };
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return null;
                yield return null;
                RecordInputDiagnostics(context, button, mouse, position, "move");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                RecordInputDiagnostics(context, button, mouse, position, "press");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return null;
                yield return null;
                RecordInputDiagnostics(context, button, mouse, position, "release");
            }
            finally
            {
                actions.devices = previousDevices;
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previousSettings;
                UnityEngine.Object.Destroy(verificationSettings);
            }
        }

        private static void RecordInputDiagnostics(Context context, Button button, Mouse mouse, Vector2 position, string step)
        {
            EventSystem events = EventSystem.current;
            var module = events.currentInputModule as InputSystemUIInputModule;
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = position }, hits);
            string hit = hits.Count == 0 ? "none" : hits[0].gameObject.name;
            string point = module != null && module.point != null && module.point.action != null
                ? module.point.action.enabled + "/" + module.point.action.ReadValue<Vector2>() : "missing";
            string click = module != null && module.leftClick != null && module.leftClick.action != null
                ? module.leftClick.action.enabled + "/" + module.leftClick.action.ReadValue<float>() : "missing";
            context.report.inputDiagnostics.Add(button.name + " " + step + ": focused=" + Application.isFocused
                + ", eventFocused=" + events.isFocused + ", requested=" + position + ", mouse=" + mouse.position.ReadValue()
                + ", pointEnabled/value=" + point + ", clickEnabled/value=" + click + ", raycast=" + hit
                + ", screen=" + Screen.width + "x" + Screen.height + ", phase=" + context.controller.Phase
                + ", temporaryBackgroundBehavior=" + InputSystem.settings.backgroundBehavior);
        }

        private static void VerifyLayout(Context context, string field, int count)
        {
            RectTransform root = context.view.GetComponent<Canvas>().rootCanvas.transform as RectTransform;
            RectTransform row = ReadField<RectTransform>(context.view, field);
            var bounds = new List<Rect>();
            foreach (CharacterCardView card in row.GetComponentsInChildren<CharacterCardView>())
            {
                if (!card.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4];
                ((RectTransform)card.transform).GetWorldCorners(corners);
                Vector3 min = root.InverseTransformPoint(corners[0]);
                Vector3 max = root.InverseTransformPoint(corners[2]);
                Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                Require(rect.xMin >= root.rect.xMin - 0.5f && rect.xMax <= root.rect.xMax + 0.5f
                    && rect.yMin >= root.rect.yMin - 0.5f && rect.yMax <= root.rect.yMax + 0.5f, "Character card extends beyond the Canvas.");
                foreach (Rect previous in bounds) Require(!rect.Overlaps(previous), "Character cards overlap.");
                bounds.Add(rect);
            }
            Require(bounds.Count == count, "Visible card count differs from formation.");
        }

        private static void VerifyFont(Context context)
        {
            Text text = ReadField<Text>(context.view, "turnText");
            Require(text.font != null, "Battle UI has no font.");
            context.report.font = text.font.name;
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Require(fallback != null, "The Unity 2022 built-in LegacyRuntime font is unavailable.");
            context.report.builtInFallbackFont = fallback.name;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                context.report.warnings.Add("Null graphics device: Chinese glyph rendering and screenshots were not verified.");
                return;
            }
            const string characters = "回合静置战斗胜利失败剑客腐化史莱姆护甲生命";
            text.font.RequestCharactersInTexture(characters, 18, FontStyle.Normal);
            foreach (char character in characters)
                Require(text.font.HasCharacter(character), "UI font is missing Chinese character " + character);
            Check(context, "Active UI font contains the required Chinese glyphs (visual rendering requires screenshot review)");
        }

        private static void RecordText(Context context)
        {
            foreach (Text text in context.view.GetComponentsInChildren<Text>())
                if (!string.IsNullOrEmpty(text.text)) context.report.textSnapshot.Add(text.gameObject.name + ": " + text.text);
        }

        private static IEnumerator Capture(Context context, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) yield break;
            if (Application.isEditor)
            {
                if (!context.report.warnings.Contains("Editor batch Play Mode screenshots are deferred to the Windows player capture."))
                    context.report.warnings.Add("Editor batch Play Mode screenshots are deferred to the Windows player capture.");
                yield break;
            }
            string directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(context.reportPath)), "BookUI-Screenshots");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            Canvas.ForceUpdateCanvases();
            // Player initialization and Canvas rendering must both finish before capture.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            int frames = 0;
            while (!File.Exists(path) && frames++ < 120) yield return null;
            if (File.Exists(path)) context.report.screenshots.Add(path);
            else context.report.warnings.Add("Screenshot was not produced: " + path);
        }

        private static void Check(Context context, string message) { context.report.checks.Add(message); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static T ReadField<T>(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, "Missing verification binding: " + field);
            return (T)info.GetValue(owner);
        }

        private static void SetField(object owner, string field, object value)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(info != null, "Missing verification binding: " + field);
            info.SetValue(owner, value);
        }

        public static void WriteReport(BattleVerificationReport report, string path)
        {
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
#endif
