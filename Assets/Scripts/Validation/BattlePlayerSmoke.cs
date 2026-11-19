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
                : Path.Combine(Application.dataPath, "..", "Logs", "BattlePlayerVerification.json");
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
            float deadline = Time.realtimeSinceStartup + 20f;
            BattleController controller;
            while ((controller = UnityEngine.Object.FindObjectOfType<BattleController>()) == null || controller.TurnNumber == 0)
            {
                Require(Time.realtimeSinceStartup < deadline, "Battle scene did not initialize within 20 seconds.");
                yield return null;
            }

            var context = new Context
            {
                controller = controller,
                view = UnityEngine.Object.FindObjectOfType<BattleView>(),
                report = report,
                reportPath = reportPath
            };
            Require(context.view != null, "BattleView is missing.");
            report.unityVersion = Application.unityVersion;
            report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            report.screenWidth = Screen.width;
            report.screenHeight = Screen.height;
            Require(UnityEngine.Object.FindObjectsOfType<BattleController>().Length == 1, "Expected one BattleController.");
            EventSystem[] systems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
            Require(systems.Length == 1 && systems[0].currentInputModule != null
                && systems[0].currentInputModule.GetType().Name == "InputSystemUIInputModule", "Expected one active New Input System UI module.");
            CanvasScaler scaler = context.view.GetComponent<CanvasScaler>();
            Require(scaler != null && scaler.referenceResolution == new Vector2(1280f, 720f), "Canvas must use the approved reference resolution.");
            if (!Application.isEditor)
                Require(Screen.width == 1280 && Screen.height == 720 && !Screen.fullScreen, "Windows player must open at 1280 x 720 in a window.");
            SetField(controller, "enemyActionDelay", 0.01f);
            BattleDefinition original = ReadField<BattleDefinition>(controller, "definition");
            string originalJson = JsonUtility.ToJson(original);
            CheckInitial(context);
            VerifyFont(context);
            RecordText(context);
            Check(context, "Default scene, input module, Canvas, and 2v2 initialization");
            yield return Capture(context, "01-initial");

            BattleUnitState ally1 = controller.Allies[0];
            BattleUnitState ally2 = controller.Allies[1];
            BattleUnitState enemy1 = controller.Enemies[0];
            BattleUnitState enemy2 = controller.Enemies[1];
            controller.BeginStandby();
            controller.SelectTarget(enemy1);
            Require(controller.Phase == BattlePhase.PlayerTurn && enemy1.CurrentHealth == 30, "Actions without an attacker must be ignored.");
            yield return PointerClick(context, CardButton(context, ally1));
            Require(ReferenceEquals(controller.SelectedAlly, ally1), "Input System pointer did not select an ally card.");
            yield return PointerClick(context, context.Button("standbyButton"));
            Require(controller.Phase == BattlePhase.SelectingTarget, "Standby did not enter target selection.");
            Require(!context.Button("endTurnButton").interactable, "End turn must be disabled while selecting a target.");
            Require(!CardButton(context, ally2).interactable && CardButton(context, enemy1).interactable,
                "Target selection must allow enemies and lock allies.");
            controller.SelectTarget(ally2);
            controller.EndPlayerTurn();
            Require(controller.Phase == BattlePhase.SelectingTarget && !ally1.HasUsedStandby, "Illegal target/end-turn input changed target selection.");
            yield return Capture(context, "02-target-selection");
            Click(context.Button("cancelButton"));
            Require(controller.Phase == BattlePhase.PlayerTurn && controller.SelectedAlly == null
                && !ally1.HasUsedStandby && enemy1.CurrentHealth == 30 && enemy1.CurrentArmor == 2,
                "Cancel must leave numbers and standby eligibility untouched.");
            Check(context, "Real card/action/cancel button callbacks and invalid-input guards");

            ClickCard(context, ally1);
            Click(context.Button("standbyButton"));
            Button firstTarget = CardButton(context, enemy1);
            Click(firstTarget);
            firstTarget.onClick.Invoke();
            controller.SelectTarget(enemy1);
            Require(enemy1.CurrentHealth == 16 && enemy1.CurrentArmor == 0 && ally1.HasUsedStandby,
                "First 16 damage must consume 2 armor, deal 14 health, and commit exactly once.");
            CardButton(context, ally1).onClick.Invoke();
            controller.SelectAlly(ally1);
            controller.BeginStandby();
            Require(controller.SelectedAlly == null && controller.Phase == BattlePhase.PlayerTurn,
                "A used ally must not attack again in the same turn.");
            ClickCard(context, ally2);
            Require(ReadField<Text>(FindCard(context, enemy2), "distanceText").text.Contains("3"), "Distance must include both living positions.");
            Click(context.Button("standbyButton"));
            ClickCard(context, enemy1);
            Require(!enemy1.IsAlive && enemy1.CurrentHealth == 0 && controller.Phase == BattlePhase.PlayerTurn,
                "The second standby must kill the first enemy and await manual end turn.");
            Require(!context.Button("standbyButton").interactable && context.Button("endTurnButton").interactable,
                "After all standbys, the player must still end the turn manually.");
            Require(ReadField<Text>(FindCard(context, enemy2), "orderText").text == "前排 1", "Surviving enemy must compact into front position.");
            Check(context, "16 damage, one standby per ally, duplicate target protection, distance, and enemy compaction");

            Click(context.Button("endTurnButton"));
            Require(controller.Phase == BattlePhase.EnemyTurn, "End turn must lock immediately.");
            context.Button("endTurnButton").onClick.Invoke();
            controller.EndPlayerTurn();
            controller.SelectAlly(ally1);
            controller.BeginStandby();
            controller.SelectTarget(enemy2);
            Require(controller.Phase == BattlePhase.EnemyTurn && controller.SelectedAlly == null && enemy2.CurrentHealth == 30,
                "Enemy phase must reject player input and duplicate end turn.");
            yield return WaitForEnemy(context);
            Require(controller.TurnNumber == 2 && ally1.CurrentHealth == 40 && ally1.CurrentArmor == 0
                && ally2.CurrentHealth == 44 && ally2.CurrentArmor == 4 && !ally1.HasUsedStandby && !ally2.HasUsedStandby,
                "Only the surviving enemy may act once; armor must not refill and standby must reset.");
            Attack(context, ally1, enemy2);
            Attack(context, ally2, enemy2);
            Require(controller.Phase == BattlePhase.Victory && controller.TurnNumber == 2, "Shortest victory must finish immediately in player turn 2.");
            CheckTerminal(context, BattlePhase.Victory);
            yield return Capture(context, "03-victory");
            Check(context, "Dead enemy skipped, one enemy turn, turn reset, shortest victory, and terminal lock");
            Click(context.Button("restartButton"));
            context.Button("restartButton").onClick.Invoke();
            CheckInitial(context);
            yield return null;
            CheckInitial(context);
            Check(context, "Victory restart resets all state without duplicate listeners or residual enemy actions");

            int[] frontHealth = { 34, 22, 4, 0, 0, 0, 0 };
            int[] rearHealth = { 44, 44, 44, 42, 30, 12, 0 };
            int[] nextIntents = { 6, 9, 6, 6, 9, 6, 6 };
            for (int round = 0; round < frontHealth.Length; round++)
            {
                Click(context.Button("endTurnButton"));
                context.Button("endTurnButton").onClick.Invoke();
                yield return WaitForEnemy(context);
                Require(controller.Allies[0].CurrentHealth == frontHealth[round]
                    && controller.Allies[1].CurrentHealth == rearHealth[round], "Defeat health trajectory differs at enemy round " + (round + 1));
                Require(controller.Enemies[0].NextIntentDamage == nextIntents[round]
                    && controller.Enemies[1].NextIntentDamage == nextIntents[round], "Enemy intents differ at enemy round " + (round + 1));
                if (round == 3)
                {
                    Require(controller.Allies[1].CurrentArmor == 0, "The second attacker must retarget the new front ally and consume its armor.");
                    Require(ReadField<Text>(FindCard(context, controller.Allies[1]), "orderText").text == "前排 1",
                        "Surviving ally must compact after death.");
                }
            }
            CheckTerminal(context, BattlePhase.Defeat);
            Require(controller.TurnNumber == 7, "Defeat must not start another player turn.");
            yield return Capture(context, "04-defeat");
            Check(context, "6/6/9 enemy intents, exact 7-round defeat, armor persistence, per-hit retargeting, and ally compaction");
            Click(context.Button("restartButton"));
            CheckInitial(context);
            yield return WinBattle(context);

            foreach (int size in new[] { 1, 4 })
            {
                BattleDefinition temporary = ScriptableObject.CreateInstance<BattleDefinition>();
                temporary.allies = new AllyDefinition[size];
                temporary.enemies = new EnemyDefinition[size];
                for (int index = 0; index < size; index++)
                {
                    temporary.allies[index] = new AllyDefinition { name = "验证剑客 " + (index + 1) };
                    temporary.enemies[index] = new EnemyDefinition { name = "验证史莱姆 " + (index + 1) };
                }
                SetField(controller, "definition", temporary);
                Click(context.Button("restartButton"));
                Require(controller.Allies.Count == size && controller.Enemies.Count == size, "Temporary formation size was not applied.");
                Canvas.ForceUpdateCanvases();
                VerifyLayout(context, "allyContainer", size);
                VerifyLayout(context, "enemyContainer", size);
                if (size == 4)
                {
                    ClickCard(context, controller.Allies[3]);
                    Require(ReadField<Text>(FindCard(context, controller.Enemies[3]), "distanceText").text.Contains("7"), "4th to 4th distance must be 7.");
                    Click(context.Button("standbyButton"));
                    ClickCard(context, controller.Enemies[3]);
                    Require(controller.Enemies[3].CurrentHealth == 16, "Displayed range must not block a distance-7 attack.");
                    yield return Capture(context, "05-four-vs-four");
                }
                yield return WinBattle(context);
                SetField(controller, "definition", original);
                UnityEngine.Object.Destroy(temporary);
                Check(context, size + "v" + size + " in-memory formation, visible card layout, and complete victory");
            }

            Click(context.Button("restartButton"));
            CheckInitial(context);
            Require(JsonUtility.ToJson(original) == originalJson, "Verification modified the authored battle configuration.");
            Require(report.runtimeErrors.Count == 0, "Unity emitted runtime errors during verification.");
            Check(context, "Default 2v2 restored and ScriptableObject configuration unchanged");
        }

        private static void CheckInitial(Context context)
        {
            BattleController controller = context.controller;
            Require(controller.enabled && controller.Phase == BattlePhase.PlayerTurn && controller.TurnNumber == 1
                && controller.SelectedAlly == null && controller.ActiveEnemy == null, "Initial phase, turn, and selections are incorrect.");
            Require(controller.Allies.Count == 2 && controller.Enemies.Count == 2, "Expected the default 2v2 formation.");
            Require(controller.Allies[0].CurrentHealth == 40 && controller.Allies[0].CurrentArmor == 6
                && controller.Allies[1].CurrentHealth == 44 && controller.Allies[1].CurrentArmor == 4, "Ally initial health/armor are incorrect.");
            foreach (BattleUnitState ally in controller.Allies)
                Require(ally.IsAlive && !ally.HasUsedStandby && ally.StandbyDamage == 16, "Ally standby state did not reset.");
            foreach (BattleUnitState enemy in controller.Enemies)
                Require(enemy.CurrentHealth == 30 && enemy.CurrentArmor == 2 && enemy.NextIntentDamage == 6, "Enemy state did not reset.");
            Require(!ReadField<GameObject>(context.view, "resultPanel").activeSelf && !context.Button("restartButton").interactable,
                "Result UI must be hidden outside terminal phases.");
        }

        private static void CheckTerminal(Context context, BattlePhase phase)
        {
            BattleController controller = context.controller;
            Require(controller.Phase == phase && ReadField<GameObject>(context.view, "resultPanel").activeSelf
                && context.Button("restartButton").interactable, "Result panel or restart button was not enabled.");
            Require(!context.Button("standbyButton").interactable && !context.Button("endTurnButton").interactable,
                "Terminal battle actions must be disabled.");
            context.Button("endTurnButton").onClick.Invoke();
            context.Button("standbyButton").onClick.Invoke();
            controller.SelectAlly(controller.Allies[0]);
            controller.SelectTarget(controller.Enemies[0]);
            controller.CancelSelection();
            Require(controller.Phase == phase && controller.SelectedAlly == null && controller.ActiveEnemy == null,
                "Terminal battle accepted input.");
        }

        private static IEnumerator WinBattle(Context context)
        {
            BattleController controller = context.controller;
            int roundLimit = 20;
            while (controller.Phase != BattlePhase.Victory)
            {
                Require(controller.Phase == BattlePhase.PlayerTurn && roundLimit-- > 0, "Victory helper encountered an unexpected phase or exceeded its round limit.");
                for (int index = 0; index < controller.Allies.Count && controller.Phase == BattlePhase.PlayerTurn; index++)
                {
                    BattleUnitState ally = controller.Allies[index];
                    if (!ally.IsAlive || ally.HasUsedStandby) continue;
                    BattleUnitState target = null;
                    foreach (BattleUnitState enemy in controller.Enemies)
                        if (enemy.IsAlive) { target = enemy; break; }
                    Require(target != null, "No live enemy before victory.");
                    Attack(context, ally, target);
                }
                if (controller.Phase == BattlePhase.Victory) break;
                Click(context.Button("endTurnButton"));
                yield return WaitForEnemy(context);
            }
        }

        private static IEnumerator WaitForEnemy(Context context)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (context.controller.Phase == BattlePhase.EnemyTurn || context.controller.Phase == BattlePhase.Resolving)
            {
                Require(Time.realtimeSinceStartup < deadline, "Enemy turn did not finish within 15 seconds.");
                yield return null;
            }
        }

        private static void Attack(Context context, BattleUnitState ally, BattleUnitState target)
        {
            ClickCard(context, ally);
            Click(context.Button("standbyButton"));
            ClickCard(context, target);
        }

        private static void ClickCard(Context context, BattleUnitState unit) { Click(CardButton(context, unit)); }
        private static Button CardButton(Context context, BattleUnitState unit) { return ReadField<Button>(FindCard(context, unit), "cardButton"); }

        private static CharacterCardView FindCard(Context context, BattleUnitState unit)
        {
            foreach (CharacterCardView card in context.view.GetComponentsInChildren<CharacterCardView>(true))
                if (card.gameObject.activeInHierarchy && ReferenceEquals(card.Unit, unit)) return card;
            throw new InvalidOperationException("No active card for " + unit.Name);
        }

        private static void Click(Button button)
        {
            Require(button != null && button.isActiveAndEnabled && button.IsInteractable(), "Attempted to click a disabled UI button.");
            button.onClick.Invoke();
        }

        private static IEnumerator PointerClick(Context context, Button button)
        {
            Require(button != null && button.IsInteractable(), "Pointer click target is disabled.");
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
            string directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(context.reportPath)), "BattleScreenshots");
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
